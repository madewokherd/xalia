using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xalia.Uia
{
    public class UiaCommandThread
    {
        struct CommandThreadRequest
        {
            public Action Action;
        }

        class CommandQueue
        {
            public int pid;
            public ConcurrentQueue<CommandThreadRequest> requests = new ConcurrentQueue<CommandThreadRequest>();
            public long locked;
        }

        Dictionary<int, CommandQueue> pid_queues = new Dictionary<int, CommandQueue>();

        ConcurrentQueue<CommandQueue> overall_queue = new ConcurrentQueue<CommandQueue>();

        AutoResetEvent tasks_available;

        long threads_waiting = 0;

        internal UiaCommandThread()
        {
            tasks_available = new AutoResetEvent(false);
        }

        private void CreateNewThread(CommandQueue queue)
        {
            var thread = new Thread(ThreadProc);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start(queue);
        }

        private void QueueRequest(CommandThreadRequest request, int pid)
        {
            if (!pid_queues.TryGetValue(pid, out var queue))
            {
                queue = new CommandQueue();
                queue.pid = pid;
                pid_queues[pid] = queue;
            }

            queue.requests.Enqueue(request);

            if (Interlocked.Read(ref queue.locked) == 0)
            {
                bool lock_taken = false;
                if (Interlocked.Read(ref threads_waiting) == 0)
                {
                    lock_taken = Interlocked.CompareExchange(ref queue.locked, 1, 0) == 0;
                    if (lock_taken)
                        CreateNewThread(queue);
                }
                if (!lock_taken)
                {
                    overall_queue.Enqueue(queue);
                    tasks_available.Set();
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
                catch (Exception e)
                {
                    if (UiaElement.IsExpectedException(e))
                        return null;
                    throw;
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
                catch (Exception e)
                {
                    if (UiaElement.IsExpectedException(e))
                        return new UiaElementWrapper[0];
                    throw;
                }

                var result = new UiaElementWrapper[elements.Length];
                bool assume_unique = !element.Connection.HasNonIdChildren(element);

                for (var i = 0; i < elements.Length; i++)
                {
                    result[i] = element.Connection.WrapElement(elements[i], element.UniqueId, assume_unique);
                    if (!result[i].IsValid)
                        return new UiaElementWrapper[0];
                }

                return result;
            }, element);
        }

        public async Task<PatternId[]> GetSupportedPatterns(UiaElementWrapper element)
        {
            return await OnBackgroundThread(() =>
            {
                return element.AutomationElement.GetSupportedPatterns();
            }, element);
        }

        private void ThreadProc(object initial_info)
        {
            var queue = (CommandQueue)initial_info;
            // We start with the lock held
            bool lock_held = true;
            while (TryProcessQueue(queue, lock_held))
            {
                // All work is done in TryProcessQueue
                lock_held = false;
            }
            try
            {
                while (true)
                {
                    while (overall_queue.TryDequeue(out queue))
                    {
                        while (TryProcessQueue(queue, false))
                        {
                            // All work is done in TryProcessQueue
                        }
                    }
                    Interlocked.Increment(ref threads_waiting);
                    tasks_available.WaitOne();
                    Interlocked.Decrement(ref threads_waiting);
                }
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }
        }

        private bool TryProcessQueue(CommandQueue queue, bool lock_held)
        {
            // It's important that we check this while the lock is not held.
            // See the comment at the end for why this is necessary.
            if (!lock_held && queue.requests.IsEmpty)
                // Nothing to do.
                return false;

            bool lock_taken = false;

            try
            {
                if (lock_held)
                    lock_taken = true;
                else
                    lock_taken = Interlocked.CompareExchange(ref queue.locked, 1, 0) == 0;

                if (!lock_taken)
                    return false;

                while (queue.requests.TryDequeue(out var request))
                {
                    request.Action();
                }
            }
            finally
            {
                if (lock_taken)
                    Interlocked.Exchange(ref queue.locked, 0);
            }

            /* It's possible that the main thread added a request to the queue after
             * we called TryDequeue but before we unset locked. If this happened,
             * the queue is no longer empty, but no other thread will be woken up to
             * serve the request, and there is no new entry in requests. We
             * must recheck the queue while the lock is not held to account for this case.*/
            return true;
        }
    }
}

