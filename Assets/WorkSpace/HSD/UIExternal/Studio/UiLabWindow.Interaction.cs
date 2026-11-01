using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace GGD.UI.Authoring.Studio
{
    public sealed partial class UiLabWindow
    {
        private const float MinimumLayoutWidth = 1280, MinimumLayoutHeight = 800, LiveApplyInterval = .045f, LiveGestureGap = .4f;
        private readonly Dictionary<string, string> _pendingLiveStyle = new Dictionary<string, string>();
        private string _pendingStyleScreen, _pendingStyleScope;
        private bool _pendingLiveMotion, _panningPreview;
        private float _lastLiveInput, _nextLiveApply;
        private Vector2 _previewPan, _lastPanPoint;
        private bool _recordInspectorControls;
        private readonly List<string> _layoutOverflows = new List<string>();
        /// <summary>Horizontal inspector control overflows observed during the most recent actual repaint.</summary>
        public IReadOnlyList<string> LayoutOverflows => _layoutOverflows;
        /// <summary>Whether horizontal and vertical thumb geometry is centered within its own rail.</summary>
        public bool ScrollbarGeometryAligned => _theme != null && _theme.Skin.horizontalSlider.padding.vertical == 0 && _theme.Skin.horizontalSliderThumb.fixedHeight == _theme.Skin.horizontalSlider.fixedHeight && Mathf.Approximately(_theme.Skin.verticalScrollbar.fixedWidth, _theme.Skin.verticalScrollbarThumb.fixedWidth + _theme.Skin.verticalScrollbar.padding.horizontal);
        private float InterfaceScale => Mathf.Min(1, Mathf.Max(.1f, Screen.width / MinimumLayoutWidth), Mathf.Max(.1f, Screen.height / MinimumLayoutHeight));
        private float LayoutWidth => Screen.width / InterfaceScale;
        private float LayoutHeight => Screen.height / InterfaceScale;
        /// <summary>Actual pixel viewport, excluding all scaled desktop controls.</summary>
        public Rect PreviewBounds { get { var r = LogicalPreviewBounds; return new Rect(r.x * InterfaceScale, r.y * InterfaceScale, r.width * InterfaceScale, r.height * InterfaceScale); } }
        /// <summary>Current preview magnification relative to fit.</summary>
        public float PreviewZoom => _previewZoom;
        /// <summary>Routes a wheel gesture only when its screen-space pointer is inside the preview.</summary>
        public bool HandlePreviewWheel(Vector2 screenPoint, float delta)
        {
            var viewport = PreviewBounds; if (Mathf.Approximately(delta, 0) || !viewport.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y))) return false;
            float old = _previewZoom; _previewZoom = Mathf.Clamp(old * Mathf.Exp(delta * .15f), .25f, 4);
            var center = new Vector2(viewport.center.x, Screen.height - viewport.center.y); var pointer = screenPoint - center;
            _previewPan = pointer - (pointer - _previewPan) * (_previewZoom / old); ClampPreviewPan(); return true;
        }
        /// <summary>Pans a zoomed preview in physical pixels without touching authored UI positions.</summary>
        public void PanPreview(Vector2 delta) { _previewPan += delta; ClampPreviewPan(); }
        private void ClampPreviewPan()
        {
            var view = PreviewBounds; float fit = Mathf.Min(view.width / 1080, view.height / _ratioHeight) * .93f;
            var limit = (new Vector2(1080, _ratioHeight) * fit * _previewZoom + view.size) * .5f - Vector2.one * 48;
            _previewPan.x = Mathf.Clamp(_previewPan.x, -Mathf.Max(0, limit.x), Mathf.Max(0, limit.x)); _previewPan.y = Mathf.Clamp(_previewPan.y, -Mathf.Max(0, limit.y), Mathf.Max(0, limit.y));
        }
        private void ChooseFont(string key) { _font = key; EditLiveProperty("font", key); }
        /// <summary>Accepts the same live field edit as the GUI. Incomplete/invalid input stays a draft.</summary>
        public bool EditLiveProperty(string property, string value)
        {
            SetDraft(property, value);
            var values = new Dictionary<string, string> { [property] = value };
            try { ValidateStyleValues(values); } catch (Exception) { _message = "입력 중인 값은 완성되면 반영됩니다."; return false; }
            bool elementOnly = property == "text" || property == "tint" || property == "dx" || property == "dy" || property == "width" || property == "height";
            string scope;
            try { scope = elementOnly ? _selected ?? throw new InvalidOperationException() : Scope; }
            catch { _message = "미리보기에서 요소를 선택하거나 적용 범위를 고르세요."; return false; }
            if (!elementOnly && _shared && scope != "all" && !scope.StartsWith("role:")) { _message = "공통 적용은 제목/본문/버튼/숫자/전체 범위를 고르세요."; return false; }
            string screen = !elementOnly && _shared ? "*" : _screen;
            if (_pendingLiveStyle.Count > 0 && (_pendingStyleScreen != screen || _pendingStyleScope != scope)) FinishLiveEdits();
            _pendingStyleScreen = screen; _pendingStyleScope = scope; _pendingLiveStyle[property] = value;
            if (property == "size") { _auto = false; _pendingLiveStyle["auto"] = "false"; }
            _lastLiveInput = Time.unscaledTime; return true;
        }
        private void SetDraft(string key, string value)
        {
            switch (key)
            {
                case "font": _font = value; break; case "size": _size = value; break; case "color": _color = value; break; case "outline": _outline = value; break; case "edge": _edge = value; break;
                case "soft": _soft = value; break; case "spacing": _spacing = value; break; case "line": _line = value; break; case "text": _copy = value; break;
                case "tint": _tint = value; break; case "dx": _dx = value; break; case "dy": _dy = value; break; case "width": _width = value; break; case "height": _height = value; break;
                case "auto": if (bool.TryParse(value, out bool auto)) _auto = auto; break;
            }
        }
        private Dictionary<string, string> TypographyDraft() => new Dictionary<string, string> { ["font"] = _font, ["size"] = _size, ["auto"] = _auto.ToString().ToLowerInvariant(), ["color"] = _color, ["outline"] = _outline, ["edge"] = _edge, ["soft"] = _soft, ["spacing"] = _spacing, ["line"] = _line };
        private Dictionary<string, string> AppearanceDraft() => new Dictionary<string, string> { ["tint"] = _tint, ["dx"] = _dx, ["dy"] = _dy, ["width"] = _width, ["height"] = _height };
        private void QueueChangedFields(Dictionary<string, string> before, Dictionary<string, string> after)
        { foreach (var pair in after) if (!before.TryGetValue(pair.Key, out string previous) || previous != pair.Value) EditLiveProperty(pair.Key, pair.Value); }
        private string MotionDraft() => string.Join("|", _kind, _ease, _duration, _delay, _scale, _distance, _stagger);
        private void QueueLiveMotion()
        {
            try
            {
                foreach (string value in new[] { _duration, _delay, _scale, _distance, _stagger }) UiLabWorkspace.Number(value);
                if (UiLabWorkspace.Number(_duration) <= 0 || UiLabWorkspace.Number(_delay) < 0 || UiLabWorkspace.Number(_scale) < 0 || UiLabWorkspace.Number(_stagger) < 0) throw new FormatException();
            }
            catch { _message = "연출 수치가 완성되면 반영됩니다."; return; }
            _pendingLiveMotion = true; _lastLiveInput = Time.unscaledTime;
        }
        private void ProcessLiveEdits()
        {
            if (Time.unscaledTime >= _nextLiveApply && (_pendingLiveStyle.Count > 0 || _pendingLiveMotion))
            { FlushLiveEdits(); _nextLiveApply = Time.unscaledTime + LiveApplyInterval; }
            if (Time.unscaledTime - _lastLiveInput > LiveGestureGap && GUIUtility.hotControl == 0) Workspace.EndLiveGroup();
        }
        private void FlushLiveEdits()
        {
            if (Workspace == null) return;
            if (_pendingLiveStyle.Count > 0)
            {
                var values = new Dictionary<string, string>(_pendingLiveStyle); _pendingLiveStyle.Clear();
                try { Workspace.BeginLiveGroup(); Workspace.Edit(_pendingStyleScreen, _pendingStyleScope, values); _showA = false; Rebuild(); _message = "변경 즉시 반영됨 (파일 저장은 상단 실험 저장)"; }
                catch (Exception error) { _error = error.Message; }
            }
            if (_pendingLiveMotion)
            {
                _pendingLiveMotion = false;
                try { Workspace.BeginLiveGroup(); float progress = Playback.Progress; bool playing = Playback.Playing; SaveMotion(); Playback.Seek(progress); Playback.Playing = playing; _message = "연출 설정이 바로 반영됐습니다."; }
                catch (Exception error) { _error = error.Message; }
            }
        }
        private void FinishLiveEdits() { FlushLiveEdits(); Workspace?.EndLiveGroup(); }
        private void RefreshSelectedFields() { if (_selected != null && _template.Ids.Contains(_selected)) Select(_selected); }
        private void RestoreScope(string screen, string scope) { FinishLiveEdits(); Workspace.Reset(screen, scope); Rebuild(); RefreshSelectedFields(); }
        private void RecordInspectorControl(string label)
        {
            if (!_recordInspectorControls || Event.current.type != EventType.Repaint) return;
            var rect = GUILayoutUtility.GetLastRect(); if (rect.x < -2 || rect.xMax > Inspector - 68) _layoutOverflows.Add(label + ": " + rect.x.ToString("0.#", CultureInfo.InvariantCulture) + ".." + rect.xMax.ToString("0.#", CultureInfo.InvariantCulture));
        }
    }
}
