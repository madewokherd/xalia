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
        HashSet<string> watching_actions = new HashSet<string>();

        private InputSystem()
        {

        }

        public static InputSystem Instance { get; } = new InputSystem();

        public void WatchAction(string action)
        {
            watching_actions.Add(action);
            foreach (var backend in backends)
            {
                backend.WatchAction(action);
            }
        }

        public void UnwatchAction(string action)
        {
            watching_actions.Remove(action);
            foreach (var backend in backends)
            {
                backend.UnwatchAction(action);
            }
        }

        internal void RegisterBackend(InputBackend backend)
        {
            Utils.RunIdle(() => { // Delay this because the backend may not be fully constructed yet.
                backends.Add(backend);
                foreach (var action in watching_actions)
                {
                    backend.WatchAction(action);
                }
            });
        }
    }
}
