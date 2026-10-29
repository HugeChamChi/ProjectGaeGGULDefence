using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GGD.UI.Authoring
{
    /// <summary>Owns only file-decoded sprites; installed assets and exact TMP font references remain caller-owned.</summary>
    public sealed class AuthoringResources : IDisposable
    {
        private readonly AuthoringAssets _assets;
        private readonly AuthoringWorkspace _workspace;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private long _pixels;
        private readonly string _artRoot;
        /// <summary>Actual sources successfully loaded by this preview, keyed by image key.</summary>
        public Dictionary<string, string> Sources { get; } = new Dictionary<string, string>();
        /// <summary>Studio uses external PNGs; game uses only installed references.</summary>
        public AuthoringResources(AuthoringAssets assets, AuthoringWorkspace workspace = null, string artRoot = null) { _assets = assets; _workspace = workspace; _artRoot = artRoot; }
        /// <summary>Reads the exact bundled TMP font.</summary>
        public TMP_FontAsset Font(string key) => _assets.Font(key);
        /// <summary>Reads one resource by stable key and owns its external texture if required.</summary>
        public Sprite Sprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_sprites.TryGetValue(key, out var loaded)) return loaded;
            if (_workspace == null) return _assets.Images.FirstOrDefault(x => x.Key == key)?.Asset ?? throw new FormatException("Installed image missing " + key);
            var metadata = _workspace.Documents["images.xml"].Root.Elements("Image").FirstOrDefault(x => (string)x.Attribute("key") == key) ?? throw new FormatException("Unknown image " + key);
            string path = ArtImageSource.Resolve(_workspace, metadata, _artRoot, out bool original);
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new FormatException("Image exceeds 16MiB");
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { name = "Studio:" + key, wrapMode = TextureWrapMode.Clamp };
            _textures.Add(texture);
            if (!ImageConversion.LoadImage(texture, bytes, true)) throw new FormatException("Invalid PNG/JPG " + path);
            _pixels += (long)texture.width * texture.height;
            if (texture.width > 8192 || texture.height > 8192 || _pixels > 64 * 1024 * 1024) throw new FormatException("Kit image budget exceeded");
            texture.filterMode = (FilterMode)(int)AuthoringWorkspace.Number(metadata, "filter", 1);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(AuthoringWorkspace.Number(metadata, "pivotX", .5f), AuthoringWorkspace.Number(metadata, "pivotY", .5f)),
                AuthoringWorkspace.Number(metadata, "ppu", 100), 0, SpriteMeshType.FullRect,
                new Vector4(AuthoringWorkspace.Number(metadata, "left"), AuthoringWorkspace.Number(metadata, "bottom"), AuthoringWorkspace.Number(metadata, "right"), AuthoringWorkspace.Number(metadata, "top")));
            sprite.name = key; _sprites[key] = sprite;
            Sources[key] = (original ? "아트 원본: " : "키트 복사본: ") + path;
            return sprite;
        }
        /// <summary>Releases file-owned sprites and textures; repeated disposal is safe.</summary>
        public void Dispose()
        { foreach (var sprite in _sprites.Values) UnityEngine.Object.Destroy(sprite); foreach (var texture in _textures) UnityEngine.Object.Destroy(texture); _sprites.Clear(); _textures.Clear(); }
    }
}
