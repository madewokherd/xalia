using System;
using System.Threading.Tasks;

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
        static async Task<int> Main()
        {
            if (linuxBuild || ((Environment.GetEnvironmentVariable("GAZELLE_USE_ATSPI") ?? "0") != "0"))
            {
                var connection = await AtSpiConnection.Connect();
                return 0;
            }

            return 1;
        }
    }
}
