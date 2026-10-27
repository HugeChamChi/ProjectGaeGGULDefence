using System;
using System.IO;
using GGD.UI.External;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace GGD.UI.Preview
{
    /// <summary>Standalone file polling, transactional reload, preview audio and diagnostics.</summary>
    public sealed class PreviewPlayer : MonoBehaviour
    {
        private const float PollSeconds = .5f;
        private const int ToneSampleRate = 22050;
        [SerializeField] private TMP_FontAsset _font;
        [Inject] private PreviewSession _session;
        private RectTransform _host;
        private CanvasScaler _scaler;
        private UiView _view;
        private AudioSource _audio;
        private AudioClip _tone;
        private string _lastPointer;
        private float _nextPoll;
        private float _nextStatus;
        private int _width, _height;
        private Rect _safeArea;
        private bool _useSafeArea;
        private bool _loading;

        /// <summary>External publication root, configurable with --content.</summary>
        public string ContentRoot { get; private set; }
        /// <summary>Last successfully committed content version.</summary>
        public string Version { get; private set; } = "none";
        /// <summary>Latest failed candidate's error; displayed screen remains valid.</summary>
        public string Error { get; private set; } = "";
        /// <summary>Number of successfully built trees.</summary>
        public int ReloadCount { get; private set; }
        /// <summary>Current tree, exposed for targeted runtime checks.</summary>
        public GameObject ScreenRoot => _view?.Root;
        /// <summary>Temporary state and command diagnostics.</summary>
        public PreviewSession Session => _session;

        /// <summary>Build tooling assigns the existing Korean font before saving the dedicated scene.</summary>
        public void SetFont(TMP_FontAsset font) { _font = font; }

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 30;
            string[] args = Environment.GetCommandLineArgs();
            ContentRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Content"));
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--content") ContentRoot = Path.GetFullPath(args[i + 1]);
            var canvas = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = canvas.GetComponent<CanvasScaler>(); _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; _scaler.matchWidthOrHeight = 0;
            _host = UiView.Rect("ContentHost", canvas.transform); UiView.Stretch(_host);
            var events = new GameObject("PreviewEventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(transform, false);
            gameObject.AddComponent<AudioListener>(); _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.loop = true;
            float[] samples = new float[ToneSampleRate];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * 440 * i / ToneSampleRate) * .12f;
            _tone = AudioClip.Create("Preview test tone", samples.Length, 1, ToneSampleRate, false); _tone.SetData(samples, 0); _audio.clip = _tone;
            ReloadNow();
        }

        private void Update()
        {
            if (_host == null) return;
            if (_width != UnityEngine.Screen.width || _height != UnityEngine.Screen.height || _safeArea != UnityEngine.Screen.safeArea) UpdateSafeArea();
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollSeconds;
                try
                {
                    string pointerPath = Path.Combine(ContentRoot, "current.json");
                    if (File.Exists(pointerPath) && new FileInfo(pointerPath).Length <= 4096)
                    {
                        string pointer = File.ReadAllText(pointerPath);
                        if (pointer != _lastPointer) { _lastPointer = pointer; ReloadNow(); }
                    }
                }
                catch (Exception error) { Error = error.Message; }
            }
            if (Time.unscaledTime >= _nextStatus) { _nextStatus = Time.unscaledTime + 1; WriteStatus(); }
        }

        /// <summary>Stages a complete candidate before swapping; any error retains both prior view and prior state.</summary>
        public bool ReloadNow(bool resetState = false)
        {
            if (_loading || _host == null) return false;
            _loading = true;
            UiImageSet images = null;
            UiView candidate = null;
            try
            {
                if (_font == null) throw new InvalidOperationException("Bundled TMP font is missing");
                var package = UiContentPackage.Load(ContentRoot);
                var bindings = _session.Prepare(package, resetState);
                images = UiImageSet.Load(package);
                var document = UiDocument.Parse(package, bindings, images.Keys);
                candidate = UiView.Create(document, _host, bindings, _font, images);
                images = null; // candidate owns it from here
                bindings.OnCloseRequested += () => candidate.Root.SetActive(false);
                bindings.OnTestToneRequested += ToggleTone;
                var old = _view;
                bool open = old == null || old.Root.activeSelf;
                if (_session.Current != null)
                {
                    _session.Current.OnChanged -= ApplyAudio;
                    _session.Current.OnTestToneRequested -= ToggleTone;
                }
                _view = candidate; _session.Commit(bindings);
                _useSafeArea = document.SafeArea;
                _scaler.referenceResolution = new Vector2(document.ReferenceWidth, document.ReferenceHeight);
                UpdateSafeArea();
                // A dragged old slider must not receive an up/click after the swap.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                candidate.Root.SetActive(open); old?.Dispose();
                Version = package.Version; ReloadCount++; Error = "";
                _lastPointer = package.PointerText;
                ApplyAudio(); bindings.OnChanged += ApplyAudio;
                WriteStatus();
                Debug.Log("UI_PREVIEW_APPLIED " + Version);
                return true;
            }
            catch (Exception error)
            {
                candidate?.Dispose(); images?.Dispose(); Error = error.Message;
                Debug.LogWarning("UI_PREVIEW_REJECTED " + Error); WriteStatus();
                return false;
            }
            finally { _loading = false; }
        }

        private void ApplyAudio()
        {
            if (_audio == null || _session.Current == null) return;
            _audio.volume = (float)_session.Current.Read("audio.masterVolume") / 100f;
            _audio.mute = (bool)_session.Current.Read("audio.muted");
        }

        private void ToggleTone() { if (_audio.isPlaying) _audio.Stop(); else _audio.Play(); }

        private void UpdateSafeArea()
        {
            _width = UnityEngine.Screen.width; _height = UnityEngine.Screen.height; _safeArea = UnityEngine.Screen.safeArea;
            if (_width == 0 || _height == 0) return;
            Rect area = _useSafeArea ? _safeArea : new Rect(0, 0, _width, _height);
            // Reserve a fixed pixel toolbar outside authored content.
            area.height = Mathf.Max(1, area.height - 100);
            _host.anchorMin = area.min / new Vector2(_width, _height);
            _host.anchorMax = area.max / new Vector2(_width, _height);
            _host.offsetMin = _host.offsetMax = Vector2.zero;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(0, 0, UnityEngine.Screen.width, 100), "");
            if (GUI.Button(new Rect(8, 6, 100, 28), "Reload")) ReloadNow();
            if (GUI.Button(new Rect(114, 6, 100, 28), "Reopen")) _view?.Root.SetActive(true);
            if (GUI.Button(new Rect(220, 6, 120, 28), "Reset state")) ReloadNow(true);
            GUI.Label(new Rect(8, 36, UnityEngine.Screen.width - 16, 24), $"{Version} | {_session?.LastCommand}");
            GUI.color = string.IsNullOrEmpty(Error) ? Color.white : new Color(1, .55f, .5f);
            GUI.Label(new Rect(8, 60, UnityEngine.Screen.width - 16, 40), string.IsNullOrEmpty(Error)
                ? "Edit XML/images, then publish. Watching every 0.5s." : Error);
            GUI.color = Color.white;
        }

        private void WriteStatus()
        {
            if (string.IsNullOrEmpty(ContentRoot)) return;
            try
            {
                Directory.CreateDirectory(ContentRoot);
                var state = _session?.Current;
                var status = new Status
                {
                    version = Version, error = Error, reloads = ReloadCount, commands = _session?.CommandCount ?? 0,
                    lastCommand = _session?.LastCommand, scenario = state?.Scenario,
                    volume = state == null ? 0 : (float)state.Read("audio.masterVolume"), muted = state != null && (bool)state.Read("audio.muted"),
                    clicks = state == null ? 0 : (float)state.Read("preview.clicks"),
                    screenOpen = _view != null && _view.Root.activeSelf,
                    sceneRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount
                };
                string path = Path.Combine(ContentRoot, "status.json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(status, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
            }
            catch (IOException) { /* A diagnostic reader may briefly hold the file. Retry next tick. */ }
            catch (UnauthorizedAccessException) { /* Read-only content can still be previewed. */ }
        }

        private void OnDestroy()
        {
            if (_session?.Current != null) _session.Current.OnChanged -= ApplyAudio;
            _view?.Dispose();
            if (_audio != null) _audio.Stop();
            if (_tone != null) Destroy(_tone);
        }

        [Serializable] private sealed class Status
        {
            public string version, error, lastCommand, scenario;
            public int reloads, commands, sceneRoots;
            public float volume, clicks;
            public bool muted, screenOpen;
        }
    }
}
