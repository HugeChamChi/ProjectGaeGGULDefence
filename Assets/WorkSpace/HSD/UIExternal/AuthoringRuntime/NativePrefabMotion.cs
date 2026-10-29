using UnityEngine;

namespace GGD.UI.Authoring
{
    /// <summary>Preview-only playback which never writes transient animation values to authoring documents.</summary>
    public sealed class NativePrefabMotion : MonoBehaviour
    {
        private NativePrefabTemplate _source;
        private RectTransform _target;
        private Vector3 _origin;
        private float _elapsed;
        private bool _opening;
        /// <summary>Whether a preview is currently playing.</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>Connects the original animation profile to the edited native target.</summary>
        public void Configure(NativePrefabTemplate source, RectTransform target) { _source = source; _target = target; }
        /// <summary>Replays opening or closing from the edited resting pose.</summary>
        public void Play(bool opening)
        {
            Stop(); if (_source == null || _target == null) return;
            _origin = _target.localScale; _elapsed = 0; _opening = opening; IsPlaying = true; Sample(0);
        }
        private void Update()
        {
            if (!IsPlaying) return;
            _elapsed += Time.unscaledDeltaTime;
            float delay = _opening ? _source.DelayIn : _source.DelayOut;
            float duration = _opening ? _source.DurationIn : _source.DurationOut;
            Sample(Mathf.Clamp01((_elapsed - delay) / Mathf.Max(.00001f, duration)));
            if (_elapsed >= delay + duration) Stop();
        }
        private void Sample(float t)
        {
            var multiplier = _opening ? _source.StartMultiplier : _source.EndMultiplier;
            var end = new Vector3(_origin.x * multiplier.x, _origin.y * multiplier.y, _origin.z);
            float eased = (_opening ? _source.EaseIn : _source.EaseOut).Evaluate(t);
            _target.localScale = _opening ? Vector3.LerpUnclamped(end, _origin, eased) : Vector3.LerpUnclamped(_origin, end, eased);
        }
        /// <summary>Stops playback and restores the edited pose, including when the preview is replaced.</summary>
        public void Stop() { if (IsPlaying && _target != null) _target.localScale = _origin; IsPlaying = false; }
        private void OnDisable() => Stop();
    }
}
