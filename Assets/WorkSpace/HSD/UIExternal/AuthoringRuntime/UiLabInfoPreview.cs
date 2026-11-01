using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.Authoring
{
    /// <summary>Portable brief/detailed information-window presentation, without game services.</summary>
    public sealed class UiLabInfoPreview : MonoBehaviour
    {
        /// <summary>The native Unity switch and its captured display references.</summary>
        public Toggle Toggle;
        /// <summary>Original generated track and thumb, with persistent bundled sprites.</summary>
        public Image Track;
        /// <summary>Original thumb transform.</summary>
        public RectTransform Knob;
        /// <summary>Original brief and detailed labels.</summary>
        public TMP_Text[] Labels;
        /// <summary>Description text populated by the original presenter.</summary>
        public TMP_Text Description;
        /// <summary>Captured states produced by the original view and presenter.</summary>
        public Mode Brief, Detailed;
        private bool _lastState;

        private void OnEnable() { if (Toggle != null) _lastState = Toggle.isOn; }
        private void LateUpdate()
        {
            if (_lastState == Toggle.isOn) return;
            SetDetailed(Toggle.isOn);
        }

        /// <summary>Changes the preview state without dispatching any game callbacks.</summary>
        public void SetDetailed(bool detailed)
        {
            _lastState = detailed; Toggle.SetIsOnWithoutNotify(detailed);
            var mode = detailed ? Detailed : Brief;
            Description.text = mode.Description;
            foreach (var pose in mode.Layout) pose.Apply();
            ApplyVisual(mode);
        }

        private void ApplyVisual(Mode mode)
        {
            Track.color = mode.TrackColor;
            Knob.anchoredPosition = mode.KnobPosition;
            for (int i = 0; i < Labels.Length; i++)
            {
                Labels[i].color = mode.LabelColors[i];
                Labels[i].fontStyle = mode.LabelStyles[i];
            }
        }

        /// <summary>A visual state captured from the actual game view in an isolated editor preview.</summary>
        [Serializable]
        public sealed class Mode
        {
            public string Description;
            public Color TrackColor;
            public Vector2 KnobPosition;
            public Color[] LabelColors;
            public FontStyles[] LabelStyles;
            public RectPose[] Layout = Array.Empty<RectPose>();
        }

        /// <summary>Only transforms resized by the original information-window description fitting.</summary>
        [Serializable]
        public sealed class RectPose
        {
            public RectTransform Rect;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            /// <summary>Restores the captured mode geometry.</summary>
            public void Apply()
            {
                Rect.anchorMin = AnchorMin; Rect.anchorMax = AnchorMax; Rect.pivot = Pivot;
                Rect.anchoredPosition = Position; Rect.sizeDelta = Size;
            }
        }
    }
}
