using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Built-in publishing for portable Studio. No Python or Unity Editor is needed by the creator.</summary>
    public static class KitPublisher
    {
        /// <summary>Validates memory and resources, writes an immutable package, then swaps its pointer.</summary>
        public static string Publish(AuthoringWorkspace workspace)
        {
            foreach (var pair in workspace.Documents.Where(x => x.Value.Root.Name == "Screen" || x.Value.Root.Name == "Prefab")) UiComposer.Resolve(workspace, pair.Key);
            string version = Guid.NewGuid().ToString("N");
            string versionRoot = AuthoringWorkspace.SafePath(workspace.DirectoryPath, "Published/versions/" + version);
            Directory.CreateDirectory(versionRoot);
            var manifest = new XElement("Manifest", new XAttribute("schema", "2"), new XAttribute("version", version));
            foreach (var pair in workspace.Documents)
                Write(pair.Key, Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(pair.Value)));
            foreach (var resource in workspace.Documents["images.xml"].Root.Elements("Image"))
            {
                string path = AuthoringWorkspace.Required(resource, "file");
                byte[] bytes = File.ReadAllBytes(AuthoringWorkspace.SafePath(workspace.DirectoryPath, path));
                if (bytes.Length > 16 * 1024 * 1024) throw new FormatException("Image exceeds 16MiB: " + path);
                Write(path, bytes);
            }
            byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest.ToString());
            File.WriteAllBytes(Path.Combine(versionRoot, "manifest.xml"), manifestBytes);
            string pointer = new XElement("Current", new XAttribute("version", version), new XAttribute("sha256", AuthoringWorkspace.Hash(manifestBytes))).ToString();
            AuthoringWorkspace.AtomicWrite(AuthoringWorkspace.SafePath(workspace.DirectoryPath, "Published/current.xml"), Encoding.UTF8.GetBytes(pointer));
            return version;

            void Write(string path, byte[] bytes)
            {
                string destination = AuthoringWorkspace.SafePath(versionRoot, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllBytes(destination, bytes);
                manifest.Add(new XElement("File", new XAttribute("path", path), new XAttribute("bytes", bytes.Length), new XAttribute("sha256", AuthoringWorkspace.Hash(bytes))));
            }
        }
    }
}
