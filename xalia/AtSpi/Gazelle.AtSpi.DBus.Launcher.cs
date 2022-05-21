using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.DBus;

// Generated using Tmds.DBus.Tool based on xml in at-spi-bus-launcher.c from
// https://gitlab.gnome.org/GNOME/at-spi2-core

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace Xalia.AtSpi.DBus
{
    [DBusInterface("org.a11y.Bus")]
    interface IBus : IDBusObject
    {
        Task<string> GetAddressAsync();
    }

    [DBusInterface("org.a11y.Status")]
    interface IStatus : IDBusObject
    {
        Task<T> GetAsync<T>(string prop);
        Task<StatusProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class StatusProperties
    {
        private bool _IsEnabled = default(bool);
        public bool IsEnabled
        {
            get
            {
                return _IsEnabled;
            }

            set
            {
                _IsEnabled = (value);
            }
        }

        private bool _ScreenReaderEnabled = default(bool);
        public bool ScreenReaderEnabled
        {
            get
            {
                return _ScreenReaderEnabled;
            }

            set
            {
                _ScreenReaderEnabled = (value);
            }
        }
    }

    static class StatusExtensions
    {
        public static Task<bool> GetIsEnabledAsync(this IStatus o) => o.GetAsync<bool>("IsEnabled");
        public static Task<bool> GetScreenReaderEnabledAsync(this IStatus o) => o.GetAsync<bool>("ScreenReaderEnabled");
        public static Task SetIsEnabledAsync(this IStatus o, bool val) => o.SetAsync("IsEnabled", val);
        public static Task SetScreenReaderEnabledAsync(this IStatus o, bool val) => o.SetAsync("ScreenReaderEnabled", val);
    }
}