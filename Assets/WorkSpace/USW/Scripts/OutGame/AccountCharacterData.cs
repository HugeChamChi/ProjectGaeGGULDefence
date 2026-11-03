using System;
using System.Collections.Generic;
using System.IO;

/// <summary>Units and chiefs share this ownership/growth record; definitions remain in master assets.</summary>
[Serializable]
public sealed class AccountCharacterData : AccountDocumentData
{
    [Serializable] public sealed class Entry { public string Id; public int Count; public int Level = 1; }
    public List<Entry> Characters = new();
    // Preserve the existing upgrade prototype economy; it is not PlayerData.Gold.
    public int UpgradeBalance = 12000;
    public bool LegacyOwnershipImported;
    public static void Validate(AccountCharacterData data)
    {
        if (data.SchemaVersion != 1 || data.Characters == null || data.UpgradeBalance < 0) throw new InvalidDataException("Invalid characters.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in data.Characters)
            if (entry == null || string.IsNullOrEmpty(entry.Id) || !ids.Add(entry.Id) || entry.Count < 0 || entry.Level < 1)
                throw new InvalidDataException("Invalid character entry.");
    }
}
