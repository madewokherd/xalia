using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AtSpiState : UiDomValue
    {
        public AtSpiState(uint[] flags)
        {
            Flags = flags;
        }

        public uint[] Flags { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is AtSpiState st)
            {
                if (Flags.Length != st.Flags.Length)
                    return false;
                for (int i=0; i<Flags.Length; i++)
                    if (Flags[i] != st.Flags[i])
                        return false;
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(AtSpiState).GetHashCode() ^ StructuralComparisons.StructuralEqualityComparer.GetHashCode(Flags);
        }

        public static uint[] SetState(uint[] flags, int state, bool value)
        {
            int idx = state / 32;
            uint flag = (uint)1 << (state % 32);
            uint[] result = (uint[])flags.Clone();
            if (value)
                result[idx] |= flag;
            else
                result[idx] &= ~flag;
            return result;
        }

        public static uint[] SetState(uint[] flags, string state, bool value)
        {
            if (AtSpiElement.name_to_state.TryGetValue(state, out var state_num))
            {
                return SetState(flags, state_num, value);
            }
            return null;
        }

        public static bool IsStateSet(uint[] flags, int state)
        {
            int idx = state / 32;
            uint flag = (uint)1 << (state % 32);
            return idx < flags.Length && (flags[idx] & flag) != 0;
        }

        public static bool IsStateSet(uint[] flags, string state)
        {
            if (AtSpiElement.name_to_state.TryGetValue(state, out var state_num))
            {
                return IsStateSet(flags, state_num);
            }
            return false;
        }

        public bool IsStateSet(int state) => IsStateSet(Flags, state);

        public bool IsStateSet(string state) => IsStateSet(Flags, state);

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (AtSpiElement.name_to_state.TryGetValue(id, out var state))
                return UiDomBoolean.FromBool(IsStateSet(state));
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            bool first_token = true;
            sb.Append("<spi_state(");
            foreach (uint value in Flags)
            {
                if (!first_token)
                    sb.Append(',');
                first_token = false;
                sb.Append($"0x{value:x}");
            }
            sb.Append(") ");
            first_token = true;
            for (int i=0; i<AtSpiElement.state_names.Length; i++)
            {
                if (IsStateSet(i))
                {
                    if (!first_token)
                        sb.Append('|');
                    first_token = false;
                    sb.Append(AtSpiElement.state_names[i]);
                }
            }
            sb.Append('>');
            return sb.ToString();
        }
    }
}
