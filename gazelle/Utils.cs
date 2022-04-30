using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gazelle
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
                Console.WriteLine("Unhandled exception:");
                Console.WriteLine(e);
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

        internal static void OnError(Exception obj)
        {
            Console.WriteLine("Unhandled exception:");
            Console.WriteLine(obj);
        }

        static void RunActionCallback(object o)
        {
            try
            {
                ((Action)o).Invoke();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception:");
                Console.WriteLine(e);
            }
        }

        internal static void RunIdle(Action action)
        {
            SynchronizationContext.Current.Post(RunActionCallback, action);
        }

        internal static void RunIdle(Task t)
        {
            SynchronizationContext.Current.Post(RunTaskCallback, t);
        }
    }
}
