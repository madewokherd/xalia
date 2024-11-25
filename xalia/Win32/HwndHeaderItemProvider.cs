using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndHeaderItemProvider : UiDomProviderBase, IWin32LocationChange
    {
        public HwndHeaderItemProvider(HwndHeaderProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndHeaderProvider Parent { get; }
        public UiDomElement Element { get; }

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + 1;
            }
        }

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "column_header", "columnheader" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
        };

        public RECT Location;
        public bool LocationKnown;
        private bool watching_location;

        private Win32RemoteProcessMemory remote_process_memory;

        public override void DumpProperties(UiDomElement element)
        {
            Utils.DebugWriteLine($"  msaa_child_id: {ChildId}");
            Parent.HwndProvider.ChildDumpProperties();
            if (LocationKnown)
            {
                var screen_location = Parent.HwndProvider.ClientRectToScreen(Location);
                Utils.DebugWriteLine($"  win32_x: {screen_location.left}");
                Utils.DebugWriteLine($"  win32_y: {screen_location.top}");
                Utils.DebugWriteLine($"  win32_width: {screen_location.width}");
                Utils.DebugWriteLine($"  win32_height: {screen_location.height}");
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_header_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_x":
                case "win32_y":
                case "win32_width":
                case "win32_height":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                    depends_on.Add((Element, new IdentifierExpression("win32_pos")));
                    if (LocationKnown)
                    {
                        var screen_location = Parent.HwndProvider.ClientRectToScreen(Location);
                        switch (identifier)
                        {
                            case "win32_x":
                                return new UiDomInt(screen_location.left);
                            case "win32_y":
                                return new UiDomInt(screen_location.top);
                            case "win32_width":
                                return new UiDomInt(screen_location.width);
                            case "win32_height":
                                return new UiDomInt(screen_location.height);
                        }
                    }
                    break;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    return role;
                case "column_header":
                case "columnheader":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        watching_location = true;
                        if (!LocationKnown)
                        {
                            Utils.RunTask(FetchLocation());
                        }
                        break;
                }
            }
            return base.WatchProperty(element, expression);
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        watching_location = false;
                        break;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        public async Task<(bool, RECT)> QueryClientLocationAsync()
        {
            if (remote_process_memory is null)
                remote_process_memory = Win32RemoteProcessMemory.FromPid(Parent.Pid);
            IntPtr result;
            RECT rc;
            using (var memory = remote_process_memory.Alloc<RECT>())
            {
                result = await SendMessageAsync(Parent.Hwnd, HDM_GETITEMRECT, (IntPtr)(ChildId - 1), new IntPtr((long)memory.Address));
                rc = memory.Read<RECT>();
            }

            if (result == IntPtr.Zero)
                return (false, default);
            return (true, rc);
        }

        private async Task FetchLocation()
        {
            var result = await QueryClientLocationAsync();

            if (!result.Item1)
            {
                if (LocationKnown)
                {
                    LocationKnown = false;
                    Element.PropertyChanged("win32_pos", "undefined");
                }
            }
            else
            {
                var rc = result.Item2;
                if (!LocationKnown || !Location.Equals(rc))
                {
                    LocationKnown = true;
                    Location = rc;
                    Element.PropertyChanged(new IdentifierExpression("win32_pos"));
                    if (Element.MatchesDebugCondition())
                    {
                        rc = Parent.HwndProvider.ClientRectToScreen(rc);
                        Utils.DebugWriteLine($"{Element}.win32_pos: {rc.left},{rc.top} {rc.width}x{rc.height}");
                    }
                }
            }
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            if (!(remote_process_memory is null))
            {
                remote_process_memory.Unref();
                remote_process_memory = null;
            }
            base.NotifyElementRemoved(element);
        }

        internal void InvalidateBounds()
        {
            if (watching_location)
            {
                Utils.RunTask(FetchLocation());
            }
            else
            {
                LocationKnown = false;
            }
        }

        public void MsaaLocationChange()
        {
            InvalidateBounds();
        }

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (!LocationKnown)
            {
                await FetchLocation();
            }
            if (LocationKnown)
            {
                var screen_location = Parent.HwndProvider.ClientRectToScreen(Location);
                return (true, (screen_location.left + screen_location.right) / 2,
                    (screen_location.top + screen_location.bottom) / 2);
            }
            return await base.GetClickablePointAsync(element);
        }
    }
}
