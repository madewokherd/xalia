using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;

namespace Xalia.Uia
{
    internal class UiaCommandThread
    {
        enum CommandThreadRequestType
        {
            GetPropertyValue,
        }

        struct CommandThreadRequest
        {
            public CommandThreadRequestType RequestType;
            public object CompletionSource;
            public AutomationElement Element;
            public PropertyId PropertyId;
        }

        Channel<CommandThreadRequest> channel;

        Thread thread;

        const int num_threads = 3;

        internal UiaCommandThread()
        {
            UnboundedChannelOptions options = new UnboundedChannelOptions();
            options.AllowSynchronousContinuations = true;
            options.SingleReader = num_threads == 1;
            options.SingleWriter = true;
            channel = Channel.CreateUnbounded<CommandThreadRequest>(options);

            for (int i = 0; i < num_threads; i++)
            {
                thread = new Thread(ThreadProc);
                thread.SetApartmentState(ApartmentState.MTA);
                thread.Start();
            }
        }

        public async Task<object> GetPropertyValue(AutomationElement element, PropertyId propid)
        {
            var request = new CommandThreadRequest();
            request.RequestType = CommandThreadRequestType.GetPropertyValue;
            var source = new TaskCompletionSource<object>();
            request.CompletionSource = source;
            request.Element = element;
            request.PropertyId = propid;
            request.CompletionSource = source;

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
                    case CommandThreadRequestType.GetPropertyValue:
                        {
                            var completion_source = (TaskCompletionSource<object>)request.CompletionSource;
                            try
                            {
                                var result = request.Element.FrameworkAutomationElement.GetPropertyValue(request.PropertyId);

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

