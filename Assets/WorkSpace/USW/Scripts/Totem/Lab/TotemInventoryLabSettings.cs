using UnityEngine;

/// <summary>Visual timing and authored sample data for the isolated inventory interaction lab.</summary>
[CreateAssetMenu(menuName = "Game/Lab/Totem Inventory")]
public sealed class TotemInventoryLabSettings : ScriptableObject
{
    /// <summary>Authored totems used by the preview; never modified by the lab.</summary>
    public TotemData[] Samples;
    /// <summary>Number of board columns.</summary>
    [Min(1)] public int Columns = 5;
    /// <summary>Number of board rows.</summary>
    [Min(1)] public int Rows = 5;
    /// <summary>Storage limit used by recall and exchange.</summary>
    [Range(1, 5)] public int Capacity = 5;
    /// <summary>Unscaled travel time for each transfer.</summary>
    [Min(.01f)] public float TransferSeconds = .24f;
    /// <summary>Small upward bend during a transfer, in canvas units.</summary>
    [Min(0f)] public float TransferLift = 34f;
    /// <summary>Unscaled settling time after arrival.</summary>
    [Min(.01f)] public float SettleSeconds = .13f;
    /// <summary>Arrival scale used by the restrained pop.</summary>
    [Range(.5f, 1f)] public float ArrivalScale = .88f;
}
