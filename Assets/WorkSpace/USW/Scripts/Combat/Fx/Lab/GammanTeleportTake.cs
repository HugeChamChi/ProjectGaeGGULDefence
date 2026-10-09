using System;
using UnityEngine;

/// <summary>Authored choreography, rather than impact decoration, for one teleport attack take.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Gamman Teleport Take")]
public sealed class GammanTeleportTake : ScriptableObject
{
    /// <summary>One timed pose or movement within a visual-only take.</summary>
    [Serializable]
    public sealed class Beat
    {
        public string Label;
        public float Duration = .1f;
        public bool AtHome;
        public Vector3 From;
        public Vector3 To;
        public bool Body = true;
        public bool Fire;
        public bool Snap;
        public bool HomeGhost;
        public float Charge;
        public float Squeeze;
    }
    /// <summary>Comparison button label.</summary>
    public string Label;
    /// <summary>Choreography description.</summary>
    public string Description;
    /// <summary>Ordered visual-only beats.</summary>
    public Beat[] Beats;
    /// <summary>Beam thickness; all takes share the same attack art.</summary>
    public float BeamWidth = .24f;
    /// <summary>Shared accent colour.</summary>
    public Color Tint = new Color(1f, .86f, .2f);
}
