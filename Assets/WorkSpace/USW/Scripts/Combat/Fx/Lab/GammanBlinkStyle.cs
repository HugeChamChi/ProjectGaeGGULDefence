using UnityEngine;

/// <summary>Authored presentation-only settings for the Gamman teleport experiment.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Gamman Blink Style")]
public sealed class GammanBlinkStyle : ScriptableObject
{
    /// <summary>Label displayed in the comparison lab.</summary>
    public string Label;
    /// <summary>Short description of the visual direction.</summary>
    public string Description;
    /// <summary>Teleport accent colour.</summary>
    public Color Tint = new Color(1f, .86f, .2f);
    /// <summary>World offset from the boss to the airborne drone.</summary>
    public Vector3 AirOffset = new Vector3(0f, 2.7f, -.4f);
    /// <summary>Command anticipation duration.</summary>
    public float CommandTime = .35f;
    /// <summary>Departure and arrival duration.</summary>
    public float BlinkTime = .2f;
    /// <summary>Invisible interval between endpoints.</summary>
    public float TransitTime = .1f;
    /// <summary>Ring size.</summary>
    public float RingSize = 1.2f;
    /// <summary>Squash the ring into a portal.</summary>
    public bool Portal;
    /// <summary>Leave short silhouettes at each endpoint.</summary>
    public bool Afterimages;
    /// <summary>Boss-side detonation motif: nodes, implosion, circuit, fracture, blossom.</summary>
    [Range(0, 4)] public int DetonationPattern;
    /// <summary>Colour of implanted hacking marks.</summary>
    public Color HackTint = new Color(.2f, 1f, .85f);
    /// <summary>Time from shot start until marks rupture (after beam contact at 0.10 seconds).</summary>
    public float BurstDelay = .32f;
    /// <summary>Time for boss-side detonation to settle.</summary>
    public float BurstDuration = .75f;
}
