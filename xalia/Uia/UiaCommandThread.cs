using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using FlaUI.Core.Identifiers;

namespace Xalia.Uia
{
    public class UiaCommandThread
    {
        struct CommandThreadRequest
        {
            public Action Action;
        }

        Dictionary<int, ConcurrentQueue<CommandThreadRequest>> pid_command_queue = new Dictionary<int, ConcurrentQueue<CommandThreadRequest>>();

        Dictionary<int, SpinLock> pid_worker_lock = new Dictionary<int, SpinLock>();

        ConcurrentQueue<(int, ConcurrentQueue<CommandThreadRequest>, SpinLock)> worker_pid_queue = new ConcurrentQueue<(int, ConcurrentQueue<CommandThreadRequest>, SpinLock)>();

        Semaphore worker_pid_queue_sem; // Upper bound on the number of threads that should be woken to process current requests.

        Thread thread;

        const int num_threads = 5;

        internal UiaCommandThread()
        {
            worker_pid_queue_sem = new Semaphore(0, num_threads);
            for (int i = 0; i < num_threads; i++)
            {
                thread = new Thread(ThreadProc);
                thread.SetApartmentState(ApartmentState.MTA);
                thread.Start();
            }
        }

        private void QueueRequest(CommandThreadRequest request, int pid)
        {
            ConcurrentQueue<CommandThreadRequest> command_queue;
            if (!pid_command_queue.TryGetValue(pid, out command_queue))
            {
                command_queue = new ConcurrentQueue<CommandThreadRequest>();
                pid_command_queue[pid] = command_queue;
            }

            SpinLock sl;
            if (!pid_worker_lock.TryGetValue(pid, out sl))
            {
                sl = new SpinLock(true);
                pid_worker_lock[pid] = sl;
            }

            command_queue.Enqueue(request);

            if (!sl.IsHeld)
            {
                worker_pid_queue.Enqueue((pid, command_queue, sl));
                try
                {
                    worker_pid_queue_sem.Release();
                }
                catch (SemaphoreFullException) {
                    // All threads are busy. This is Fine.
                }
            }
        }

        public Task<T> OnBackgroundThread<T>(Func<T> func, UiaElementWrapper element)
        {
            return OnBackgroundThread(func, element.Pid);
        }

        public async Task<T> OnBackgroundThread<T>(Func<T> func, int pid = 0)
        {
            var request = new CommandThreadRequest();
            var completion_source = new TaskCompletionSource<T>();
            request.Action = () =>
            {
                try
                {
                    var result = func();
                    completion_source.SetResult(result);
                }
                catch (Exception e)
                {
                    completion_source.SetException(e);
                }
            };

            QueueRequest(request, pid);

            return await completion_source.Task;
        }

        public Task OnBackgroundThread(Action func, UiaElementWrapper element)
        {
            return OnBackgroundThread(func, element.Pid);
        }

        public async Task OnBackgroundThread(Action func, int pid = 0)
        {
            var request = new CommandThreadRequest();
            var completion_source = new TaskCompletionSource<bool>();
            request.Action = () =>
            {
                try
                {
                    func();
                    completion_source.SetResult(true);
                }
                catch (Exception e)
                {
                    completion_source.SetException(e);
                }
            };

            QueueRequest(request, pid);

            await completion_source.Task;
        }

        public async Task<object> GetPropertyValue(UiaElementWrapper element, PropertyId propid)
        {
            return await OnBackgroundThread(() =>
            {
                try
                {
                    return element.AutomationElement.FrameworkAutomationElement.GetPropertyValue(propid);
                }
                catch (COMException)
                {
                    return null;
                }
                catch (PropertyNotSupportedException)
                {
                    return null;
                }
            }, element);
        }

        public async Task<UiaElementWrapper[]> GetChildren(UiaElementWrapper element)
        {
            return await OnBackgroundThread(() =>
            {
                AutomationElement[] elements;
                try
                {
                    elements = element.AutomationElement.FindAllChildren();
                }
                catch (COMException)
                {
                    return new UiaElementWrapper[0];
                }

                var result = new UiaElementWrapper[elements.Length];

                for (var i = 0; i < elements.Length; i++)
                {
                    result[i] = element.Connection.WrapElement(elements[i]);
                }

                return result;
            }, element);
        }

        public async Task<UiaElementWrapper> GetFocusedElement(UiaConnection connection)
        {
            return await OnBackgroundThread(() =>
            {
                var result = connection.Automation.FocusedElement();

                return connection.WrapElement(result);
            }, 0);
        }

        public async Task<PatternId[]> GetSupportedPatterns(UiaElementWrapper element)
        {
            return await OnBackgroundThread(() =>
            {
                return element.AutomationElement.GetSupportedPatterns();
            }, element);
        }

        private void ThreadProc()
        {
            while (true)
            {
                (int, ConcurrentQueue<CommandThreadRequest>, SpinLock) pid_queue_info;
                while (worker_pid_queue.TryDequeue(out pid_queue_info))
                {
                    while (TryProcessQueue(pid_queue_info))
                    {
                        // All work is done in TryProcessQueue
                    }
                }
                worker_pid_queue_sem.WaitOne();
            }
        }

        private bool TryProcessQueue((int, ConcurrentQueue<CommandThreadRequest>, SpinLock) pid_queue_info)
        {
            var queue = pid_queue_info.Item2;
            var spinlock = pid_queue_info.Item3;

            // It's important that we check this while the lock is not held.
            // See the comment at the end for why this is necessary.
            if (queue.IsEmpty)
                // Nothing to do.
                return false;

            bool lock_taken = false;

            try
            {
                spinlock.TryEnter(ref lock_taken);

                if (!lock_taken)
                    return false;

                while (queue.TryDequeue(out var request))
                {
                    request.Action();
                }
            }
            finally
            {
                if (lock_taken)
                    spinlock.Exit();
            }

            /* It's possible that the main thread added a request to the queue after
             * we called TryDequeue but before we called spinlock.Exit. If this happened,
             * the queue is no longer empty, but no other thread will be woken up to
             * serve the request, and there is no new entry in worker_pid_queue. We
             * must recheck the queue while the lock is not held to account for this case.*/
            return true;
        }
    }
}

