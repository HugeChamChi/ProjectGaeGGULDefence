using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Portable visual authoring workspace over the exact production UGUI renderer and XML originals.</summary>
    public sealed class StudioWindow : MonoBehaviour
    {
        private const float LeftWidth = 285, RightWidth = 365, HeaderHeight = 108, FooterHeight = 108;
        [SerializeField] private AuthoringAssets _assets;
        [SerializeField] private Font _editorFont;
        private RectTransform _viewport, _design;
        private AuthoredUiView _view;
        private AssetBundle _nativeBundle;
        private string _nativeBundleHash;
        private NativePrefabTemplate _nativeTemplate;
        private AuthoringResources _previewResources;
        private string _artRootText = "";
        private ResolvedUiDocument _resolved;
        private StudioBindings _bindings;
        private Vector2 _treeScroll, _propertyScroll, _pickerScroll;
        private string _selected, _error = "", _message = "실제 설정창을 선택하고, 끌어서 이동하거나 오른쪽 속성을 수정하세요.";
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _propertyBaseline = new Dictionary<string, string>();
        private readonly HashSet<string> _collapsed = new HashSet<string>(), _locked = new HashSet<string>(), _hidden = new HashSet<string>();
        private bool _pendingProperties, _testing, _ingame, _fit = true;
        private float _zoom = .38f, _designHeight = 1920, _nextRecovery;
        private Vector2 _pan;
        private bool _panning;
        private Vector2 _panStart, _panPointer;
        private float _safeTop, _safeBottom;
        private string _compareMemory, _compareDisk;
        private Vector2 _compareScroll;
        private string _pickerProperty, _pickerFilter = "", _newName = "", _dialog, _resourceKey;
        private Action _confirmed;
        private Vector2 _dragStart, _startPosition, _startSize;
        private bool _dragging, _resizing;
        private Dictionary<string, string> _gesture;
        private RectTransform _dragRect;
        private int _lastWidth, _lastHeight;

        /// <summary>Current original documents; tests and developer tooling can inspect the same model used by GUI actions.</summary>
        public AuthoringWorkspace Workspace { get; private set; }
        /// <summary>Current rendered real screen.</summary>
        public AuthoredUiView View => _view;
        /// <summary>Mock services used only in Studio test mode.</summary>
        public StudioBindings Bindings => _bindings;
        /// <summary>Selected stable element identity.</summary>
        public string SelectedId => _selected;
        /// <summary>Current validation/loading problem.</summary>
        public string Error => _error;
        /// <summary>Advisory diagnostics for the last successful publication.</summary>
        public IReadOnlyList<string> PublishDiagnostics { get; private set; } = Array.Empty<string>();
        /// <summary>Whether UGUI controls receive input instead of selection/dragging.</summary>
        public bool Testing => _testing;
        /// <summary>Whether this kit edits a connected Unity prefab original.</summary>
        public bool NativeMode => Workspace != null && NativePrefabContract.Read(Workspace) != null;

        /// <summary>Build tooling supplies exact preview assets and a bundled Korean editor font.</summary>
        public void Configure(AuthoringAssets assets, Font editorFont) { _assets = assets; _editorFont = editorFont; }

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            _artRootText = PersonalArtSettings.ReadRoot();
            var canvas = new GameObject("StudioCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.transform.SetParent(transform, false);
            _viewport = NewRect("Viewport", canvas.transform); _viewport.gameObject.AddComponent<RectMask2D>();
            _design = NewRect("Design1080", _viewport); _design.anchorMin = _design.anchorMax = _design.pivot = new Vector2(.5f, .5f);
            new GameObject("StudioEvents", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Authoring"));
            string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--kit") directory = Path.GetFullPath(args[i + 1]);
            try { OpenKit(directory); } catch (Exception error) { _error = error.Message; Debug.LogError("STUDIO_START: " + error); }
            Application.wantsToQuit += CheckQuit;
        }
        private void Update()
        {
            if (_viewport == null) return;
            if (_lastWidth != Screen.width || _lastHeight != Screen.height) { _lastWidth = Screen.width; _lastHeight = Screen.height; UpdateViewport(); }
            if (Workspace?.IsDirty == true && Time.unscaledTime > _nextRecovery)
            { _nextRecovery = Time.unscaledTime + 15; try { Workspace.SaveRecovery(); } catch (Exception error) { _message = "자동 복구본 저장 실패: " + error.Message; } }
            if (_view != null)
            {
                _view.Root.SetActive(!_testing || !_bindings.Closed);
                _view.SetInput(_testing && _dialog == null && _pickerProperty == null);
                foreach (string id in _hidden) if (_view.Elements.TryGetValue(id, out var rect)) rect.gameObject.SetActive(false);
            }
        }
        /// <summary>Opens the complete designer kit; existing unsaved originals remain in memory if loading fails.</summary>
        public void OpenKit(string directory)
        {
            var candidate = AuthoringWorkspace.Open(directory);
            if (candidate.IsReadOnly)
            {
                _view?.Dispose(); _view = null; Workspace = candidate; _bindings = null; _selected = null;
                _resolved = ResolvedUiDocument.ReadOnlySource(candidate.Documents[candidate.CurrentPath].Root, candidate.CurrentPath);
                _error = "상위 스키마 · 읽기 전용입니다. 원본은 보존되며 새 키트 실행기가 필요합니다."; return;
            }
            var resolved = UiComposer.Resolve(candidate, validate: false);
            var bindings = new StudioBindings(candidate.Catalog.Root);
            AuthoredUiView view = null; string problem = "";
            try { UiComposer.Resolve(candidate); view = CreatePreview(resolved, candidate, bindings); }
            catch (Exception error) { problem = error.Message; Debug.LogWarning("STUDIO_LOAD: " + error); }
            Workspace = candidate; _resolved = resolved; _bindings = bindings;
            if (view != null) { var old = _view; _view = view; view.Root.SetActive(true); view.SetInput(_testing); old?.Dispose(); }
            _selected = null; _properties.Clear(); _error = problem; _resourceKey = null; _collapsed.Clear(); _locked.Clear(); _hidden.Clear(); UpdateViewport();
            if (File.Exists(AuthoringWorkspace.SafePath(directory, ".studio-recovery.xml"))) _message = "자동 복구본이 있습니다. 상단 [복구본]에서 복원할 수 있습니다.";
            else if (NativeMode) _message = "원본 프리팹 편집: 위치·크기·그림·글씨를 수정하고 기존 열기/닫기 연출을 확인합니다. 요소 추가·삭제와 새 기능 연결은 Unity에서 합니다.";
        }
        /// <summary>Rebuilds a hidden candidate and retains the last valid screen on validation/resource failures.</summary>
        public void Rebuild()
        {
            if (Workspace.IsReadOnly)
            {
                _resolved = ResolvedUiDocument.ReadOnlySource(Workspace.Documents[Workspace.CurrentPath].Root, Workspace.CurrentPath);
                _error = "상위 스키마 · 읽기 전용입니다. 원본 XML을 확인할 수 있습니다."; return;
            }
            try
            {
                _resolved = UiComposer.Resolve(Workspace, validate: false);
                var resolved = UiComposer.Resolve(Workspace);
                var candidate = CreatePreview(resolved, Workspace, _bindings);
                var old = _view; _view = candidate; candidate.Root.SetActive(true); candidate.SetInput(_testing); old?.Dispose(); _error = "";
            }
            catch (Exception error) { _error = error.Message; }
            if (_selected != null) LoadProperties();
        }
        /// <summary>Selects an element independently of its current name or parent.</summary>
        public void Select(string id) { _selected = id; _resourceKey = null; _propertyScroll = Vector2.zero; LoadProperties(); }
        /// <summary>Applies a property as a source edit or a prefab override, in one undo action.</summary>
        public void Change(string id, string property, string value) { Workspace.Edit(() => Workspace.SetProperty(id, property, value)); Rebuild(); }
        /// <summary>Persists memory documents with optimistic file-conflict detection.</summary>
        public void Save() { ApplyProperties(); Workspace.Save(); _message = "저장 완료. 같은 키트를 재열면 배치와 프리팹 참조가 유지됩니다."; }
        /// <summary>Validates and publishes through the bundled C# implementation.</summary>
        public string Publish()
        {
            ApplyProperties(); string version = KitPublisher.Publish(Workspace, out var diagnostics, PersonalArtSettings.ReadRoot()); PublishDiagnostics = diagnostics;
            _message = "게임용 게시 완료. 게시 검사 " + diagnostics.Count + "건: " + version;
            _dialog = "게시 검사\n" + string.Join("\n\n", diagnostics); _confirmed = null; return version;
        }
        /// <summary>Changes screen-owned Canvas isolation as one undoable source edit.</summary>
        public void SetOwnCanvas(bool own)
        {
            if (Workspace.Documents[Workspace.CurrentPath].Root.Name != "Screen") throw new InvalidOperationException("Canvas isolation belongs to a screen");
            Workspace.Edit(() => Workspace.Documents[Workspace.CurrentPath].Root.SetAttributeValue("canvas", own ? "own" : "inherit")); Rebuild();
        }
        /// <summary>Saves only the per-user root preference and reloads images without discarding XML edits.</summary>
        public void SetArtRoot(string root) { PersonalArtSettings.SaveRoot(root); ReloadArt(); }
        /// <summary>Reloads current source images and rereads the downloader's personal preference.</summary>
        public void ReloadArt() { _artRootText = PersonalArtSettings.ReadRoot(); Rebuild(); }
        /// <summary>Reports the source actually used by the last valid preview.</summary>
        public string ImageSource(string key) => _previewResources != null && _previewResources.Sources.TryGetValue(key, out var source) ? source : "미리보기에 사용하지 않은 그림";
        private AuthoredUiView CreatePreview(ResolvedUiDocument resolved, AuthoringWorkspace workspace, StudioBindings bindings)
        {
            var resources = new AuthoringResources(_assets, workspace, PersonalArtSettings.ReadRoot());
            AuthoredUiView candidate;
            var native = NativePrefabContract.Read(workspace);
            if (native != null && workspace.CurrentPath == "Screens/UI3030.xml")
            {
                NativePrefabContract.Validate(workspace, resolved);
                string hash = AuthoringWorkspace.Required(native, "bundleHash");
                if (_nativeBundleHash != hash)
                {
                    string path = AuthoringWorkspace.SafePath(workspace.DirectoryPath, AuthoringWorkspace.Required(native, "bundle"));
                    if (AuthoringWorkspace.Hash(File.ReadAllBytes(path)) != hash) throw new FormatException("원본 프리팹 패키지 해시가 다릅니다.");
                    var bundle = AssetBundle.LoadFromFile(path) ?? throw new FormatException("원본 프리팹 패키지를 열 수 없습니다.");
                    var prefab = bundle.LoadAllAssets<GameObject>().Single(x => x.GetComponent<NativePrefabTemplate>() != null);
                    _nativeBundle?.Unload(false); _nativeBundle = bundle; _nativeBundleHash = hash; _nativeTemplate = prefab.GetComponent<NativePrefabTemplate>();
                }
                candidate = AuthoredUiView.FromNative(_nativeTemplate, resolved, _design, resources, bindings);
            }
            else candidate = AuthoredUiView.Create(resolved, _design, resources, bindings);
            _previewResources = resources;
            return candidate;
        }
        /// <summary>Plays the original popup animation against the edited resting pose.</summary>
        public void PreviewMotion(bool opening)
        {
            ApplyProperties();
            var motion = _view?.Root.GetComponent<NativePrefabMotion>();
            if (motion == null) { _message = "원본 프리팹 애니메이션이 포함된 키트를 열어 주세요."; return; }
            _bindings?.Reopen(); motion.Play(opening);
        }
        /// <summary>Undoes one GUI/source transaction.</summary>
        public void Undo() { Workspace.Undo(); Rebuild(); }
        /// <summary>Redoes one GUI/source transaction.</summary>
        public void Redo() { Workspace.Redo(); Rebuild(); }
        /// <summary>Switches pointer input between authoring and actual control testing.</summary>
        public void SetTesting(bool value) { _testing = value; _view?.SetInput(value); _message = value ? "테스트 모드: 기존 키 계약의 모의 서비스로 조작합니다." : "편집 모드: 클릭=선택, 드래그=이동, 오른쪽 아래 손잡이=크기."; }

        private void UpdateViewport()
        {
            _viewport.anchorMin = _viewport.anchorMax = Vector2.zero; _viewport.pivot = Vector2.zero;
            _viewport.anchoredPosition = new Vector2(LeftWidth + 10, FooterHeight); _viewport.sizeDelta = new Vector2(Mathf.Max(100, Screen.width - LeftWidth - RightWidth - 20), Mathf.Max(100, Screen.height - HeaderHeight - FooterHeight));
            if (_fit) _zoom = Mathf.Min(_viewport.sizeDelta.x / 1080, _viewport.sizeDelta.y / _designHeight) * .92f;
            _design.sizeDelta = new Vector2(1080, _designHeight - _safeTop - _safeBottom); _design.localScale = Vector3.one * _zoom; _design.anchoredPosition = _pan + new Vector2(0, (_safeBottom - _safeTop) * .5f * _zoom);
        }
        private void OnGUI()
        {
            GUI.skin.font = _editorFont; GUI.skin.label.fontSize = 15; GUI.skin.button.fontSize = 15; GUI.skin.textField.fontSize = 15; GUI.skin.box.fontSize = 15;
            GUI.skin.label.wordWrap = false;
            DrawHeader();
            if (Workspace == null) { GUI.Label(new Rect(30, 150, Screen.width - 60, 250), "키트를 열어 주세요.\n" + _error); DrawDialog(); return; }
            GUI.Box(new Rect(0, HeaderHeight, LeftWidth, Screen.height - HeaderHeight - FooterHeight), "");
            GUI.Box(new Rect(Screen.width - RightWidth, HeaderHeight, RightWidth, Screen.height - HeaderHeight - FooterHeight), "");
            // Canvas gestures get first refusal. Scroll views can otherwise consume their mouse-down event.
            if (!_testing && _dialog == null && _pickerProperty == null && _view?.Root.GetComponent<NativePrefabMotion>()?.IsPlaying != true) HandleCanvasInput(Event.current);
            DrawHierarchy(); DrawInspector(); DrawFooter();
            if (!_testing && _dialog == null) DrawSelection();
            HandleKeys(Event.current); DrawPicker(); DrawDialog();
        }
        private void DrawHeader()
        {
            GUI.Box(new Rect(0, 0, Screen.width, HeaderHeight), "");
            GUILayout.BeginArea(new Rect(10, 8, Screen.width - 20, 94));
            GUILayout.BeginHorizontal(); GUILayout.Label((NativeMode ? "UI 제작실 | 원본 프리팹" : "GaeGGUL  UI 제작실") + (Workspace?.IsDirty == true ? "  ● 미저장" : ""), GUILayout.Width(245));
            Button("키트 열기", () => AskDiscard(() => { string path = StudioFileDialog.Open("제작 키트|kit.xml", "제작 키트 열기"); if (path != null) OpenKit(Path.GetDirectoryName(path)); }), 100);
            Button("저장", Save, 72); Button("다른 이름", SaveCopy, 100); Button("실행 취소", Undo, 100); Button("다시 실행", Redo, 100);
            Button("복구본", () => { Workspace.Recover(); Rebuild(); }, 80); Button("게시 / 게임 전달", () => Publish(), 150);
            if (GUILayout.Button(_testing ? "편집으로" : "▶ 테스트", GUILayout.Width(100))) SetTesting(!_testing);
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label(Workspace?.CurrentPath ?? "", GUILayout.Width(320));
            if (GUILayout.Button(_ingame ? "인게임 상태" : "로비 상태", GUILayout.Width(115))) { _ingame = !_ingame; _bindings?.Reset(_ingame); }
            if (GUILayout.Button("모의 값 초기화", GUILayout.Width(125))) _bindings?.Reset(_ingame);
            if (NativeMode)
            {
                if (GUILayout.Button("열기 연출", GUILayout.Width(90))) PreviewMotion(true);
                if (GUILayout.Button("닫기 연출", GUILayout.Width(90))) PreviewMotion(false);
            }
            else if (GUILayout.Button("화면 다시 열기", GUILayout.Width(125))) _bindings?.Reopen();
            if (GUILayout.Button("1080×1920", GUILayout.Width(110))) { _designHeight = 1920; _fit = true; UpdateViewport(); }
            if (GUILayout.Button("1080×2340", GUILayout.Width(110))) { _designHeight = 2340; _fit = true; UpdateViewport(); }
            if (GUILayout.Button(_safeTop == 0 ? "안전영역: 없음" : "안전영역: 상80 하60", GUILayout.Width(155))) { _safeTop = _safeTop == 0 ? 80 : 0; _safeBottom = _safeTop == 0 ? 0 : 60; UpdateViewport(); }
            GUILayout.Label("확대", GUILayout.Width(36)); float zoom = GUILayout.HorizontalSlider(_zoom, .1f, 1.2f, GUILayout.Width(130));
            if (!Mathf.Approximately(zoom, _zoom)) { _zoom = zoom; _fit = false; UpdateViewport(); }
            if (GUILayout.Button("맞춤", GUILayout.Width(60))) { _fit = true; _pan = Vector2.zero; UpdateViewport(); }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        private void DrawHierarchy()
        {
            GUILayout.BeginArea(new Rect(10, HeaderHeight + 8, LeftWidth - 20, Screen.height - HeaderHeight - FooterHeight - 16));
            GUILayout.Label("화면 / 공용 프리팹");
            GUILayout.BeginHorizontal(); Button("새 화면", () => NameDialog("새 화면 키", "Screen", CreateScreen), 80); Button("프리팹 추출", () => NameDialog("새 프리팹 키", "component", key => { PrefabEditing.Extract(Workspace, _selected, key); Rebuild(); }), 100); GUILayout.EndHorizontal();
            foreach (var doc in Workspace.Documents.Where(x => x.Value.Root.Name == "Screen" || x.Value.Root.Name == "Prefab" || Workspace.IsReadOnly && (x.Key.StartsWith("Screens/") || x.Key.StartsWith("Prefabs/"))))
                if (GUILayout.Button((doc.Key == Workspace.CurrentPath ? "● " : "") + Path.GetFileNameWithoutExtension(doc.Key))) { Workspace.CurrentPath = doc.Key; _selected = null; Rebuild(); }
            if (!Workspace.IsReadOnly && Workspace.Documents[Workspace.CurrentPath].Root.Name == "Screen")
            {
                bool own = (string)Workspace.Documents[Workspace.CurrentPath].Root.Attribute("canvas") == "own";
                bool next = GUILayout.Toggle(own, "화면 전용 Canvas"); if (next != own) Try(() => SetOwnCanvas(next));
            }
            GUILayout.Space(8); GUILayout.BeginHorizontal();
            Button("추가", () => _pickerProperty = "__component", 50); Button("복제", Duplicate, 50); Button("삭제", Delete, 50); Button("프리팹 배치", () => _pickerProperty = "__prefab", 90); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); Button("위", () => Reorder(-1), 40); Button("아래", () => Reorder(1), 40); Button("부모 변경", () => _pickerProperty = "__parent", 90); Button("정렬", () => _pickerProperty = "__align", 60); GUILayout.EndHorizontal();
            _treeScroll = GUILayout.BeginScrollView(_treeScroll);
            if (_resolved != null) foreach (var node in _resolved.Root.Elements("Element")) DrawNode(node, 0);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawNode(XElement node, int level)
        {
            string id = (string)node.Attribute("id"); var background = GUI.backgroundColor; if (id == _selected) GUI.backgroundColor = new Color(.5f, .75f, 1);
            GUILayout.BeginHorizontal(); GUILayout.Space(Mathf.Min(level * 12, 96));
            if (GUILayout.Button(_collapsed.Contains(id) ? "+" : "−", GUILayout.Width(22))) Toggle(_collapsed, id);
            if (GUILayout.Button(((string)node.Attribute("name") ?? id) + (id.Contains("/") ? " ◇" : ""), GUILayout.MinWidth(60))) Select(id);
            if (GUILayout.Button(_locked.Contains(id) ? "잠" : "L", GUILayout.Width(25))) Toggle(_locked, id);
            if (GUILayout.Button(_hidden.Contains(id) ? "숨" : "V", GUILayout.Width(25))) { Toggle(_hidden, id); if (_view?.Elements.TryGetValue(id, out var rect) == true) rect.gameObject.SetActive(!_hidden.Contains(id)); }
            GUILayout.EndHorizontal(); GUI.backgroundColor = background;
            if (!_collapsed.Contains(id)) foreach (var child in node.Elements("Element")) DrawNode(child, level + 1);
        }
        private void DrawInspector()
        {
            GUILayout.BeginArea(new Rect(Screen.width - RightWidth + 12, HeaderHeight + 8, RightWidth - 24, Screen.height - HeaderHeight - FooterHeight - 16));
            GUILayout.Label("아트 루트 폴더 (이 PC 전용)");
            _artRootText = GUILayout.TextField(_artRootText);
            GUILayout.BeginHorizontal();
            Button("찾기", () => { string root = StudioFileDialog.Folder(); if (root != null) SetArtRoot(root); }, 60);
            Button("적용", () => SetArtRoot(_artRootText), 60);
            Button("해제", () => SetArtRoot(""), 60);
            Button("그림 새로고침", ReloadArt, 125);
            GUILayout.EndHorizontal();
            if (_previewResources != null) GUILayout.Label("현재 그림: 원본 " + _previewResources.Sources.Values.Count(x => x.StartsWith("아트 원본:")) + " / 복사본 " + _previewResources.Sources.Values.Count(x => x.StartsWith("키트 복사본:")));
            GUILayout.Label(_resourceKey == null ? "선택 요소 속성" : "리소스 메타 / 9-slice");
            if (Workspace.IsReadOnly)
            {
                GUILayout.Label("읽기 전용 · 모르는 속성을 삭제하거나 마이그레이션하지 않습니다.");
                _propertyScroll = GUILayout.BeginScrollView(_propertyScroll);
                string original = Workspace.Documents[Workspace.CurrentPath].ToString();
                GUILayout.TextArea(original.Length > 65536 ? original.Substring(0, 65536) + "\n[표시 생략 — 원본 보존]" : original);
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            GUILayout.BeginHorizontal(); Button("그림 가져오기", ImportImage, 130); Button("리소스 목록", () => _pickerProperty = "__resource", 120); GUILayout.EndHorizontal();
            if (_resourceKey == null && _selected == null) { GUILayout.Label("캔버스 또는 계층에서 요소를 선택하세요.\n버튼·글자·그림은 직접 이동할 수 있습니다."); GUILayout.EndArea(); return; }
            GUILayout.Label(_resourceKey ?? _selected, GUILayout.Height(38));
            string selectedImage = _resourceKey ?? (_properties.TryGetValue("Image.sprite", out var imageKey) ? imageKey : null);
            if (!string.IsNullOrEmpty(selectedImage)) GUILayout.TextArea(ImageSource(selectedImage), GUILayout.Height(58));
            if (_resourceKey != null) Button("리소스 키 바꾸기", () => NameDialog("새 그림 키 (사용처 함께 변경)", _resourceKey, newKey =>
            {
                string previous = _resourceKey;
                _dialog = "그림 키 " + previous + " → " + newKey + "\n원본·오버라이드·공개 그림 입력의 사용처를 하나의 Undo 작업으로 바꿉니다. 파일명/GUID는 유지합니다.";
                _confirmed = () => { Workspace.RenameImage(previous, newKey); _resourceKey = newKey; Rebuild(); SelectResource(newKey); };
            }), 180);
            if (_selected != null && _resourceKey == null && _resolved?.SourceFiles.TryGetValue(_selected, out string source) == true) GUILayout.Label("원본: " + source);
            if (_selected?.Contains("/") == true && _resourceKey == null)
            {
                GUILayout.BeginHorizontal(); Button("원본 열기", EditPrefabSource, 95); Button("원본에 적용", () => ConfirmApply(), 110); Button("연결 해제", () => { _selected = PrefabEditing.Detach(Workspace, _resolved, _selected); Rebuild(); }, 100); GUILayout.EndHorizontal();
            }
            if (_pendingProperties) GUILayout.Label("입력 중 — [속성 적용]으로 반영합니다.");
            Button("속성 적용", ApplyProperties, 130);
            _propertyScroll = GUILayout.BeginScrollView(_propertyScroll);
            foreach (string key in _properties.Keys.ToArray())
            {
                string origin = null; _resolved?.Origins.TryGetValue((_selected ?? "") + "|" + key, out origin);
                GUILayout.BeginHorizontal(); GUILayout.Label(new GUIContent(Caption(key), key + " · " + (origin ?? "source")), GUILayout.Width(115));
                string previous = _properties[key]; string next;
                if (previous == "true" || previous == "false") next = GUILayout.Toggle(previous == "true", "", GUILayout.Width(45)) ? "true" : "false";
                else next = GUILayout.TextField(previous ?? "", GUILayout.MinWidth(80));
                if (next != previous) { _properties[key] = next; _pendingProperties = true; }
                if (IsKeyProperty(key) && GUILayout.Button("…", GUILayout.Width(25))) { _pickerProperty = key; _pickerFilter = ""; }
                if (origin?.Contains("override") == true && GUILayout.Button("↶", GUILayout.Width(25))) { PrefabEditing.Revert(Workspace, _selected, key); Rebuild(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawFooter()
        {
            GUILayout.BeginArea(new Rect(12, Screen.height - FooterHeight + 6, Screen.width - 24, FooterHeight - 10));
            GUI.contentColor = _error.Length > 0 ? new Color(1, .55f, .45f) : Color.white;
            GUILayout.Label(_error.Length > 0 ? "게시 불가 · 마지막 정상 화면 유지: " + _error : _message);
            GUI.contentColor = Color.white;
            if (_error.Length > 0 && GUILayout.Button("문제 요소 선택", GUILayout.Width(140)))
            { var id = _resolved?.SourceFiles.Keys.FirstOrDefault(x => _error.Contains(x)); if (id != null) Select(id); }
            GUILayout.Label("모의 명령: " + (_bindings == null ? "" : string.Join("  |  ", _bindings.Commands.TakeLast(3))));
            GUILayout.Label("원본: " + Workspace?.DirectoryPath + "    Undo " + Workspace?.UndoCount + " / Redo " + Workspace?.RedoCount);
            GUILayout.EndArea();
        }
        private void DrawSelection()
        {
            if (_selected == null || _view?.Elements.TryGetValue(_selected, out var rect) != true || !rect.gameObject.activeInHierarchy) return;
            Rect bounds = Bounds(rect); var old = GUI.color; GUI.color = new Color(.25f, .85f, 1);
            GUI.DrawTexture(new Rect(bounds.x, bounds.y, bounds.width, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(bounds.x, bounds.yMax - 2, bounds.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bounds.x, bounds.y, 2, bounds.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(bounds.xMax - 2, bounds.y, 2, bounds.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bounds.xMax - 5, bounds.yMax - 5, 10, 10), Texture2D.whiteTexture); GUI.color = old;
        }
        private void HandleCanvasInput(Event input)
        {
            Rect area = new Rect(LeftWidth + 10, HeaderHeight, Screen.width - LeftWidth - RightWidth - 20, Screen.height - HeaderHeight - FooterHeight);
            if (_testing || _view == null) return;
            if (input.type == EventType.MouseDown && input.button == 2 && area.Contains(input.mousePosition))
            { _panning = true; _panStart = _pan; _panPointer = input.mousePosition; input.Use(); return; }
            if (input.type == EventType.MouseDrag && _panning)
            { Vector2 delta = input.mousePosition - _panPointer; _pan = _panStart + new Vector2(delta.x, -delta.y); UpdateViewport(); input.Use(); return; }
            if (input.type == EventType.MouseUp && _panning) { _panning = false; input.Use(); return; }
            if (input.type == EventType.ScrollWheel && area.Contains(input.mousePosition))
            { _zoom = Mathf.Clamp(_zoom * (1 - input.delta.y * .05f), .1f, 1.2f); _fit = false; UpdateViewport(); input.Use(); return; }
            if (input.type == EventType.MouseDown && input.button == 0 && area.Contains(input.mousePosition))
            {
                string id = _view.Elements.Reverse().Where(x => x.Value.gameObject.activeInHierarchy && (x.Value.GetComponent<Graphic>() != null || x.Value.GetComponent<Selectable>() != null))
                    .FirstOrDefault(x => Bounds(x.Value).Contains(input.mousePosition)).Key;
                if (id == null) return;
                var hit = _view.Elements[id]; var control = hit.GetComponentInParent<Selectable>();
                if (control != null && _view.Elements.Any(x => x.Value == control.transform)) id = _view.Elements.First(x => x.Value == control.transform).Key;
                if (_locked.Contains(id)) return;
                if (_selected != id) Select(id);
                _dragRect = _view.Elements[id];
                if (_dragRect.parent.GetComponent<LayoutGroup>() != null) { _message = "레이아웃 그룹 자식입니다. 부모 Layout 속성을 편집하거나 그룹을 해제하세요."; return; }
                _resizing = Vector2.Distance(input.mousePosition, new Vector2(Bounds(_dragRect).xMax, Bounds(_dragRect).yMax)) < 14;
                _gesture = Workspace.Snapshot(); _startPosition = _dragRect.anchoredPosition; _startSize = _dragRect.sizeDelta;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragRect.parent, new Vector2(input.mousePosition.x, Screen.height - input.mousePosition.y), null, out _dragStart);
                _dragging = true; input.Use();
            }
            else if (input.type == EventType.MouseDrag && _dragging && input.button == 0)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragRect.parent, new Vector2(input.mousePosition.x, Screen.height - input.mousePosition.y), null, out Vector2 current);
                Vector2 delta = current - _dragStart;
                if (_resizing)
                {
                    _dragRect.sizeDelta = _startSize + new Vector2(delta.x, -delta.y);
                    Workspace.SetProperty(_selected, "width", F(_dragRect.sizeDelta.x)); Workspace.SetProperty(_selected, "height", F(_dragRect.sizeDelta.y));
                }
                else { _dragRect.anchoredPosition = _startPosition + delta; Workspace.SetProperty(_selected, "x", F(_dragRect.anchoredPosition.x)); Workspace.SetProperty(_selected, "y", F(_dragRect.anchoredPosition.y)); }
                input.Use();
            }
            else if (input.type == EventType.MouseUp && _dragging)
            { _dragging = false; Workspace.CommitGesture(_gesture); _gesture = null; Rebuild(); input.Use(); }
        }
        private void HandleKeys(Event input)
        {
            if (Workspace == null || _dialog != null || input.type != EventType.KeyDown || !input.control) return;
            if (input.keyCode == KeyCode.S) { Try(Save); input.Use(); }
            else if (input.keyCode == KeyCode.Z) { Try(Undo); input.Use(); }
            else if (input.keyCode == KeyCode.Y) { Try(Redo); input.Use(); }
            else if (input.keyCode == KeyCode.D) { Try(Duplicate); input.Use(); }
        }
        private void LoadProperties()
        {
            _properties.Clear(); _pendingProperties = false;
            var node = _resolved?.Root.Descendants("Element").FirstOrDefault(x => (string)x.Attribute("id") == _selected);
            if (node == null) return;
            foreach (string key in new[] { "name", "key", "x", "y", "width", "height" }) if (node.Attribute(key) != null) _properties[key] = (string)node.Attribute(key);
            foreach (var component in node.Elements().Where(x => x.Name != "Element")) foreach (var attribute in component.Attributes()) _properties[component.Name + "." + attribute.Name] = attribute.Value;
            foreach (var attribute in node.Attributes().Where(x => x.Name != "id" && !_properties.ContainsKey(x.Name.LocalName))) _properties[attribute.Name.LocalName] = attribute.Value;
            _propertyBaseline.Clear(); foreach (var pair in _properties) _propertyBaseline[pair.Key] = pair.Value;
        }
        private void ApplyProperties()
        {
            if (!_pendingProperties) return;
            var values = new Dictionary<string, string>(_properties);
            Workspace.Edit(() =>
            {
                foreach (var pair in values.Where(x => !_propertyBaseline.TryGetValue(x.Key, out string old) || old != x.Value))
                    if (_resourceKey == null) Workspace.SetProperty(_selected, pair.Key, pair.Value);
                    else Workspace.Documents["images.xml"].Root.Elements("Image").Single(x => (string)x.Attribute("key") == _resourceKey).SetAttributeValue(pair.Key, pair.Value);
            });
            _pendingProperties = false; Rebuild();
            if (_resourceKey != null) SelectResource(_resourceKey);
        }
        private void Duplicate()
        { var node = OriginalSelection(); Workspace.Edit(() => _selected = (string)Workspace.Duplicate(node).Attribute("id")); Rebuild(); }
        private void Delete()
        { var node = OriginalSelection(); if (node.Parent == Workspace.Documents[Workspace.CurrentPath].Root) throw new InvalidOperationException("화면 루트는 삭제할 수 없습니다."); Workspace.Edit(node.Remove); _selected = null; Rebuild(); }
        private XElement OriginalSelection()
        {
            if (_selected == null) throw new InvalidOperationException("요소를 선택하세요.");
            if (_selected.Contains("/")) throw new InvalidOperationException("프리팹 원본을 열거나 연결을 해제한 다음 구조를 바꾸세요.");
            return Workspace.Find(Workspace.CurrentPath, _selected) ?? throw new FormatException("선택 요소가 없습니다.");
        }
        private void Reorder(int direction)
        { var node = OriginalSelection(); Workspace.Edit(() => { var other = direction < 0 ? node.ElementsBeforeSelf().LastOrDefault(x => x.Name == "Element" || x.Name == "Instance") : node.ElementsAfterSelf().FirstOrDefault(x => x.Name == "Element" || x.Name == "Instance"); if (other == null) return; node.Remove(); if (direction < 0) other.AddBeforeSelf(node); else other.AddAfterSelf(node); }); Rebuild(); }
        private void CreateScreen(string key)
        {
            string path = "Screens/" + key + ".xml";
            Workspace.Edit(() => Workspace.AddDocument(path, new XDocument(new XElement("Screen", new XAttribute("schema", "2"), new XAttribute("key", key), new XAttribute("width", 1080), new XAttribute("height", 1920), NewElement("새 화면", "Panel")))));
            Workspace.CurrentPath = path; _selected = null; Rebuild();
        }
        private XElement NewElement(string name, string kind)
        {
            var node = new XElement("Element", new XAttribute("id", Guid.NewGuid().ToString("N")), new XAttribute("name", name), new XAttribute("key", name), new XAttribute("width", 300), new XAttribute("height", 100), new XAttribute("x", 0), new XAttribute("y", 0));
            string font = (string)Workspace.Documents["fonts.xml"].Root.Elements("Font").First().Attribute("key");
            if (kind == "Text") node.Add(new XElement("Text", new XAttribute("font", font), new XAttribute("text", "새 텍스트"), new XAttribute("size", 36), new XAttribute("color", "#FFFFFFFF")));
            else if (kind == "Image") node.Add(new XElement("Image", new XAttribute("sprite", (string)Workspace.Documents["images.xml"].Root.Elements("Image").First().Attribute("key")), new XAttribute("color", "#FFFFFFFF")));
            else if (kind == "Button")
            { node.Add(new XElement("Image", new XAttribute("color", "#75618DFF")), new XElement("Button", new XAttribute("command", "settings.close"))); var text = NewElement("버튼 글자", "Text"); text.SetAttributeValue("width", 260); text.SetAttributeValue("height", 70); node.Add(text); }
            else if (kind == "Slider" || kind == "Toggle" || kind == "Switch")
            {
                var source = UiComposer.Resolve(Workspace, "Screens/UI3030.xml").Root.Descendants("Element").First(x => x.Element(kind) != null);
                node = new XElement(source);
                var ids = node.DescendantsAndSelf("Element").ToDictionary(x => (string)x.Attribute("id"), x => Guid.NewGuid().ToString("N"));
                foreach (var attribute in node.DescendantsAndSelf().Attributes()) if (ids.TryGetValue(attribute.Value, out string id)) attribute.Value = id;
                node.SetAttributeValue("x", 0); node.SetAttributeValue("y", 0); node.SetAttributeValue("name", name); node.SetAttributeValue("key", name);
            }
            else if (kind == "Slot")
            {
                if (Workspace.Documents[Workspace.CurrentPath].Root.Name != "Prefab") throw new InvalidOperationException("공용 프리팹 원본에서 슬롯을 추가하세요.");
                node.Name = "Slot"; node.SetAttributeValue("key", "slot-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            }
            return node;
        }
        private void AddComponentElement(string kind)
        {
            var node = NewElement(kind, kind);
            Workspace.Edit(() =>
            {
                XElement parent;
                if (_selected?.Contains("/") == true)
                {
                    string file = _resolved.SourceFiles[_selected]; var slot = Workspace.Find(file, _selected.Split('/').Last());
                    if (slot?.Name != "Slot") throw new InvalidOperationException("인스턴스는 공개 슬롯에만 자식을 넣을 수 있습니다. 원본을 편집하거나 연결을 해제하세요.");
                    var instance = Workspace.Find(Workspace.CurrentPath, _selected.Split('/')[0]); string key = (string)slot.Attribute("key");
                    parent = instance.Elements("Slot").FirstOrDefault(x => (string)x.Attribute("key") == key);
                    if (parent == null) { parent = new XElement("Slot", new XAttribute("key", key)); instance.Add(parent); }
                }
                else parent = _selected == null ? Workspace.Documents[Workspace.CurrentPath].Root.Elements("Element").First() : OriginalSelection();
                parent.Add(node);
            });
            _selected = (string)node.Attribute("id"); Rebuild();
        }
        private void EditPrefabSource()
        {
            string instanceId = _selected.Split('/')[0]; var instance = Workspace.Find(Workspace.CurrentPath, instanceId);
            string key = (string)instance.Attribute("prefab"); Workspace.CurrentPath = Workspace.Documents.Single(x => x.Value.Root.Name == "Prefab" && (string)x.Value.Root.Attribute("key") == key).Key; _selected = null; Rebuild();
        }
        private void ConfirmApply()
        {
            var instance = Workspace.Find(Workspace.CurrentPath, _selected.Split('/')[0]); string key = (string)instance.Attribute("prefab");
            int uses = Workspace.Documents.Sum(x => x.Value.Descendants("Instance").Count(n => (string)n.Attribute("prefab") == key));
            _dialog = "공용 원본에 적용하면 " + uses + "개 사용처가 갱신됩니다. 인스턴스 오버라이드는 유지됩니다.";
            _confirmed = () => { PrefabEditing.ApplyToSource(Workspace, _selected); Rebuild(); };
        }
        private void SaveCopy()
        {
            string destination = StudioFileDialog.Save("제작 키트|kit.xml", "새 키트 폴더에 kit.xml 저장"); if (destination == null) return;
            string directory = Path.GetDirectoryName(destination);
            if (directory == Workspace.DirectoryPath) { Save(); return; }
            Directory.CreateDirectory(directory);
            foreach (var image in Workspace.Documents["images.xml"].Root.Elements("Image"))
            { string file = AuthoringWorkspace.Required(image, "file"); string target = AuthoringWorkspace.SafePath(directory, file); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(AuthoringWorkspace.SafePath(Workspace.DirectoryPath, file), target, false); }
            foreach (var pair in Workspace.Documents)
            { string target = AuthoringWorkspace.SafePath(directory, pair.Key); Directory.CreateDirectory(Path.GetDirectoryName(target)); AuthoringWorkspace.AtomicWrite(target, Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(pair.Value))); }
            OpenKit(directory); _message = "다른 이름으로 저장 완료";
        }
        private void ImportImage()
        {
            string file = StudioFileDialog.Open("그림|*.png;*.jpg;*.jpeg", "PNG/JPG 가져오기"); if (file == null) return;
            ImportImageFile(file);
        }
        /// <summary>Imports a creator image into this kit and optionally assigns it to the selected Image in one undo action.</summary>
        public void ImportImageFile(string file)
        {
            if (Workspace.IsReadOnly) throw new InvalidOperationException("읽기 전용 키트에는 이미지를 가져올 수 없습니다.");
            byte[] bytes = File.ReadAllBytes(file); var texture = new Texture2D(2, 2);
            try { if (bytes.Length > 16 * 1024 * 1024 || !ImageConversion.LoadImage(texture, bytes)) throw new FormatException("지원하지 않는 이미지입니다."); }
            finally { Destroy(texture); }
            string key = Path.GetFileNameWithoutExtension(file), resource = "Images/" + Guid.NewGuid().ToString("N") + Path.GetExtension(file).ToLowerInvariant();
            if (Workspace.Documents["images.xml"].Root.Elements("Image").Any(x => (string)x.Attribute("key") == key)) key += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            File.WriteAllBytes(AuthoringWorkspace.SafePath(Workspace.DirectoryPath, resource), bytes);
            string artRoot = PersonalArtSettings.ReadRoot();
            string artPath = null;
            if (!string.IsNullOrEmpty(artRoot))
            {
                string prefix = Path.GetFullPath(artRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(file);
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) artPath = full.Substring(prefix.Length).Replace('\\', '/');
            }
            Workspace.Edit(() =>
            {
                var screen = Workspace.Documents[Workspace.CurrentPath].Root;
                if (screen.Name != "Screen") screen = Workspace.Documents.Values.First(x => x.Root.Name == "Screen").Root;
                string atlas = (string)screen.Attribute("key") ?? "UI";
                if (!System.Text.RegularExpressions.Regex.IsMatch(atlas, "^[A-Za-z0-9_-]+$")) atlas = "UI";
                Workspace.Documents["images.xml"].Root.Add(new XElement("Image", new XAttribute("key", key), new XAttribute("file", resource), new XAttribute("atlas", atlas), new XAttribute("ppu", 100), new XAttribute("left", 0), new XAttribute("bottom", 0), new XAttribute("right", 0), new XAttribute("top", 0), new XAttribute("pivotX", .5), new XAttribute("pivotY", .5), new XAttribute("filter", 1)));
                if (artPath != null) Workspace.Documents["images.xml"].Root.Elements("Image").Last().SetAttributeValue("artPath", artPath);
                if (_selected != null && _properties.ContainsKey("Image.sprite")) Workspace.SetProperty(_selected, "Image.sprite", key);
            });
            Rebuild(); _message = "그림 추가: " + key;
        }
        private void SelectResource(string key)
        { _resourceKey = key; _properties.Clear(); foreach (var attribute in Workspace.Documents["images.xml"].Root.Elements("Image").Single(x => (string)x.Attribute("key") == key).Attributes().Where(x => !new[] { "key", "guid", "localId", "sha256", "file" }.Contains(x.Name.LocalName))) _properties[attribute.Name.LocalName] = attribute.Value; _propertyBaseline.Clear(); foreach (var pair in _properties) _propertyBaseline[pair.Key] = pair.Value; _pendingProperties = false; }
        private void DrawPicker()
        {
            if (_pickerProperty == null || Workspace == null) return;
            Rect window = new Rect(Screen.width * .5f - 270, 130, 540, Mathf.Min(650, Screen.height - 160)); GUI.Box(window, ""); GUILayout.BeginArea(new Rect(window.x + 12, window.y + 12, window.width - 24, window.height - 24));
            GUILayout.BeginHorizontal(); GUILayout.Label("목록에서 선택: " + _pickerProperty); if (GUILayout.Button("닫기", GUILayout.Width(60))) _pickerProperty = null; GUILayout.EndHorizontal();
            if (_pickerProperty == null) { GUILayout.EndArea(); return; }
            _pickerFilter = GUILayout.TextField(_pickerFilter); _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            foreach (string key in PickerValues().Where(x => x.IndexOf(_pickerFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray())
                if (GUILayout.Button(key))
                {
                    string property = _pickerProperty; _pickerProperty = null;
                    Try(() =>
                    {
                        if (property == "__component") AddComponentElement(key);
                        else if (property == "__resource") SelectResource(key);
                        else if (property == "__prefab")
                        { var parent = _selected == null ? Workspace.Documents[Workspace.CurrentPath].Root.Elements("Element").First() : OriginalSelection(); Workspace.Edit(() => parent.Add(new XElement("Instance", new XAttribute("id", Guid.NewGuid().ToString("N")), new XAttribute("key", key), new XAttribute("prefab", key)))); Rebuild(); }
                        else if (property == "__parent") Reparent(key);
                        else if (property == "__align") AlignSelected(key);
                        else { _properties[property] = key; _pendingProperties = true; ApplyProperties(); }
                    });
                }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private IEnumerable<string> PickerValues()
        {
            if (_pickerProperty == "__component") return new[] { "Panel", "Image", "Text", "Button", "Slider", "Toggle", "Switch", "Slot" };
            if (_pickerProperty == "__align") return new[] { "가로 가운데", "세로 가운데", "왼쪽", "오른쪽", "위", "아래" };
            if (_pickerProperty == "__prefab") return Workspace.Documents.Values.Where(x => x.Root.Name == "Prefab").Select(x => (string)x.Root.Attribute("key"));
            if (_pickerProperty == "__parent") return Workspace.Documents[Workspace.CurrentPath].Descendants("Element").Select(x => (string)x.Attribute("id"));
            if (_pickerProperty == "__resource" || _pickerProperty.EndsWith(".sprite", StringComparison.Ordinal)) return Workspace.Documents["images.xml"].Root.Elements("Image").Select(x => (string)x.Attribute("key"));
            if (_pickerProperty.EndsWith(".font", StringComparison.Ordinal)) return Workspace.Documents["fonts.xml"].Root.Elements("Font").Select(x => (string)x.Attribute("key"));
            string component = _pickerProperty.Split('.')[0]; string expected = component == "Toggle" || component == "Switch" ? "bool" : component == "Slider" ? "number" : _pickerProperty.EndsWith(".command", StringComparison.Ordinal) ? "none" : null;
            return Workspace.Catalog.Root.Elements(_pickerProperty.EndsWith(".command", StringComparison.Ordinal) ? "Command" : "State").Where(x => expected == null || (string)x.Attribute("type") == expected).Select(x => (string)x.Attribute("key"));
        }
        private void Reparent(string parentId)
        {
            var node = OriginalSelection(); var parent = Workspace.Find(Workspace.CurrentPath, parentId);
            if (parent == node || parent.Ancestors().Contains(node)) throw new InvalidOperationException("자기 하위로 이동할 수 없습니다.");
            var rect = _view.Elements[_selected]; Vector3 position = rect.position; var newParent = _view.Elements[parentId]; Vector3 local = newParent.InverseTransformPoint(position);
            Workspace.Edit(() => { node.Remove(); parent.Add(node); node.SetAttributeValue("anchorMinX", .5); node.SetAttributeValue("anchorMinY", .5); node.SetAttributeValue("anchorMaxX", .5); node.SetAttributeValue("anchorMaxY", .5); node.SetAttributeValue("x", F(local.x)); node.SetAttributeValue("y", F(local.y)); }); Rebuild();
        }
        /// <summary>Aligns a selected visual rect inside its parent as one undoable source/override edit.</summary>
        public void AlignSelected(string alignment)
        {
            if (_selected == null || !_view.Elements.TryGetValue(_selected, out var rect)) throw new InvalidOperationException("정렬할 요소를 선택하세요.");
            var parent = (RectTransform)rect.parent;
            if (parent.GetComponent<LayoutGroup>() != null) throw new InvalidOperationException("레이아웃 그룹의 정렬 속성을 편집하세요.");
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var local = corners.Select(parent.InverseTransformPoint).ToArray();
            float minX = local.Min(x => x.x), maxX = local.Max(x => x.x), minY = local.Min(x => x.y), maxY = local.Max(x => x.y);
            Vector2 position = rect.anchoredPosition;
            string property;
            switch (alignment)
            {
                case "가로 가운데": position.x += parent.rect.center.x - (minX + maxX) * .5f; property = "x"; break;
                case "세로 가운데": position.y += parent.rect.center.y - (minY + maxY) * .5f; property = "y"; break;
                case "왼쪽": position.x += parent.rect.xMin - minX; property = "x"; break;
                case "오른쪽": position.x += parent.rect.xMax - maxX; property = "x"; break;
                case "위": position.y += parent.rect.yMax - maxY; property = "y"; break;
                case "아래": position.y += parent.rect.yMin - minY; property = "y"; break;
                default: throw new ArgumentException("Unknown alignment");
            }
            Change(_selected, property, F(property == "x" ? position.x : position.y));
        }
        private void DrawDialog()
        {
            if (_dialog == null) return;
            bool compare = _dialog.StartsWith("File conflict:", StringComparison.Ordinal);
            bool publication = _dialog.StartsWith("게시 검사", StringComparison.Ordinal);
            float width = compare ? Mathf.Min(1100, Screen.width - 40) : 650, height = compare || publication ? Mathf.Min(700, Screen.height - 80) : 270;
            GUI.ModalWindow(901, new Rect(Screen.width / 2f - width / 2, Screen.height / 2f - height / 2, width, height), _ =>
            {
                if (publication) _compareScroll = GUILayout.BeginScrollView(_compareScroll, GUILayout.Height(height - 85));
                GUILayout.Label(_dialog, new GUIStyle(GUI.skin.label) { wordWrap = true }, publication ? GUILayout.ExpandHeight(true) : GUILayout.Height(85));
                if (publication) GUILayout.EndScrollView();
                if (compare)
                {
                    _compareScroll = GUILayout.BeginScrollView(_compareScroll, GUILayout.Height(height - 155));
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(); GUILayout.Label("현재 편집 메모리"); GUILayout.TextArea(_compareMemory ?? ""); GUILayout.EndVertical();
                    GUILayout.BeginVertical(); GUILayout.Label("외부에서 저장된 파일"); GUILayout.TextArea(_compareDisk ?? "[파일 없음]"); GUILayout.EndVertical();
                    GUILayout.EndHorizontal(); GUILayout.EndScrollView();
                }
                if (_dialog.StartsWith("이름:", StringComparison.Ordinal)) _newName = GUILayout.TextField(_newName);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("확인")) { var action = _confirmed; _dialog = null; _confirmed = null; Try(action); }
                if (GUILayout.Button("취소")) { _dialog = null; _confirmed = null; }
                GUILayout.EndHorizontal();
            }, "확인");
        }
        private void NameDialog(string message, string initial, Action<string> action)
        { _newName = initial; _dialog = "이름: " + message; _confirmed = () => { if (string.IsNullOrWhiteSpace(_newName) || _newName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')) throw new ArgumentException("이름은 문자·숫자·._-만 사용하세요."); action(_newName); }; }
        private void AskDiscard(Action action)
        { if (Workspace?.IsDirty != true) { Try(action); return; } _dialog = "저장하지 않은 변경이 있습니다. 계속하면 현재 편집 버퍼를 닫습니다. 저장하려면 취소 후 저장하세요."; _confirmed = action; }
        private bool CheckQuit()
        { if (Workspace?.IsDirty != true) return true; _dialog = "미저장 변경이 있습니다. 종료하면 자동 복구본에 남깁니다. 저장 후 종료하려면 취소하세요."; _confirmed = () => { Workspace.SaveRecovery(); Application.wantsToQuit -= CheckQuit; Application.Quit(); }; return false; }
        private void Button(string label, Action action, float width) { if (GUILayout.Button(label, GUILayout.Width(width))) Try(action); }
        private void Try(Action action)
        {
            try { action?.Invoke(); }
            catch (IOException error)
            {
                _error = error.Message;
                if (error.Message.StartsWith("File conflict:", StringComparison.Ordinal))
                {
                    string relative = error.Message.Substring("File conflict: ".Length).Split(new[] { ". Reopen" }, StringSplitOptions.None)[0];
                    string full = AuthoringWorkspace.SafePath(Workspace.DirectoryPath, relative);
                    _compareMemory = Workspace.Documents.TryGetValue(relative, out var document) ? document.ToString() : "";
                    _compareDisk = File.Exists(full) ? File.ReadAllText(full) : "[파일 없음]";
                    _dialog = error.Message + "\n외부 변경과 현재 메모리가 다릅니다. 확인=메모리로 덮어쓰기, 취소=원본 보존. [키트 열기]로 디스크를 다시 열 수 있습니다.";
                    _confirmed = () => Workspace.Save(true);
                }
            }
            catch (Exception error) { _error = error.Message; Debug.LogWarning("STUDIO: " + error.Message); }
        }
        private static bool IsKeyProperty(string key) => key.EndsWith(".sprite", StringComparison.Ordinal) || key.EndsWith(".font", StringComparison.Ordinal) || key.EndsWith(".bind", StringComparison.Ordinal) || key.EndsWith(".command", StringComparison.Ordinal) || key == "visible" || key.EndsWith(".enabled", StringComparison.Ordinal);
        private static void Toggle(HashSet<string> values, string id) { if (!values.Add(id)) values.Remove(id); }
        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Caption(string key) => key switch { "name" => "이름", "key" => "요소 키", "x" => "X 좌표", "y" => "Y 좌표", "width" => "너비", "height" => "높이", "Image.sprite" => "그림 키", "Text.text" => "문구", "Text.font" => "폰트", "Text.size" => "글자 크기", "Button.command" => "클릭 동작", "Slider.bind" => "음량 상태", "Slider.command" => "음량 변경", "Toggle.bind" or "Switch.bind" => "체크 상태", "Toggle.command" or "Switch.command" => "체크 변경", "Image.color" or "Text.color" => "색상", "active" => "표시", "visible" => "표시 조건", _ => key };
        private static RectTransform NewRect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static Rect Bounds(RectTransform rect)
        { var points = new Vector3[4]; rect.GetWorldCorners(points); return new Rect(points[0].x, Screen.height - points[1].y, points[2].x - points[0].x, points[1].y - points[0].y); }
        private void OnDestroy() { Application.wantsToQuit -= CheckQuit; _view?.Dispose(); _nativeBundle?.Unload(false); }
    }
}
