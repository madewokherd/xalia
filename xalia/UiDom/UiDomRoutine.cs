using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRoutine : UiDomValue
    {
        public UiDomObject Element { get; }
        public string Name { get; }

        public UiDomRoutine(UiDomObject element, string name)
        {
            Element = element;
            Name = name;
        }

        public UiDomRoutine() : this(null, null) { }
        public UiDomRoutine(UiDomObject element) : this(element, null) { }
        public UiDomRoutine(string name) : this(null, name) { }

        public event InputSystem.ActionStateChangeEventHandler InputEvent;

        public virtual void OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            var handler = InputEvent;
            if (handler != null)
                InputEvent(this, e);
        }

        public override string ToString()
        {
            if (Name != null)
            {
                if (Element != null)
                {
                    return $"{Element}.{Name}";
                }
                return Name;
            }
            return base.ToString();
        }

        public event EventHandler CompletedEvent;

        public virtual void OnCompleted(EventArgs e)
        {
            var handler = CompletedEvent;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
