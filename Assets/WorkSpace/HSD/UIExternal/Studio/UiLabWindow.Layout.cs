using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;

namespace GGD.UI.Authoring.Studio
{
    public sealed partial class UiLabWindow
    {
        private UiLabTheme _theme;
        private UiLabFontPreview _fontSpecimen;
        private int _libraryTab, _inspectorTab;
        private float _previewZoom = 1;
        private bool _fontPathExpanded;
        private string _colorEditor;
        private readonly Dictionary<string, Color> _lastValidColors = new Dictionary<string, Color>();
        private Vector2 _libraryScroll;
        /// <summary>Actual preview viewport in GUI coordinates, excluding all tool controls.</summary>
        private Rect LogicalPreviewBounds => new Rect(Sidebar + 20, Top + 52, Mathf.Max(100, LayoutWidth - Sidebar - Inspector - 40), Mathf.Max(100, LayoutHeight - Top - Bottom - 68));
        /// <summary>Whether desktop theme and layout have been drawn.</summary>
        public bool VisualUiReady => _theme != null;
        /// <summary>Switches the properties page without modifying experiment values.</summary>
        public void SetInspectorTab(int tab) { FinishLiveEdits(); _inspectorTab = Mathf.Clamp(tab, 0, 2); _rightScroll = Vector2.zero; }
        /// <summary>Switches screen, element and font library views.</summary>
        public void SetLibraryTab(int tab) { _libraryTab = Mathf.Clamp(tab, 0, 2); _libraryScroll = Vector2.zero; }
        /// <summary>Changes preview magnification relative to fit, without writing display geometry.</summary>
        public void SetPreviewZoom(float zoom) { _previewZoom = Mathf.Clamp(zoom, .25f, 4); if (Mathf.Approximately(_previewZoom, 1)) _previewPan = Vector2.zero; ClampPreviewPan(); }
        /// <summary>Expands a named color control without applying or saving its displayed values.</summary>
        public void ToggleColorEditor(string key) { if (key != "text" && key != "outline" && key != "image") throw new ArgumentException("Unknown color control", nameof(key)); _colorEditor = _colorEditor == key ? null : key; }
        private void OnGUI()
        {
            if (Workspace == null || _template == null) return;
            _theme ??= new UiLabTheme(GUI.skin, _editorFont); GUI.skin = _theme.Skin;
            if (Event.current.type == EventType.Repaint) _layoutOverflows.Clear();
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(InterfaceScale, InterfaceScale, 1));
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = !_quitPrompt && !StudioFileDialog.Busy;
                DrawShell(); DrawHeader(); DrawPreviewToolbar(); DrawLeft(); DrawRight(); DrawBottom(); DrawSelection();
                GUI.enabled = enabled; if (_quitPrompt) DrawQuitPrompt();
            }
            finally { GUI.enabled = enabled; GUI.matrix = previous; }
        }
        private void DrawShell()
        {
            GUI.Box(new Rect(12, 12, LayoutWidth - 24, 76), GUIContent.none, _theme.Panel);
            GUI.Box(new Rect(12, Top, Sidebar - 12, LayoutHeight - Top - Bottom - 8), GUIContent.none, _theme.Panel);
            GUI.Box(new Rect(LayoutWidth - Inspector, Top, Inspector - 12, LayoutHeight - Top - Bottom - 8), GUIContent.none, _theme.Panel);
            GUI.Box(new Rect(12, LayoutHeight - Bottom, LayoutWidth - 24, Bottom - 12), GUIContent.none, _theme.Panel);
        }
        private void DrawHeader()
        {
            GUILayout.BeginArea(new Rect(30, 24, LayoutWidth - 60, 56)); GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(205)); GUILayout.Label("UI LAB", _theme.Title); GUILayout.Label("GaeGGUL / Visual workspace", _theme.Note); GUILayout.EndVertical();
            GUILayout.Space(12); GUILayout.BeginVertical(); GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label((string)_screens.Single(x => (string)x.Attribute("key") == _screen).Attribute("title"), _theme.Section);
            GUILayout.FlexibleSpace(); GUILayout.Label(Workspace.IsDirty ? "저장 전 변경 있음" : "모든 변경 저장됨", _theme.Note);
            GUILayout.EndHorizontal(); GUILayout.EndVertical(); GUILayout.Space(20);
            Button("실행 취소", Undo, null, GUILayout.Width(74)); Button("다시 실행", Redo, null, GUILayout.Width(74));
            Button("UI 캡처", Capture, null, GUILayout.Width(74)); Button("실험 저장", Save, _theme.Primary, GUILayout.Width(100));
            Button("제작실", () => { FinishLiveEdits(); if (Workspace.IsDirty) Save(); _close?.Invoke(); }, null, GUILayout.Width(72));
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        private void DrawPreviewToolbar()
        {
            GUILayout.BeginArea(new Rect(Sidebar + 20, Top + 2, LayoutWidth - Sidebar - Inspector - 40, 42)); GUILayout.BeginHorizontal();
            GUILayout.Label(_showA ? "A / 비교본" : "B / 편집본", _theme.Badge, GUILayout.Width(95));
            Button("A 보관", CaptureA, null, GUILayout.Width(66)); Button(_showA ? "B 보기" : "A 보기", () => ShowA(!_showA), null, GUILayout.Width(66));
            Button("원본", () => { _a = new XDocument(new XElement("Experiment")); ShowA(true); }, null, GUILayout.Width(56));
            GUILayout.FlexibleSpace();
            Button("−", () => SetPreviewZoom(_previewZoom - .15f), null, GUILayout.Width(28)); GUILayout.Label(Mathf.RoundToInt(_previewZoom * 100) + "%", _theme.Note, GUILayout.Width(40));
            Button("+", () => SetPreviewZoom(_previewZoom + .15f), null, GUILayout.Width(28)); Button("맞춤", () => SetPreviewZoom(1), null, GUILayout.Width(48));
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        private void DrawLeft()
        {
            GUILayout.BeginArea(new Rect(26, Top + 12, Sidebar - 40, LayoutHeight - Top - Bottom - 32));
            GUILayout.Label("라이브러리", _theme.Section); int tab = Tabs(_libraryTab, "화면", "요소", "폰트"); if (tab != _libraryTab) SetLibraryTab(tab);
            _libraryScroll = GUILayout.BeginScrollView(_libraryScroll);
            if (_libraryTab == 0)
            {
                GUILayout.Space(8); int category = Tabs(_category == "인게임" ? 0 : 1, "인게임", "아웃게임"); _category = category == 0 ? "인게임" : "아웃게임";
                GUILayout.Label("실험할 화면을 선택하세요", _theme.Note); GUILayout.Space(10); int index = 1;
                foreach (var screen in _screens.Where(x => (string)x.Attribute("category") == _category))
                {
                    string key = (string)screen.Attribute("key");
                    Button((index++).ToString("00") + "   " + (string)screen.Attribute("title"), () => OpenScreen(key), key == _screen ? _theme.Selected : _theme.Row, GUILayout.Height(40));
                }
                GUILayout.Space(20); GUILayout.Label("화면 선택 후 글자를 클릭하면\n오른쪽에서 스타일을 조절할 수 있어요.", _theme.Note);
            }
            else if (_libraryTab == 1)
            {
                GUILayout.Space(8); _filter = GUILayout.TextField(_filter); GUILayout.Label("요소 이름 또는 역할로 검색", _theme.Note); GUILayout.Space(6);
                for (int i = 0; i < _template.Rects.Length; i++)
                {
                    if (!_hasGraphic[i]) continue; var rect = _template.Rects[i]; var text = rect.GetComponent<TMP_Text>();
                    string role = text == null ? "그림" : RoleLabel(UiLabWorkspace.Role(text)); string label = rect.name;
                    if (_filter != "" && (label + role).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int captured = i; Button(label, () => Select(_template.Ids[captured]), _selected == _template.Ids[i] ? _theme.Selected : _theme.Row);
                    GUILayout.Label("  " + role, _theme.Note);
                }
            }
            else DrawFontLibrary();
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawFontLibrary()
        {
            GUILayout.Space(10);
            Button(StudioFileDialog.Busy ? "파일 선택 창이 열려 있어요" : "+  폰트 가져오기", ImportFont, _theme.Primary);
            GUILayout.Label("TTF / OTF 파일을 바로 등록합니다", _theme.Note); GUILayout.Space(8);
            Button("원본 폰트", () => ChooseFont(""), _font == "" ? _theme.Selected : _theme.Row);
            foreach (var font in Workspace.Fonts)
            {
                string key = (string)font.Attribute("key"); Button((string)font.Attribute("name"), () => ChooseFont(key), _font == key ? _theme.Selected : _theme.Row);
            }
            GUILayout.Space(14); DrawFontSpecimen();
            GUILayout.Label("글자나 범위를 고르면 폰트가 바로 반영됩니다", _theme.Note);
            GUILayout.Space(10); _fontPathExpanded = GUILayout.Toggle(_fontPathExpanded, "파일 경로로 등록");
            if (_fontPathExpanded) { _fontPath = GUILayout.TextField(_fontPath); Button("경로 등록", () => RegisterFont(_fontPath)); }
        }
        private void DrawFontSpecimen()
        {
            GUILayout.Label("FONT PREVIEW", _theme.Note);
            try
            {
                var font = _font == "" ? SelectedText?.font ?? _template.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault()?.font : Workspace.Font(_font);
                _fontSpecimen ??= new UiLabFontPreview(); _fontSpecimen.Render(font);
                Rect rect = GUILayoutUtility.GetRect(100, 78, GUILayout.ExpandWidth(true)); GUI.DrawTexture(rect, _fontSpecimen.Texture, ScaleMode.ScaleToFit);
            }
            catch (Exception error) { GUILayout.Label(error.Message, _theme.Note); }
        }
        private TMP_Text SelectedText => _selected == null ? null : _template.Rects[Array.IndexOf(_template.Ids, _selected)].GetComponent<TMP_Text>();
        private string Scope => _scope == "현재 화면 전체" ? "all" : _scope == "제목" ? "role:title" : _scope == "본문" ? "role:body" : _scope == "버튼" ? "role:button" : _scope == "숫자" ? "role:number" : _selected ?? throw new InvalidOperationException("먼저 요소를 선택하세요.");
        private void DrawRight()
        {
            GUILayout.BeginArea(new Rect(LayoutWidth - Inspector + 16, Top + 12, Inspector - 44, LayoutHeight - Top - Bottom - 32));
            GUILayout.Label("속성", _theme.Section);
            int tab = Tabs(_inspectorTab, "글자", "외형", "연출"); if (tab != _inspectorTab) SetInspectorTab(tab);
            string selected = _selected == null ? "요소를 선택하거나 적용 범위를 고르세요" : _template.Rects[Array.IndexOf(_template.Ids, _selected)].name;
            GUILayout.Label(selected, _theme.Note); _rightScroll = GUILayout.BeginScrollView(_rightScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(Inspector - 70));
            _recordInspectorControls = true;
            if (_inspectorTab == 0) DrawTypography(); else if (_inspectorTab == 1) DrawAppearance(); else DrawAnimation();
            _recordInspectorControls = false; GUILayout.EndVertical(); GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawScope()
        {
            GUILayout.Label("적용 범위", _theme.Section);
            string[] scopes = { "선택 요소", "제목", "본문", "버튼", "숫자", "현재 화면 전체" };
            for (int row = 0; row < 2; row++) { GUILayout.BeginHorizontal(); foreach (string scope in scopes.Skip(row * 3).Take(3)) Button(scope == "현재 화면 전체" ? "전체" : scope, () => { FinishLiveEdits(); _scope = scope; }, _scope == scope ? _theme.Selected : null); GUILayout.EndHorizontal(); }
            bool shared = GUILayout.Toggle(_shared, "여러 화면에 공통 적용"); if (shared != _shared) { FinishLiveEdits(); _shared = shared; }
        }
        private void DrawTypography()
        {
            DrawScope(); GUILayout.Label("폰트", _theme.Section);
            var before = TypographyDraft();
            string name = _font == "" ? "원본 폰트 사용" : (string)Workspace.Fonts.Single(x => (string)x.Attribute("key") == _font).Attribute("name");
            Button(name + "  ›", () => SetLibraryTab(2), _theme.Row);
            NumberSlider("글자 크기", ref _size, 12, 120); _auto = GUILayout.Toggle(_auto, "영역에 맞춰 자동 크기 조절");
            ColorControl("글자 색", "text", ref _color); GUILayout.Label("아웃라인", _theme.Section);
            NumberSlider("두께", ref _outline, 0, 1); ColorControl("외곽선 색", "outline", ref _edge); NumberSlider("부드러움", ref _soft, 0, 1);
            GUILayout.Label("간격", _theme.Section); NumberSlider("자간", ref _spacing, -10, 30); NumberSlider("행간", ref _line, -10, 50);
            QueueChangedFields(before, TypographyDraft());
            GUILayout.Space(8); GUILayout.Label("변경 즉시 미리보기에 반영됩니다", _theme.Note);
            Button("범위 원본 복원", () => RestoreScope(_shared ? "*" : _screen, Scope));
            GUILayout.Label("문구 실험", _theme.Section); string copy = GUILayout.TextArea(_copy, GUILayout.Height(76), GUILayout.MinWidth(0)); if (copy != _copy) EditLiveProperty("text", copy);
            Button("누락 글자 검사", CheckGlyphs);
        }
        private void DrawAppearance()
        {
            var before = AppearanceDraft();
            GUILayout.Label("선택 요소", _theme.Section); GUILayout.Label("미리보기의 글자나 그림을 클릭하세요", _theme.Note);
            ColorControl("그림 색", "image", ref _tint); GUILayout.Label("배치와 크기", _theme.Section);
            Field("X 이동량", ref _dx); Field("Y 이동량", ref _dy); Field("너비", ref _width); Field("높이", ref _height);
            QueueChangedFields(before, AppearanceDraft()); GUILayout.Label("유효한 입력값이 바로 반영됩니다", _theme.Note);
            Button("선택 요소 원본 복원", () => RestoreScope(_screen, _selected ?? throw new InvalidOperationException("요소를 선택하세요.")));
            GUILayout.Label("미리보기", _theme.Section); GUILayout.Label("화면비", _theme.Note); int ratio = Tabs(_ratioHeight == 1920 ? 0 : 1, "9 : 16", "긴 화면"); _ratioHeight = ratio == 0 ? 1920 : 2340;
            GUILayout.Label("배경 밝기", _theme.Note); _background = GUILayout.HorizontalSlider(_background, 0, .8f);
        }
        private void DrawAnimation()
        {
            string before = MotionDraft();
            GUILayout.Label("재생 대상", _theme.Section); Button("선택 해제 / 화면 전체", () => { FinishLiveEdits(); Playback.Stop(); _selected = null; });
            GUILayout.Label("실험 프리셋", _theme.Section);
            string[] kinds = { "팝업", "슬라이드", "페이드", "눌림", "선택 강조", "순차 등장" };
            for (int row = 0; row < 3; row++) { GUILayout.BeginHorizontal(); for (int col = 0; col < 2; col++) { int kind = row * 2 + col; Button(kinds[kind], () => _kind = kind, _kind == kind ? _theme.Selected : null); } GUILayout.EndHorizontal(); }
            GUILayout.Label("타이밍", _theme.Section); NumberSlider("시간 (초)", ref _duration, .05f, 2); NumberSlider("지연 (초)", ref _delay, 0, 2); NumberSlider("순차 간격", ref _stagger, 0, .5f);
            GUILayout.Label("움직임", _theme.Section); NumberSlider("시작 배율", ref _scale, 0, 1.5f); NumberSlider("이동 거리", ref _distance, 0, 500);
            GUILayout.Label("속도 곡선", _theme.Section); _ease = Tabs(_ease, "일정", "부드럽게", "감속", "통통");
            if (before != MotionDraft()) QueueLiveMotion();
            GUILayout.Space(8); GUILayout.Label("설정이 바로 반영됩니다. 하단에서 재생하세요.", _theme.Note);
            GUILayout.Label(_template.MotionTarget == null ? "이 화면은 실험 연출로 재생합니다." : "하단에서 원본 스케일 연출도 선택할 수 있어요.", _theme.Note);
        }
        private void DrawBottom()
        {
            GUILayout.BeginArea(new Rect(30, LayoutHeight - Bottom + 10, LayoutWidth - 60, Bottom - 30));
            GUILayout.BeginHorizontal(); GUILayout.Label("표시 상태", _theme.Note, GUILayout.Width(60));
            string[] states = { "기본", "긴 문구", "재화 부족", "최대 단계" };
            for (int i = 0; i < states.Length; i++) { int state = i; Button(states[i], () => SetState(state), _state == state ? _theme.Selected : null, GUILayout.Width(76)); }
            GUILayout.FlexibleSpace(); _closing = GUILayout.Toggle(_closing, "퇴장", GUILayout.Width(66));
            if (_template.MotionTarget != null) _original = GUILayout.Toggle(_original, "원본 스케일", GUILayout.Width(110)); else _original = false;
            _loop = GUILayout.Toggle(_loop, "반복", GUILayout.Width(66)); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Button(Playback.Playing ? "일시정지" : "재생", () => { if (Playback.Playing) Playback.Playing = false; else PlayFromSelection(); }, _theme.Primary, GUILayout.Width(88));
            Button("정지", Playback.Stop, null, GUILayout.Width(52));
            float progress = GUILayout.HorizontalSlider(Playback.Progress, 0, 1, GUILayout.ExpandWidth(true)); if (!Mathf.Approximately(progress, Playback.Progress)) { Playback.Playing = false; Playback.Seek(progress); }
            GUILayout.Label(Mathf.RoundToInt(Playback.Progress * 100).ToString("000") + "%", _theme.Note, GUILayout.Width(44));
            foreach (float speed in new[] { .25f, .5f, 1f }) Button(speed.ToString("0.##") + "x", () => { _speed = speed; Playback.Speed = speed; }, _speed == speed ? _theme.Selected : null, GUILayout.Width(58));
            Playback.Loop = _loop; GUILayout.EndHorizontal();
            var previous = GUI.color; if (_error != "") GUI.color = new Color(1, .53f, .48f); GUILayout.Label(_error == "" ? _message : _error, _theme.Note); GUI.color = previous;
            GUILayout.EndArea();
        }
        private void PlayFromSelection()
        {
            if (Playback.Progress > 0 && Playback.Progress < 1) { Playback.Playing = true; return; }
            Playback.Stop(); Playback.Configure(_template, _selected == null ? _template.transform as RectTransform : _template.Rects[Array.IndexOf(_template.Ids, _selected)]); ConfigureMotion(Playback); Playback.Seek(0); Playback.Playing = true;
        }
        private int Tabs(int selected, params string[] names)
        {
            GUILayout.BeginHorizontal(); for (int i = 0; i < names.Length; i++) if (GUILayout.Button(names[i], selected == i ? _theme.Selected : GUI.skin.button, GUILayout.ExpandWidth(true))) selected = i; GUILayout.EndHorizontal(); return selected;
        }
        private void NumberSlider(string label, ref string value, float min, float max)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(78)); float parsed = float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number) ? number : min;
            float next = GUILayout.HorizontalSlider(Mathf.Clamp(parsed, min, max), min, max, GUILayout.MinWidth(20), GUILayout.ExpandWidth(true)); if (!Mathf.Approximately(next, Mathf.Clamp(parsed, min, max))) value = F(next);
            value = GUILayout.TextField(value, GUILayout.Width(64)); RecordInspectorControl(label); GUILayout.EndHorizontal();
        }
        private void ColorControl(string label, string key, ref string value)
        {
            Color color = ColorUtility.TryParseHtmlString(value, out var parsed) ? parsed : _lastValidColors.TryGetValue(key, out var previousColor) ? previousColor : Color.white;
            _lastValidColors[key] = color;
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(78)); var previous = GUI.backgroundColor; GUI.backgroundColor = color;
            if (GUILayout.Button("", _theme.Swatch, GUILayout.Width(36), GUILayout.Height(28))) ToggleColorEditor(key);
            GUI.backgroundColor = previous; value = GUILayout.TextField(value, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)); RecordInspectorControl(label); GUILayout.EndHorizontal();
            if (_colorEditor != key) return;
            if (ColorUtility.TryParseHtmlString(value, out var typedColor)) { color = typedColor; _lastValidColors[key] = color; }
            Color beforePicker = color;
            GUILayout.BeginVertical(_theme.Inset); GUILayout.BeginHorizontal();
            foreach (Color swatch in new[] { Color.white, Color.black, UiLabTheme.Accent, new Color(.29f, .57f, 1), new Color(1, .77f, .25f), new Color(1, .4f, .6f) })
            { previous = GUI.backgroundColor; GUI.backgroundColor = swatch; if (GUILayout.Button("", _theme.Swatch, GUILayout.Width(29), GUILayout.Height(26))) color = swatch; GUI.backgroundColor = previous; }
            GUILayout.EndHorizontal(); color.r = Channel("R", color.r); color.g = Channel("G", color.g); color.b = Channel("B", color.b); color.a = Channel("A", color.a);
            if (color != beforePicker) { value = "#" + ColorUtility.ToHtmlStringRGBA(color); _lastValidColors[key] = color; }
            GUILayout.EndVertical();
        }
        private float Channel(string label, float value) { GUILayout.BeginHorizontal(); GUILayout.Label(label, _theme.Note, GUILayout.Width(18)); float next = GUILayout.HorizontalSlider(value, 0, 1); GUILayout.Label(Mathf.RoundToInt(next * 255).ToString(), _theme.Note, GUILayout.Width(28)); GUILayout.EndHorizontal(); return next; }
        private void DrawQuitPrompt()
        {
            GUI.Box(new Rect(0, 0, LayoutWidth, LayoutHeight), GUIContent.none);
            GUILayout.BeginArea(new Rect((LayoutWidth - 460) / 2, (LayoutHeight - 190) / 2, 460, 190), _theme.Panel);
            GUILayout.Space(14); GUILayout.Label("저장하지 않은 실험이 있어요", _theme.Section);
            GUILayout.Label("종료하기 전에 실험을 저장할까요? 게임 원본에는 어느 쪽도 적용되지 않습니다.", _theme.Note);
            if (_error != "") { var color = GUI.color; GUI.color = new Color(1, .53f, .48f); GUILayout.Label(_error, _theme.Note); GUI.color = color; }
            GUILayout.FlexibleSpace(); GUILayout.BeginHorizontal();
            Button("저장 후 종료", () => ResolveQuit(true, true), _theme.Primary, GUILayout.Height(36));
            Button("저장 안 하고 종료", () => ResolveQuit(true, false), null, GUILayout.Height(36));
            Button("취소", () => ResolveQuit(false, false), null, GUILayout.Height(36), GUILayout.Width(80));
            GUILayout.EndHorizontal(); GUILayout.Space(10); GUILayout.EndArea();
        }
        private static string RoleLabel(string role) => role == "title" ? "제목" : role == "number" ? "숫자" : role == "button" ? "버튼" : "본문";
        private void DrawSelection()
        {
            if (_selected == null) return; var rect = _template.Rects[Array.IndexOf(_template.Ids, _selected)]; if (!rect.gameObject.activeInHierarchy) return;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); float x = corners.Min(c => c.x) / InterfaceScale, y = LayoutHeight - corners.Max(c => c.y) / InterfaceScale, w = (corners.Max(c => c.x) - corners.Min(c => c.x)) / InterfaceScale, h = (corners.Max(c => c.y) - corners.Min(c => c.y)) / InterfaceScale;
            var bounds = LogicalPreviewBounds; float left = Mathf.Max(x, bounds.x), top = Mathf.Max(y, bounds.y), right = Mathf.Min(x + w, bounds.xMax), bottom = Mathf.Min(y + h, bounds.yMax); if (right <= left || bottom <= top) return;
            var color = GUI.color; GUI.color = UiLabTheme.Accent; GUI.DrawTexture(new Rect(left, top, right - left, 1), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(left, bottom - 1, right - left, 1), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(left, top, 1, bottom - top), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(right - 1, top, 1, bottom - top), Texture2D.whiteTexture); GUI.color = color;
        }
    }
}
