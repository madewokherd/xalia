using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;

namespace Xalia.Uia
{
    public class UiaCommandThread
    {
        enum CommandThreadRequestType
        {
            GetPropertyValue,
            GetChildren,
            GetFocusedElement,
            GetSupportedPatterns,
            Invoke,
        }

        struct CommandThreadRequest
        {
            public CommandThreadRequestType RequestType;
            public object CompletionSource;
            public UiaElementWrapper Element;
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

        public async Task<object> GetPropertyValue(UiaElementWrapper element, PropertyId propid)
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

        public async Task<UiaElementWrapper[]> GetChildren(UiaElementWrapper element)
        {
            var request = new CommandThreadRequest();
            request.RequestType = CommandThreadRequestType.GetChildren;
            var source = new TaskCompletionSource<UiaElementWrapper[]>();
            request.CompletionSource = source;
            request.Element = element;

            await channel.Writer.WriteAsync(request);

            return await source.Task;
        }

        public async Task<UiaElementWrapper> GetFocusedElement(UiaElementWrapper desktop)
        {
            var request = new CommandThreadRequest();
            request.RequestType = CommandThreadRequestType.GetFocusedElement;
            var source = new TaskCompletionSource<UiaElementWrapper>();
            request.CompletionSource = source;
            request.Element = desktop;

            await channel.Writer.WriteAsync(request);

            return await source.Task;
        }

        public async Task<PatternId[]> GetSupportedPatterns(UiaElementWrapper element)
        {
            var request = new CommandThreadRequest();
            request.RequestType = CommandThreadRequestType.GetSupportedPatterns;
            var source = new TaskCompletionSource<PatternId[]>();
            request.CompletionSource = source;
            request.Element = element;

            await channel.Writer.WriteAsync(request);

            return await source.Task;
        }

        public async Task Invoke(UiaElementWrapper element)
        {
            var request = new CommandThreadRequest();
            request.RequestType = CommandThreadRequestType.Invoke;
            var source = new TaskCompletionSource<bool>();
            request.CompletionSource = source;
            request.Element = element;

            await channel.Writer.WriteAsync(request);

            await source.Task;
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
                                var result = request.Element.AutomationElement.FrameworkAutomationElement.GetPropertyValue(request.PropertyId);

                                if (result is AutomationElement ae)
                                {
                                    result = request.Element.Connection.WrapElement(ae);
                                }

                                completion_source.SetResult(result);
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                            break;
                        }
                    case CommandThreadRequestType.GetChildren:
                        {
                            var completion_source = (TaskCompletionSource<UiaElementWrapper[]>)request.CompletionSource;
                            try
                            {
                                var elements = request.Element.AutomationElement.FindAllChildren();

                                var result = new UiaElementWrapper[elements.Length];

                                for (var i=0; i < elements.Length; i++)
                                {
                                    result[i] = request.Element.Connection.WrapElement(elements[i]);
                                }

                                completion_source.SetResult(result);
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                            break;
                        }
                    case CommandThreadRequestType.GetFocusedElement:
                        {
                            var completion_source = (TaskCompletionSource<UiaElementWrapper>)request.CompletionSource;
                            try
                            {
                                var result = request.Element.Connection.Automation.FocusedElement();

                                completion_source.SetResult(request.Element.Connection.WrapElement(result));
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                            break;
                        }
                    case CommandThreadRequestType.GetSupportedPatterns:
                        {
                            var completion_source = (TaskCompletionSource<PatternId[]>)request.CompletionSource;
                            try
                            {
                                var result = request.Element.AutomationElement.GetSupportedPatterns();

                                completion_source.SetResult(result);
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                        }
                        break;
                    case CommandThreadRequestType.Invoke:
                        {
                            var completion_source = (TaskCompletionSource<bool>)request.CompletionSource;
                            try
                            {
                                request.Element.AutomationElement.Patterns.Invoke.Pattern.Invoke();

                                completion_source.SetResult(true);
                            }
                            catch (Exception e)
                            {
                                completion_source.SetException(e);
                            }
                        }
                        break;
                }
            }
        }
    }
}

