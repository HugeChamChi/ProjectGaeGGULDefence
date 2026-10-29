using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace GGD.UI.External
{
    /// <summary>Validated, version-one UGUI screen definition; independent of preview services.</summary>
    public sealed class UiDocument
    {
        private const string LayoutAttributes = "id,style,x,y,width,height,background,color,fontSize,image,interactable";
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _images;

        /// <summary>Validated screen tree with resolved style attributes.</summary>
        public XElement Screen { get; private set; }
        /// <summary>Design width used by CanvasScaler, matching the game's width-based scaling.</summary>
        public float ReferenceWidth { get; private set; }
        /// <summary>Design height used for the initial window aspect ratio.</summary>
        public float ReferenceHeight { get; private set; }
        /// <summary>Whether the content host uses the current safe area.</summary>
        public bool SafeArea { get; private set; }

        private UiDocument(HashSet<string> images) { _images = images; }

        /// <summary>Resolves styles and validates the entire screen before creating any GameObjects.</summary>
        public static UiDocument Parse(UiContentPackage package, IUiBindings bindings, HashSet<string> images)
        {
            XElement styles = UiXml.Parse(package.Text("styles.xml"), "styles.xml").Root;
            if (styles.Name != "Styles") throw UiXml.Error(styles, "Expected Styles");
            UiXml.Attributes(styles, "");
            var dictionary = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var style in styles.Elements())
            {
                if (style.Name != "Style") throw UiXml.Error(style, "Expected Style");
                UiXml.Attributes(style, "id,width,height,background,color,fontSize,image");
                if (style.HasElements) throw UiXml.Error(style, "Style cannot have children");
                if (!dictionary.TryAdd(UiXml.Required(style, "id"), style)) throw UiXml.Error(style, "Duplicate style");
            }
            var document = new UiDocument(images);
            document.Screen = UiXml.Parse(package.Text("screen.xml"), "screen.xml").Root;
            if (document.Screen.Name != "Screen") throw UiXml.Error(document.Screen, "Expected Screen");
            UiXml.Attributes(document.Screen, "id,schema,referenceWidth,referenceHeight,safeArea,background");
            if (UiXml.Required(document.Screen, "schema") != "1") throw UiXml.Error(document.Screen, "Unsupported schema");
            document.ReferenceWidth = UiXml.Number(document.Screen, "referenceWidth", 1080, 320, 4096);
            document.ReferenceHeight = UiXml.Number(document.Screen, "referenceHeight", 1920, 320, 4096);
            document.SafeArea = UiXml.Boolean(document.Screen, "safeArea", true);
            UiXml.Color(document.Screen, "background", Color.black);
            document.Validate(document.Screen, bindings, dictionary, 0);
            return document;
        }

        private void Validate(XElement node, IUiBindings bindings, Dictionary<string, XElement> styles, int depth)
        {
            if (depth > 24 || _ids.Count >= 256) throw UiXml.Error(node, "Screen exceeds depth/node limit");
            if (!_ids.Add(UiXml.Required(node, "id"))) throw UiXml.Error(node, "Duplicate node id");
            string tag = node.Name.LocalName;
            if (tag == "Screen" && depth != 0) throw UiXml.Error(node, "Nested screens are unsupported");
            if (tag != "Screen")
            {
                string extras = tag switch
                {
                    "Panel" => "",
                    "Column" or "Row" => ",gap,padding",
                    "Text" => ",text,bind,suffix,align",
                    "Image" => ",preserveAspect",
                    "Button" => ",text,command",
                    "Slider" => ",min,max,wholeNumbers,bind,onChange",
                    "Toggle" => ",text,bind,onChange",
                    _ => throw UiXml.Error(node, "Unknown component " + tag)
                };
                UiXml.Attributes(node, LayoutAttributes + extras);
                var styleId = (string)node.Attribute("style");
                if (styleId != null)
                {
                    if (!styles.TryGetValue(styleId, out var style)) throw UiXml.Error(node, "Unknown style " + styleId);
                    foreach (var attribute in style.Attributes())
                        if (attribute.Name != "id" && node.Attribute(attribute.Name) == null)
                            node.SetAttributeValue(attribute.Name, attribute.Value);
                }
                UiXml.Number(node, "x", 0); UiXml.Number(node, "y", 0);
                UiXml.Number(node, "width", 800, 1, 4096); UiXml.Number(node, "height", 80, 1, 4096);
                UiXml.Number(node, "fontSize", 32, 12, 160);
                UiXml.Color(node, "background", Color.clear); UiXml.Color(node, "color", Color.white);
                UiXml.Boolean(node, "interactable", true);
                var image = (string)node.Attribute("image");
                if (image != null && !_images.Contains(image)) throw UiXml.Error(node, "Unknown image " + image);
                if (tag == "Image") { UiXml.Required(node, "image"); UiXml.Boolean(node, "preserveAspect", true); }
                if (tag == "Text")
                {
                    var align = (string)node.Attribute("align") ?? "left";
                    if (align != "left" && align != "center" && align != "right") throw UiXml.Error(node, "Invalid align");
                    if (node.Attribute("bind") != null) ValidateBinding(node, bindings, null);
                }
                if (tag == "Button")
                    ValidateCommand(node, bindings, UiXml.Required(node, "command"), null, null);
                if (tag == "Slider" || tag == "Toggle")
                {
                    ValidateBinding(node, bindings, tag == "Slider" ? typeof(float) : typeof(bool));
                    if (tag == "Slider")
                    {
                        float min = UiXml.Number(node, "min", 0, 0, 100);
                        float max = UiXml.Number(node, "max", 100, 0, 100);
                        if (max <= min) throw UiXml.Error(node, "Slider max must exceed min");
                        float value = Convert.ToSingle(bindings.Read(UiXml.Required(node, "bind")));
                        if (value < min || value > max) throw UiXml.Error(node, "Bound value outside slider range");
                        if (UiXml.Boolean(node, "wholeNumbers", false) && (min != Mathf.Floor(min) || max != Mathf.Floor(max) || value != Mathf.Floor(value)))
                            throw UiXml.Error(node, "Integer slider requires integer bounds and value");
                    }
                }
                if (tag == "Column" || tag == "Row")
                {
                    UiXml.Number(node, "gap", 16, 0, 500); UiXml.Number(node, "padding", 24, 0, 500);
                    foreach (var child in node.Elements())
                        if (child.Attribute("x") != null || child.Attribute("y") != null)
                            throw UiXml.Error(child, "Flow children use width/height; x/y belong to Panel children");
                }
            }
            bool container = tag == "Screen" || tag == "Panel" || tag == "Column" || tag == "Row";
            if (!container && node.HasElements) throw UiXml.Error(node, "Leaf component cannot have children");
            foreach (var child in node.Elements()) Validate(child, bindings, styles, depth + 1);
        }

        private static void ValidateBinding(XElement node, IUiBindings bindings, Type type) =>
            ValidateCommand(node, bindings, type == null ? null : UiXml.Required(node, "onChange"), UiXml.Required(node, "bind"), type);

        private static void ValidateCommand(XElement node, IUiBindings bindings, string command, string key, Type type)
        {
            try { bindings.Validate(key, command, type); }
            catch (Exception error) { throw UiXml.Error(node, error.Message); }
        }
    }
}
