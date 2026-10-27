using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

namespace GGD.UI.External
{
    /// <summary>Owns external textures and sprites for exactly one staged screen.</summary>
    public sealed class UiImageSet : IDisposable
    {
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        /// <summary>Image keys available to the screen validator.</summary>
        public HashSet<string> Keys => new HashSet<string>(_sprites.Keys, StringComparer.Ordinal);
        /// <summary>Reads a declared image.</summary>
        public Sprite Get(string key) => _sprites[key];

        /// <summary>Decodes PNG/JPG bytes with explicit sRGB and slice metadata; releases partial loads on failure.</summary>
        public static UiImageSet Load(UiContentPackage package)
        {
            var result = new UiImageSet();
            try
            {
                XElement root = UiXml.Parse(package.Text("images.xml"), "images.xml").Root;
                if (root.Name != "Images") throw UiXml.Error(root, "Expected Images");
                UiXml.Attributes(root, "");
                long pixels = 0;
                foreach (var node in root.Elements())
                {
                    if (node.Name != "Image" || node.HasElements) throw UiXml.Error(node, "Expected image metadata");
                    UiXml.Attributes(node, "id,file,left,bottom,right,top,ppu,pivotX,pivotY");
                    string id = UiXml.Required(node, "id"), file = UiXml.Required(node, "file");
                    if (result._sprites.ContainsKey(id)) throw UiXml.Error(node, "Duplicate image id");
                    if (!(file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
                        throw UiXml.Error(node, "Only PNG/JPG images are supported");
                    byte[] bytes = package.Bytes(file);
                    Vector2Int dimensions = Dimensions(bytes);
                    pixels += (long)dimensions.x * dimensions.y;
                    if (dimensions.x < 1 || dimensions.y < 1 || dimensions.x > 4096 || dimensions.y > 4096 || pixels > 16 * 1024 * 1024)
                        throw UiXml.Error(node, "Decoded image budget exceeded");
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { name = id, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    result._textures.Add(texture);
                    if (!ImageConversion.LoadImage(texture, bytes, true) || texture.width != dimensions.x || texture.height != dimensions.y)
                        throw UiXml.Error(node, "Image decode failed or size mismatch: " + file);
                    var border = new Vector4(UiXml.Number(node, "left", 0, 0, texture.width), UiXml.Number(node, "bottom", 0, 0, texture.height),
                        UiXml.Number(node, "right", 0, 0, texture.width), UiXml.Number(node, "top", 0, 0, texture.height));
                    if (border.x + border.z > texture.width || border.y + border.w > texture.height) throw UiXml.Error(node, "Slice borders exceed image size");
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(UiXml.Number(node, "pivotX", .5f, 0, 1), UiXml.Number(node, "pivotY", .5f, 0, 1)),
                        UiXml.Number(node, "ppu", 100, 1, 1000), 0, SpriteMeshType.FullRect, border);
                    sprite.name = id;
                    result._sprites.Add(id, sprite);
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        // Inspect allocation dimensions before handing compressed bytes to Unity's decoder.
        private static Vector2Int Dimensions(byte[] bytes)
        {
            if (bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10 &&
                bytes[12] == 73 && bytes[13] == 72 && bytes[14] == 68 && bytes[15] == 82)
                return new Vector2Int(BigEndian(bytes, 16, 4), BigEndian(bytes, 20, 4));
            if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216)
            {
                for (int i = 2; i + 4 < bytes.Length;)
                {
                    if (bytes[i] != 255) break;
                    while (i < bytes.Length && bytes[i] == 255) i++;
                    if (i >= bytes.Length) break;
                    int marker = bytes[i++];
                    if (marker == 216 || marker == 217 || (marker >= 208 && marker <= 215)) continue;
                    if (i + 2 > bytes.Length) break;
                    int length = BigEndian(bytes, i, 2);
                    if (length < 2 || i + length > bytes.Length) break;
                    if ((marker == 192 || marker == 193 || marker == 194) && length >= 7)
                        return new Vector2Int(BigEndian(bytes, i + 5, 2), BigEndian(bytes, i + 3, 2));
                    i += length;
                }
            }
            throw new FormatException("images.xml: malformed or unsupported PNG/JPG header");
        }

        private static int BigEndian(byte[] bytes, int offset, int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++) value = unchecked((value << 8) | bytes[offset + i]);
            return value;
        }

        /// <summary>Releases every owned Unity resource; bundled fonts/materials remain caller-owned.</summary>
        public void Dispose()
        {
            foreach (var sprite in _sprites.Values) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in _textures) UnityEngine.Object.Destroy(texture);
            _sprites.Clear(); _textures.Clear();
        }
    }
}
