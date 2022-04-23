using System;
using System.Threading;
using System.Threading.Tasks;

using Tmds.DBus;

using Gazelle.UiDom;
using Gazelle.AtSpi.DBus;

namespace Gazelle.AtSpi
{
    internal class AtSpiObject : UiDomObject
    {
        internal readonly AtSpiConnection Connection;
        internal readonly string Service;
        internal readonly string Path;
        internal override string DebugId => string.Format("{0}:{1}", Service, Path);

        internal IAccessible acc;

        internal AtSpiObject(AtSpiConnection connection, string service, string path)
        {
            Path = path;
            Service = service;
            Connection = connection;
            acc = connection.connection.CreateProxy<IAccessible>(service, path);
        }

        private async Task RefreshChildrenTask()
        {
            (string, ObjectPath)[] children = await acc.GetChildrenAsync();

            for (int i=0; i<children.Length; i++)
            {
                string service = children[i].Item1;
                ObjectPath path = children[i].Item2;
                Console.WriteLine(children[i].Item1);
                Console.WriteLine(children[i].Item2);
                Console.WriteLine(await Connection.connection.CreateProxy<IAccessible>(service, path).GetNameAsync());
            }
        }

        internal void RefreshChildren()
        {
#if DEBUG
            Console.WriteLine("RefreshChildren for {0}", DebugId);
#endif
            Utils.RunTask(RefreshChildrenTask());
        }

        protected override void SetAlive(bool value)
        {
            base.SetAlive(value);

            // FIXME: Use UI decription language for this
            if (Path == "/org/a11y/atspi/accessible/root" && value == true)
            {
                RefreshChildren();
            }
        }
    }
}
