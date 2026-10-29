using System;
using UnityEngine;

namespace GGD.UI.Authoring
{
    /// <summary>Native Unity hierarchy exported for isolated visual editing, without game service components.</summary>
    public sealed class NativePrefabTemplate : MonoBehaviour
    {
        /// <summary>Stable authoring identities paired with serialized native transform references.</summary>
        public string[] Ids = Array.Empty<string>();
        /// <summary>Native transform references automatically remapped by Instantiate.</summary>
        public RectTransform[] Rects = Array.Empty<RectTransform>();
        /// <summary>Source animation target identity.</summary>
        public string AnimationId;
        /// <summary>Captured original entrance and exit timing.</summary>
        public float DurationIn, DurationOut, DelayIn, DelayOut;
        /// <summary>Captured original scale multipliers.</summary>
        public Vector2 StartMultiplier, EndMultiplier;
        /// <summary>Original easing sampled at export, never replaced with a newly designed effect.</summary>
        public AnimationCurve EaseIn, EaseOut;
    }
}
