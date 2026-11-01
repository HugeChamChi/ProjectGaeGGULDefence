using System;
using UnityEngine;

namespace GGD.UI.Authoring
{
    /// <summary>Portable visual snapshot with stable identities; no game services are carried into the lab.</summary>
    public sealed class UiLabTemplate : MonoBehaviour
    {
        /// <summary>Screen identity and display name.</summary>
        public string Key, Title, Category, Source;
        /// <summary>Original transform identities and references.</summary>
        public string[] Ids = Array.Empty<string>();
        /// <summary>Transforms corresponding to Ids.</summary>
        public RectTransform[] Rects = Array.Empty<RectTransform>();
        /// <summary>Captured original popup animation, when one exists.</summary>
        public RectTransform MotionTarget;
        /// <summary>Original popup durations.</summary>
        public float InSeconds, OutSeconds, InDelay, OutDelay;
        /// <summary>Original scale multipliers.</summary>
        public Vector2 InScale = Vector2.one, OutScale = Vector2.one;
        /// <summary>Original sampled easing curves.</summary>
        public AnimationCurve InEase = AnimationCurve.Linear(0, 0, 1, 1), OutEase = AnimationCurve.Linear(0, 0, 1, 1);
    }
}
