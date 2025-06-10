using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Xalia.Sdl
{
    internal abstract class XdgWindowingSystem : WindowingSystem
    {
        public override bool CanShowKeyboard()
        {
            return true;
        }

        public override Task ShowKeyboardAsync()
        {
            // This is pretty low-effort, but we don't have any suitable keyboards available on XDG
            try
            {
                Process.Start("onboard");
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }

            return Task.CompletedTask;
        }
    }
}
