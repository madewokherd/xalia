using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Gazelle.AtSpi;

namespace Gazelle
{
    public class MainClass
    {
#if LINUX
        const bool linuxBuild = true;
#else
        const bool linuxBuild = false;
#endif
        static async Task Init()
        {
            AtSpiConnection connection = null;
            try
            {
                if (linuxBuild || ((Environment.GetEnvironmentVariable("GAZELLE_USE_ATSPI") ?? "0") != "0"))
                {
                    connection = await AtSpiConnection.Connect();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Environment.Exit(1);
            }
        }

        public static int Main()
        {
            new Control(); // Set up WindowsFormsSynchronizationContext

            Utils.RunTask(Init());

            Application.Run();

            return 0;
        }
    }
}
