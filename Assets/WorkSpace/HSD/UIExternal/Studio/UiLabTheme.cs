using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Owned dark desktop skin with consistent typography, rounded surfaces and a restrained teal accent.</summary>
    public sealed class UiLabTheme : IDisposable
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        /// <summary>Readable foreground and muted label colors.</summary>
        public static readonly Color Text = new Color(.88f, .92f, .97f), Muted = new Color(.67f, .73f, .82f), Accent = new Color(.28f, .86f, .75f);
        /// <summary>Private GUI skin; never modifies Unity's global source skin.</summary>
        public GUISkin Skin { get; }
        /// <summary>Panel, heading, note and action styles.</summary>
        public readonly GUIStyle Panel, Inset, Title, Section, Note, Primary, Selected, Row, Badge, Swatch;
        /// <summary>Builds all small reusable textures once, on the first GUI pass.</summary>
        public UiLabTheme(GUISkin source, Font font)
        {
            Skin = UnityEngine.Object.Instantiate(source); Skin.font = font;
            var surface = Tile(new Color(.095f, .115f, .15f)); var field = Tile(new Color(.055f, .071f, .098f));
            var normal = Tile(new Color(.14f, .17f, .22f)); var hover = Tile(new Color(.19f, .24f, .30f)); var selected = Tile(new Color(.105f, .27f, .275f)); var accent = Tile(Accent);
            Panel = new GUIStyle { normal = { background = surface }, border = new RectOffset(9, 9, 9, 9), padding = new RectOffset(16, 16, 16, 16) };
            Inset = new GUIStyle(Panel) { normal = { background = field }, padding = new RectOffset(10, 10, 10, 10) };
            Skin.label = new GUIStyle(source.label) { font = font, fontSize = 13, normal = { textColor = Text }, wordWrap = false, margin = new RectOffset(0, 0, 3, 3), padding = new RectOffset(0, 0, 2, 2) };
            Skin.button = new GUIStyle(source.button) { font = font, fontSize = 13, alignment = TextAnchor.MiddleCenter, fixedHeight = 32, border = new RectOffset(9, 9, 9, 9), padding = new RectOffset(10, 10, 4, 4), margin = new RectOffset(2, 2, 3, 3), normal = { background = normal, textColor = Text }, hover = { background = hover, textColor = Color.white }, active = { background = selected, textColor = Accent }, focused = { background = hover, textColor = Color.white } };
            Skin.toggle = new GUIStyle(Skin.button) { alignment = TextAnchor.MiddleLeft, onNormal = { background = selected, textColor = Accent }, onHover = { background = selected, textColor = Color.white }, onActive = { background = selected, textColor = Accent } };
            Skin.textField = new GUIStyle(source.textField) { font = font, fontSize = 13, fixedHeight = 30, border = new RectOffset(9, 9, 9, 9), padding = new RectOffset(9, 9, 5, 5), margin = new RectOffset(2, 2, 3, 3), normal = { background = field, textColor = Text }, focused = { background = normal, textColor = Color.white } };
            Skin.textArea = new GUIStyle(Skin.textField) { fixedHeight = 0, wordWrap = true, alignment = TextAnchor.UpperLeft };
            Skin.box = new GUIStyle(Panel);
            // Rail artwork is vertically inset; hit geometry has no inset, so the thumb stays centered.
            Skin.horizontalSlider = new GUIStyle { fixedHeight = 18, stretchWidth = true, padding = new RectOffset(), border = new RectOffset(6, 6, 7, 7), margin = new RectOffset(4, 4, 8, 6), normal = { background = HorizontalTrack() } };
            Skin.horizontalSliderThumb = new GUIStyle { fixedWidth = 14, fixedHeight = 18, border = new RectOffset(6, 6, 6, 6), normal = { background = accent }, hover = { background = Tile(new Color(.5f, .98f, .85f)) }, active = { background = accent } };
            Skin.verticalScrollbar = new GUIStyle { fixedWidth = 12, stretchHeight = true, border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(3, 3, 2, 2), margin = new RectOffset(4, 0, 0, 0), normal = { background = field } };
            Skin.verticalScrollbarThumb = new GUIStyle { fixedWidth = 6, stretchHeight = true, border = new RectOffset(3, 3, 6, 6), normal = { background = hover }, hover = { background = accent }, active = { background = accent } };
            Skin.verticalScrollbarUpButton = new GUIStyle { fixedWidth = 12, fixedHeight = 0 };
            Skin.verticalScrollbarDownButton = new GUIStyle { fixedWidth = 12, fixedHeight = 0 };
            Title = new GUIStyle(Skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            Section = new GUIStyle(Skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 12, 7) };
            Note = new GUIStyle(Skin.label) { fontSize = 12, normal = { textColor = Muted }, wordWrap = true };
            Primary = new GUIStyle(Skin.button) { normal = { background = accent, textColor = new Color(.025f, .13f, .14f) }, hover = { background = Tile(new Color(.42f, .95f, .83f)), textColor = Color.black }, active = { background = selected, textColor = Color.white }, fontStyle = FontStyle.Bold };
            Selected = new GUIStyle(Skin.button) { normal = { background = selected, textColor = Accent }, hover = { background = selected, textColor = Color.white } };
            Row = new GUIStyle(Skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 10, 4, 4) };
            Badge = new GUIStyle(Selected) { fixedHeight = 25, fontSize = 11, alignment = TextAnchor.MiddleCenter };
            Swatch = new GUIStyle(Skin.button) { normal = { background = Texture2D.whiteTexture }, hover = { background = Texture2D.whiteTexture }, active = { background = Texture2D.whiteTexture }, border = new RectOffset(), fixedHeight = 26 };
        }
        private Texture2D HorizontalTrack()
        {
            var texture = new Texture2D(24, 18, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[24 * 18];
            for (int y = 7; y <= 10; y++) for (int x = 0; x < 24; x++) if (x > 0 && x < 23 || y == 8 || y == 9) pixels[y * 24 + x] = new Color(.19f, .23f, .29f);
            texture.SetPixels(pixels); texture.Apply(false, true); _textures.Add(texture); return texture;
        }
        private Texture2D Tile(Color color)
        {
            const int size = 24; const float radius = 7;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius)); float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                float alpha = dx > 0 && dy > 0 ? Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)) : 1;
                var pixel = color; pixel.a *= alpha; pixels[y * size + x] = pixel;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); _textures.Add(texture); return texture;
        }
        /// <summary>Releases only generated GUI textures and the private skin.</summary>
        public void Dispose() { foreach (var texture in _textures) UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(Skin); }
    }
}
