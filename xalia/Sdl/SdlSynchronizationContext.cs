using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

using static SDL3.SDL;

namespace Xalia.Sdl
{
    internal class SdlSynchronizationContext : SynchronizationContext
    {
        public static SdlSynchronizationContext Instance { get; } = new SdlSynchronizationContext();

        private object _main_thread_obj;

        public Thread MainThread { get; private set; }

        private bool _quitting;

        ConcurrentQueue<(SendOrPostCallback, object, StackTrace)> _posts = new ConcurrentQueue<(SendOrPostCallback, object, StackTrace)>();

        private class SendCallback
        {
            public SendOrPostCallback callback;
            public object state;
            public EventWaitHandle completed_event;
            public StackTrace stacktrace;
        }

        ConcurrentQueue<SendCallback> _sends = new ConcurrentQueue<SendCallback>();

        private uint _queue_updated_event;

        private SdlSynchronizationContext()
        {
        }

        public void Init(SDL_InitFlags flags)
        {
            if (Interlocked.CompareExchange(ref _main_thread_obj, Thread.CurrentThread, null) != null)
            {
                throw new InvalidOperationException("Init called more than once");
            }
            MainThread = Thread.CurrentThread;

            SetSynchronizationContext(this);

            SDL_SetMainReady();

            if (!SDL_Init(flags))
            {
                throw new ApplicationException(SDL_GetError());
            }

            _queue_updated_event = SDL_RegisterEvents(1);
        }

        public void Init()
        {
            Init(SDL_InitFlags.SDL_INIT_EVENTS);
        }

        public void AssertMainThread()
        {
            if (MainThread is null)
                throw new InvalidOperationException("SdlSynchronizationContext.Init must be called before this method");
            if (Thread.CurrentThread != MainThread)
                throw new InvalidOperationException("must be called from main SDL thread");
        }

        public void Quit()
        {
            AssertMainThread();
            if (DebugMainLoop)
                Utils.DebugWriteLine($"MAINLOOP: Queued quit");
            _quitting = true;
            SDL_Quit();
        }

        public class SdlEventArgs : EventArgs
        {
            public SdlEventArgs(SDL_Event sdl_event)
            {
                SdlEvent = sdl_event;
            }

            public bool Cancel { get; set; }
            public SDL_Event SdlEvent { get; }
        }

        public delegate void SdlEventHandler(object sender, SdlEventArgs e);

        public event SdlEventHandler SdlEvent;

        static bool DebugMainLoop = !(Environment.GetEnvironmentVariable("XALIA_DEBUG_MAINLOOP") is null &&
            Environment.GetEnvironmentVariable("XALIA_DEBUG_INPUT") != "0");

        public void MainLoop()
        {
            AssertMainThread();
            while (!_quitting)
            {
                if (_sends.TryDequeue(out var send))
                {

                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Handling Send: {send.stacktrace}");
                    send.callback(send.state);
                    send.completed_event.Set();
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Completed handling Send");
                    continue;
                }
                if (SDL_PollEvent(out var poll_e))
                {
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Handling SDL event: {(SDL_EventType)poll_e.type}");
                    try
                    {
                        HandleEvent(poll_e);
                    }
                    catch (Exception e)
                    {
                        Utils.OnError(e);
                    }
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Completed handling event");
                    continue;
                }
                if (_posts.TryDequeue(out var post))
                {
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Handling Post: {post.Item3}");
                    post.Item1(post.Item2);
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Completed handling Post");
                    continue;
                }
                if (DebugMainLoop)
                    Utils.DebugWriteLine($"MAINLOOP: waiting for events");
                if (SDL_WaitEvent(out var wait_e))
                {
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Handling SDL event: {(SDL_EventType)wait_e.type}");
                    try
                    {
                        HandleEvent(wait_e);
                    }
                    catch (Exception e)
                    {
                        Utils.OnError(e);
                    }
                    if (DebugMainLoop)
                        Utils.DebugWriteLine($"MAINLOOP: Completed handling event");
                    continue;
                }
                else
                    throw new ApplicationException(SDL_GetError());
            }
            if (DebugMainLoop)
                Utils.DebugWriteLine($"MAINLOOP: Quitting");
        }

        private void HandleEvent(SDL_Event e)
        {
            var handler = SdlEvent;
            var eventargs = new SdlEventArgs(e);
            if (handler != null)
                handler(this, eventargs);
            if (eventargs.Cancel)
                return;
            if ((SDL_EventType)e.type == SDL_EventType.SDL_EVENT_QUIT)
                Quit();
        }

        private void NotifyQueue(bool force)
        {
            if (Thread.CurrentThread == MainThread && !force)
                return;
            SDL_Event e = new SDL_Event();
            e.type = _queue_updated_event;
            SDL_PushEvent(ref e);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _posts.Enqueue((d, state, DebugMainLoop ? new StackTrace() : null));

            if (DebugMainLoop)
                Utils.DebugWriteLine($"MAINLOOP: queued Post");

            NotifyQueue(_posts.Count == 1);
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
            if (DebugMainLoop)
            {
                callback.stacktrace = new StackTrace();
            }

            _sends.Enqueue(callback);

            if (DebugMainLoop)
                Utils.DebugWriteLine($"MAINLOOP: queued Send");

            NotifyQueue(false);

            callback.completed_event.WaitOne();
            callback.completed_event.Dispose();
        }
    }
}
