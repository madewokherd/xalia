using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomRoutineSequence : UiDomRoutine
    {
        public UiDomRoutineSequence(UiDomRoutine first, UiDomRoutine second)
        {
            First = first;
            Second = second;
        }

        public UiDomRoutine First { get; }
        public UiDomRoutine Second { get; }

        public override bool Equals(object obj)
        {
            if (obj is UiDomRoutineSequence seq)
            {
                return First.Equals(seq.First) && Second.Equals(seq.Second);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (First, Second).GetHashCode() ^ typeof(UiDomRoutineSequence).GetHashCode();
        }

        public override string ToString()
        {
            return $"{First}+{Second}";
        }

        public override async Task OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            await First.OnInput(e);
            await Second.OnInput(e);
        }
    }
}
