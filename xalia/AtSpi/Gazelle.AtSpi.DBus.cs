using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.DBus;

// Generated using Tmds.DBus.Tool based on xml files from
// https://gitlab.gnome.org/GNOME/at-spi2-core

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace Xalia.AtSpi.DBus
{
    [DBusInterface("org.a11y.atspi.Accessible")]
    interface IAccessible : IDBusObject
    {
        Task<(string, ObjectPath)> GetChildAtIndexAsync(int Index);
        Task<(string, ObjectPath)[]> GetChildrenAsync();
        Task<int> GetIndexInParentAsync();
        Task<(uint, (string, ObjectPath)[])[]> GetRelationSetAsync();
        Task<uint> GetRoleAsync();
        Task<string> GetRoleNameAsync();
        Task<string> GetLocalizedRoleNameAsync();
        Task<uint[]> GetStateAsync();
        Task<IDictionary<string, string>> GetAttributesAsync();
        Task<(string, ObjectPath)> GetApplicationAsync();
        Task<string[]> GetInterfacesAsync();
        Task<T> GetAsync<T>(string prop);
        Task<AccessibleProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class AccessibleProperties
    {
        private string _Name = default(string);
        public string Name
        {
            get
            {
                return _Name;
            }

            set
            {
                _Name = (value);
            }
        }

        private string _Description = default(string);
        public string Description
        {
            get
            {
                return _Description;
            }

            set
            {
                _Description = (value);
            }
        }

        private (string, ObjectPath) _Parent = default((string, ObjectPath));
        public (string, ObjectPath) Parent
        {
            get
            {
                return _Parent;
            }

            set
            {
                _Parent = (value);
            }
        }

        private int _ChildCount = default(int);
        public int ChildCount
        {
            get
            {
                return _ChildCount;
            }

            set
            {
                _ChildCount = (value);
            }
        }

        private string _Locale = default(string);
        public string Locale
        {
            get
            {
                return _Locale;
            }

            set
            {
                _Locale = (value);
            }
        }

        private string _AccessibleId = default(string);
        public string AccessibleId
        {
            get
            {
                return _AccessibleId;
            }

            set
            {
                _AccessibleId = (value);
            }
        }
    }

    static class AccessibleExtensions
    {
        public static Task<string> GetNameAsync(this IAccessible o) => o.GetAsync<string>("Name");
        public static Task<string> GetDescriptionAsync(this IAccessible o) => o.GetAsync<string>("Description");
        public static Task<(string, ObjectPath)> GetParentAsync(this IAccessible o) => o.GetAsync<(string, ObjectPath)>("Parent");
        public static Task<int> GetChildCountAsync(this IAccessible o) => o.GetAsync<int>("ChildCount");
        public static Task<string> GetLocaleAsync(this IAccessible o) => o.GetAsync<string>("Locale");
        public static Task<string> GetAccessibleIdAsync(this IAccessible o) => o.GetAsync<string>("AccessibleId");
    }

    [DBusInterface("org.a11y.atspi.Action")]
    interface IAction : IDBusObject
    {
        Task<string> GetDescriptionAsync(int Index);
        Task<string> GetNameAsync(int Index);
        Task<string> GetLocalizedNameAsync(int Index);
        Task<string> GetKeyBindingAsync(int Index);
        Task<(string, string, string)[]> GetActionsAsync();
        Task<bool> DoActionAsync(int Index);
        Task<T> GetAsync<T>(string prop);
        Task<ActionProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class ActionProperties
    {
        private int _NActions = default(int);
        public int NActions
        {
            get
            {
                return _NActions;
            }

            set
            {
                _NActions = (value);
            }
        }
    }

    static class ActionExtensions
    {
        public static Task<int> GetNActionsAsync(this IAction o) => o.GetAsync<int>("NActions");
    }

    [DBusInterface("org.a11y.atspi.Application")]
    interface IApplication : IDBusObject
    {
        Task<string> GetLocaleAsync(uint Lctype);
        Task RegisterEventListenerAsync(string Event);
        Task DeregisterEventListenerAsync(string Event);
        Task<T> GetAsync<T>(string prop);
        Task<ApplicationProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class ApplicationProperties
    {
        private string _ToolkitName = default(string);
        public string ToolkitName
        {
            get
            {
                return _ToolkitName;
            }

            set
            {
                _ToolkitName = (value);
            }
        }

        private string _Version = default(string);
        public string Version
        {
            get
            {
                return _Version;
            }

            set
            {
                _Version = (value);
            }
        }

        private string _AtspiVersion = default(string);
        public string AtspiVersion
        {
            get
            {
                return _AtspiVersion;
            }

            set
            {
                _AtspiVersion = (value);
            }
        }

        private int _Id = default(int);
        public int Id
        {
            get
            {
                return _Id;
            }

            set
            {
                _Id = (value);
            }
        }
    }

    static class ApplicationExtensions
    {
        public static Task<string> GetToolkitNameAsync(this IApplication o) => o.GetAsync<string>("ToolkitName");
        public static Task<string> GetVersionAsync(this IApplication o) => o.GetAsync<string>("Version");
        public static Task<string> GetAtspiVersionAsync(this IApplication o) => o.GetAsync<string>("AtspiVersion");
        public static Task<int> GetIdAsync(this IApplication o) => o.GetAsync<int>("Id");
        public static Task SetIdAsync(this IApplication o, int val) => o.SetAsync("Id", val);
    }

    [DBusInterface("org.a11y.atspi.Cache")]
    interface ICache : IDBusObject
    {
        Task<((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[])[]> GetItemsAsync();
        Task<IDisposable> WatchAddAccessibleAsync(Action<((string, ObjectPath) nodeAdded, (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[])> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRemoveAccessibleAsync(Action<(string nodeRemoved, ObjectPath)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Collection")]
    interface ICollection : IDBusObject
    {
        Task<(string, ObjectPath)[]> GetMatchesAsync((int[], int, IDictionary<string, string>, int, int[], int, string[], int, bool) Rule, uint Sortby, int Count, bool Traverse);
        Task<(string, ObjectPath)[]> GetMatchesToAsync(ObjectPath CurrentObject, (int[], int, IDictionary<string, string>, int, int[], int, string[], int, bool) Rule, uint Sortby, uint Tree, bool LimitScope, int Count, bool Traverse);
        Task<(string, ObjectPath)[]> GetMatchesFromAsync(ObjectPath CurrentObject, (int[], int, IDictionary<string, string>, int, int[], int, string[], int, bool) Rule, uint Sortby, uint Tree, int Count, bool Traverse);
        Task<(string, ObjectPath)> GetActiveDescendantAsync();
    }

    [DBusInterface("org.a11y.atspi.Component")]
    interface IComponent : IDBusObject
    {
        Task<bool> ContainsAsync(int X, int Y, uint CoordType);
        Task<(string, ObjectPath)> GetAccessibleAtPointAsync(int X, int Y, uint CoordType);
        Task<(int, int, int, int)> GetExtentsAsync(uint CoordType);
        Task<(int x, int y)> GetPositionAsync(uint CoordType);
        Task<(int width, int height)> GetSizeAsync();
        Task<uint> GetLayerAsync();
        Task<short> GetMDIZOrderAsync();
        Task<bool> GrabFocusAsync();
        Task<double> GetAlphaAsync();
        Task<bool> SetExtentsAsync(int X, int Y, int Width, int Height, uint CoordType);
        Task<bool> SetPositionAsync(int X, int Y, uint CoordType);
        Task<bool> SetSizeAsync(int Width, int Height);
        Task<bool> ScrollToAsync(uint Type);
        Task<bool> ScrollToPointAsync(uint Type, int X, int Y);
    }

    [DBusInterface("org.a11y.atspi.DeviceEventController")]
    interface IDeviceEventController : IDBusObject
    {
        Task<bool> RegisterKeystrokeListenerAsync(ObjectPath Listener, (int, int, string, int)[] Keys, uint Mask, uint[] Type, (bool, bool, bool) Mode);
        Task DeregisterKeystrokeListenerAsync(ObjectPath Listener, (int, int, string, int)[] Keys, uint Mask, uint Type);
        Task<bool> RegisterDeviceEventListenerAsync(ObjectPath Listener, uint Types);
        Task DeregisterDeviceEventListenerAsync(ObjectPath Listener, uint Types);
        Task GenerateKeyboardEventAsync(int Keycode, string Keystring, uint Type);
        Task GenerateMouseEventAsync(int X, int Y, string EventName);
        Task<bool> NotifyListenersSyncAsync((uint, int, uint, uint, int, string, bool) Event);
        Task NotifyListenersAsyncAsync((uint, int, uint, uint, int, string, bool) Event);
    }

    [DBusInterface("org.a11y.atspi.DeviceEventListener")]
    interface IDeviceEventListener : IDBusObject
    {
        Task<bool> NotifyEventAsync((uint, int, uint, uint, int, string, bool) Event);
    }

    [DBusInterface("org.a11y.atspi.Document")]
    interface IDocument : IDBusObject
    {
        Task<string> GetLocaleAsync();
        Task<string> GetAttributeValueAsync(string Attributename);
        Task<IDictionary<string, string>> GetAttributesAsync();
        Task<T> GetAsync<T>(string prop);
        Task<DocumentProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class DocumentProperties
    {
        private int _CurrentPageNumber = default(int);
        public int CurrentPageNumber
        {
            get
            {
                return _CurrentPageNumber;
            }

            set
            {
                _CurrentPageNumber = (value);
            }
        }

        private int _PageCount = default(int);
        public int PageCount
        {
            get
            {
                return _PageCount;
            }

            set
            {
                _PageCount = (value);
            }
        }
    }

    static class DocumentExtensions
    {
        public static Task<int> GetCurrentPageNumberAsync(this IDocument o) => o.GetAsync<int>("CurrentPageNumber");
        public static Task<int> GetPageCountAsync(this IDocument o) => o.GetAsync<int>("PageCount");
    }

    [DBusInterface("org.a11y.atspi.EditableText")]
    interface IEditableText : IDBusObject
    {
        Task<bool> SetTextContentsAsync(string NewContents);
        Task<bool> InsertTextAsync(int Position, string Text, int Length);
        Task CopyTextAsync(int StartPos, int EndPos);
        Task<bool> CutTextAsync(int StartPos, int EndPos);
        Task<bool> DeleteTextAsync(int StartPos, int EndPos);
        Task<bool> PasteTextAsync(int Position);
    }

    [DBusInterface("org.a11y.atspi.Event.Object")]
    interface IObject : IDBusObject
    {
        Task<IDisposable> WatchPropertyChangeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchBoundsChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchLinkSelectedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchStateChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchChildrenChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchVisibleDataChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchSelectionChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchModelChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchActiveDescendantChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRowInsertedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRowReorderedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRowDeletedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchColumnInsertedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchColumnReorderedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchColumnDeletedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTextBoundsChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTextSelectionChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTextChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTextAttributesChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchTextCaretMovedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchAttributesChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Window")]
    interface IWindow : IDBusObject
    {
        Task<IDisposable> WatchPropertyChangeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchMinimizeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchMaximizeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRestoreAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchCloseAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchCreateAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchReparentAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchDesktopCreateAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchDesktopDestroyAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchDestroyAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchActivateAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchDeactivateAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRaiseAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchLowerAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchMoveAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchResizeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchShadeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchuUshadeAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRestyleAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Mouse")]
    interface IMouse : IDBusObject
    {
        Task<IDisposable> WatchAbsAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchRelAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchButtonAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Keyboard")]
    interface IKeyboard : IDBusObject
    {
        Task<IDisposable> WatchModifiersAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Terminal")]
    interface ITerminal : IDBusObject
    {
        Task<IDisposable> WatchLineChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchColumncountChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchLinecountChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchApplicationChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchCharwidthChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Document")]
    interface IDocument0 : IDBusObject
    {
        Task<IDisposable> WatchLoadCompleteAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchReloadAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchLoadStoppedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchContentChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchAttributesChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchPageChangedAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Event.Focus")]
    interface IFocus : IDBusObject
    {
        Task<IDisposable> WatchFocusAsync(Action<(string, uint, uint, object)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Hyperlink")]
    interface IHyperlink : IDBusObject
    {
        Task<(string, ObjectPath)> GetObjectAsync(int I);
        Task<string> GetURIAsync(int I);
        Task<bool> IsValidAsync();
        Task<T> GetAsync<T>(string prop);
        Task<HyperlinkProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class HyperlinkProperties
    {
        private short _NAnchors = default(short);
        public short NAnchors
        {
            get
            {
                return _NAnchors;
            }

            set
            {
                _NAnchors = (value);
            }
        }

        private int _StartIndex = default(int);
        public int StartIndex
        {
            get
            {
                return _StartIndex;
            }

            set
            {
                _StartIndex = (value);
            }
        }

        private int _EndIndex = default(int);
        public int EndIndex
        {
            get
            {
                return _EndIndex;
            }

            set
            {
                _EndIndex = (value);
            }
        }
    }

    static class HyperlinkExtensions
    {
        public static Task<short> GetNAnchorsAsync(this IHyperlink o) => o.GetAsync<short>("NAnchors");
        public static Task<int> GetStartIndexAsync(this IHyperlink o) => o.GetAsync<int>("StartIndex");
        public static Task<int> GetEndIndexAsync(this IHyperlink o) => o.GetAsync<int>("EndIndex");
    }

    [DBusInterface("org.a11y.atspi.Hypertext")]
    interface IHypertext : IDBusObject
    {
        Task<int> GetNLinksAsync();
        Task<(string, ObjectPath)> GetLinkAsync(int LinkIndex);
        Task<int> GetLinkIndexAsync(int CharacterIndex);
    }

    [DBusInterface("org.a11y.atspi.Image")]
    interface IImage : IDBusObject
    {
        Task<(int, int, int, int)> GetImageExtentsAsync(uint CoordType);
        Task<(int x, int y)> GetImagePositionAsync(uint CoordType);
        Task<(int width, int height)> GetImageSizeAsync();
        Task<T> GetAsync<T>(string prop);
        Task<ImageProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class ImageProperties
    {
        private string _ImageDescription = default(string);
        public string ImageDescription
        {
            get
            {
                return _ImageDescription;
            }

            set
            {
                _ImageDescription = (value);
            }
        }

        private string _ImageLocale = default(string);
        public string ImageLocale
        {
            get
            {
                return _ImageLocale;
            }

            set
            {
                _ImageLocale = (value);
            }
        }
    }

    static class ImageExtensions
    {
        public static Task<string> GetImageDescriptionAsync(this IImage o) => o.GetAsync<string>("ImageDescription");
        public static Task<string> GetImageLocaleAsync(this IImage o) => o.GetAsync<string>("ImageLocale");
    }

    [DBusInterface("org.a11y.atspi.Registry")]
    interface IRegistry : IDBusObject
    {
        Task RegisterEventAsync(string Event);
        Task DeregisterEventAsync(string Event);
        Task<(string, string)[]> GetRegisteredEventsAsync();
        Task<IDisposable> WatchEventListenerRegisteredAsync(Action<(string bus, string path)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchEventListenerDeregisteredAsync(Action<(string bus, string path)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Selection")]
    interface ISelection : IDBusObject
    {
        Task<(string, ObjectPath)> GetSelectedChildAsync(int SelectedChildIndex);
        Task<bool> SelectChildAsync(int ChildIndex);
        Task<bool> DeselectSelectedChildAsync(int SelectedChildIndex);
        Task<bool> IsChildSelectedAsync(int ChildIndex);
        Task<bool> SelectAllAsync();
        Task<bool> ClearSelectionAsync();
        Task<bool> DeselectChildAsync(int ChildIndex);
        Task<T> GetAsync<T>(string prop);
        Task<SelectionProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class SelectionProperties
    {
        private int _NSelectedChildren = default(int);
        public int NSelectedChildren
        {
            get
            {
                return _NSelectedChildren;
            }

            set
            {
                _NSelectedChildren = (value);
            }
        }
    }

    static class SelectionExtensions
    {
        public static Task<int> GetNSelectedChildrenAsync(this ISelection o) => o.GetAsync<int>("NSelectedChildren");
    }

    [DBusInterface("org.a11y.atspi.Socket")]
    interface ISocket : IDBusObject
    {
        Task<(string socket, ObjectPath)> EmbedAsync((string, ObjectPath) Plug);
        Task UnembedAsync((string, ObjectPath) Plug);
        Task<IDisposable> WatchAvailableAsync(Action<(string socket, ObjectPath)> handler, Action<Exception> onError = null);
    }

    [DBusInterface("org.a11y.atspi.Table")]
    interface ITable : IDBusObject
    {
        Task<(string, ObjectPath)> GetAccessibleAtAsync(int Row, int Column);
        Task<int> GetIndexAtAsync(int Row, int Column);
        Task<int> GetRowAtIndexAsync(int Index);
        Task<int> GetColumnAtIndexAsync(int Index);
        Task<string> GetRowDescriptionAsync(int Row);
        Task<string> GetColumnDescriptionAsync(int Column);
        Task<int> GetRowExtentAtAsync(int Row, int Column);
        Task<int> GetColumnExtentAtAsync(int Row, int Column);
        Task<(string, ObjectPath)> GetRowHeaderAsync(int Row);
        Task<(string, ObjectPath)> GetColumnHeaderAsync(int Column);
        Task<int[]> GetSelectedRowsAsync();
        Task<int[]> GetSelectedColumnsAsync();
        Task<bool> IsRowSelectedAsync(int Row);
        Task<bool> IsColumnSelectedAsync(int Column);
        Task<bool> IsSelectedAsync(int Row, int Column);
        Task<bool> AddRowSelectionAsync(int Row);
        Task<bool> AddColumnSelectionAsync(int Column);
        Task<bool> RemoveRowSelectionAsync(int Row);
        Task<bool> RemoveColumnSelectionAsync(int Column);
        Task<(bool, int row, int col, int rowExtents, int colExtents, bool isSelected)> GetRowColumnExtentsAtIndexAsync(int Index);
        Task<T> GetAsync<T>(string prop);
        Task<TableProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class TableProperties
    {
        private int _NRows = default(int);
        public int NRows
        {
            get
            {
                return _NRows;
            }

            set
            {
                _NRows = (value);
            }
        }

        private int _NColumns = default(int);
        public int NColumns
        {
            get
            {
                return _NColumns;
            }

            set
            {
                _NColumns = (value);
            }
        }

        private (string, ObjectPath) _Caption = default((string, ObjectPath));
        public (string, ObjectPath) Caption
        {
            get
            {
                return _Caption;
            }

            set
            {
                _Caption = (value);
            }
        }

        private (string, ObjectPath) _Summary = default((string, ObjectPath));
        public (string, ObjectPath) Summary
        {
            get
            {
                return _Summary;
            }

            set
            {
                _Summary = (value);
            }
        }

        private int _NSelectedRows = default(int);
        public int NSelectedRows
        {
            get
            {
                return _NSelectedRows;
            }

            set
            {
                _NSelectedRows = (value);
            }
        }

        private int _NSelectedColumns = default(int);
        public int NSelectedColumns
        {
            get
            {
                return _NSelectedColumns;
            }

            set
            {
                _NSelectedColumns = (value);
            }
        }
    }

    static class TableExtensions
    {
        public static Task<int> GetNRowsAsync(this ITable o) => o.GetAsync<int>("NRows");
        public static Task<int> GetNColumnsAsync(this ITable o) => o.GetAsync<int>("NColumns");
        public static Task<(string, ObjectPath)> GetCaptionAsync(this ITable o) => o.GetAsync<(string, ObjectPath)>("Caption");
        public static Task<(string, ObjectPath)> GetSummaryAsync(this ITable o) => o.GetAsync<(string, ObjectPath)>("Summary");
        public static Task<int> GetNSelectedRowsAsync(this ITable o) => o.GetAsync<int>("NSelectedRows");
        public static Task<int> GetNSelectedColumnsAsync(this ITable o) => o.GetAsync<int>("NSelectedColumns");
    }

    [DBusInterface("org.a11y.atspi.TableCell")]
    interface ITableCell : IDBusObject
    {
        Task<(bool, int row, int col, int rowExtents, int colExtents)> GetRowColumnSpanAsync();
        Task<T> GetAsync<T>(string prop);
        Task<TableCellProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class TableCellProperties
    {
        private int _ColumnSpan = default(int);
        public int ColumnSpan
        {
            get
            {
                return _ColumnSpan;
            }

            set
            {
                _ColumnSpan = (value);
            }
        }

        private (int, int) _Position = default((int, int));
        public (int, int) Position
        {
            get
            {
                return _Position;
            }

            set
            {
                _Position = (value);
            }
        }

        private int _RowSpan = default(int);
        public int RowSpan
        {
            get
            {
                return _RowSpan;
            }

            set
            {
                _RowSpan = (value);
            }
        }

        private (string, ObjectPath) _Table = default((string, ObjectPath));
        public (string, ObjectPath) Table
        {
            get
            {
                return _Table;
            }

            set
            {
                _Table = (value);
            }
        }
    }

    static class TableCellExtensions
    {
        public static Task<int> GetColumnSpanAsync(this ITableCell o) => o.GetAsync<int>("ColumnSpan");
        public static Task<(int, int)> GetPositionAsync(this ITableCell o) => o.GetAsync<(int, int)>("Position");
        public static Task<int> GetRowSpanAsync(this ITableCell o) => o.GetAsync<int>("RowSpan");
        public static Task<(string, ObjectPath)> GetTableAsync(this ITableCell o) => o.GetAsync<(string, ObjectPath)>("Table");
    }

    [DBusInterface("org.a11y.atspi.Text")]
    interface IText : IDBusObject
    {
        Task<(string, int startOffset, int endOffset)> GetStringAtOffsetAsync(int Offset, uint Granularity);
        Task<string> GetTextAsync(int StartOffset, int EndOffset);
        Task<bool> SetCaretOffsetAsync(int Offset);
        Task<(string, int startOffset, int endOffset)> GetTextBeforeOffsetAsync(int Offset, uint Type);
        Task<(string, int startOffset, int endOffset)> GetTextAtOffsetAsync(int Offset, uint Type);
        Task<(string, int startOffset, int endOffset)> GetTextAfterOffsetAsync(int Offset, uint Type);
        Task<int> GetCharacterAtOffsetAsync(int Offset);
        Task<string> GetAttributeValueAsync(int Offset, string AttributeName);
        Task<(IDictionary<string, string>, int startOffset, int endOffset)> GetAttributesAsync(int Offset);
        Task<IDictionary<string, string>> GetDefaultAttributesAsync();
        Task<(int x, int y, int width, int height)> GetCharacterExtentsAsync(int Offset, uint CoordType);
        Task<int> GetOffsetAtPointAsync(int X, int Y, uint CoordType);
        Task<int> GetNSelectionsAsync();
        Task<(int startOffset, int endOffset)> GetSelectionAsync(int SelectionNum);
        Task<bool> AddSelectionAsync(int StartOffset, int EndOffset);
        Task<bool> RemoveSelectionAsync(int SelectionNum);
        Task<bool> SetSelectionAsync(int SelectionNum, int StartOffset, int EndOffset);
        Task<(int x, int y, int width, int height)> GetRangeExtentsAsync(int StartOffset, int EndOffset, uint CoordType);
        Task<(int, int, string, object)[]> GetBoundedRangesAsync(int X, int Y, int Width, int Height, uint CoordType, uint XClipType, uint YClipType);
        Task<(IDictionary<string, string>, int startOffset, int endOffset)> GetAttributeRunAsync(int Offset, bool IncludeDefaults);
        Task<IDictionary<string, string>> GetDefaultAttributeSetAsync();
        Task<bool> ScrollSubstringToAsync(int StartOffset, int EndOffset, uint Type);
        Task<bool> ScrollSubstringToPointAsync(int StartOffset, int EndOffset, uint Type, int X, int Y);
        Task<T> GetAsync<T>(string prop);
        Task<TextProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class TextProperties
    {
        private int _CharacterCount = default(int);
        public int CharacterCount
        {
            get
            {
                return _CharacterCount;
            }

            set
            {
                _CharacterCount = (value);
            }
        }

        private int _CaretOffset = default(int);
        public int CaretOffset
        {
            get
            {
                return _CaretOffset;
            }

            set
            {
                _CaretOffset = (value);
            }
        }
    }

    static class TextExtensions
    {
        public static Task<int> GetCharacterCountAsync(this IText o) => o.GetAsync<int>("CharacterCount");
        public static Task<int> GetCaretOffsetAsync(this IText o) => o.GetAsync<int>("CaretOffset");
    }

    [DBusInterface("org.a11y.atspi.Value")]
    interface IValue : IDBusObject
    {
        Task<T> GetAsync<T>(string prop);
        Task<ValueProperties> GetAllAsync();
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    [Dictionary]
    class ValueProperties
    {
        private double _MinimumValue = default(double);
        public double MinimumValue
        {
            get
            {
                return _MinimumValue;
            }

            set
            {
                _MinimumValue = (value);
            }
        }

        private double _MaximumValue = default(double);
        public double MaximumValue
        {
            get
            {
                return _MaximumValue;
            }

            set
            {
                _MaximumValue = (value);
            }
        }

        private double _MinimumIncrement = default(double);
        public double MinimumIncrement
        {
            get
            {
                return _MinimumIncrement;
            }

            set
            {
                _MinimumIncrement = (value);
            }
        }

        private double _CurrentValue = default(double);
        public double CurrentValue
        {
            get
            {
                return _CurrentValue;
            }

            set
            {
                _CurrentValue = (value);
            }
        }
    }

    static class ValueExtensions
    {
        public static Task<double> GetMinimumValueAsync(this IValue o) => o.GetAsync<double>("MinimumValue");
        public static Task<double> GetMaximumValueAsync(this IValue o) => o.GetAsync<double>("MaximumValue");
        public static Task<double> GetMinimumIncrementAsync(this IValue o) => o.GetAsync<double>("MinimumIncrement");
        public static Task<double> GetCurrentValueAsync(this IValue o) => o.GetAsync<double>("CurrentValue");
        public static Task SetCurrentValueAsync(this IValue o, double val) => o.SetAsync("CurrentValue", val);
    }
}
