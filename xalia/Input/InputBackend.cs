using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Input
{
    public abstract class InputBackend
    {
        protected InputBackend()
        {
            // TODO: Add to InputSystem.
        }

        protected abstract bool WatchAction(string name);
        protected abstract bool UnwatchAction(string name);

        protected void ActionMappingUpdated(string name, InputMapping[] mappings)
        {
            throw new NotImplementedException();
        }

        protected void ActionStateUpdated(string name)
        {
            throw new NotImplementedException();
        }

        protected virtual bool ActivateMode(string name)
        {
            return false;
        }

        protected virtual bool DeactivateMode(string name)
        {
            return false;
        }

        protected void ModeActivated(string name)
        {
            throw new NotImplementedException();
        }

        protected void ModeDeactivated(string name)
        {
            throw new NotImplementedException();
        }
    }
}
