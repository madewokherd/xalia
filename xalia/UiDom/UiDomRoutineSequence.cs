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

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputQueue queue1 = new InputQueue();
            Utils.RunTask(First.ProcessInputQueue(queue1));
            InputQueue queue2 = new InputQueue();
            Utils.RunTask(Second.ProcessInputQueue(queue2));
            InputState state;
            do
            {
                state = await queue.Dequeue();

                queue1.Enqueue(state);
                if (state.Kind != InputStateKind.Disconnected)
                    await queue1.WaitForConsumer();

                queue2.Enqueue(state);
                if (state.Kind != InputStateKind.Disconnected)
                    await queue2.WaitForConsumer();
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}
