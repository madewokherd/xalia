#if WINDOWS
using Microsoft.Win32.SafeHandles;
#endif
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Ui;
using Xalia.UiDom;
using Xalia.Sdl;

using static SDL3.SDL;
#if WINDOWS
using static Xalia.Interop.Win32;
using Xalia.Win32;
#endif

namespace Xalia
{
    public class MainClass
    {
#if WINDOWS
        static async Task MakeWineSystemProcess()
        {
            int status = NtSetInformationProcess(GetCurrentProcess(),
                PROCESSINFOCLASS.ProcessWineMakeProcessSystem,
                out SafeWaitHandle exit_event, IntPtr.Size);

            Marshal.ThrowExceptionForHR(status);

            await Utils.WaitAsync(exit_event, true);

            Environment.Exit(0);
        }
#endif

        static async Task Init(GudlStatement[] config)
        {
            try
            {
                var application = new UiMain();

                UiDomRoot connection = null;

                if (connection == null &&
                    ((Environment.GetEnvironmentVariable("XALIA_USE_ATSPI") ?? (Utils.IsUnix() ? "1" : "0")) != "0"))
                {
                    connection = new UiDomRoot(config, application);
                    await Xalia.AtSpi2.AtSpiConnection.Connect(connection);
                }

#if WINDOWS
                if (connection == null &&
                    ((Environment.GetEnvironmentVariable("XALIA_USE_WIN32") ?? (Utils.IsWindows() ? "1" : "0")) != "0"))
                {
                    connection = new UiDomRoot(config, application);
                    new Win32Connection(connection);
                }
#endif

                if (connection == null)
                {
                    Utils.DebugWriteLine("No Accessibility API available");
                }

                GameControllerInput.Init();

#if WINFORMS
                if ((Environment.GetEnvironmentVariable("XALIA_DEBUG_TREEVIEW") ?? "0") == "1")
                {
                    var main_context = SynchronizationContext.Current;
                    Thread treeview_thread = new Thread(() =>
                    {
                        Xalia.Viewer.UiDomViewer.ThreadProc(main_context, connection);
                    });
                    treeview_thread.SetApartmentState(ApartmentState.STA);
                    treeview_thread.Start();
                }
#endif
            }
            catch (Exception e)
            {
                Utils.DebugWriteLine(e);
                Environment.Exit(1);
            }
        }

        [STAThread()]
        public static int Main(string[] argv)
        {
            AppDomain.CurrentDomain.UnhandledException += Utils.UnhandledException;

#if NETCOREAPP3_0_OR_GREATER
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), OnDllImport);
#endif

            SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

            SdlSynchronizationContext.Instance.Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD);

#if WINDOWS
            if (Utils.IsWindows() &&
                (argv.Contains("-wineSystemProcess") ||
                 (Utils.TryGetEnvironmentVariable("XALIA_WINE_SYSTEM_PROCESS", out var system_process) && system_process != "0")))
            {
                Utils.RunTask(MakeWineSystemProcess());
            }
#endif

            GudlStatement[] config;

            if (!GudlParser.TryParse(
                Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "main.gudl"),
                out config, out var error))
            {
                Utils.DebugWriteLine(error);
            }

            Utils.RunTask(Init(config));

            SdlSynchronizationContext.Instance.MainLoop();

            return 0;
        }

#if NETCOREAPP3_0_OR_GREATER
        private static IntPtr OnDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            string actual_library = null;

            switch ($"{Environment.OSVersion.Platform}:{libraryName}")
            {
                case "Win32NT:SDL3":
                    actual_library = "SDL3.dll";
                    break;
                case "Unix:SDL3":
                    actual_library = "libSDL3.so.0";
                    break;
                case "MacOSX:SDL3":
                    actual_library = "libSDL3.0.dylib";
                    break;
                case "Unix:X11":
                    actual_library = "libX11.so.6";
                    break;
                case "Unix:Xext":
                    actual_library = "libXext.so.6";
                    break;
                case "Unix:Xtst":
                    actual_library = "libXtst.so.6";
                    break;
            }

            if (!(actual_library is null) && NativeLibrary.TryLoad(actual_library, assembly, searchPath, out IntPtr result))
                return result;

            return IntPtr.Zero;
        }
#endif
    }
}
