using System;
using UnityEngine;

/// <summary>Explicit console chart-to-currency mappings. Unconfigured items cannot be claimed.</summary>
[CreateAssetMenu(menuName = "Game/Mail Reward Mapping", fileName = "MailRewardConfig")]
public sealed class MailRewardConfig : ScriptableObject
{
    /// <summary>Authored mappings; chart and identifier must both match.</summary>
    public Entry[] Entries = Array.Empty<Entry>();
    /// <summary>One approved backend chart row.</summary>
    [Serializable]
    public sealed class Entry
    {
        public string ChartName;
        public string IdColumn = "itemID";
        public string ItemId;
        public Currency Currency;
    }
    /// <summary>Supported persisted account balances.</summary>
    public enum Currency { Gold, Diamond, Stamina }
}
