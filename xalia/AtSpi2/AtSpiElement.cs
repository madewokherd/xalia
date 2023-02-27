using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AtSpiElement : UiDomElement
    {
        public AtSpiElement(AtSpiConnection root, string peer, string path): base(root)
        {
            Root = root;
            Peer = peer;
            Path = path;
        }

        public new AtSpiConnection Root { get; }
        public string Peer { get; }
        public string Path { get; }

        public override string DebugId => $"{Peer}:{Path}";

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "spi_peer":
                    return new UiDomString(Peer);
                case "spi_path":
                    return new UiDomString(Path);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
