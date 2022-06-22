using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Xalia.AtSpi;
using Xalia.Gudl;
using Xalia.Ui;
using Xalia.UiDom;
using Xalia.Uia;
using Xalia.Sdl;

using SDL2;

namespace Xalia
{
    public class MainClass
    {
        static bool IsUnix()
        {
            int p = (int)Environment.OSVersion.Platform;
            // Intentionally excluding macOS from this check as AT-SPI is not standard there
            return p == 4 || p == 128;
        }

        static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        static async Task Init(GudlStatement[] config)
        {
            try
            {
                var application = new UiMain();

                UiDomRoot connection = null;

                if (((Environment.GetEnvironmentVariable("XALIA_USE_ATSPI") ?? (IsUnix() ? "1" : "0")) != "0"))
                {
                    connection = await AtSpiConnection.Connect(config, application);
                }

                if (connection == null &&
                    (Environment.GetEnvironmentVariable("XALIA_USE_UIA3") ?? (IsWindows() ? "1" : "0")) != "0")
                {
                    connection = new UiaConnection(config, application);
                }

                if (connection == null)
                {
                    Console.WriteLine("No Accessibility API available");
                }

                GameControllerInput.Init();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Environment.Exit(1);
            }
        }

        public static int Main()
        {
            SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

            SdlSynchronizationContext.Instance.Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_JOYSTICK | SDL.SDL_INIT_GAMECONTROLLER);

            GudlStatement[] config;

            if (!GudlParser.TryParse(
                Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "main.gudl"),
                out config, out var error))
            {
                Console.WriteLine(error);
            }

            Utils.RunTask(Init(config));

            SdlSynchronizationContext.Instance.MainLoop();

            return 0;
        }
    }
}
