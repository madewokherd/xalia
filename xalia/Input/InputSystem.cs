using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Input
{
    public class InputSystem
    {
        private InputSystem()
        {

        }

        public static InputSystem Instance { get; } = new InputSystem();
    }
}
