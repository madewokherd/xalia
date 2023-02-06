using System;
using System.Collections.Generic;

namespace Xalia.Input
{
    public class VirtualInputSink : IDisposable
    {
        public string Action { get; }

        InputState current_state;

        internal VirtualInputSink(string action)
        {
            Action = action;
        }

        public void Dispose()
        {
            InputSystem.Instance.VirtualInputBackend.RemoveInputSink(this);
        }

        internal InputState GetState()
        {
            return current_state;
        }

        public void SetInputState(InputState state)
        {
            current_state = state;
            InputSystem.Instance.VirtualInputBackend.ActionStateUpdated(Action);
        }

        public void SendInputStates(IEnumerable<InputState> states)
        {
            foreach (var state in states)
            {
                SetInputState(state);
            }
        }
    }
}
