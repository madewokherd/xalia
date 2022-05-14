using System;
using System.Collections.Concurrent;
using System.Threading;
using SDL2;

namespace Gazelle.Sdl
{
    internal class SdlSynchronizationContext : SynchronizationContext
    {
        public static SdlSynchronizationContext Instance { get; } = new SdlSynchronizationContext();

        private object _main_thread_obj;

        public Thread MainThread { get; private set; }

        private bool _quitting;

        ConcurrentQueue<(SendOrPostCallback, object)> _posts = new ConcurrentQueue<(SendOrPostCallback, object)>();

        private class SendCallback
        {
            public SendOrPostCallback callback;
            public object state;
            public EventWaitHandle completed_event;
        }

        ConcurrentQueue<SendCallback> _sends = new ConcurrentQueue<SendCallback>();

        private uint _queue_updated_event;

        private SdlSynchronizationContext()
        {
        }

        public void Init(uint flags)
        {
            if (Interlocked.CompareExchange(ref _main_thread_obj, Thread.CurrentThread, null) != null)
            {
                throw new InvalidOperationException("Init called more than once");
            }
            MainThread = Thread.CurrentThread;

            SetSynchronizationContext(this);

            SDL.SDL_SetMainReady();

            SDL.SDL_Init(flags);

            _queue_updated_event = SDL.SDL_RegisterEvents(1);
        }

        public void Init()
        {
            Init(SDL.SDL_INIT_EVERYTHING);
        }

        public void Quit()
        {
            if (Thread.CurrentThread != MainThread)
                throw new InvalidOperationException("must be called from main SDL thread");
            _quitting = true;
            SDL.SDL_Quit();
        }

        public class SdlEventArgs : EventArgs
        {
            public SdlEventArgs(SDL.SDL_Event sdl_event)
            {
                SdlEvent = sdl_event;
            }

            public bool Cancel { get; set; }
            public SDL.SDL_Event SdlEvent { get; }
        }

        public delegate void SdlEventHandler(object sender, SdlEventArgs e);

        public SdlEventHandler SdlEvent;

        public void MainLoop()
        {
            while (!_quitting)
            {
                if (_sends.TryDequeue(out var send))
                {
                    send.callback(send.state);
                    send.completed_event.Set();
                    continue;
                }    
                if (SDL.SDL_PollEvent(out var poll_e) != 0)
                {
                    HandleEvent(poll_e);
                    continue;
                }
                if (_posts.TryDequeue(out var post))
                {
                    post.Item1(post.Item2);
                    continue;
                }
                if (SDL.SDL_WaitEvent(out var wait_e) != 0)
                {
                    HandleEvent(wait_e);
                    continue;
                }
            }
        }

        private void HandleEvent(SDL.SDL_Event e)
        {
            var handler = SdlEvent;
            var eventargs = new SdlEventArgs(e);
            if (handler != null)
                handler(this, eventargs);
            if (eventargs.Cancel)
                return;
            if (e.type == SDL.SDL_EventType.SDL_QUIT)
                Quit();
        }

        private void NotifyQueue()
        {
            if (Thread.CurrentThread == MainThread)
                return;
            SDL.SDL_Event e = new SDL.SDL_Event();
            e.type = (SDL.SDL_EventType)_queue_updated_event;
            SDL.SDL_PushEvent(ref e);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _posts.Enqueue((d, state));
            NotifyQueue();
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread == MainThread)
            {
                d(state);
                return;
            }
            var callback = new SendCallback();
            callback.callback = d;
            callback.state = state;
            callback.completed_event = new EventWaitHandle(false, EventResetMode.ManualReset);

            _sends.Enqueue(callback);

            NotifyQueue();

            callback.completed_event.WaitOne();
            callback.completed_event.Dispose();
        }
    }
}
