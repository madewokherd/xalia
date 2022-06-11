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
        Dictionary<string, Queue<InputState>> injected_inputs = new Dictionary<string, Queue<InputState>>();

        VirtualInputBackend _virtualInputBackend;

        internal VirtualInputBackend VirtualInputBackend
        {
            get
            {
                if (_virtualInputBackend is null)
                {
                    _virtualInputBackend = new VirtualInputBackend();
                }
                return _virtualInputBackend;
            }
        }

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
            public bool JustPressed
            {
                get
                {
                    if (State.Kind == InputStateKind.Pulse)
                        return true;
                    if (PreviousState.Kind == InputStateKind.Disconnected)
                        // If a button was *already pressed* when first connected, it wasn't "just pressed".
                        return false;
                    if (State.Kind == InputStateKind.Repeat)
                        return true;
                    return State.Pressed && !PreviousState.Pressed;
                }
            }
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

            if (injected_inputs.TryGetValue(action, out var queue) && queue.Count != 0)
            {
                return queue.Dequeue();
            }

            foreach (var backend in backends)
            {
                result = InputState.Combine(result, backend.GetActionState(action));
            }

            return result;
        }

        public void UpdateActionState(string action)
        {
            bool injected;
            do
            {
                injected = false;
                if (injected_inputs.TryGetValue(action, out var queue))
                {
                    injected = queue.Count != 0;
                }
                InputState old_state = new InputState();
                old_state.Kind = InputStateKind.Disconnected;
                if (watching_actions.TryGetValue(action, out old_state) || injected)
                {
                    InputState new_state = PollAction(action);
                    if (!old_state.Equals(new_state))
                    {
                        if (watching_actions.ContainsKey(action))
                            watching_actions[action] = new_state;
                        var handler = ActionStateChangeEvent;
                        if (handler != null)
                        {
                            var args = new ActionStateChangeEventArgs(action, new_state, old_state);
                            handler(this, args);
                        }
                    }
                    if (injected && new_state.Kind != InputStateKind.Disconnected && !watching_actions.ContainsKey(action))
                    {
                        InputState disconnected_state = default;
                        var handler = ActionStateChangeEvent;
                        if (handler != null)
                        {
                            var args = new ActionStateChangeEventArgs(action, disconnected_state, new_state);
                            handler(this, args);
                        }
                    }                     
                }
            } while (injected);
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
                // The loop may modify values in watching_actions, which invalidates Keys while enumerating
                foreach (var action in watching_actions.Keys.ToArray())
                {
                    if (backend.WatchAction(action))
                        UpdateActionState(action);
                }
            });
        }

        public void InjectInput(string action, InputState state)
        {
            if (!injected_inputs.TryGetValue(action, out var queue))
            {
                queue = new Queue<InputState>();
                injected_inputs[action] = queue;
            }

            queue.Enqueue(state);

            UpdateActionState(action);
        }

        public VirtualInputSink CreateVirtualInput(string action)
        {
            return VirtualInputBackend.AddInputSink(action);
        }
    }
}
