using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Source-preserving prefab authoring operations. Every operation is wrapped in one workspace transaction.</summary>
    public static class PrefabEditing
    {
        /// <summary>Extracts a selected original subtree into a reusable definition and replaces it with a reference.</summary>
        public static string Extract(AuthoringWorkspace workspace, string selectedId, string key)
        {
            if (selectedId.Contains("/")) throw new InvalidOperationException("Edit the prefab source or detach before extracting");
            string path = "Prefabs/" + key + ".xml";
            workspace.Edit(() =>
            {
                var selected = workspace.Find(workspace.CurrentPath, selectedId) ?? throw new FormatException("Selection missing");
                var resolved = UiComposer.Resolve(workspace);
                var subtree = resolved.Root.Descendants("Element").First(x => (string)x.Attribute("id") == selectedId || ((string)x.Attribute("id")).StartsWith(selectedId + "/", StringComparison.Ordinal));
                string instanceId = Guid.NewGuid().ToString("N");
                var map = subtree.DescendantsAndSelf("Element").ToDictionary(x => (string)x.Attribute("id"), x => instanceId + "/" + (string)x.Attribute("id"));
                var parameters = workspace.Documents[workspace.CurrentPath].Root.Element("Parameters");
                workspace.AddDocument(path, new XDocument(new XElement("Prefab", new XAttribute("schema", "2"), new XAttribute("key", key),
                    new XAttribute("width", "1080"), new XAttribute("height", "1920"), parameters == null ? null : new XElement(parameters), new XElement(selected))));
                selected.ReplaceWith(new XElement("Instance", new XAttribute("id", instanceId), new XAttribute("key", key), new XAttribute("prefab", key),
                    parameters?.Elements("Parameter").Select(x => new XElement("Input", new XAttribute("key", (string)x.Attribute("key")), new XAttribute("value", "${" + (string)x.Attribute("key") + "}")))));
                RemapUsages(workspace, workspace.CurrentPath, map, new HashSet<string>());
                // Outward dependencies cannot be embedded in a reusable definition; roll back the entire edit.
                ValidateAll(workspace);
            });
            return path;
        }

        /// <summary>Removes exactly one selected override, revealing the current prefab source value.</summary>
        public static void Revert(AuthoringWorkspace workspace, string selectedId, string property)
        {
            workspace.Edit(() =>
            {
                string[] parts = selectedId.Split('/');
                if (parts.Length < 2) return;
                var instance = workspace.Find(workspace.CurrentPath, parts[0]);
                string target = string.Join("/", parts.Skip(1));
                instance.Elements("Override").Where(x => (string)x.Attribute("target") == target && (string)x.Attribute("property") == property).Remove();
            });
        }

        /// <summary>Applies an instance's deltas to its definition. Caller shows the affected-use count before this operation.</summary>
        public static void ApplyToSource(AuthoringWorkspace workspace, string selectedId)
        {
            workspace.Edit(() =>
            {
                var instance = workspace.Find(workspace.CurrentPath, selectedId.Split('/')[0]);
                if (instance?.Name != "Instance") throw new InvalidOperationException("Select a prefab instance");
                string key = AuthoringWorkspace.Required(instance, "prefab");
                string definition = workspace.Documents.Single(x => x.Value.Root.Name == "Prefab" && (string)x.Value.Root.Attribute("key") == key).Key;
                string current = workspace.CurrentPath;
                try
                {
                    workspace.CurrentPath = definition;
                    foreach (var change in instance.Elements("Override").ToArray()) workspace.SetProperty(AuthoringWorkspace.Required(change, "target"), AuthoringWorkspace.Required(change, "property"), (string)change.Attribute("value") ?? "");
                    instance.Elements("Override").Remove();
                }
                finally { workspace.CurrentPath = current; }
            });
        }

        /// <summary>Detaches one instance into a complete editable copy with remapped internal references.</summary>
        public static string Detach(AuthoringWorkspace workspace, ResolvedUiDocument resolved, string selectedId)
        {
            string newId = null;
            workspace.Edit(() =>
            {
                string instanceId = selectedId.Split('/')[0];
                var instance = workspace.Find(workspace.CurrentPath, instanceId);
                if (instance?.Name != "Instance") throw new InvalidOperationException("Selection is not a prefab instance");
                var tree = new XElement(resolved.Root.Descendants("Element").First(x => ((string)x.Attribute("id")).StartsWith(instanceId + "/", StringComparison.Ordinal)));
                var map = tree.DescendantsAndSelf("Element").ToDictionary(x => (string)x.Attribute("id"), x => Guid.NewGuid().ToString("N"));
                foreach (var attribute in tree.DescendantsAndSelf().Attributes()) if (map.TryGetValue(attribute.Value, out string id)) attribute.Value = id;
                newId = (string)tree.Attribute("id"); instance.ReplaceWith(tree);
                RemapUsages(workspace, workspace.CurrentPath, map, new HashSet<string>());
                ValidateAll(workspace);
            });
            return newId;
        }

        private static void ValidateAll(AuthoringWorkspace workspace)
        {
            foreach (var pair in workspace.Documents.Where(x => x.Value.Root.Name == "Screen" || x.Value.Root.Name == "Prefab"))
                UiComposer.Resolve(workspace, pair.Key);
        }

        private static void RemapUsages(AuthoringWorkspace workspace, string path, Dictionary<string, string> map, HashSet<string> stack)
        {
            if (!stack.Add(path)) throw new FormatException("Cyclic prefab reference during remapping");
            var root = workspace.Documents[path].Root;
            foreach (var attribute in root.DescendantsAndSelf().Attributes())
            {
                string name = attribute.Name.LocalName;
                bool reference = new[] { "animation", "fill", "handle", "target", "graphic", "on", "off", "visualSlider" }.Contains(name);
                if (name == "value" && attribute.Parent.Name == "Override")
                    reference = new[] { "fill", "handle", "target", "graphic", "on", "off", "visualSlider" }.Contains(((string)attribute.Parent.Attribute("property") ?? "").Split('.').Last());
                if (reference && map.TryGetValue(attribute.Value, out string replacement)) attribute.Value = replacement;
            }
            if (root.Name == "Prefab")
            {
                string key = (string)root.Attribute("key");
                foreach (var document in workspace.Documents.ToArray())
                {
                    var usages = document.Value.Descendants("Instance").Where(x => (string)x.Attribute("prefab") == key).ToArray();
                    if (usages.Length == 0) continue;
                    var qualified = new Dictionary<string, string>();
                    foreach (var usage in usages)
                    {
                        foreach (var change in usage.Elements("Override"))
                            if (map.TryGetValue((string)change.Attribute("target"), out string target)) change.SetAttributeValue("target", target);
                        string prefix = (string)usage.Attribute("id") + "/";
                        foreach (var pair in map) qualified[prefix + pair.Key] = prefix + pair.Value;
                    }
                    RemapUsages(workspace, document.Key, qualified, stack);
                }
            }
            stack.Remove(path);
        }
    }
}
