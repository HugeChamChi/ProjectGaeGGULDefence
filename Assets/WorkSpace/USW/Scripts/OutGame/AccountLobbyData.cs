using System;
using System.Collections.Generic;
using System.IO;
[Serializable]
public sealed class AccountLobbyData : AccountDocumentData
{
    public string LastParty = "";
    public List<string> CompletedTutorials = new();
    public List<string> ReadMail = new();
    public static void Validate(AccountLobbyData data)
    {
        if (data.SchemaVersion != 1 || data.CompletedTutorials == null || data.ReadMail == null || data.LastParty == null)
            throw new InvalidDataException("Invalid lobby progress.");
        ValidateIds(data.CompletedTutorials); ValidateIds(data.ReadMail);
    }
    private static void ValidateIds(List<string> values)
    { var ids = new HashSet<string>(); foreach (var id in values) if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidDataException("Invalid progress ID."); }
}
