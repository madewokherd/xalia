using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal abstract class NonclientProvider : UiDomProviderBase
    {
        public NonclientProvider(HwndProvider hwndProvider, UiDomElement element)
        {
            HwndProvider = hwndProvider;
            Element = element;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element { get; }
        public Win32Connection Connection => HwndProvider.Connection;
        public int Pid => HwndProvider.Pid;
        public int Tid => HwndProvider.Tid;
        public CommandThread CommandThread => HwndProvider.CommandThread;

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_win32_subelement":
                case "is_win32_nonclient":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }
    }
}