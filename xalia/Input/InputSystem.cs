using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Input
{
    public class InputSystem
    {
        List<InputBackend> backends = new List<InputBackend>();
        Dictionary<string, InputState> watching_actions = new Dictionary<string, InputState>();

        private InputSystem()
        {

        }

        public static InputSystem Instance { get; } = new InputSystem();

        public class ActionStateChangeEventArgs : EventArgs
        {
            public ActionStateChangeEventArgs(string action, InputState state, InputState previousState)
            {
                Action = action;
                State = state;
                PreviousState = previousState;
            }
            public string Action { get; }
            public InputState State { get; }
            public InputState PreviousState { get; }
            public bool JustPressed => State.Pressed && !PreviousState.Pressed;
            public bool JustReleased => !State.Pressed && PreviousState.Pressed;

            // Set LockInput to true to receive further events.
            // This only applies to events from UiDomRoutine.
            // Events from InputSystem are controlled by WatchAction/UnwatchAction.
            public bool LockInput { get; set; }
        }

        public delegate void ActionStateChangeEventHandler(object sender, ActionStateChangeEventArgs e);

        public event ActionStateChangeEventHandler ActionStateChangeEvent;

        public InputState PollAction(string action)
        {
            InputState result = default;

            foreach (var backend in backends)
            {
                result = InputState.Combine(result, backend.GetActionState(action));
            }

            return result;
        }

        public void UpdateActionState(string action)
        {
            if (watching_actions.TryGetValue(action, out var old_state))
            {
                InputState new_state = PollAction(action);
                if (!old_state.Equals(new_state))
                {
                    watching_actions[action] = new_state;
                    var handler = ActionStateChangeEvent;
                    if (handler != null)
                    {
                        var args = new ActionStateChangeEventArgs(action, new_state, old_state);
                        handler(this, args);
                    }
                }
            }
        }

        public void WatchAction(string action)
        {
            watching_actions[action] = default;
            foreach (var backend in backends)
            {
                backend.WatchAction(action);
            }
            UpdateActionState(action);
        }

        public void UnwatchAction(string action)
        {
            var old_state = watching_actions[action];
            watching_actions.Remove(action);
            foreach (var backend in backends)
            {
                backend.UnwatchAction(action);
            }
            if (old_state.Kind != InputStateKind.Disconnected)
            {
                var handler = ActionStateChangeEvent;
                if (handler != null)
                {
                    InputState new_state = default;
                    new_state.Kind = InputStateKind.Disconnected;
                    var args = new ActionStateChangeEventArgs(action, old_state, new_state);
                    handler(this, args);
                }
            }
        }

        internal void RegisterBackend(InputBackend backend)
        {
            Utils.RunIdle(() => { // Delay this because the backend may not be fully constructed yet.
                backends.Add(backend);
                foreach (var action in watching_actions.Keys)
                {
                    if (backend.WatchAction(action))
                        UpdateActionState(action);
                }
            });
        }
    }
}
