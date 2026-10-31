using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GGD.UI.External
{
    /// <summary>Verified immutable content snapshot. Never reads a partially authored screen.</summary>
    public sealed class UiContentPackage
    {
        private const int MaxFileBytes = 8 * 1024 * 1024;
        private const int MaxPackageBytes = 32 * 1024 * 1024;
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>Published generation used by diagnostics and reload coordination.</summary>
        public string Version { get; private set; }
        /// <summary>Exact pointer used for this snapshot, preventing a concurrent publish from being skipped.</summary>
        public string PointerText { get; private set; }

        /// <summary>Loads a pointer, verifies all hashes, and owns its bytes independent of later file changes.</summary>
        public static UiContentPackage Load(string root)
        {
            string pointerPath = ConfinedPath(root, "current.json");
            string pointerText = ReadText(pointerPath, 4096);
            var pointer = JsonUtility.FromJson<Pointer>(pointerText);
            if (pointer == null || pointer.schema != 1 || !ValidVersion(pointer.version))
                throw new FormatException("current.json: invalid schema/version");
            string versionRoot = ConfinedPath(root, "versions/" + pointer.version);
            byte[] manifestBytes = ReadBytes(ConfinedPath(versionRoot, "manifest.json"), 64 * 1024);
            if (Hash(manifestBytes) != pointer.manifestSha256) throw new FormatException("manifest.json: hash mismatch");
            var manifest = JsonUtility.FromJson<Manifest>(Encoding.UTF8.GetString(manifestBytes));
            if (manifest == null || manifest.schema != 1 || manifest.version != pointer.version ||
                manifest.files == null || manifest.files.Length > 128)
                throw new FormatException("manifest.json: invalid schema/version/files");
            var result = new UiContentPackage { Version = pointer.version, PointerText = pointerText };
            long total = 0;
            foreach (var file in manifest.files)
            {
                if (file == null || file.path == "manifest.json" || result._files.ContainsKey(file.path ?? ""))
                    throw new FormatException("manifest.json: duplicate/invalid file");
                byte[] bytes = ReadBytes(ConfinedPath(versionRoot, file.path), MaxFileBytes);
                total += bytes.Length;
                if (total > MaxPackageBytes || file.bytes != bytes.Length || Hash(bytes) != file.sha256)
                    throw new FormatException(file.path + ": size/hash mismatch or package limit exceeded");
                result._files.Add(file.path, bytes);
            }
            // These reads ensure that even a syntactically valid manifest cannot omit required files.
            result.Text("screen.xml"); result.Text("styles.xml"); result.Text("state.xml"); result.Text("images.xml");
            return result;
        }

        /// <summary>Reads an explicitly manifested file.</summary>
        public byte[] Bytes(string path) => _files.TryGetValue(path, out var bytes) ? bytes :
            throw new FormatException("Unmanifested resource: " + path);

        /// <summary>Decodes UTF-8 XML with invalid-byte rejection.</summary>
        public string Text(string path) => new UTF8Encoding(false, true).GetString(Bytes(path)).TrimStart('\uFEFF');

        /// <summary>Returns SHA-256 for stable content identity.</summary>
        public static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Confines relative content paths and rejects link/reparse redirection.</summary>
        public static string ConfinedPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") || relative.Contains(":") || Path.IsPathRooted(relative))
                throw new FormatException("Invalid content path: " + relative);
            foreach (string part in relative.Split('/'))
                if (part == ".." || part == "." || part.Length == 0) throw new FormatException("Invalid content path: " + relative);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new FormatException("Path outside content root");
            for (string current = path; current != null && current.Length >= fullRoot.TrimEnd(Path.DirectorySeparatorChar).Length;
                 current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new FormatException("Content links are unsupported: " + current);
            return path;
        }

        private static bool ValidVersion(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-') return false;
            return true;
        }

        private static string ReadText(string path, int limit) => Encoding.UTF8.GetString(ReadBytes(path, limit));
        private static byte[] ReadBytes(string path, int limit)
        {
            if (new FileInfo(path).Length > limit) throw new FormatException(path + ": file too large");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > limit) throw new FormatException(path + ": file too large");
            return bytes;
        }

        [Serializable] private sealed class Pointer { public int schema; public string version; public string manifestSha256; }
        [Serializable] private sealed class Manifest { public int schema; public string version; public Entry[] files; }
        [Serializable] private sealed class Entry { public string path; public int bytes; public string sha256; }
    }
}
