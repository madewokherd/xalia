using System;

namespace Xalia.Input
{
    public abstract class InputBackend
    {
        protected InputBackend()
        {
            InputSystem.Instance.RegisterBackend(this);
        }

        protected internal abstract bool WatchAction(string name);
        protected internal abstract bool UnwatchAction(string name);

        protected void ActionMappingUpdated(string name, InputMapping[] mappings)
        {
        }

        protected void ActionStateUpdated(string name)
        {
            InputSystem.Instance.UpdateActionState(name);
        }

        protected internal abstract InputState GetActionState(string action);

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
