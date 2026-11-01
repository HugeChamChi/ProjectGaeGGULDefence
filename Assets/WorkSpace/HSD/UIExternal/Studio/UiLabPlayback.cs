using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Seekable real-time visual experiments. Every evaluation starts from the edited resting pose.</summary>
    public sealed class UiLabPlayback : MonoBehaviour
    {
        private readonly List<(RectTransform Rect, Vector3 Scale, Vector2 Position)> _poses = new List<(RectTransform, Vector3, Vector2)>();
        private UiLabTemplate _template;
        private CanvasGroup _group;
        private RectTransform _target;
        private float _alpha;
        /// <summary>Normalized preview position.</summary>
        public float Progress { get; private set; } = 1;
        /// <summary>Whether the preview is advancing.</summary>
        public bool Playing { get; set; }
        /// <summary>Whether playback restarts at the end.</summary>
        public bool Loop { get; set; }
        /// <summary>Preview-only playback rate.</summary>
        public float Speed { get; set; } = 1;
        /// <summary>Experiment kind: pop, slide, fade, press, emphasize, stagger.</summary>
        public int Kind { get; set; }
        /// <summary>Experiment duration in seconds.</summary>
        public float Seconds { get; set; } = .35f;
        /// <summary>Experiment delay in seconds.</summary>
        public float Delay { get; set; }
        /// <summary>Starting scale or press depth.</summary>
        public float StartScale { get; set; } = .8f;
        /// <summary>Experiment travel distance in reference UI units.</summary>
        public float Distance { get; set; } = 160;
        /// <summary>Stagger between visual items.</summary>
        public float Stagger { get; set; } = .1f;
        /// <summary>Experiment ease: linear, smooth, cubic out, back out.</summary>
        public int Ease { get; set; } = 3;
        /// <summary>Use captured original popup scale, when available.</summary>
        public bool Original { get; set; }
        /// <summary>Whether this is an exit preview.</summary>
        public bool Closing { get; set; }
        /// <summary>Captures immutable resting poses after style and state application.</summary>
        public void Configure(UiLabTemplate template, RectTransform target)
        {
            _template = template; _target = target != null ? target : template.transform as RectTransform;
            _group = template.GetComponent<CanvasGroup>() ?? template.gameObject.AddComponent<CanvasGroup>(); _alpha = _group.alpha;
            _poses.Clear(); foreach (var rect in template.Rects) _poses.Add((rect, rect.localScale, rect.anchoredPosition));
        }
        private void Update()
        {
            if (!Playing) return;
            float duration = TotalSeconds;
            float next = Progress + Time.unscaledDeltaTime * Speed / Mathf.Max(.01f, duration);
            if (next >= 1) { if (Loop) next %= 1; else { next = 1; Playing = false; } }
            Seek(next);
        }
        private float TotalSeconds => Original ? (Closing ? _template.OutDelay + _template.OutSeconds : _template.InDelay + _template.InSeconds) : Delay + Seconds + (Kind == 5 ? Stagger * 2 : 0);
        /// <summary>Restores all transformed properties without changing experiment documents.</summary>
        public void Stop() { Playing = false; Restore(); Progress = 1; }
        private void Restore() { foreach (var pose in _poses) if (pose.Rect != null) { pose.Rect.localScale = pose.Scale; pose.Rect.anchoredPosition = pose.Position; } if (_group != null) _group.alpha = _alpha; }
        /// <summary>Evaluates the same deterministic pose for play, pause, A/B and seeking.</summary>
        public void Seek(float progress)
        {
            Progress = Mathf.Clamp01(progress); Restore(); if (_template == null || _target == null) return;
            float elapsed = Progress * TotalSeconds;
            if (Original)
            {
                if (_template.MotionTarget == null) return;
                float duration = Closing ? _template.OutSeconds : _template.InSeconds, delay = Closing ? _template.OutDelay : _template.InDelay;
                float t = Mathf.Clamp01((elapsed - delay) / Mathf.Max(.0001f, duration));
                float eased = (Closing ? _template.OutEase : _template.InEase).Evaluate(t);
                var multiplier = Closing ? _template.OutScale : _template.InScale;
                var target = _template.MotionTarget;
                var factor = Closing ? Vector2.LerpUnclamped(Vector2.one, multiplier, eased) : Vector2.LerpUnclamped(multiplier, Vector2.one, eased);
                target.localScale = Vector3.Scale(target.localScale, new Vector3(factor.x, factor.y, 1)); return;
            }
            float raw = Mathf.Clamp01((elapsed - Delay) / Mathf.Max(.01f, Seconds)); float p = Closing ? 1 - raw : raw; float e = Eased(p);
            switch (Kind)
            {
                case 0: _target.localScale *= Mathf.LerpUnclamped(StartScale, 1, e); _group.alpha = _alpha * Mathf.Clamp01(p * 3); break;
                case 1: _target.anchoredPosition += new Vector2(0, Distance * (1 - e)); _group.alpha = _alpha * p; break;
                case 2: _group.alpha = _alpha * p; break;
                case 3: _target.localScale *= 1 - (1 - StartScale) * Mathf.Sin(raw * Mathf.PI); break;
                case 4: _target.localScale *= 1 + .12f * Mathf.Sin(raw * Mathf.PI); break;
                case 5:
                    int index = 0;
                    foreach (var pose in _poses)
                        if (pose.Rect.name.StartsWith("LabCard_") || pose.Rect.name.StartsWith("Band_"))
                        { float step = Mathf.Clamp01((elapsed - Delay - index++ * Stagger) / Mathf.Max(.01f, Seconds)); pose.Rect.localScale = pose.Scale * Mathf.LerpUnclamped(StartScale, 1, Eased(Closing ? 1 - step : step)); }
                    break;
            }
        }
        private float Eased(float p) { if (Ease == 0) return p; if (Ease == 1) return p * p * (3 - 2 * p); if (Ease == 2) return 1 - Mathf.Pow(1 - p, 3); float q = p - 1; return 1 + 2.70158f * q * q * q + 1.70158f * q * q; }
        private void OnDisable() => Stop();
    }
}
