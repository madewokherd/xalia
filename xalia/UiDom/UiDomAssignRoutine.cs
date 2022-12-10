using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomAssignRoutine : UiDomRoutine
    {
        public UiDomAssignRoutine(UiDomElement element, string name, UiDomValue value) : base(element)
        {
            PropName = name;
            PropValue = value;
        }

        public string PropName { get; }
        public UiDomValue PropValue { get; }

        public override Task OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            if (e.JustPressed)
            {
                Element.AssignProperty(PropName, PropValue);
            }
            return Task.CompletedTask;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomAssignRoutine assign)
            {
                return Element.Equals(assign.Element) && PropName.Equals(assign.PropName) &&
                    PropValue.Equals(assign.PropValue);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomAssignRoutine).GetHashCode() ^ (Element, PropName, PropValue).GetHashCode();
        }

        public override string ToString()
        {
            // TODO: Escape PropName?
            return $"{Element}.assign(\"{PropName}\", {PropValue})";
        }
    }
}
