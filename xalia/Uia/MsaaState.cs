using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Uia
{
    internal class MsaaState : UiDomValue
    {
        internal MsaaState(int state)
        {
            State = state;
        }

        public int State { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is MsaaState st)
                return State == st.State;
            return false;
        }

        public override int GetHashCode()
        {
            return State ^ typeof(MsaaState).GetHashCode();
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (MsaaElement.msaa_name_to_state.TryGetValue(id, out var state))
            {
                return UiDomBoolean.FromBool((State & state) != 0);
            }
            if (id == "as_int")
            {
                return new UiDomInt(State);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        public override string ToString()
        {
            StringBuilder flags_string = new StringBuilder();
            bool first_flag = true;
            int flag = 1;
            foreach (string name in MsaaElement.msaa_state_names)
            {
                if ((State & flag) != 0)
                {
                    if (first_flag)
                        first_flag = false;
                    else
                        flags_string.Append("|");
                    flags_string.Append(name);
                }
                flag <<= 1;
            }
            return $"<msaa_state(0x{State:x}) {flags_string}>";
        }
    }
}
