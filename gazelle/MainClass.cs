using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Gazelle.AtSpi;
using Gazelle.Gudl;
using Gazelle.Ui;
using Gazelle.Sdl;

using SDL2;

namespace Gazelle
{
    public class MainClass
    {
#if LINUX
        const bool linuxBuild = true;
#else
        const bool linuxBuild = false;
#endif
        static async Task Init(GudlStatement[] config)
        {
            try
            {
                AtSpiConnection connection = null;

                if (linuxBuild || ((Environment.GetEnvironmentVariable("GAZELLE_USE_ATSPI") ?? "0") != "0"))
                {
                    connection = await AtSpiConnection.Connect(config);
                }

                //new UiMain(connection);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Environment.Exit(1);
            }
        }

        public static int Main()
        {
            SdlSynchronizationContext.Instance.Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_JOYSTICK);

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
