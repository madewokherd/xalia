using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndDialogProvider : UiDomProviderBase
    {
        public HwndDialogProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        public int DefId { get; private set; }
        public bool DefIdKnown { get; private set; }
        private bool _fetchingDefId;

        public override void DumpProperties(UiDomElement element)
        {
            if (DefIdKnown)
                Utils.DebugWriteLine($"  win32_dialog_defid: {DefId}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_win32_dialog":
                    return UiDomBoolean.True;
                case "win32_dialog_defid":
                    depends_on.Add((element, new IdentifierExpression("win32_dialog_defid")));
                    if (DefIdKnown)
                        return new UiDomInt(DefId);
                    return UiDomUndefined.Instance;
            }
            return UiDomUndefined.Instance;
        }

        static readonly UiDomValue role = new UiDomEnum(new string[] { "dialog" });

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    return role;
                case "dialog":
                    return UiDomBoolean.True;
                case "defid":
                    return element.EvaluateIdentifier("win32_dialog_defid", element.Root, depends_on);
            }
            return UiDomUndefined.Instance;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_dialog_defid":
                        if (!_fetchingDefId)
                        {
                            _fetchingDefId = true;
                            Utils.RunTask(FetchDefId());
                        }
                        return true;
                }
            }
            return false;
        }

        private async Task FetchDefId()
        {
            int result;
            try
            {
                result = (int)await SendMessageAsync(HwndProvider.Hwnd, DM_GETDEFID, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return;
            }
            if (HIWORD(result) == DC_HASDEFID)
            {
                DefId = LOWORD(result);
                DefIdKnown = true;
                HwndProvider.Element.PropertyChanged("win32_dialog_defid", DefId);
            }
        }
    }
}