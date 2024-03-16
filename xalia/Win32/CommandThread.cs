using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    public enum CommandThreadPriority
    {
        User, // For user actions
        Query, // For normal queries
        Poll // For polls
    }

    public class CommandThread : IDisposable
    {
        struct CommandThreadRequest
        {
            public Action Action;
            public Action Cancel;
        }

        private ConcurrentQueue<CommandThreadRequest> user_requests = new ConcurrentQueue<CommandThreadRequest>();
        private ConcurrentQueue<CommandThreadRequest> query_requests = new ConcurrentQueue<CommandThreadRequest>();
        private ConcurrentQueue<CommandThreadRequest> poll_requests = new ConcurrentQueue<CommandThreadRequest>();

        AutoResetEvent tasks_available;

        bool disposed = false;

        internal CommandThread()
        {
            tasks_available = new AutoResetEvent(false);

            var thread = new Thread(ThreadProc);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void QueueRequest(CommandThreadRequest request, CommandThreadPriority priority)
        {
            switch (priority)
            {
                case CommandThreadPriority.User:
                    user_requests.Enqueue(request);
                    break;
                case CommandThreadPriority.Query:
                    query_requests.Enqueue(request);
                    break;
                case CommandThreadPriority.Poll:
                    poll_requests.Enqueue(request);
                    break;
            }
            tasks_available.Set();
        }

        private void CheckDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        public async Task<T> OnBackgroundThread<T>(Func<T> func, CommandThreadPriority priority)
        {
            CheckDisposed();

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

            request.Cancel = completion_source.SetCanceled;

            QueueRequest(request, priority);

            return await completion_source.Task;
        }

        public async Task OnBackgroundThread(Action func, CommandThreadPriority priority)
        {
            CheckDisposed();

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

            request.Cancel = completion_source.SetCanceled;

            QueueRequest(request, priority);

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

        private void ThreadProc()
        {
            try
            {
                while (!disposed)
                {
                    if (user_requests.TryDequeue(out var request) ||
                        query_requests.TryDequeue(out request) ||
                        poll_requests.TryDequeue(out request))
                    {
                        request.Action();
                    }
                    else
                    {
                        MsgWaitOne(tasks_available);
                    }
                }

                while (user_requests.TryDequeue(out var request) ||
                    query_requests.TryDequeue(out request) ||
                    poll_requests.TryDequeue(out request))
                {
                    request.Cancel();
                }
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                tasks_available.Set();
                tasks_available.Dispose();
            }
        }
    }
}

