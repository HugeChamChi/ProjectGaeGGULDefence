using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    /// <summary>Multiple highlight holes with matching input filtering, in screen coordinates.</summary>
    public sealed class IngameTutorialOverlay : MaskableGraphic, ICanvasRaycastFilter, IPointerClickHandler
    {
        public enum Gesture { Tap, Hold, Drag, HoldDrag, Swipe }
        private readonly List<Func<Rect>> _targets = new();
        private readonly List<Rect> _holes = new();
        private readonly List<float> _xs = new();
        private readonly List<float> _ys = new();
        private IngameTutorialSettings _settings;
        private bool _allowTargets;
        private bool _dim;
        private bool _hand;
        private bool _pointerOnly;
        private Gesture _gesture;
        private float _started;
        private readonly Vector3[] _corners = new Vector3[4];
        private IngameTutorialDialogue _dialogue;
        private const float HandReferenceWidth = 1080f;
        private Image _handImage;
        private Material _spotlight;
        private const int MaxSpotlightHoles = 8;
        private readonly Vector4[] _spotlightHoles = new Vector4[MaxSpotlightHoles];
        private static readonly int HolesId = Shader.PropertyToID("_Holes");
        private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        // Hold cue vertices sit far from every hole so the spotlight shader keeps their color.
        private static readonly Vector2 OutsideSpotlight = new Vector2(1e5f, 1e5f);

        /// <summary>Number of taps consumed by the overlay; taps never also select underlying UI.</summary>
        public int TapCount { get; private set; }
        public bool IsTransitioning => _dim && _targets.Count > 0 && Time.unscaledTime - _started < _settings.FocusSeconds;

        /// <summary>Shows awareness, a forced tap/drag, or an input-only blocker.</summary>
        public void Show(IngameTutorialSettings settings, bool allowTargets, bool dim, bool hand, Gesture gesture,
            params Func<Rect>[] targets)
        {
            _settings = settings;
            _allowTargets = allowTargets;
            _dim = dim;
            _hand = hand;
            _pointerOnly = false;
            _gesture = gesture;
            _started = Time.unscaledTime;
            _targets.Clear();
            _targets.AddRange(targets);
            if (_dialogue == null) _dialogue = transform.parent.GetComponentInChildren<IngameTutorialDialogue>(true);
            _dialogue?.AvoidAreas(this, _targets);
            raycastTarget = true;
            gameObject.SetActive(true);
            RefreshHoles();
            UpdateHand();
            SetVerticesDirty();
        }

        /// <summary>Restores normal UI input.</summary>
        public void Hide()
        {
            _targets.Clear();
            _holes.Clear();
            raycastTarget = false;
            gameObject.SetActive(false);
        }

        /// <summary>Animates a pointing tap without intercepting the modal's own input.</summary>
        public void PointAt(IngameTutorialSettings settings, Func<Rect> target)
        {
            Show(settings, true, false, true, Gesture.Tap, target);
            _pointerOnly = true;
            raycastTarget = false;
        }

        /// <summary>Consumes an awareness dismissal without forwarding it to a choice.</summary>
        public void OnPointerClick(PointerEventData eventData) => TapCount++;

        /// <summary>Only explicitly permitted target interiors pass through to UI/world input.</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (_pointerOnly) return false;
            if (!_allowTargets) return true;
            if (IsTransitioning) return true;
            foreach (var target in _targets)
                if (target().Contains(screenPoint)) return false;
            return true;
        }

        /// <summary>Screen rectangle of a UI target, including camera-space canvases.</summary>
        public Rect ScreenRect(RectTransform target, Vector4 inset = default)
        {
            if (target == null) return Rect.zero;
            var c = target.GetComponentInParent<Canvas>();
            var cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            var bounds = target.rect;
            bounds.xMin += inset.x; bounds.yMin += inset.y;
            bounds.xMax -= inset.z; bounds.yMax -= inset.w;
            _corners[0] = target.TransformPoint(new Vector3(bounds.xMin, bounds.yMin));
            _corners[1] = target.TransformPoint(new Vector3(bounds.xMin, bounds.yMax));
            _corners[2] = target.TransformPoint(new Vector3(bounds.xMax, bounds.yMax));
            _corners[3] = target.TransformPoint(new Vector3(bounds.xMax, bounds.yMin));
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in _corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(cam, corner);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void Update()
        {
            RefreshHoles();
            UpdateHand();
            SetVerticesDirty();
        }

        private void RefreshHoles()
        {
            _holes.Clear();
            foreach (var getRect in _targets)
            {
                var r = getRect();
                if (r.width <= 0 || r.height <= 0) continue;
                float p = _settings.HighlightPaddingPixels;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, r.min - Vector2.one * p, null, out var min);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, r.max + Vector2.one * p, null, out var max);
                var target = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                float progress = Mathf.Clamp01((Time.unscaledTime - _started) / _settings.FocusSeconds);
                float t = 1f - Mathf.Pow(1f - progress, 3f);
                var bounds = rectTransform.rect;
                // Keep the focus center fixed; shrink an enlarged copy of the target from beyond every screen edge.
                var reach = Vector2.Max(target.center - bounds.min, bounds.max - target.center);
                float startScale = Mathf.Max(1f, Mathf.Max(reach.x * 2f / target.width, reach.y * 2f / target.height));
                var size = target.size * Mathf.Lerp(startScale, 1f, t);
                _holes.Add(new Rect(target.center - size * 0.5f, size));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_settings == null) return;
            var bounds = rectTransform.rect;
            if (_dim && UseSpotlight())
            {
                float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _started) / (_settings.FocusSeconds * 0.2f)));
                // One full-screen quad; the shader cuts rounded, feathered holes from local coordinates in UV0.
                Quad(vh, bounds, new Color(0, 0, 0, _settings.DimAlpha * fade), true);
            }
            else if (_dim)
            {
                float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _started) / (_settings.FocusSeconds * 0.2f)));
                var dimColor = new Color(0, 0, 0, _settings.DimAlpha * fade);
                // Partition along hole edges, so overlaps never stack dark rectangles.
                _xs.Clear(); _ys.Clear();
                _xs.Add(bounds.xMin); _xs.Add(bounds.xMax);
                _ys.Add(bounds.yMin); _ys.Add(bounds.yMax);
                foreach (var hole in _holes)
                {
                    _xs.Add(Mathf.Clamp(hole.xMin, bounds.xMin, bounds.xMax));
                    _xs.Add(Mathf.Clamp(hole.xMax, bounds.xMin, bounds.xMax));
                    _ys.Add(Mathf.Clamp(hole.yMin, bounds.yMin, bounds.yMax));
                    _ys.Add(Mathf.Clamp(hole.yMax, bounds.yMin, bounds.yMax));
                }
                _xs.Sort(); _ys.Sort();
                for (int x = 1; x < _xs.Count; x++)
                for (int y = 1; y < _ys.Count; y++)
                {
                    var r = Rect.MinMaxRect(_xs[x-1], _ys[y-1], _xs[x], _ys[y]);
                    bool inside = false;
                    foreach (var hole in _holes) if (hole.Contains(r.center)) { inside = true; break; }
                    if (!inside) Quad(vh, r, dimColor);
                }
            }
            // Even an invisible input blocker needs geometry for GraphicRaycaster.
            if (!_dim) Quad(vh, bounds, Color.clear);
            DrawHoldCue(vh);
        }

        private void DrawHoldCue(VertexHelper vh)
        {
            if (!_hand || _holes.Count == 0 || IsTransitioning ||
                (_gesture != Gesture.Hold && _gesture != Gesture.HoldDrag)) return;
            float elapsed = Mathf.Max(0f, Time.unscaledTime - _started - _settings.FocusSeconds) % _settings.GestureSeconds;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, _settings.HoldGestureSeconds));
            if (elapsed > _settings.HoldGestureSeconds) return;
            float scale = rectTransform.rect.width / HandReferenceWidth;
            float radius = _settings.HoldCueRadius * scale;
            float inner = Mathf.Max(0f, radius - _settings.HoldCueThickness * scale);
            const int segments = 48;
            var center = _holes[0].center + _settings.HoldCueOffset * scale;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 0.5f - Mathf.PI * 2f * i / segments;
                float b = Mathf.PI * 0.5f - Mathf.PI * 2f * (i + 1) / segments;
                var color = _settings.HoldCueColor;
                if ((i + 1f) / segments > progress) color.a *= 0.2f;
                var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int n = vh.currentVertCount;
                vh.AddVert(center + from * inner, color, OutsideSpotlight);
                vh.AddVert(center + from * radius, color, OutsideSpotlight);
                vh.AddVert(center + to * radius, color, OutsideSpotlight);
                vh.AddVert(center + to * inner, color, OutsideSpotlight);
                vh.AddTriangle(n, n + 1, n + 2);
                vh.AddTriangle(n, n + 2, n + 3);
            }
        }

        private void UpdateHand()
        {
            bool visible = _settings != null && _hand && _holes.Count > 0 && !IsTransitioning
                && _settings.HandIdleSprite != null && _settings.HandPressedSprite != null;
            if (!visible)
            {
                if (_handImage != null) _handImage.gameObject.SetActive(false);
                return;
            }
            if (_handImage == null)
            {
                var hand = new GameObject("TutorialHand", typeof(RectTransform), typeof(Image));
                hand.transform.SetParent(transform, false);
                _handImage = hand.GetComponent<Image>();
                _handImage.raycastTarget = false;
                _handImage.preserveAspect = true;
            }
            _handImage.gameObject.SetActive(true);
            float elapsed = Mathf.Max(0f, Time.unscaledTime - _started - _settings.FocusSeconds) % _settings.GestureSeconds;
            float hold = _gesture == Gesture.Hold || _gesture == Gesture.HoldDrag ? _settings.HoldGestureSeconds : 0f;
            float t = Mathf.Clamp01((elapsed - hold) / Mathf.Max(0.1f, _settings.GestureSeconds - hold));
            Vector2 from = _holes[0].center;
            Vector2 to = _gesture == Gesture.Drag && _holes.Count > 1 ? _holes[1].center : from;
            if (_gesture == Gesture.Swipe)
            {
                from.x += _holes[0].width * 0.3f;
                to.x -= _holes[0].width * 0.3f;
            }
            if (_gesture == Gesture.HoldDrag) to += Vector2.right * _holes[0].width;
            Vector2 tip = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
            bool pressed = _gesture == Gesture.Tap
                ? elapsed < _settings.GestureSeconds * _settings.HandTapPressedFraction
                : _gesture == Gesture.Hold ? elapsed < hold : elapsed < hold || t < 0.85f;
            _handImage.sprite = pressed ? _settings.HandPressedSprite : _settings.HandIdleSprite;
            var rect = _handImage.rectTransform;
            rect.anchorMin = rect.anchorMax = rectTransform.pivot;
            rect.pivot = pressed ? _settings.HandPressedTip : _settings.HandIdleTip;
            rect.sizeDelta = _settings.HandSize * (rectTransform.rect.width / HandReferenceWidth);
            rect.anchoredPosition = tip;
            rect.localScale = Vector3.one * (pressed && hold > 0f ? 0.88f : 1f);
        }

        // Creates the per-overlay spotlight material and uploads the current holes in local coordinates.
        private bool UseSpotlight()
        {
            if (_settings.SpotlightMaterial == null) return false;
            if (_spotlight == null)
            {
                _spotlight = new Material(_settings.SpotlightMaterial) { name = "TutorialSpotlight (Instance)" };
                material = _spotlight;
            }
            float scale = rectTransform.rect.width / HandReferenceWidth;
            int count = Mathf.Min(_holes.Count, MaxSpotlightHoles);
            for (int i = 0; i < count; i++)
                _spotlightHoles[i] = new Vector4(_holes[i].xMin, _holes[i].yMin, _holes[i].xMax, _holes[i].yMax);
            _spotlight.SetVectorArray(HolesId, _spotlightHoles);
            _spotlight.SetInt(HoleCountId, count);
            _spotlight.SetFloat(RadiusId, _settings.SpotlightCornerRadius * scale);
            _spotlight.SetFloat(FeatherId, _settings.SpotlightFeather * scale);
            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_spotlight != null) Destroy(_spotlight);
        }

        private static void Quad(VertexHelper vh, Rect r, Color color, bool localUv = false)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, localUv ? new Vector2(r.xMin, r.yMin) : OutsideSpotlight);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, localUv ? new Vector2(r.xMin, r.yMax) : OutsideSpotlight);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, localUv ? new Vector2(r.xMax, r.yMax) : OutsideSpotlight);
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, localUv ? new Vector2(r.xMax, r.yMin) : OutsideSpotlight);
            vh.AddTriangle(n, n+1, n+2); vh.AddTriangle(n, n+2, n+3);
        }
    }
}
