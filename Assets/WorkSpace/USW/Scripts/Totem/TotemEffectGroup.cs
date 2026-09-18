using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One independently colored effect and its own application range.</summary>
[Serializable]
public sealed class TotemEffectGroup
{
    /// <summary>Short legend label, e.g. attack or attack speed.</summary>
    public string Label;
    /// <summary>Description shown in this group's color.</summary>
    [TextArea] public string Description;
    /// <summary>Shared color for text, miniature range and world stripes.</summary>
    public Color Color = new Color(1f, 0.5f, 0.05f, 1f);
    /// <summary>Functions applied only inside this group's range.</summary>
    [SerializeReference, SelectableReference] public List<ITotemFunction> Functions = new List<ITotemFunction>();
    /// <summary>Range rules for this group.</summary>
    [SerializeReference, SelectableReference] public List<ITotemRange> Ranges = new List<ITotemRange>();
    /// <summary>Collects unique affected cells using the same rules as gameplay.</summary>
    public List<GridCell> GetCells(TotemBase totem, GridManager grid)
    {
        var cells = new List<GridCell>();
        foreach (var range in Ranges)
            if (range != null) foreach (var cell in range.GetCells(totem, grid))
                if (cell != null && !cells.Contains(cell)) cells.Add(cell);
        return cells;
    }
    /// <summary>Offsets for card and info-panel diagrams.</summary>
    public List<Vector2Int> GetPreviewOffsets()
    {
        var offsets = new List<Vector2Int>();
        foreach (var range in Ranges)
            if (range != null) foreach (var offset in range.GetPreviewOffsets())
                if (!offsets.Contains(offset)) offsets.Add(offset);
        return offsets;
    }
}
