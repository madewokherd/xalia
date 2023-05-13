using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Interop;

#if WINDOWS
using static Xalia.Interop.Win32;
#endif

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

#if WINDOWS
        private static void WineDebugWriteLine(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str + "\n\0");

            while (bytes.Length > 1018)
            {
                // Line is too long for Wine to output, break it up.
                string substr = str.Substring(0, 300);

                if (substr.Contains("\n"))
                {
                    int index = substr.LastIndexOf('\n');
                    substr = str.Substring(0, index);
                    str = str.Substring(index + 1);
                }
                else
                {
                    str = str.Substring(300);
                }

                bytes = Encoding.UTF8.GetBytes(substr + "\n\0");
                __wine_dbg_output(bytes);

                bytes = Encoding.UTF8.GetBytes(str + "\n\0");
            }

            __wine_dbg_output(bytes);
        }

        static bool useWineDebug = true;
#endif

        internal static void DebugWriteLine(string str)
        {
#if WINDOWS
            if (IsWindows() && useWineDebug)
            {
                try
                {
                    WineDebugWriteLine(str);
                    return;
                }
                catch (EntryPointNotFoundException)
                {
                    useWineDebug = false;
                }
            }
#endif
            Console.Error.WriteLine(str);
        }

        internal static void DebugWriteLine(object obj)
        {
            DebugWriteLine(obj.ToString());
        }

        internal static void OnError(Exception obj)
        {
            DebugWriteLine("Unhandled exception in Xalia:");
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

        internal static Task WaitAsync(WaitHandle handle)
        {
            var result = new TaskCompletionSource<bool>();

            ThreadPool.RegisterWaitForSingleObject(handle, (object state, bool timedOut) =>
            {
                result.SetResult(timedOut);
            }, null, -1, true);

            return result.Task;
        }

        internal static Task WaitAsync(SafeWaitHandle handle, bool ownHandle)
        {
            return WaitAsync(new Win32WaitHandle(handle, ownHandle));
        }

        internal static Task WaitAsync(SafeWaitHandle handle)
        {
            return WaitAsync(handle, false);
        }

        internal static bool TryGetEnvironmentVariable(string name, out string result)
        {
            result = Environment.GetEnvironmentVariable(name);
            return !(result is null);
        }
        internal static bool IsUnix()
        {
            int p = (int)Environment.OSVersion.Platform;
            // Intentionally excluding macOS from this check as AT-SPI is not standard there
            return p == 4 || p == 128;
        }

        internal static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        internal static bool DictionariesEqual(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var value) || kvp.Value != value)
                    return false;
            }
            return true;
        }
    }
}
