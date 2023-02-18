using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xalia
{
    internal static class Utils
    {
        static async Task DoRunTask(object o)
        {
            try
            {
                Task t = (Task)o;
                await t;
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        static void RunTaskCallback(object o)
        {
            _ = DoRunTask(o);
        }

        internal static void RunTask(Task t)
        {
            SynchronizationContext.Current.Send(RunTaskCallback, t);
        }

        internal static void DebugWriteLine(string str)
        {
            Console.Error.WriteLine(str);
        }

        internal static void DebugWriteLine(object obj)
        {
            DebugWriteLine(obj.ToString());
        }

        internal static void OnError(Exception obj)
        {
            DebugWriteLine("Unhandled exception:");
            DebugWriteLine(obj);
#if DEBUG
            Environment.FailFast(obj.ToString());
#endif
        }

        static void RunActionCallback(object o)
        {
            try
            {
                ((Action)o).Invoke();
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        internal static void RunIdle(Action action)
        {
            SynchronizationContext.Current.Post(RunActionCallback, action);
        }

        internal static T WaitTask<T>(ValueTask<T> task)
        {
            return WaitTask(task.AsTask());
        }

        internal static T WaitTask<T>(Task<T> task)
        {
            task.Wait();
            if (!(task.Exception is null))
                throw task.Exception;
            else
                return task.Result;
        }

        internal static void WaitTask(Task task)
        {
            task.Wait();
            if (!(task.Exception is null))
                throw task.Exception;
        }

        internal static bool TryGetEnvironmentVariable(string name, out string result)
        {
            result = Environment.GetEnvironmentVariable(name);
            return !(result is null);
        }
    }
}
