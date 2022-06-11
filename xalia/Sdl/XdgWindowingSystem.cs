using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Sdl
{
    internal class XdgWindowingSystem : WindowingSystem
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
