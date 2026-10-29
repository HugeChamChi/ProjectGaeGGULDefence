using System;
using System.IO;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Resolves portable art-relative paths without putting a user's absolute path into shared XML.</summary>
    public static class ArtImageSource
    {
        /// <summary>Validates a path relative to GGD_ArtWork.</summary>
        public static void Validate(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return;
            if (relative.Contains("\\") || relative.Contains(":")) throw new FormatException("아트 상대경로는 /로 구분해야 합니다.");
            AuthoringWorkspace.SafePath(Path.GetTempPath(), relative);
            string extension = Path.GetExtension(relative).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") throw new FormatException("아트 상대경로는 PNG/JPG만 지원합니다.");
        }
        /// <summary>Uses an available local original, otherwise the portable kit copy.</summary>
        public static string Resolve(AuthoringWorkspace workspace, XElement image, string root, out bool original)
        {
            string relative = (string)image.Attribute("artPath"); Validate(relative);
            original = false;
            if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(relative))
            {
                string candidate = AuthoringWorkspace.SafePath(root, relative);
                if (File.Exists(candidate)) { original = true; return candidate; }
            }
            return AuthoringWorkspace.SafePath(workspace.DirectoryPath, AuthoringWorkspace.Required(image, "file"));
        }
    }
}
