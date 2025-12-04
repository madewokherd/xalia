using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndTabItemProvider : UiDomProviderBase
    {
        public HwndTabItemProvider(HwndTabProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndTabProvider Parent { get; }
        public UiDomElement Element { get; }
        public int Pid => Parent.Pid;
        public IntPtr Hwnd => Parent.Hwnd;

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + 1;
            }
        }

        private bool fetching_name;
        public bool NameKnown { get; private set; }
        public string Name { get; private set; }

        public override void DumpProperties(UiDomElement element)
        {
            if (NameKnown)
                Utils.DebugWriteLine($"  win32_name: {Name}");
            if (Parent.SelectionIndexKnown)
                Utils.DebugWriteLine($"  selected: {Parent.SelectionIndex == ChildId - 1}");
            if (Parent.ItemRectsKnown && ChildId <= Parent.ItemRects.Length)
            {
                Utils.DebugWriteLine($"  rect: {new Win32Rect(Parent.ItemRects[ChildId - 1])}");
            }
            base.DumpProperties(element);
        }

        static readonly UiDomEnum role = new UiDomEnum(new[] { "tab_item", "tabitem", "page_tab", "pagetab" });

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_tab_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_name":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (NameKnown && Name != null)
                        return new UiDomString(Name);
                    break;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "selected":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_selection_index")));
                    if (Parent.SelectionIndexKnown)
                        return UiDomBoolean.FromBool(Parent.SelectionIndex == ChildId - 1);
                    break;
                case "x":
                    {
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                        var rects = Parent.GetItemRects(depends_on);
                        if (!(rects is null) && ChildId <= rects.Length && Parent.HwndProvider.WindowRectsKnown)
                            return new UiDomInt(Parent.HwndProvider.X + rects[ChildId - 1].left);
                        break;
                    }
                case "y":
                    {
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                        var rects = Parent.GetItemRects(depends_on);
                        if (!(rects is null) && ChildId <= rects.Length && Parent.HwndProvider.WindowRectsKnown)
                            return new UiDomInt(Parent.HwndProvider.Y + rects[ChildId - 1].top);
                        break;
                    }
                case "width":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (!(rects is null) && ChildId <= rects.Length)
                            return new UiDomInt(rects[ChildId - 1].right - rects[ChildId - 1].left);
                        break;
                    }
                case "height":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (!(rects is null) && ChildId <= rects.Length)
                            return new UiDomInt(rects[ChildId - 1].bottom - rects[ChildId - 1].top);
                        break;
                    }
                case "rect":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (!(rects is null) && ChildId <= rects.Length)
                            return new Win32Rect(rects[ChildId - 1]);
                        break;
                    }
                case "tab_item":
                case "tabitem":
                case "page_tab":
                case "pagetab":
                case "enabled":
                case "visible":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "name":
                    return EvaluateIdentifier(element, "win32_name", depends_on);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            var rects = Parent.GetItemRects(new HashSet<(UiDomElement, GudlExpression)>());

            if (rects is null)
            {
                await Parent.FetchItemRects();
                rects = Parent.GetItemRects(new HashSet<(UiDomElement, GudlExpression)>());
            }

            if (!(rects is null) && ChildId <= rects.Length)
            {
                var rect = rects[ChildId - 1];
                return (true, rect.left + rect.width / 2, rect.top + rect.height / 2);
            }

            return await base.GetClickablePointAsync(element);
        }

        const int BUFFER_LENGTH = 64;

        private async Task<ulong> FetchName64(Win32RemoteProcessMemory mem, Win32RemoteProcessMemory.MemoryAllocation mem_text)
        {
            TCITEMW64 item = default;
            item.mask = TCIF_TEXT;
            item.pszText = mem_text.Address;
            item.cchTextMax = BUFFER_LENGTH;
            using (var mem_item = mem.WriteAlloc(item))
            {
                var msg_result = await SendMessageAsync(Hwnd, TCM_GETITEMW,
                    new IntPtr(Element.IndexInParent), new IntPtr(unchecked((long)mem_item.Address)));
                if (msg_result == IntPtr.Zero)
                    return 0;

                var ret_item = mem_item.Read<TCITEMW64>();
                return ret_item.pszText;
            }
        }

        private async Task<ulong> FetchName32(Win32RemoteProcessMemory mem, Win32RemoteProcessMemory.MemoryAllocation mem_text)
        {
            TCITEMW32 item = default;
            item.mask = TCIF_TEXT;
            item.pszText = (uint)mem_text.Address;
            item.cchTextMax = BUFFER_LENGTH;
            using (var mem_item = mem.WriteAlloc(item))
            {
                var msg_result = await SendMessageAsync(Hwnd, TCM_GETITEMW,
                    new IntPtr(Element.IndexInParent), new IntPtr(unchecked((long)mem_item.Address)));
                if (msg_result == IntPtr.Zero)
                    return 0;

                var ret_item = mem_item.Read<TCITEMW32>();
                return ret_item.pszText;
            }
        }

        private async Task FetchName()
        {
            var mem = Win32RemoteProcessMemory.FromPid(Pid);
            try {
                using (var mem_text = mem.Alloc(2 * BUFFER_LENGTH))
                {
                    ulong mem_result;
                    if (mem.Is64Bit())
                        mem_result = await FetchName64(mem, mem_text);
                    else
                        mem_result = await FetchName32(mem, mem_text);

                    if (mem_result == 0)
                        // No text associated with this item
                        return;

                    if (mem_result != mem_text.Address)
                    {
                        // MSDN says the control can change this pointer, but it's not clear how to safely read it if so
                        Utils.DebugWriteLine("WARNING: HwndTabItemProvider.FetchName got changed pointer");
                        return;
                    }

                    NameKnown = true;
                    Name = mem_text.ReadStringUni();
                    Element.PropertyChanged("win32_name", Name);
                }
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }
            finally
            {
                mem.Unref();
            }
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_name":
                        if (!NameKnown && !fetching_name)
                        {
                            // TODO: Account for name changes?
                            fetching_name = true;
                            Utils.RunTask(FetchName());
                        }
                        return true;
                }
            }
            return false;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_name":
                        return true;
                }
            }
            return false;
        }
    }
}
