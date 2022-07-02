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
        struct CommandThreadRequest
        {
            public Action Action;
            public UiaElementWrapper Element;
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

        public async Task<T> OnBackgroundThread<T>(Func<T> func, UiaElementWrapper element = default)
        {
            var request = new CommandThreadRequest();
            var completion_source = new TaskCompletionSource<T>();
            request.Element = element;
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

            await channel.Writer.WriteAsync(request);

            return await completion_source.Task;
        }

        public async Task OnBackgroundThread(Action func, UiaElementWrapper element = default)
        {
            var request = new CommandThreadRequest();
            var completion_source = new TaskCompletionSource<bool>();
            request.Element = element;
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

            await channel.Writer.WriteAsync(request);

            await completion_source.Task;
        }

        public async Task<object> GetPropertyValue(UiaElementWrapper element, PropertyId propid)
        {
            return await OnBackgroundThread(() =>
            {
                return element.AutomationElement.FrameworkAutomationElement.GetPropertyValue(propid);
            }, element);
        }

        public async Task<UiaElementWrapper[]> GetChildren(UiaElementWrapper element)
        {
            return await OnBackgroundThread(() =>
            {
                var elements = element.AutomationElement.FindAllChildren();

                var result = new UiaElementWrapper[elements.Length];

                for (var i = 0; i < elements.Length; i++)
                {
                    result[i] = element.Connection.WrapElement(elements[i]);
                }

                return result;
            }, element);
        }

        public async Task<UiaElementWrapper> GetFocusedElement(UiaElementWrapper desktop)
        {
            return await OnBackgroundThread(() =>
            {
                var result = desktop.Connection.Automation.FocusedElement();

                return desktop.Connection.WrapElement(result);
            }, desktop);
        }

        public async Task<PatternId[]> GetSupportedPatterns(UiaElementWrapper element)
        {
            return await OnBackgroundThread(() =>
            {
                return element.AutomationElement.GetSupportedPatterns();
            }, element);
        }

        public async Task Invoke(UiaElementWrapper element)
        {
            await OnBackgroundThread(() =>
            {
                element.AutomationElement.Patterns.Invoke.Pattern.Invoke();
            }, element);
        }

        private void ThreadProc()
        {
            while (true)
            {
                var request = Utils.WaitTask(channel.Reader.ReadAsync());
                request.Action();
            }
        }
    }
}

