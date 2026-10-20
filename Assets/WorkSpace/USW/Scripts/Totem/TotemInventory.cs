using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Scene-scoped storage. Entries are consumed only after successful placement.</summary>
public sealed class TotemInventory
{
    private readonly List<TotemData> _items = new List<TotemData>();
    private readonly List<int> _rotations = new List<int>();
    private readonly IReadOnlyList<TotemData> _view;
    private readonly TotemSpawner _spawner;
    private readonly TotemInteractionSettings _settings;
    private bool _placing;
    /// <summary>Optional scene-owned placement restriction; unset during normal play.</summary>
    public Func<int, GridCell, bool> CanPlace { get; set; }
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
        _rotations.Add(0);
        OnChanged?.Invoke();
        return true;
    }
    /// <summary>Stored orientation, retained when a field totem is recalled.</summary>
    public int GetRotationStep(int index) => index >= 0 && index < _rotations.Count ? _rotations[index] : 0;

    /// <summary>Accepts an empty usable cell or an existing totem, never a unit or sealed cell.</summary>
    public bool CanPlaceAt(int index, GridCell cell)
        => index >= 0 && index < _items.Count && cell != null && cell.Model != null && cell.Model.IsAvailable
            && cell.OccupyingUnit == null && (cell.OccupyingTotem == null ||
                cell.OccupyingTotem.IsPlaced && cell.OccupyingTotem.CurrentCell == cell)
            && (CanPlace == null || CanPlace(index, cell));

    /// <summary>Recalls to storage without currency. Full storage and guided placement leave the field unchanged.</summary>
    public bool TryRecall(TotemBase totem)
    {
        if (_placing || CanPlace != null || _items.Count >= Capacity || totem == null || !totem.IsPlaced
            || totem.CurrentCell == null || totem.CurrentCell.OccupyingTotem != totem || totem.DamageSourceData == null) return false;
        var data = totem.DamageSourceData;
        int rotation = totem.RotationStep;
        if (!_spawner.RemoveTotemFromField(totem)) return false;
        _items.Add(data);
        _rotations.Add(rotation);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Places or exchanges atomically, preserving storage on cancellation, load failure or invalid targets.</summary>
    public async UniTask<bool> TryPlaceAsync(int index, GridCell cell, CancellationToken token)
    {
        if (_placing || !CanPlaceAt(index, cell)) return false;
        var outgoing = cell.OccupyingTotem;
        var outgoingData = outgoing != null ? outgoing.DamageSourceData : null;
        if (outgoing != null && outgoingData == null) return false;
        _placing = true;
        try
        {
            if (!await _spawner.PlaceOrExchangeTotemAsync(_items[index], _rotations[index], cell, outgoing,
                () => CanPlaceAt(index, cell), token)) return false;
            if (outgoingData != null)
            {
                _items[index] = outgoingData;
                _rotations[index] = outgoing.RotationStep;
            }
            else
            {
                _items.RemoveAt(index);
                _rotations.RemoveAt(index);
            }
            OnChanged?.Invoke();
            return true;
        }
        finally { _placing = false; }
    }
}
