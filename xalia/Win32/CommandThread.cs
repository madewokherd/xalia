using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    public class CommandThread
    {
        struct CommandThreadRequest
        {
            public Action Action;
        }

        class CommandQueue
        {
            public int key;
            public ConcurrentQueue<CommandThreadRequest> requests = new ConcurrentQueue<CommandThreadRequest>();
            public long locked;
        }

        Dictionary<int, CommandQueue> keyed_queues = new Dictionary<int, CommandQueue>();

        ConcurrentQueue<CommandQueue> overall_queue = new ConcurrentQueue<CommandQueue>();

        AutoResetEvent tasks_available;

        long threads_waiting = 0;

        internal CommandThread()
        {
            tasks_available = new AutoResetEvent(false);
        }

        private void CreateNewThread(CommandQueue queue)
        {
            var thread = new Thread(ThreadProc);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(queue);
        }

        private void QueueRequest(CommandThreadRequest request, int key)
        {
            if (!keyed_queues.TryGetValue(key, out var queue))
            {
                queue = new CommandQueue();
                queue.key = key;
                keyed_queues[key] = queue;
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

        public async Task<T> OnBackgroundThread<T>(Func<T> func, int key = 0)
        {
            /* Commands with the same key will be executed sequentially.
             * By convention key is:
                0 - operations not associated with a thread
                tid - user-initiated operations
                tid+1 - queries not related to user-initiated operations
                tid+2 - polling */

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

            QueueRequest(request, key);

            return await completion_source.Task;
        }

        public async Task OnBackgroundThread(Action func, int key = 0)
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

            QueueRequest(request, key);

            await completion_source.Task;
        }

        private void MsgWaitOne(WaitHandle waitHandle)
        {
            while (true)
            {
                var ret = MsgWaitForSingleObject(waitHandle.SafeWaitHandle, INFINITE, QS_ALLINPUT);
                switch (ret)
                {
                    case 0:
                        return;
                    case 1:
                        {
                            while (PeekMessageW(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                            {
                                TranslateMessage(ref msg);
                                DispatchMessageW(ref msg);
                            }
                            break;
                        }
                    default:
                        throw new Win32Exception();
                }
            }
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
                    MsgWaitOne(tasks_available);
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

