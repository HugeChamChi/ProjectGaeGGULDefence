using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public interface IDescriptionSource { DescriptionSnapshot Capture(); }
public interface IDescriptionTermResolver { bool TryResolve(string id, out DescriptionTermModel term); }
public interface IDescriptionPopup
{
    bool BlocksOwnerInput { get; }
    void Show(string title, string body);
    void Close();
    void HideImmediately();
}

public sealed class DescriptionSnapshot
{
    public string Detailed { get; }
    public string Simple { get; }
    public IReadOnlyDictionary<string, string> Values { get; }
    public DescriptionSnapshot(string detailed, string simple, IDictionary<string, string> values = null)
    {
        Detailed = detailed ?? string.Empty;
        Simple = simple ?? string.Empty;
        Values = new ReadOnlyDictionary<string, string>(values == null
            ? new Dictionary<string, string>() : new Dictionary<string, string>(values));
    }
}

public sealed class DescriptionTermModel
{
    public string DisplayName { get; }
    public Color Color { get; }
    public string Body { get; }
    public DescriptionTermModel(string name, Color color, string body)
    { DisplayName = name; Color = color; Body = body; }
}

public sealed class DescriptionDisplaySettings
{
    public const string PreferenceKey = "UI.Description.Detailed";
    public bool Detailed { get; private set; } = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
    public event Action Changed;
    public void SetDetailed(bool detailed)
    {
        if (Detailed == detailed) return;
        Detailed = detailed;
        PlayerPrefs.SetInt(PreferenceKey, detailed ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
