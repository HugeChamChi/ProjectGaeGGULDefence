using System.Collections.Generic;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Disposable resolved value tree with source provenance; never used as the writable authoring document.</summary>
    public sealed class ResolvedUiDocument
    {
        /// <summary>Provides identity browsing for a future source version while preserving its original XML untouched.</summary>
        public static ResolvedUiDocument ReadOnlySource(XElement source, string path)
        {
            var result = new ResolvedUiDocument { Root = new XElement("Resolved") };
            foreach (var node in source.Elements("Element")) result.Root.Add(new XElement(node));
            foreach (var node in result.Root.Descendants("Element")) if (node.Attribute("id") != null) result.SourceFiles[(string)node.Attribute("id")] = path;
            return result;
        }
        /// <summary>Resolved root containing editable elements and explicit UGUI components.</summary>
        public XElement Root { get; internal set; }
        /// <summary>Resolved identity to source filename.</summary>
        public Dictionary<string, string> SourceFiles { get; } = new Dictionary<string, string>();
        /// <summary>Resolved identity/property to source, inherited or override provenance.</summary>
        public Dictionary<string, string> Origins { get; } = new Dictionary<string, string>();
    }
}
