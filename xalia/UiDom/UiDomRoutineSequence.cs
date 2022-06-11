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
        string action;

        public UiDomRoutineSequence(UiDomRoutine first, UiDomRoutine second)
        {
            First = first;
            Second = second;

            First.CompletedEvent += First_CompletedEvent;
            Second.CompletedEvent += Second_CompletedEvent;
        }

        private void PulseRoutine(UiDomRoutine routine)
        {
            InputState disconnected = new InputState();
            InputState pulse = new InputState(InputStateKind.Pulse);

            InputSystem.ActionStateChangeEventArgs e = new InputSystem.ActionStateChangeEventArgs(action, pulse, disconnected);

            routine.OnInput(e);

            e = new InputSystem.ActionStateChangeEventArgs(action, disconnected, pulse);

            routine.OnInput(e);
        }

        private void First_CompletedEvent(object sender, EventArgs e)
        {
            PulseRoutine(Second);
        }

        private void Second_CompletedEvent(object sender, EventArgs e)
        {
            OnCompleted(e);
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

        public override void OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            if (e.JustPressed)
            {
                action = e.Action;
                PulseRoutine(First);
            }
        }
    }
}
