using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Input
{
    public class InputQueue
    {
        private Queue<InputState> states = new Queue<InputState>();
        private TaskCompletionSource<bool> input_ready_task = new TaskCompletionSource<bool>();
        private TaskCompletionSource<bool> input_exhausted_task = new TaskCompletionSource<bool>();

        public InputQueue(string action)
        {
            Action = action;
        }

        public string Action { get; }

        private InputState DequeueInternal()
        {
            var result = states.Dequeue();
            input_ready_task = new TaskCompletionSource<bool>();
            return result;
        }

        public async Task<InputState> Dequeue(TimeSpan timeout)
        {
            while (states.Count == 0)
            {
                if (!input_exhausted_task.Task.IsCompleted)
                    input_exhausted_task.SetResult(true);
                await Task.WhenAny(input_ready_task.Task, Task.Delay(timeout));
            }

            return DequeueInternal();
        }

        public async Task<InputState> Dequeue()
        {
            while (states.Count == 0)
            {
                if (!input_exhausted_task.Task.IsCompleted)
                    input_exhausted_task.SetResult(true);
                await input_ready_task.Task;
            }

            return DequeueInternal();
        }

        public async Task<InputState> Dequeue(int timeout_ms)
        {
            while (states.Count == 0)
            {
                if (!input_exhausted_task.Task.IsCompleted)
                    input_exhausted_task.SetResult(true);
                await Task.WhenAny(input_ready_task.Task, Task.Delay(timeout_ms));
            }

            return DequeueInternal();
        }


        public void Enqueue(InputState state)
        {
            states.Enqueue(state);

            if (input_exhausted_task.Task.IsCompleted)
                input_exhausted_task = new TaskCompletionSource<bool>();
            if (!input_ready_task.Task.IsCompleted)
                input_ready_task.SetResult(true);
        }

        public Task WaitForConsumer()
        {
            return input_exhausted_task.Task;
        }

        public Task WaitForInput()
        {
            if (states.Count != 0)
                return Task.CompletedTask;
            if (!input_exhausted_task.Task.IsCompleted)
                input_exhausted_task.SetResult(true);
            return input_ready_task.Task;
        }

        public bool IsEmpty
        {
            get
            {
                return states.Count == 0;
            }
        }
    }
}
