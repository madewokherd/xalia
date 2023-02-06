using System.Collections.Generic;

namespace Xalia.Input
{
    internal class VirtualInputBackend : InputBackend
    {
        Dictionary<string, List<VirtualInputSink>> input_sinks = new Dictionary<string, List<VirtualInputSink>>();

        protected internal override InputState GetActionState(string action)
        {
            InputState result = default;

            if (input_sinks.TryGetValue(action, out var sinks))
            {
                foreach (var sink in sinks)
                {
                    result = InputState.Combine(result, sink.GetState());
                }
            }

            return result;
        }

        new internal void ActionStateUpdated(string action)
        {
            base.ActionStateUpdated(action);
        }

        internal VirtualInputSink AddInputSink(string action)
        {
            var result = new VirtualInputSink(action);

            List<VirtualInputSink> sinks;

            if (!input_sinks.TryGetValue(action, out sinks))
            {
                sinks = new List<VirtualInputSink>();
                input_sinks[action] = sinks;
            }

            sinks.Add(result);

            return result;
        }

        internal void RemoveInputSink(VirtualInputSink sink)
        {
            var sinks = input_sinks[sink.Action];
            sinks.Remove(sink);
            ActionStateUpdated(sink.Action);
            // ActionMappingUpdated(sink.Action);
        }

        protected internal override bool UnwatchAction(string name)
        {
            return input_sinks.TryGetValue(name, out var sinks) && sinks.Count != 0;
        }

        protected internal override bool WatchAction(string name)
        {
            return UnwatchAction(name);
        }
    }
}
