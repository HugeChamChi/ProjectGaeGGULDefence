using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 스프라이트를 어둡게 했다가 원래 색으로 되돌리는 유틸.
/// Dim 시점의 색을 기억해 두었다가 RestoreAll에서 그대로 복원한다 (파괴된 렌더러는 건너뜀).
/// </summary>
public sealed class UnitDimmer
{
    /// <summary>기본 딤 색 (스프라이트 색에 곱해짐).</summary>
    public static readonly Color DefaultDimColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private readonly Dictionary<SpriteRenderer, Color> _dimmed = new Dictionary<SpriteRenderer, Color>();
    private readonly List<SpriteRenderer> _buffer = new List<SpriteRenderer>();

    /// <summary>현재 어둡게 된 스프라이트가 있는지.</summary>
    public bool IsActive => _dimmed.Count > 0;

    /// <summary>unit의 모든 자식 스프라이트를 dimColor를 곱해 어둡게 한다. 이미 어둡게 된 스프라이트는 건너뛴다.</summary>
    public void Dim(UnitBase unit, Color dimColor)
    {
        if (unit == null) return;
        unit.GetComponentsInChildren(_buffer);
        foreach (var sr in _buffer)
        {
            if (sr == null || _dimmed.ContainsKey(sr)) continue;
            _dimmed[sr] = sr.color;
            sr.color = sr.color * dimColor;
        }
        _buffer.Clear();
    }

    /// <summary>어둡게 한 스프라이트를 모두 원래 색으로 되돌린다.</summary>
    public void RestoreAll()
    {
        foreach (var pair in _dimmed)
            if (pair.Key != null) pair.Key.color = pair.Value;
        _dimmed.Clear();
    }
}
