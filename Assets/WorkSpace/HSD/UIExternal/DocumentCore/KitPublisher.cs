using System;
using System.Collections.Generic;
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
        public static string Publish(AuthoringWorkspace workspace) => Publish(workspace, out _);

        /// <summary>Publishes the same package and returns advisory diagnostics also stored in its hashed manifest.</summary>
        public static string Publish(AuthoringWorkspace workspace, out IReadOnlyList<string> diagnostics, string artRoot = null)
        {
            foreach (var pair in workspace.Documents.Where(x => x.Value.Root.Name == "Screen" || x.Value.Root.Name == "Prefab")) UiComposer.Resolve(workspace, pair.Key);
            var warnings = UiPublishLint.Inspect(workspace).ToList();
            var native = NativePrefabContract.Read(workspace);
            if (native != null) NativePrefabContract.Validate(workspace, UiComposer.Resolve(workspace, "Screens/UI3030.xml"));
            diagnostics = warnings;
            string version = Guid.NewGuid().ToString("N");
            string versionRoot = AuthoringWorkspace.SafePath(workspace.DirectoryPath, "Published/versions/" + version);
            Directory.CreateDirectory(versionRoot);
            var manifest = new XElement("Manifest", new XAttribute("schema", "2"), new XAttribute("version", version));
            foreach (var pair in workspace.Documents)
                Write(pair.Key, Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(pair.Value)));
            if (native != null)
            {
                string path = AuthoringWorkspace.Required(native, "bundle");
                byte[] bytes = File.ReadAllBytes(AuthoringWorkspace.SafePath(workspace.DirectoryPath, path));
                if (AuthoringWorkspace.Hash(bytes) != (string)native.Attribute("bundleHash")) throw new FormatException("원본 프리팹 패키지가 변경되었습니다. Unity에서 다시 내보내세요.");
                Write(path, bytes);
            }
            foreach (var resource in workspace.Documents["images.xml"].Root.Elements("Image"))
            {
                string path = AuthoringWorkspace.Required(resource, "file");
                string source = ArtImageSource.Resolve(workspace, resource, artRoot, out bool original);
                byte[] bytes = File.ReadAllBytes(source);
                if (resource.Attribute("artPath") != null)
                    warnings.Add("[ART] " + (string)resource.Attribute("key") + (original ? ": 아트 원본 포함" : ": 키트 복사본 사용 (루트 미설정 또는 파일 없음)") + ". Unity 설치 검사에서 상대경로와 GUID를 대조합니다.");
                if (bytes.Length > 16 * 1024 * 1024) throw new FormatException("Image exceeds 16MiB: " + path);
                Write(path, bytes);
            }
            Write("publish-lint.xml", Encoding.UTF8.GetBytes(new XElement("PublishDiagnostics", warnings.Select(x => new XElement("Warning", x))).ToString()));
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
