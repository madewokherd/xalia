using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AccessibleProvider : IUiDomProvider
    {
        public AccessibleProvider(AtSpiConnection connection, string peer, string path)
        {
            Connection = connection;
            Peer = peer;
            Path = path;
        }

        public AtSpiConnection Connection { get; }
        public string Peer { get; }
        public string Path { get; }

        public void DumpProperties(UiDomElement element)
        {
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_uia_element":
                    return UiDomBoolean.False;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.True;
                case "spi_peer":
                    return new UiDomString(Peer);
                case "spi_path":
                    return new UiDomString(Path);
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }
    }
}
