using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace GGD.UI.External
{
    /// <summary>Bounded XML parsing and location-aware schema primitives.</summary>
    public static class UiXml
    {
        /// <summary>Parses a small XML document without DTDs or external entities.</summary>
        public static XDocument Parse(string text, string name)
        {
            using var source = new StringReader(text);
            using var reader = XmlReader.Create(source, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 256 * 1024, MaxCharactersFromEntities = 0
            }, name);
            XDocument document;
            try { document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri); }
            catch (XmlException error)
            { throw new FormatException($"{name}:{error.LineNumber}:{error.LinePosition}: {error.Message}", error); }
            if (document.Root == null) throw new FormatException(name + ": empty document");
            return document;
        }

        /// <summary>Creates a diagnostic retaining source and line information.</summary>
        public static Exception Error(XObject node, string message)
        {
            var line = (IXmlLineInfo)node;
            return new FormatException($"{node.BaseUri}:{line.LineNumber}:{line.LinePosition}: {message}");
        }

        /// <summary>Requires a nonempty attribute.</summary>
        public static string Required(XElement node, string key) =>
            (string)node.Attribute(key) is string value && !string.IsNullOrWhiteSpace(value)
                ? value : throw Error(node, "Missing " + key);

        /// <summary>Reads a finite, bounded invariant number.</summary>
        public static float Number(XElement node, string key, float fallback, float minimum = -10000, float maximum = 10000)
        {
            var raw = (string)node.Attribute(key);
            if (raw == null) return fallback;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw Error(node, $"Invalid {key}: {raw} ({minimum}..{maximum})");
            return value;
        }

        /// <summary>Reads an explicit boolean.</summary>
        public static bool Boolean(XElement node, string key, bool fallback)
        {
            var raw = (string)node.Attribute(key);
            return raw == null ? fallback : bool.TryParse(raw, out bool value) ? value :
                throw Error(node, "Invalid boolean " + key);
        }

        /// <summary>Reads an HTML color.</summary>
        public static Color Color(XElement node, string key, Color fallback)
        {
            var raw = (string)node.Attribute(key);
            return raw == null ? fallback : ColorUtility.TryParseHtmlString(raw, out Color color) ? color :
                throw Error(node, "Invalid color " + key);
        }

        /// <summary>Rejects unknown attributes, namespaces and free-form text.</summary>
        public static void Attributes(XElement node, string allowed)
        {
            if (node.Name.NamespaceName.Length != 0) throw Error(node, "Namespaces are unsupported");
            foreach (var attribute in node.Attributes())
                if (attribute.Name.NamespaceName.Length != 0 || !("," + allowed + ",").Contains("," + attribute.Name.LocalName + ","))
                    throw Error(attribute, "Unknown attribute " + attribute.Name);
            foreach (var text in node.Nodes())
                if (text is XText literal && !string.IsNullOrWhiteSpace(literal.Value))
                    throw Error(text, "Use the text attribute");
        }
    }
}
