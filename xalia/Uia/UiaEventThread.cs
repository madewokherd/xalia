using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.EventHandlers;
using FlaUI.Core.Identifiers;

namespace Xalia.Uia
{
    internal class UiaEventThread
    {
        enum EventThreadRequestType
        {
            RegisterPropertyChangeEvent,
        }

        struct EventThreadRequest
        {
            public EventThreadRequestType RequestType;
            public object CompletionSource;
            public AutomationElement Element;
            public PropertyId PropertyId;
            public SynchronizationContext HandlerContext;
            public object Handler;
        }

        Channel<EventThreadRequest> channel;

        Thread thread;

        internal UiaEventThread()
        {
            UnboundedChannelOptions options = new UnboundedChannelOptions();
            options.AllowSynchronousContinuations = true;
            options.SingleReader = true;
            options.SingleWriter = true;
            channel = Channel.CreateUnbounded<EventThreadRequest>(options);

            thread = new Thread(ThreadProc);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        public async Task<PropertyChangedEventHandlerBase> RegisterPropertyChangedEventAsync(AutomationElement element,
            PropertyId propid, Action<AutomationElement, PropertyId, object> action)
        {
            var request = new EventThreadRequest();
            request.RequestType = EventThreadRequestType.RegisterPropertyChangeEvent;
            var source = new TaskCompletionSource<PropertyChangedEventHandlerBase>();
            request.CompletionSource = source;
            request.Element = element;
            request.Handler = action;
            request.HandlerContext = SynchronizationContext.Current;
            request.PropertyId = propid;

            await channel.Writer.WriteAsync(request);

            return await source.Task;
        }

        private void ThreadProc()
        {
            while (true)
            {
                var task = channel.Reader.ReadAsync().AsTask();
                task.Wait();
                var request = task.Result;
                switch (request.RequestType)
                {
                    case EventThreadRequestType.RegisterPropertyChangeEvent:
                        {
                            var completion_source = (TaskCompletionSource<PropertyChangedEventHandlerBase>)request.CompletionSource;
                            try
                            {
                                var result = request.Element.RegisterPropertyChangedEvent(TreeScope.Element,
                                    (AutomationElement element, PropertyId propid, object obj) =>
                                {
                                    request.HandlerContext.Post((object state) =>
                                    {
                                        var handler = (Action<AutomationElement, PropertyId, object>)request.Handler;
                                        handler(element, propid, obj);
                                    }, null);
                                }, new PropertyId[] { request.PropertyId });

                                completion_source.SetResult(result);
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                            break;
                        }
                }
            }
        }
    }
}
