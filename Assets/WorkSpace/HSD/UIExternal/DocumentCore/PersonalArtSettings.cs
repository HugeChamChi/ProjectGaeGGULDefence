using System;
using System.IO;
using System.Text;

namespace GGD.UI.Authoring
{
    /// <summary>Per-user art location, shared with the portable downloader, never serialized into a kit.</summary>
    public static class PersonalArtSettings
    {
        /// <summary>Personal settings file used by all installed Studio versions.</summary>
        public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GGD", "UIAuthoring", "art-root.txt");
        /// <summary>Reads the current preference each time so an external download becomes visible on reload.</summary>
        public static string ReadRoot() => File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
        /// <summary>Saves a local folder, or an empty value to use only kit copies.</summary>
        public static void SaveRoot(string root)
        {
            root = string.IsNullOrWhiteSpace(root) ? "" : Path.GetFullPath(root.Trim());
            if (root.Length != 0 && !Directory.Exists(root)) throw new DirectoryNotFoundException("아트 루트 폴더를 찾을 수 없습니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            AuthoringWorkspace.AtomicWrite(FilePath, Encoding.UTF8.GetBytes(root));
        }
    }
}
