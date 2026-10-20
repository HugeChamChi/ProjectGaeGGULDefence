using System.Collections.Generic;

/// <summary>Isolated preview state. Exchanges preserve item identity and need no spare storage slot.</summary>
public sealed class TotemInventoryLabState
{
    /// <summary>A lab item retains its authored source and orientation through recall.</summary>
    public sealed class Entry
    {
        /// <summary>Unmodified authored configuration.</summary>
        public readonly TotemData Data;
        /// <summary>Quarter turns clockwise.</summary>
        public int Rotation;
        /// <summary>Creates a sample item from authored data.</summary>
        public Entry(TotemData data) { Data = data; }
    }

    private readonly List<Entry> _stored = new List<Entry>();
    private readonly IReadOnlyList<Entry> _storedView;
    private readonly Entry[] _field;
    private readonly TotemInventoryLabSettings _settings;
    /// <summary>Board contents, indexed left to right then bottom to top.</summary>
    public IReadOnlyList<Entry> Field => _field;
    /// <summary>Storage contents in visible slot order.</summary>
    public IReadOnlyList<Entry> Stored => _storedView;
    /// <summary>Creates a preview board with no dependencies on the live run.</summary>
    public TotemInventoryLabState(TotemInventoryLabSettings settings)
    {
        _settings = settings;
        _field = new Entry[settings.Columns * settings.Rows];
        _storedView = _stored.AsReadOnly();
    }
    /// <summary>Restores two field samples and a normal or full inventory.</summary>
    public void Reset(bool full)
    {
        System.Array.Clear(_field, 0, _field.Length);
        _stored.Clear();
        if (_settings.Samples == null || _settings.Samples.Length == 0) return;
        _field[_field.Length / 2] = new Entry(_settings.Samples[0]);
        if (_field.Length > 1) _field[0] = new Entry(_settings.Samples[1 % _settings.Samples.Length]);
        int count = full ? _settings.Capacity : System.Math.Min(3, _settings.Capacity);
        for (int i = 0; i < count; i++) _stored.Add(new Entry(_settings.Samples[(i + 2) % _settings.Samples.Length]));
    }
    /// <summary>Recalls once, preserving orientation. Full storage leaves both sides untouched.</summary>
    public bool Recall(int cell)
    {
        if (!ValidCell(cell) || _field[cell] == null || _stored.Count >= _settings.Capacity) return false;
        _stored.Add(_field[cell]);
        _field[cell] = null;
        return true;
    }
    /// <summary>Places into an empty cell or exchanges with its existing item atomically.</summary>
    public bool Place(int slot, int cell)
    {
        if (slot < 0 || slot >= _stored.Count || !ValidCell(cell)) return false;
        var incoming = _stored[slot];
        if (_field[cell] == null) _stored.RemoveAt(slot);
        else _stored[slot] = _field[cell];
        _field[cell] = incoming;
        return true;
    }
    /// <summary>Moves between cells, exchanging their items if occupied.</summary>
    public bool Move(int from, int to)
    {
        if (!ValidCell(from) || !ValidCell(to) || from == to || _field[from] == null) return false;
        var other = _field[to];
        _field[to] = _field[from];
        _field[from] = other;
        return true;
    }
    private bool ValidCell(int cell) => cell >= 0 && cell < _field.Length;
}
