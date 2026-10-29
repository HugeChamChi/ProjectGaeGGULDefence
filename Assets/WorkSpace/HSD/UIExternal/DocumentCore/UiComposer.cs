using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace GGD.UI.Authoring
{
    /// <summary>Version-two prefab composition, provenance and strict component/binding validation.</summary>
    public static class UiComposer
    {
        private const string Rect = "id,key,name,x,y,width,height,anchorMinX,anchorMinY,anchorMaxX,anchorMaxY,pivotX,pivotY,scaleX,scaleY,rotation,active,visible";
        private static readonly Dictionary<string, string> ComponentAttributes = new Dictionary<string, string>
        {
            ["Image"] = "sprite,color,type,fillMethod,fillAmount,fillOrigin,clockwise,preserveAspect,raycast,ppuMultiplier",
            ["Text"] = "text,font,size,color,alignment,style,autoSize,minSize,maxSize,wrap,overflow,rich,raycast,marginLeft,marginRight,marginTop,marginBottom,outlineWidth,outlineColor,bind",
            ["Button"] = "command,target,interactable,enabled,normal,highlighted,pressed,selected,disabled,transition",
            ["Slider"] = "bind,command,fill,handle,target,min,max,wholeNumbers,direction,interactable,value",
            ["Toggle"] = "bind,command,graphic,target,interactable,value,on,off,visualSlider",
            ["Switch"] = "bind,command,on,off,visualSlider,interactable",
            ["CanvasGroup"] = "alpha,interactable,blocksRaycasts",
            ["Mask"] = "showGraphic", ["RectMask"] = "",
            ["Layout"] = "axis,gap,paddingLeft,paddingRight,paddingTop,paddingBottom,alignment,controlWidth,controlHeight,expandWidth,expandHeight",
            ["LayoutElement"] = "ignore,minWidth,minHeight,preferredWidth,preferredHeight,flexibleWidth,flexibleHeight",
            ["SizeFitter"] = "horizontal,vertical"
        };
        private static readonly HashSet<string> References = new HashSet<string> { "fill", "handle", "target", "graphic", "on", "off", "visualSlider" };

        /// <summary>Composes one original into a separate tree and validates every component and contract key.</summary>
        public static ResolvedUiDocument Resolve(AuthoringWorkspace workspace, string path = null, bool validate = true)
        {
            if (workspace.IsReadOnly) throw new FormatException("Kit schema/runtime is newer than this Studio");
            path ??= workspace.CurrentPath;
            var source = workspace.Documents[path].Root;
            if (source.Name != "Screen" && source.Name != "Prefab") throw new FormatException(path + ": expected Screen or Prefab");
            CheckAttributes(source, source.Name == "Screen" ? "schema,key,width,height,animation,canvas" : "schema,key,width,height,animation");
            if ((string)source.Attribute("schema") != "2") throw new FormatException(path + ": unsupported document schema");
            foreach (var document in workspace.Documents.Values.Where(x => x.Root.Name == "Screen" || x.Root.Name == "Prefab")) CheckStructure(document.Root);
            var result = new ResolvedUiDocument { Root = new XElement("Resolved", new XAttribute("width", (string)source.Attribute("width") ?? "1080"), new XAttribute("height", (string)source.Attribute("height") ?? "1920")) };
            if (source.Attribute("animation") != null) result.Root.SetAttributeValue("animation", (string)source.Attribute("animation"));
            if (source.Attribute("canvas") != null) result.Root.SetAttributeValue("canvas", (string)source.Attribute("canvas"));
            var prefabs = workspace.Documents.Where(x => x.Value.Root.Name == "Prefab").ToDictionary(x => AuthoringWorkspace.Required(x.Value.Root, "key"), x => x.Key);
            var stack = new HashSet<string>(); int count = 0;
            var defaults = source.Element("Parameters")?.Elements("Parameter").ToDictionary(x => AuthoringWorkspace.Required(x, "key"), x => (string)x.Attribute("default") ?? "") ?? new Dictionary<string, string>();
            foreach (var node in source.Elements().Where(x => x.Name == "Element" || x.Name == "Instance" || x.Name == "Slot"))
                result.Root.Add(Expand(node, "", path, defaults, null, stack, workspace, prefabs, result, ref count, 0));
            if (validate) Validate(result, workspace.Catalog.Root, workspace.Documents["images.xml"].Root, workspace.Documents["fonts.xml"].Root);
            return result;
        }

        private static XElement Expand(XElement source, string prefix, string file, Dictionary<string, string> arguments, XElement instance,
            HashSet<string> stack, AuthoringWorkspace workspace, Dictionary<string, string> prefabs, ResolvedUiDocument result, ref int count, int depth)
        {
            if (++count > 4096 || depth > 32) throw Error(source, "Composition exceeds 4096 elements / 32 depth");
            string id = AuthoringWorkspace.Required(source, "id");
            string resolvedId = prefix.Length == 0 ? id : prefix + "/" + id;
            if (source.Name == "Instance")
            {
                CheckAttributes(source, "id,key,prefab");
                string key = AuthoringWorkspace.Required(source, "prefab");
                if (!prefabs.TryGetValue(key, out string prefabFile)) throw Error(source, "Missing prefab " + key);
                if (!stack.Add(key)) throw Error(source, "Cyclic prefab " + key);
                try
                {
                    var definition = workspace.Documents[prefabFile].Root;
                    var inputs = definition.Element("Parameters")?.Elements("Parameter").ToDictionary(x => AuthoringWorkspace.Required(x, "key"), x => (string)x.Attribute("default") ?? "") ?? new Dictionary<string, string>();
                    foreach (var input in source.Elements("Input"))
                    {
                        string inputKey = AuthoringWorkspace.Required(input, "key");
                        if (!inputs.ContainsKey(inputKey)) throw Error(input, "Unknown prefab input " + inputKey);
                        inputs[inputKey] = Substitute(AuthoringWorkspace.Required(input, "value"), arguments);
                    }
                    XElement root = definition.Elements().SingleOrDefault(x => x.Name == "Element" || x.Name == "Instance") ?? throw Error(definition, "Prefab must have exactly one root");
                    var expanded = Expand(root, resolvedId, prefabFile, inputs, source, stack, workspace, prefabs, result, ref count, depth + 1);
                    var targets = expanded.DescendantsAndSelf("Element").ToDictionary(x => (string)x.Attribute("id"));
                    foreach (var slot in source.Elements("Slot"))
                        if (!definition.Descendants("Slot").Any(x => (string)x.Attribute("key") == (string)slot.Attribute("key"))) throw Error(slot, "Unknown prefab slot");
                    foreach (var change in source.Elements("Override"))
                    {
                        string targetId = resolvedId + "/" + AuthoringWorkspace.Required(change, "target");
                        if (!targets.TryGetValue(targetId, out var target)) throw Error(change, "Orphan override " + targetId);
                        string property = AuthoringWorkspace.Required(change, "property");
                        AuthoringWorkspace.SetValue(target, property, Substitute((string)change.Attribute("value") ?? "", arguments));
                        result.Origins[targetId + "|" + property] = file + " (override)";
                    }
                    return expanded;
                }
                finally { stack.Remove(key); }
            }
            if (source.Name != "Element" && source.Name != "Slot") throw Error(source, "Unknown structural node " + source.Name);
            if (result.SourceFiles.ContainsKey(resolvedId)) throw Error(source, "Duplicate element " + resolvedId);
            result.SourceFiles.Add(resolvedId, file);
            var output = new XElement("Element", source.Attributes().Where(x => x.Name != "id" && x.Name != "key").Select(x => new XAttribute(x.Name, Substitute(x.Value, arguments))));
            output.SetAttributeValue("id", resolvedId); output.SetAttributeValue("key", (string)source.Attribute("key") ?? (string)source.Attribute("name") ?? id);
            foreach (var attribute in output.Attributes()) result.Origins[resolvedId + "|" + attribute.Name] = file + (prefix.Length == 0 ? " (source)" : " (inherited)");
            foreach (var component in source.Elements().Where(x => ComponentAttributes.ContainsKey(x.Name.LocalName)))
            {
                var copy = new XElement(component);
                foreach (var attribute in copy.Attributes())
                {
                    attribute.Value = Substitute(attribute.Value, arguments);
                    if (References.Contains(attribute.Name.LocalName) && attribute.Value.Length > 0 && prefix.Length > 0) attribute.Value = prefix + "/" + attribute.Value;
                    result.Origins[resolvedId + "|" + component.Name + "." + attribute.Name] = file + (prefix.Length == 0 ? " (source)" : " (inherited)");
                }
                output.Add(copy);
            }
            if (source.Name == "Slot")
            {
                var replacement = instance?.Elements("Slot").FirstOrDefault(x => (string)x.Attribute("key") == (string)source.Attribute("key"));
                foreach (var child in (replacement ?? source).Elements().Where(IsStructure))
                    output.Add(Expand(child, prefix, file, arguments, instance, stack, workspace, prefabs, result, ref count, depth + 1));
            }
            else
            {
                CheckAttributes(source, Rect);
                foreach (var child in source.Elements())
                {
                    if (IsStructure(child)) output.Add(Expand(child, prefix, file, arguments, instance, stack, workspace, prefabs, result, ref count, depth + 1));
                    else if (!ComponentAttributes.ContainsKey(child.Name.LocalName)) throw Error(child, "Unknown component " + child.Name);
                }
            }
            return output;
        }

        private static bool IsStructure(XElement node) => node.Name == "Element" || node.Name == "Instance" || node.Name == "Slot";
        private static void CheckStructure(XElement node)
        {
            string allowedChildren;
            switch (node.Name.LocalName)
            {
                case "Screen": case "Prefab":
                    CheckAttributes(node, node.Name == "Screen" ? "schema,key,width,height,animation,canvas" : "schema,key,width,height,animation");
                    string canvas = (string)node.Attribute("canvas");
                    if (canvas != null && canvas != "own" && canvas != "inherit") throw Error(node, "Canvas must be own or inherit");
                    allowedChildren = "Element,Instance,Slot,Parameters";
                    if (node.Elements("Parameters").Count() > 1) throw Error(node, "Duplicate Parameters");
                    break;
                case "Parameters": CheckAttributes(node, ""); allowedChildren = "Parameter"; break;
                case "Parameter": CheckAttributes(node, "key,default"); allowedChildren = ""; break;
                case "Instance": CheckAttributes(node, "id,key,prefab"); allowedChildren = "Input,Override,Slot"; break;
                case "Input": CheckAttributes(node, "key,value"); allowedChildren = ""; break;
                case "Override": CheckAttributes(node, "target,property,value"); allowedChildren = ""; break;
                case "Element": CheckAttributes(node, Rect); allowedChildren = "Element,Instance,Slot," + string.Join(",", ComponentAttributes.Keys); break;
                case "Slot":
                    CheckAttributes(node, node.Parent?.Name == "Instance" ? "key" : Rect);
                    allowedChildren = "Element,Instance,Slot" + (node.Parent?.Name == "Instance" ? "" : "," + string.Join(",", ComponentAttributes.Keys)); break;
                default:
                    if (!ComponentAttributes.TryGetValue(node.Name.LocalName, out string attributes)) throw Error(node, "Unknown XML node " + node.Name);
                    CheckAttributes(node, attributes); allowedChildren = ""; break;
            }
            foreach (var child in node.Elements())
            {
                if (!allowedChildren.Split(',').Contains(child.Name.ToString())) throw Error(child, "Unsupported XML position " + child.Name);
                CheckStructure(child);
            }
        }

        /// <summary>Checks the shared image metadata contract before preview, publication or installation.</summary>
        public static void ValidateImages(XElement images)
        {
            if (images.Name != "Images") throw Error(images, "Expected Images");
            CheckAttributes(images, "");
            var keys = new HashSet<string>();
            foreach (var item in images.Elements())
            {
                if (item.Name != "Image" || item.HasElements) throw Error(item, "Unsupported image metadata node");
                CheckAttributes(item, "key,file,guid,localId,left,bottom,right,top,ppu,pivotX,pivotY,filter,sha256,atlas,artPath");
                ArtImageSource.Validate((string)item.Attribute("artPath"));
                string atlas = (string)item.Attribute("atlas");
                if (!string.IsNullOrEmpty(atlas) && !Regex.IsMatch(atlas, "^[A-Za-z0-9_-]+$")) throw Error(item, "Atlas key must use letters, numbers, underscore or hyphen");
                if (!keys.Add(AuthoringWorkspace.Required(item, "key"))) throw Error(item, "Duplicate image key");
                string extension = System.IO.Path.GetExtension(AuthoringWorkspace.Required(item, "file")).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") throw Error(item, "Only PNG/JPG images are supported");
                if (AuthoringWorkspace.Number(item, "ppu", 100) <= 0) throw Error(item, "Image ppu must be positive");
                foreach (string border in new[] { "left", "bottom", "right", "top" })
                    if (AuthoringWorkspace.Number(item, border) < 0) throw Error(item, "Image border must be nonnegative");
                foreach (string pivot in new[] { "pivotX", "pivotY" })
                {
                    float value = AuthoringWorkspace.Number(item, pivot, .5f);
                    if (value < 0 || value > 1) throw Error(item, "Image pivot must be between 0 and 1");
                }
                float filter = AuthoringWorkspace.Number(item, "filter", 1);
                if (filter < 0 || filter > 2 || filter != (int)filter) throw Error(item, "Invalid image filter");
            }
        }
        private static string Substitute(string value, Dictionary<string, string> arguments)
        {
            foreach (var pair in arguments) value = value.Replace("${" + pair.Key + "}", pair.Value);
            if (value.Contains("${")) throw new FormatException("Unbound prefab parameter " + value);
            return value;
        }
        private static void Validate(ResolvedUiDocument result, XElement catalog, XElement images, XElement fonts)
        {
            ValidateImages(images);
            var states = catalog.Elements("State").ToDictionary(x => AuthoringWorkspace.Required(x, "key"));
            var commands = catalog.Elements("Command").ToDictionary(x => AuthoringWorkspace.Required(x, "key"));
            var imageKeys = new HashSet<string>(images.Elements("Image").Select(x => AuthoringWorkspace.Required(x, "key")));
            var fontKeys = new HashSet<string>(fonts.Elements("Font").Select(x => AuthoringWorkspace.Required(x, "key")));
            var ids = new HashSet<string>(result.Root.Descendants("Element").Select(x => AuthoringWorkspace.Required(x, "id")));
            string animation = (string)result.Root.Attribute("animation");
            if (animation != null && !ids.Contains(animation)) throw new FormatException("Missing host animation element " + animation);
            foreach (var node in result.Root.Descendants("Element"))
            {
                CheckAttributes(node, Rect);
                Boolean(node, "active");
                string visible = (string)node.Attribute("visible");
                if (visible != null && (!states.TryGetValue(visible, out var visibleContract) || (string)visibleContract.Attribute("type") != "bool")) throw Error(node, "Unknown boolean state " + visible);
                foreach (var key in new[] { "x", "y", "width", "height", "anchorMinX", "anchorMinY", "anchorMaxX", "anchorMaxY", "pivotX", "pivotY", "scaleX", "scaleY", "rotation" })
                    if (Math.Abs(AuthoringWorkspace.Number(node, key)) > 100000) throw Error(node, "Transform outside supported range");
                foreach (var component in node.Elements().Where(x => x.Name != "Element"))
                {
                    if (!ComponentAttributes.TryGetValue(component.Name.LocalName, out string allowed)) throw Error(component, "Unsupported component");
                    CheckAttributes(component, allowed);
                    foreach (var attribute in component.Attributes())
                    {
                        string key = attribute.Name.LocalName;
                        if (new[] { "color", "normal", "highlighted", "pressed", "selected", "disabled", "outlineColor" }.Contains(key) && !Regex.IsMatch(attribute.Value, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) throw Error(component, "Invalid color " + key);
                        if (new[] { "type", "fillMethod", "fillAmount", "fillOrigin", "ppuMultiplier", "size", "alignment", "style", "minSize", "maxSize", "wrap", "overflow", "marginLeft", "marginRight", "marginTop", "marginBottom", "outlineWidth", "min", "max", "direction", "alpha", "gap", "paddingLeft", "paddingRight", "paddingTop", "paddingBottom", "minWidth", "minHeight", "preferredWidth", "preferredHeight", "flexibleWidth", "flexibleHeight", "horizontal", "vertical", "transition" }.Contains(key)) AuthoringWorkspace.Number(component, key);
                        if (new[] { "clockwise", "preserveAspect", "raycast", "autoSize", "rich", "wholeNumbers", "interactable", "blocksRaycasts", "showGraphic", "controlWidth", "controlHeight", "expandWidth", "expandHeight", "ignore" }.Contains(key)) Boolean(component, key);
                    }
                    if (component.Name == "Slider")
                    {
                        float minimum = AuthoringWorkspace.Number(component, "min"), maximum = AuthoringWorkspace.Number(component, "max", 1);
                        if (maximum <= minimum) throw Error(component, "Slider max must exceed min");
                        AuthoringWorkspace.Number(component, "value");
                    }
                    if (component.Name == "Toggle") Boolean(component, "value");
                    if (component.Name == "Layout" && (string)component.Attribute("axis") != "horizontal" && (string)component.Attribute("axis") != "vertical") throw Error(component, "Invalid layout axis");
                    if (component.HasElements) throw Error(component, "Components cannot contain nested values");
                    foreach (var attribute in component.Attributes().Where(x => References.Contains(x.Name.LocalName)))
                        if (attribute.Value.Length > 0 && !ids.Contains(attribute.Value)) throw Error(component, "Missing element reference " + attribute.Value);
                    string sprite = (string)component.Attribute("sprite");
                    if (!string.IsNullOrEmpty(sprite) && !imageKeys.Contains(sprite)) throw Error(component, "Unknown image " + sprite);
                    string font = (string)component.Attribute("font");
                    if (font != null && !fontKeys.Contains(font)) throw Error(component, "Unknown font " + font);
                    string state = (string)component.Attribute("bind"), command = (string)component.Attribute("command");
                    string expected = component.Name == "Toggle" || component.Name == "Switch" ? "bool" : component.Name == "Slider" ? "number" : null;
                    if (!string.IsNullOrEmpty(state) && (!states.TryGetValue(state, out var stateContract) || expected != null && (string)stateContract.Attribute("type") != expected)) throw Error(component, "Unknown/type-mismatched state " + state);
                    if (!string.IsNullOrEmpty(command) && (!commands.TryGetValue(command, out var commandContract) || (string)commandContract.Attribute("type") != (expected ?? "none"))) throw Error(component, "Unknown/type-mismatched command " + command);
                    foreach (string dynamicKey in new[] { "visible", "enabled" })
                    {
                        string key = (string)(dynamicKey == "visible" ? node.Attribute(dynamicKey) : component.Attribute(dynamicKey));
                        if (key != null && (!states.TryGetValue(key, out var contract) || (string)contract.Attribute("type") != "bool")) throw Error(node, "Unknown boolean state " + key);
                    }
                }
            }
        }
        private static void Boolean(XElement node, string key)
        { if (node.Attribute(key) != null && !bool.TryParse((string)node.Attribute(key), out _)) throw Error(node, "Invalid boolean " + key); }
        private static void CheckAttributes(XElement node, string allowed)
        {
            foreach (var attribute in node.Attributes())
                if (attribute.Name.NamespaceName.Length > 0 || !(new HashSet<string>(allowed.Split(','))).Contains(attribute.Name.LocalName)) throw Error(node, "Unsupported property " + attribute.Name);
            if (node.Name.NamespaceName.Length > 0 || node.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value))) throw Error(node, "Unsupported XML namespace/text");
        }
        private static Exception Error(XObject node, string message)
        { var line = (IXmlLineInfo)node; string id = (string)(node as XElement)?.AncestorsAndSelf("Element").FirstOrDefault()?.Attribute("id"); return new FormatException($"{node.BaseUri}:{line.LineNumber} [{id}]: {message}"); }
    }
}
