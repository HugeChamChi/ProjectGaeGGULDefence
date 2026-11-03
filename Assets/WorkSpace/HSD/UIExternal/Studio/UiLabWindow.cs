using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Ingame/outgame visual laboratory over exported real UI hierarchies, isolated from game actions.</summary>
    public sealed partial class UiLabWindow : MonoBehaviour
    {
        private float Sidebar => Mathf.Clamp(LayoutWidth * .16f, 220, 270);
        private float Inspector => Mathf.Clamp(LayoutWidth * .23f, 330, 410);
        private const float Top = 108, Bottom = 132;
        private Font _editorFont;
        private Action _close;
        private GameObject _canvas;
        private RectTransform _frame;
        private RectTransform _previewClip;
        private RectMask2D _previewMask;
        private Image _backgroundImage;
        private AssetBundle _bundle;
        private XElement[] _screens;
        private UiLabTemplate _template;
        private string _screen, _selected, _category = "인게임", _error = "", _message = "실험값은 게임 원본에 자동 적용되지 않습니다.", _filter = "";
        private string _font = "", _fontPath = "", _scope = "선택 요소", _color = "#FFFFFFFF", _edge = "#000000FF", _copy = "", _tint = "#FFFFFFFF";
        private string _size = "36", _outline = "0.15", _soft = "0", _spacing = "0", _line = "0", _dx = "0", _dy = "0", _width = "100", _height = "100";
        private string _duration = ".35", _delay = "0", _scale = ".8", _distance = "160", _stagger = ".1";
        private int _kind, _ease = 3, _state;
        private bool _auto, _shared, _showA, _closing, _original, _loop, _quitPrompt, _quitApproved;
        private float _speed = 1, _ratioHeight = 1920, _background = .08f;
        private Vector2 _leftScroll, _rightScroll;
        private XDocument _a;
        private bool[] _hasGraphic;
        /// <summary>Lab experiment and portable font store.</summary>
        public UiLabWorkspace Workspace { get; private set; }
        /// <summary>Current real visual snapshot.</summary>
        public UiLabTemplate Template => _template;
        /// <summary>Current seekable preview.</summary>
        public UiLabPlayback Playback { get; private set; }
        /// <summary>Last failed action, with the previous valid view retained.</summary>
        public string Error => _error;
        /// <summary>Available actual screen keys.</summary>
        public IEnumerable<string> ScreenKeys => _screens.Select(x => (string)x.Attribute("key"));
        /// <summary>Initializes an independent visual canvas and its portable content.</summary>
        public void Initialize(Font editorFont, Action close, string directory = null)
        {
            _editorFont = editorFont; _close = close;
            if (directory == null) { string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--lab-root"); directory = index >= 0 ? args[index + 1] : Path.GetFullPath(Path.Combine(Application.dataPath, "../UILab")); }
            Workspace = new UiLabWorkspace(directory);
            var manifest = AuthoringWorkspace.Parse(File.ReadAllText(Path.Combine(directory, "lab.xml")), "lab.xml").Root;
            if ((string)manifest.Attribute("schema") != "1") throw new FormatException("지원하지 않는 실험실 버전입니다.");
            string bundle = AuthoringWorkspace.SafePath(directory, AuthoringWorkspace.Required(manifest, "bundle"));
            if (AuthoringWorkspace.Hash(File.ReadAllBytes(bundle)) != (string)manifest.Attribute("sha256")) throw new FormatException("실험실 화면 패키지가 변경됐습니다.");
            _bundle = AssetBundle.LoadFromFile(bundle) ?? throw new IOException("실험실 화면을 열 수 없습니다."); _screens = manifest.Elements("Screen").ToArray();
            _canvas = new GameObject("UILabCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false); var canvas = _canvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _previewClip = new GameObject("Preview viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(RectMask2D)).GetComponent<RectTransform>(); _previewClip.SetParent(_canvas.transform, false); _previewMask = _previewClip.GetComponent<RectMask2D>();
            var backdrop = _previewClip.GetComponent<UnityEngine.UI.Image>(); backdrop.color = new Color(.045f, .06f, .085f); backdrop.raycastTarget = false;
            _frame = new GameObject("Lab1080", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); _frame.SetParent(_previewClip, false);
            _backgroundImage = _frame.GetComponent<Image>(); _backgroundImage.color = new Color(_background, _background, _background); _backgroundImage.raycastTarget = false;
            OpenScreen(ScreenKeys.First()); Application.wantsToQuit += CheckQuit;
        }
        private bool CheckQuit()
        {
            if (_quitApproved) return true;
            try { FinishLiveEdits(); } catch (Exception error) { Debug.LogWarning("UI_LAB: " + error.Message); }
            if (Workspace?.IsDirty != true) return true;
            // Never block quitting silently: ask with a visible save / discard / cancel prompt.
            _quitPrompt = true; return false;
        }
        /// <summary>Whether the unsaved-changes quit prompt is showing.</summary>
        public bool QuitPromptVisible => _quitPrompt;
        /// <summary>Resolves the quit prompt: save then quit, quit without saving, or keep working.</summary>
        public void ResolveQuit(bool quit, bool save)
        {
            if (!quit) { _quitPrompt = false; return; }
            if (save) Save();
            _quitPrompt = false; _quitApproved = true; Application.Quit();
        }
        /// <summary>Switches real screens while retaining all unsaved per-screen experiment deltas.</summary>
        public void OpenScreen(string key)
        {
            FinishLiveEdits();
            string previous = _screen, selection = _selected; int state = _state; var a = _a; bool showingA = _showA;
            try { _screen = key; _selected = null; _state = 0; _a = null; _showA = false; LoadMotion(); Rebuild(); }
            catch { _screen = previous; _selected = selection; _state = state; _a = a; _showA = showingA; throw; }
        }
        /// <summary>Registers a portable font through the same path used by the GUI.</summary>
        public string RegisterFont(string path) { ChooseFont(Workspace.RegisterFont(path)); _message = "폰트 등록: " + Path.GetFileName(path); return _font; }
        /// <summary>Opens the native font picker and registers its selected file on the Unity thread.</summary>
        public void ImportFont() => StudioFileDialog.Open("폰트|*.ttf;*.otf", "실험할 폰트 선택", path => { if (this != null && path != null) Run(() => RegisterFont(path)); }, error => { if (this != null) Report(error); });
        /// <summary>Applies explicitly chosen style values as one undoable edit.</summary>
        public void ApplyStyle(string scope, IDictionary<string, string> values, bool shared = false)
        { ValidateStyleValues(values); Workspace.Edit(shared ? "*" : _screen, scope, values); _showA = false; Rebuild(); }
        private void ValidateStyleValues(IDictionary<string, string> values)
        {
            foreach (var pair in values)
            {
                if (new[] { "size", "outline", "soft", "spacing", "line", "dx", "dy", "width", "height" }.Contains(pair.Key))
                { float value = UiLabWorkspace.Number(pair.Value); if ((pair.Key == "size" || pair.Key == "width" || pair.Key == "height") && value <= 0) throw new FormatException("크기는 0보다 커야 합니다."); if ((pair.Key == "outline" || pair.Key == "soft") && (value < 0 || value > 1)) throw new FormatException("아웃라인 값은 0~1입니다."); }
                if (pair.Key == "color" || pair.Key == "edge" || pair.Key == "tint") UiLabWorkspace.ParseColor(pair.Value);
                if (pair.Key == "font" && pair.Value != "") Workspace.Font(pair.Value);
                if (pair.Key == "auto") bool.Parse(pair.Value);
            }
        }
        /// <summary>Captures A without changing the editable B experiment.</summary>
        public void CaptureA() { FinishLiveEdits(); _a = new XDocument(Workspace.Document); _message = "현재 설정을 A로 보관했습니다. 수정한 B와 전환해 비교하세요."; }
        /// <summary>Displays A or B at the same preview time and mock state.</summary>
        public void ShowA(bool show) { FinishLiveEdits(); if (show && _a == null) throw new InvalidOperationException("먼저 A 보관을 누르세요."); _showA = show; Rebuild(); }
        /// <summary>Persists all screen experiments while keeping game publishing separate.</summary>
        public void Save() { FinishLiveEdits(); Workspace.Save(); _message = "실험 저장 완료 (게임 원본 자동 적용 없음)"; }
        /// <summary>Restores one undo transaction.</summary>
        public void Undo() { FinishLiveEdits(); Workspace.Undo(); LoadMotion(); Rebuild(); RefreshSelectedFields(); }
        /// <summary>Restores one redo transaction.</summary>
        public void Redo() { FinishLiveEdits(); Workspace.Redo(); LoadMotion(); Rebuild(); RefreshSelectedFields(); }
        /// <summary>Changes mock display state without issuing game commands.</summary>
        public void SetState(int state) { FinishLiveEdits(); _state = state; Rebuild(); }
        /// <summary>Selects a stable visual identity and loads its displayed text and geometry.</summary>
        public void Select(string id)
        {
            FinishLiveEdits();
            _selected = id; int index = Array.IndexOf(_template.Ids, id); var rect = _template.Rects[index]; var text = rect.GetComponent<TMP_Text>();
            _width = F(rect.rect.width); _height = F(rect.rect.height);
            var style = Workspace.Document.Root.Elements("Style").FirstOrDefault(x => (string)x.Attribute("screen") == _screen && (string)x.Attribute("scope") == id);
            _dx = (string)style?.Attribute("dx") ?? "0"; _dy = (string)style?.Attribute("dy") ?? "0";
            if (text != null) _font = Workspace.FontKey(text.font);
            if (text != null) { _size = F(text.fontSize); _copy = text.text; _auto = text.enableAutoSizing; _color = "#" + ColorUtility.ToHtmlStringRGBA(text.color); _spacing = F(text.characterSpacing); _line = F(text.lineSpacing); _outline = F(text.fontSharedMaterial.GetFloat("_OutlineWidth")); _edge = "#" + ColorUtility.ToHtmlStringRGBA(text.fontSharedMaterial.GetColor("_OutlineColor")); _soft = F(text.fontSharedMaterial.GetFloat("_OutlineSoftness")); }
            var image = rect.GetComponent<Image>(); if (image != null) _tint = "#" + ColorUtility.ToHtmlStringRGBA(image.color);
        }
        private void Rebuild()
        {
            GameObject candidate = null;
            try
            {
                float progress = Playback != null ? Playback.Progress : 1; bool playing = Playback != null && Playback.Playing;
                var entry = _screens.Single(x => (string)x.Attribute("key") == _screen);
                var prefab = _bundle.LoadAsset<GameObject>((string)entry.Attribute("asset")) ?? throw new IOException("화면이 없습니다: " + _screen);
                candidate = Instantiate(prefab, _frame, false); var template = candidate.GetComponent<UiLabTemplate>();
                // TMP's transient fallback submesh transforms are intentionally not serialized by Unity.
                // Older snapshots included their slots; they are not editable authoring elements.
                int[] persistent = Enumerable.Range(0, template.Rects.Length).Where(i => template.Rects[i] != null).ToArray();
                template.Ids = persistent.Select(i => template.Ids[i]).ToArray(); template.Rects = persistent.Select(i => template.Rects[i]).ToArray();
                foreach (var canvas in candidate.GetComponentsInChildren<Canvas>(true)) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = false; }
                var previousInfo = _template != null && _template.Key == _screen ? _template.GetComponentInChildren<UiLabInfoPreview>(true) : null;
                if (previousInfo != null && previousInfo.Toggle.isOn) candidate.GetComponentInChildren<UiLabInfoPreview>(true)?.SetDetailed(true);
                Workspace.Apply(template, _showA ? _a : null);
                ApplyMockState(template); candidate.SetActive(true); Canvas.ForceUpdateCanvases();
                var playback = candidate.AddComponent<UiLabPlayback>();
                RectTransform target = _selected == null ? template.transform as RectTransform : template.Rects[Array.IndexOf(template.Ids, _selected)];
                playback.Configure(template, target); ConfigureMotion(playback); playback.Seek(progress); playback.Playing = playing;
                if (_template != null) { _template.gameObject.SetActive(false); Destroy(_template.gameObject); }
                _template = template; Playback = playback; candidate = null; _error = "";
                _hasGraphic = template.Rects.Select(x => x.GetComponent<Graphic>() != null).ToArray();
                Workspace.ReleaseUnusedMaterials();
                if (_selected != null && !_template.Ids.Contains(_selected)) _selected = null;
            }
            catch (Exception error) { if (candidate != null) Destroy(candidate); _error = error.Message; throw; }
        }
        private void ApplyMockState(UiLabTemplate template)
        {
            if (_state == 1)
                foreach (var text in template.GetComponentsInChildren<TMP_Text>(true)) if (UiLabWorkspace.Role(text) == "body" || UiLabWorkspace.Role(text) == "title") text.text = UiLabWorkspace.Role(text) == "title" ? "아주 긴 이름의 개구리 전사와 토템" : "아군의 공격력과 공격 속도가 증가합니다. 긴 설명이 여러 줄로 표시될 때 줄바꿈, 여백과 아웃라인이 잘리는지 확인하는 실험 문구입니다.";
            if (_screen == "upgrade")
                foreach (var text in template.GetComponentsInChildren<TMP_Text>(true)) if (text.name.ToLowerInvariant().Contains("cost")) text.text = _state == 2 ? "9,999,999" : _state == 3 ? "MAX" : text.text;
            if (_state == 2 || _state == 3)
                foreach (var button in template.GetComponentsInChildren<Button>(true)) button.interactable = _state != 2;
            foreach (var button in template.GetComponentsInChildren<Button>(true))
            {
                var rect = (RectTransform)button.transform;
                button.onClick.AddListener(() => { Playback.Stop(); Playback.Kind = 3; Playback.Original = false; Playback.Configure(template, rect); Playback.Seek(0); Playback.Playing = true; _message = "모의 버튼 피드백 (게임 기능 호출 없음)"; });
            }
        }
        private void ConfigureMotion(UiLabPlayback playback)
        {
            playback.Kind = _kind; playback.Ease = _ease; playback.Seconds = Mathf.Clamp(UiLabWorkspace.Number(_duration), .01f, 10); playback.Delay = Mathf.Clamp(UiLabWorkspace.Number(_delay), 0, 10);
            playback.StartScale = Mathf.Clamp(UiLabWorkspace.Number(_scale), 0, 2); playback.Distance = UiLabWorkspace.Number(_distance); playback.Stagger = Mathf.Clamp(UiLabWorkspace.Number(_stagger), 0, 2);
            playback.Original = _original; playback.Closing = _closing; playback.Speed = _speed; playback.Loop = _loop;
            if (_showA && _a != null)
            {
                var motion = _a.Root.Elements("Motion").FirstOrDefault(x => (string)x.Attribute("screen") == _screen);
                string Read(string key, string fallback) => (string)motion?.Attribute(key) ?? fallback;
                playback.Kind = int.Parse(Read("kind", "0")); playback.Ease = int.Parse(Read("ease", "3")); playback.Seconds = UiLabWorkspace.Number(Read("seconds", ".35")); playback.Delay = UiLabWorkspace.Number(Read("delay", "0")); playback.StartScale = UiLabWorkspace.Number(Read("scale", ".8")); playback.Distance = UiLabWorkspace.Number(Read("distance", "160")); playback.Stagger = UiLabWorkspace.Number(Read("stagger", ".1"));
            }
        }
        private void LoadMotion()
        {
            var node = Workspace.Motion(_screen); string Read(string name, string fallback) => (string)node?.Attribute(name) ?? fallback;
            _duration = Read("seconds", ".35"); _delay = Read("delay", "0"); _scale = Read("scale", ".8"); _distance = Read("distance", "160"); _stagger = Read("stagger", ".1"); _kind = int.Parse(Read("kind", "0")); _ease = int.Parse(Read("ease", "3"));
        }
        private void SaveMotion()
        {
            ConfigureMotion(Playback); Workspace.SetMotion(_screen, new Dictionary<string, string> { ["seconds"] = F(Playback.Seconds), ["delay"] = F(Playback.Delay), ["scale"] = F(Playback.StartScale), ["distance"] = F(Playback.Distance), ["stagger"] = F(Playback.Stagger), ["kind"] = _kind.ToString(), ["ease"] = _ease.ToString() }); Playback.Stop();
        }
        private void Update()
        {
            if (_frame == null) return;
            ProcessLiveEdits();
            var viewport = PreviewBounds;
            float zoom = Mathf.Min(viewport.width / 1080, viewport.height / _ratioHeight) * .93f * _previewZoom;
            _previewClip.anchorMin = _previewClip.anchorMax = Vector2.zero; _previewClip.pivot = Vector2.zero;
            _previewClip.anchoredPosition = new Vector2(viewport.x, Screen.height - viewport.yMax); _previewClip.sizeDelta = viewport.size;
            _frame.anchorMin = _frame.anchorMax = new Vector2(.5f, .5f); _frame.sizeDelta = new Vector2(1080, _ratioHeight); _frame.localScale = Vector3.one * zoom;
            _frame.anchoredPosition = _previewPan;
            _backgroundImage.color = new Color(_background, _background, _background);
            HandlePreviewWheel(Input.mousePosition, Input.mouseScrollDelta.y);
            if (Input.GetMouseButtonDown(2) && viewport.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))) { _panningPreview = true; _lastPanPoint = Input.mousePosition; }
            if (_panningPreview && Input.GetMouseButton(2)) { PanPreview((Vector2)Input.mousePosition - _lastPanPoint); _lastPanPoint = Input.mousePosition; }
            if (Input.GetMouseButtonUp(2)) _panningPreview = false;
            if (Input.GetMouseButtonDown(0) && !_quitPrompt && !StudioFileDialog.Busy)
            {
                Vector2 point = Input.mousePosition;
                if (viewport.Contains(new Vector2(point.x, Screen.height - point.y)))
                    for (int i = _template.Rects.Length - 1; i >= 0; i--) if (_template.Rects[i].gameObject.activeInHierarchy && _hasGraphic[i] && RectTransformUtility.RectangleContainsScreenPoint(_template.Rects[i], point, null)) { Select(_template.Ids[i]); break; }
            }
        }
        private void CheckGlyphs()
        {
            var missing = new HashSet<uint>();
            foreach (var text in _template.GetComponentsInChildren<TMP_Text>(true))
            { if (text.font == null) continue; text.font.TryAddCharacters(text.text, out string absent); if (absent != null) foreach (char c in absent) if (!char.IsWhiteSpace(c)) missing.Add(c); }
            _message = missing.Count == 0 ? "현재 화면의 누락 글자 없음" : "대체 폰트가 필요할 수 있는 글자: " + string.Join(" ", missing.Select(x => "U+" + x.ToString("X4")));
        }
        /// <summary>Exports only the preview UI, without tool panels.</summary>
        public void Capture()
        {
            FinishLiveEdits();
            string folder = Path.Combine(Workspace.DirectoryPath, "Captures"); Directory.CreateDirectory(folder);
            var canvas = _template.GetComponentInParent<Canvas>(); int old = canvas.targetDisplay;
            // Capture through an isolated camera so IMGUI tool panels are never included.
            var cameraObject = new GameObject("Lab capture camera", typeof(Camera)); var camera = cameraObject.GetComponent<Camera>();
            var texture = new RenderTexture(1080, (int)_ratioHeight, 24); var image = new Texture2D(1080, (int)_ratioHeight, TextureFormat.RGB24, false);
            var oldMode = _canvas.GetComponent<Canvas>().renderMode; var oldCamera = _canvas.GetComponent<Canvas>().worldCamera;
            var oldActive = RenderTexture.active; var oldScale = _frame.localScale; var oldPosition = _frame.anchoredPosition;
            bool oldMask = _previewMask.enabled;
            try
            {
                camera.orthographic = true; camera.orthographicSize = _ratioHeight / 2; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(_background, _background, _background); camera.transform.position = new Vector3(0, 0, -100); camera.farClipPlane = 1000;
                var labCanvas = _canvas.GetComponent<Canvas>(); labCanvas.renderMode = RenderMode.WorldSpace; _canvas.GetComponent<RectTransform>().position = Vector3.zero;
                _frame.localScale = Vector3.one; _frame.anchoredPosition = Vector2.zero; camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                _previewMask.enabled = false; Canvas.ForceUpdateCanvases(); camera.transform.position = _frame.position + new Vector3(0, 0, -100); camera.Render();
                image.ReadPixels(new Rect(0, 0, 1080, _ratioHeight), 0, 0); image.Apply(); string path = Path.Combine(folder, _screen + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"); File.WriteAllBytes(path, image.EncodeToPNG()); _message = "UI 캡처 저장: " + path;
            }
            finally { RenderTexture.active = oldActive; _previewMask.enabled = oldMask; _canvas.GetComponent<Canvas>().renderMode = oldMode; _canvas.GetComponent<Canvas>().worldCamera = oldCamera; _frame.localScale = oldScale; _frame.anchoredPosition = oldPosition; Destroy(texture); Destroy(image); Destroy(cameraObject); }
        }
        private void Field(string label, ref string value) { GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(110)); value = GUILayout.TextField(value, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)); RecordInspectorControl(label); GUILayout.EndHorizontal(); }
        private void Button(string label, Action action, GUIStyle style = null, params GUILayoutOption[] options) { bool clicked = GUILayout.Button(label, style ?? GUI.skin.button, options); RecordInspectorControl(label); if (clicked) Run(action); }
        private void Run(Action action) { try { action(); _error = ""; } catch (Exception error) { Report(error); } }
        private void Report(Exception error) { _error = error.Message; Debug.LogWarning("UI_LAB: " + error.Message); }
        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private void OnDestroy() { Application.wantsToQuit -= CheckQuit; _theme?.Dispose(); _fontSpecimen?.Dispose(); if (_canvas != null) Destroy(_canvas); Workspace?.Dispose(); if (_bundle != null) _bundle.Unload(true); }
    }
}
