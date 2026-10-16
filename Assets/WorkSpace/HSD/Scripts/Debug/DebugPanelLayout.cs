using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.InGameDebug
{
    /// <summary>Builds the debug panel's touch UI with scene fonts and a small, owned rounded sprite.</summary>
    public sealed class DebugPanelLayout : IDisposable
    {
        internal static readonly Color Surface = new Color32(24, 34, 49, 255);
        internal static readonly Color Muted = new Color32(148, 165, 186, 255);
        internal static readonly Color Ink = new Color32(237, 243, 250, 255);
        internal static readonly Color Accent = new Color32(117, 224, 201, 255);
        internal static readonly Color Danger = new Color32(245, 154, 157, 255);
        private readonly TMP_FontAsset _font;
        private readonly Sprite _rounded;
        private readonly Texture2D _texture;
        private readonly RectTransform _sheet;
        private readonly Image[] _tabImages = new Image[3];
        private readonly TMP_Text[] _tabLabels = new TMP_Text[3];
        private readonly Image[] _modeImages = new Image[2];
        private readonly TMP_Text[] _modeLabels = new TMP_Text[2];
        private readonly Image[] _tierImages = new Image[4];
        private readonly TMP_Text[] _tierLabels = new TMP_Text[4];
        private readonly RectTransform _viewport;
        private readonly RectTransform _tierRow;
        private readonly TMP_Text _count;
        private readonly TMP_Text _message;
        private readonly TMP_Text _empty;
        private readonly TMP_Text _pauseLabel;
        private readonly Image _pauseImage;
        internal RectTransform Root { get; }
        internal RectTransform Content { get; }
        internal ScrollRect Scroll { get; }
        internal TMP_InputField Search { get; }
        internal Button Info { get; }

        /// <summary>Creates a panel under the existing safe-area root without editing the scene asset.</summary>
        public DebugPanelLayout(Transform parent, TMP_FontAsset font, Action close, Action<int> tab,
            Action<bool> catalog, Action<int> tier, Action refresh, Action pause, Action info)
        {
            _font = font;
            _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "DebugPanelRounded", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 15.5f) - 7.5f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 15.5f) - 7.5f, 0f);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(8f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            _texture.SetPixels32(pixels); _texture.Apply(false, true);
            _rounded = Sprite.Create(_texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
            Root = Box("ModernDebug", parent, new Color(0.01f, .025f, .05f, .88f), false);
            Stretch(Root, 0, 0, 0, 0);
            Root.GetComponent<Image>().raycastTarget = true;
            _sheet = Box("Sheet", Root, new Color32(15, 23, 36, 255));
            Stretch(_sheet, 24, 24, 24, 24);
            Top(Text("Eyebrow", _sheet, "DEVELOPER TOOLS", 19, Accent).rectTransform, 30, 24, 550, 30);
            Top(Text("Title", _sheet, "플레이 테스트", 37, Ink).rectTransform, 28, 57, 600, 58);
            Top(Text("Subtitle", _sheet, "필요한 대상을 찾아 바로 테스트하세요", 21, Muted).rectTransform, 30, 119, 650, 36);
            var closeButton = Button("Close", _sheet, "닫기", close, Surface, Ink, out _);
            RightTop(closeButton, 26, 24, 100, 64);
            Info = Button("Info", _sheet, "상세 정보", info, Surface, Muted, out _).GetComponent<Button>();
            RightTop((RectTransform)Info.transform, 26, 100, 156, 58);

            var tabs = Rect("Tabs", _sheet); AcrossTop(tabs, 26, 176, 68);
            string[] names = { "유닛", "토템", "선택지" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var rect = Button("Tab" + i, tabs, names[i], () => tab(index), Surface, Muted, out _tabLabels[i]);
                Segment(rect, i, names.Length, 4); _tabImages[i] = rect.GetComponent<Image>();
            }
            var modes = Rect("Modes", _sheet); AcrossTop(modes, 26, 260, 62);
            for (int i = 0; i < 2; i++)
            {
                bool isCatalog = i == 0;
                var rect = Button(i == 0 ? "Catalog" : "Current", modes, i == 0 ? "카탈로그" : "현재 배치",
                    () => catalog(isCatalog), Surface, Muted, out _modeLabels[i]);
                Segment(rect, i, 2, 4); _modeImages[i] = rect.GetComponent<Image>();
            }
            var search = Box("Search", _sheet, Surface); AcrossTop(search, 26, 338, 68);
            search.GetComponent<Image>().raycastTarget = true;
            var inputViewport = Rect("TextArea", search); Stretch(inputViewport, 22, 12, 72, 12);
            inputViewport.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text("Placeholder", inputViewport, "이름 · 설명 검색", 24, Muted);
            Stretch(placeholder.rectTransform, 0, 0, 0, 0);
            var inputText = Text("Input", inputViewport, "", 24, Ink); Stretch(inputText.rectTransform, 0, 0, 0, 0);
            inputText.richText = false;
            Search = search.gameObject.AddComponent<TMP_InputField>();
            Search.textViewport = inputViewport; Search.textComponent = inputText; Search.placeholder = placeholder;
            Search.fontAsset = _font; Search.targetGraphic = search.GetComponent<Image>();
            Search.customCaretColor = true; Search.caretColor = Accent; Search.selectionColor = new Color(.3f, .7f, .65f, .3f);
            var clear = Button("ClearSearch", search, "×", () => Search.text = "", Surface, Muted, out _);
            RightTop(clear, 4, 4, 60, 60);

            _tierRow = Rect("Tier", _sheet); AcrossTop(_tierRow, 26, 422, 54);
            string[] tiers = { "Normal", "Rare", "Epic", "Legendary" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var rect = Button("Tier" + i, _tierRow, tiers[i], () => tier(index), Surface, Muted, out _tierLabels[i]);
                Segment(rect, i, 4, 4); _tierImages[i] = rect.GetComponent<Image>();
                _tierLabels[i].fontSize = 22;
            }
            _viewport = Rect("Viewport", _sheet); Stretch(_viewport, 26, 496, 26, 150);
            _viewport.gameObject.AddComponent<RectMask2D>();
            var hit = _viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Content = Rect("Content", _viewport);
            Content.anchorMin = new Vector2(0, 1); Content.anchorMax = Vector2.one;
            Content.pivot = new Vector2(.5f, 1); Content.sizeDelta = Vector2.zero;
            Scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            Scroll.content = Content; Scroll.viewport = _viewport; Scroll.horizontal = false;
            Scroll.movementType = ScrollRect.MovementType.Clamped; Scroll.scrollSensitivity = 38;
            _empty = Text("Empty", _viewport, "아직 배치된 대상이 없습니다\n카탈로그에서 추가해 보세요", 25, Muted);
            Stretch(_empty.rectTransform, 20, 30, 20, 30); _empty.alignment = TextAlignmentOptions.Center;
            var footer = Box("Footer", _sheet, new Color32(19, 29, 43, 255));
            footer.anchorMin = Vector2.zero; footer.anchorMax = Vector2.right;
            footer.pivot = new Vector2(.5f, 0); footer.offsetMin = new Vector2(16, 16); footer.offsetMax = new Vector2(-16, 138);
            _count = Text("Count", footer, "", 19, Muted); Top(_count.rectTransform, 18, 12, 550, 30);
            _message = Text("Feedback", footer, "", 22, Ink); Top(_message.rectTransform, 18, 48, 550, 60);
            var pauseRect = Button("Pause", footer, "게임 정지", pause, Surface, Ink, out _pauseLabel);
            RightTop(pauseRect, 14, 54, 148, 54); _pauseLabel.fontSize = 22;
            _pauseImage = pauseRect.GetComponent<Image>();
            var refreshRect = Button("Refresh", footer, "새로고침", refresh, Surface, Muted, out var refreshLabel);
            RightTop(refreshRect, 14, 8, 148, 40); refreshLabel.fontSize = 19;
        }

        internal UI_DebugItemSlot CreateSlot()
        {
            var row = Box("Item", Content, Surface);
            var iconBg = Box("IconWell", row, new Color32(34, 47, 64, 255)); Top(iconBg, 14, 20, 94, 104);
            var iconRect = Box("Icon", iconBg, Color.white, false); Stretch(iconRect, 7, 7, 7, 7);
            var icon = iconRect.GetComponent<Image>(); icon.preserveAspect = true;
            var title = Text("Name", row, "", 27, Ink); AcrossTop(title.rectTransform, 124, 14, 44, 150);
            title.fontStyle = FontStyles.Bold;
            var desc = Text("Description", row, "", 21, Muted); AcrossTop(desc.rectTransform, 124, 58, 80, 150);
            desc.textWrappingMode = TextWrappingModes.Normal; desc.overflowMode = TextOverflowModes.Ellipsis;
            var action = Button("Action", row, "추가", null, Accent, new Color32(15, 40, 36, 255), out var actionText);
            RightTop(action, 14, 42, 116, 64); actionText.fontSize = 23;
            var slot = row.gameObject.AddComponent<UI_DebugItemSlot>();
            slot.Configure(icon, title, desc, action.GetComponent<Button>(), actionText);
            return slot;
        }

        internal void Select(DebugTabType tab, bool catalog, Tier tier, bool hasInfo)
        {
            for (int i = 0; i < _tabImages.Length; i++) SetSelected(_tabImages[i], _tabLabels[i], i == (int)tab);
            for (int i = 0; i < 2; i++) SetSelected(_modeImages[i], _modeLabels[i], (i == 0) == catalog);
            for (int i = 0; i < 4; i++) SetSelected(_tierImages[i], _tierLabels[i], i == (int)tier);
            _modeLabels[1].text = tab == DebugTabType.LevelUp ? "적용 중" : "현재 배치";
            bool showTier = tab == DebugTabType.Unit && catalog;
            _tierRow.gameObject.SetActive(showTier);
            _viewport.offsetMax = new Vector2(-26, showTier ? -496 : -422);
            Info.gameObject.SetActive(hasInfo);
        }

        internal void Count(int visible, int total, bool catalog, bool loading, bool searching)
        {
            _count.text = loading ? $"카탈로그 불러오는 중 · {total}개" : $"{visible} / {total}개 표시";
            _empty.gameObject.SetActive(visible == 0);
            _empty.text = loading ? "카탈로그를 불러오고 있습니다" : searching ? "검색 결과가 없습니다\n다른 이름이나 번호로 찾아보세요" :
                catalog ? "사용 가능한 대상이 없습니다" : "아직 등록된 대상이 없습니다\n카탈로그에서 추가해 보세요";
        }
        internal void Message(string message, bool error) { _message.text = message; _message.color = error ? Danger : Ink; }
        internal void Pause(bool paused) { _pauseLabel.text = paused ? "정지 해제" : "게임 정지"; _pauseImage.color = paused ? Accent : Surface; _pauseLabel.color = paused ? Surface : Ink; }
        internal void ResetScroll() { Scroll.StopMovement(); Content.anchoredPosition = Vector2.zero; }

        private static void SetSelected(Image image, TMP_Text label, bool selected)
        { image.color = selected ? new Color32(44, 75, 79, 255) : Surface; label.color = selected ? Accent : Muted; }
        private RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        private RectTransform Box(string name, Transform parent, Color color, bool rounded = true)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            if (rounded) { image.sprite = _rounded; image.type = Image.Type.Sliced; }
            return rect;
        }
        private TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font; text.text = value; text.fontSize = size; text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }
        private RectTransform Button(string name, Transform parent, string value, Action action, Color bg, Color fg, out TMP_Text label)
        {
            var rect = Box(name, parent, bg); var button = rect.gameObject.AddComponent<Button>();
            var image = rect.GetComponent<Image>(); image.raycastTarget = true; button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.88f, .95f, 1f); colors.pressedColor = new Color(.65f, .78f, .88f); button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            label = Text("Label", rect, value, 26, fg); Stretch(label.rectTransform, 6, 0, 6, 0); label.alignment = TextAlignmentOptions.Center;
            return rect;
        }
        internal static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        internal static void Top(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        private static void RightTop(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-x, -y); rect.sizeDelta = new Vector2(w, h); }
        private static void AcrossTop(RectTransform rect, float left, float top, float height, float right = -1)
        { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-(right < 0 ? left : right), -top); }
        private static void Segment(RectTransform rect, int index, int count, float gap)
        { rect.anchorMin = new Vector2((float)index / count, 0); rect.anchorMax = new Vector2((float)(index + 1) / count, 1); rect.offsetMin = new Vector2(index == 0 ? 0 : gap, 0); rect.offsetMax = new Vector2(index == count - 1 ? 0 : -gap, 0); }

        /// <summary>Releases the generated sprite and texture with the scene-owned panel.</summary>
        public void Dispose() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); UnityEngine.Object.Destroy(_rounded); UnityEngine.Object.Destroy(_texture); }
    }
}
