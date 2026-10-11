using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Description Term Catalog")]
public sealed class DescriptionTermCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string Id;
        public string DisplayName;
        public Color Color = new Color(0.46f, 0.26f, 0.69f);
        [TextArea(3, 12)] public string Body;
    }
    public List<Entry> Entries = new List<Entry>();
}

public sealed class DescriptionTermResolver : IDescriptionTermResolver
{
    private readonly DescriptionTermCatalog _catalog;
    private readonly DebuffInfoPresenter _debuffs;
    private readonly DebuffBinding _binding;
    public DescriptionTermResolver(DescriptionTermCatalog catalog, DebuffInfoPresenter debuffs = null, DebuffBinding binding = default)
    { _catalog = catalog; _debuffs = debuffs; _binding = binding; }
    public bool TryResolve(string id, out DescriptionTermModel term)
    {
        term = null;
        if (id.StartsWith("debuff:") && int.TryParse(id.Substring(7), out int debuffId) &&
            _debuffs != null && _debuffs.TryBuild(debuffId, _binding, out var model))
        { term = new DescriptionTermModel(model.Title, new Color(0.46f, 0.26f, 0.69f), model.Body); return true; }
        if (_catalog == null) return false;
        foreach (var entry in _catalog.Entries)
            if (entry != null && entry.Id == id)
            { term = new DescriptionTermModel(entry.DisplayName, entry.Color, entry.Body); return true; }
        return false;
    }
}
