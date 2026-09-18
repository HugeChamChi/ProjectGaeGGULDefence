using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Scene-scoped storage. Entries are consumed only after successful placement.</summary>
public sealed class TotemInventory
{
    private readonly List<TotemData> _items = new List<TotemData>();
    private readonly IReadOnlyList<TotemData> _view;
    private readonly TotemSpawner _spawner;
    private readonly TotemInteractionSettings _settings;
    private bool _placing;
    /// <summary>Fires when storage contents change.</summary>
    public event Action OnChanged;
    /// <summary>Oldest entry first; view places it at the right edge.</summary>
    public IReadOnlyList<TotemData> Items => _view;
    /// <summary>Configured capacity, at most five.</summary>
    public int Capacity => _settings.Capacity;
    /// <summary>Receives the scene placement service.</summary>
    public TotemInventory(TotemSpawner spawner, TotemInteractionSettings settings)
    { _spawner = spawner; _settings = settings; _view = _items.AsReadOnly(); }
    /// <summary>Adds a reward, or returns false when full.</summary>
    public bool TryAdd(TotemData data)
    {
        if (data == null || _items.Count >= Capacity) return false;
        _items.Add(data);
        OnChanged?.Invoke();
        return true;
    }
    /// <summary>Serializes placements and preserves the entry on cancellation or invalid cells.</summary>
    public async UniTask<bool> TryPlaceAsync(int index, GridCell cell, CancellationToken token)
    {
        if (_placing || index < 0 || index >= _items.Count || cell == null || !cell.IsAvailable) return false;
        _placing = true;
        try
        {
            if (!await _spawner.PlaceTotemAtCellAsync(_items[index], cell, token)) return false;
            _items.RemoveAt(index);
            OnChanged?.Invoke();
            return true;
        }
        finally { _placing = false; }
    }
}
