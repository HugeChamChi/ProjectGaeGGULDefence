using System.Collections.Generic;

/// <summary>LevelUpManager가 소유하는 런별 획득 상태. 카드 SO는 변경하지 않는다.</summary>
public sealed class DroneSelectionState
{
    private readonly Dictionary<int, DroneSelectionEffect> _effects = new();
    private readonly Dictionary<int, float> _rolls = new();
    /// <summary>배치된 소환자가 선택지 획득/해제를 즉시 반영한다.</summary>
    public event System.Action OnChanged;
    /// <summary>표시 전에 정한 값을 같은 런에서 재사용한다. SO에 기록하지 않는다.</summary>
    public float PreviewValue(LevelUpData card)
    {
        var effect = card.droneEffect;
        if (effect == null) return 0;
        if (effect.Kind != DroneSelectionKind.DeltanDamageTaken) return effect.Value;
        if (!_rolls.TryGetValue(card.chooseId, out var value))
        {
            int min = UnityEngine.Mathf.RoundToInt(effect.Value * 100);
            int max = UnityEngine.Mathf.Max(min, UnityEngine.Mathf.RoundToInt(effect.MaxValue * 100));
            value = UnityEngine.Random.Range(min, max + 1) / 100f;
            _rolls.Add(card.chooseId, value);
        }
        return value;
    }
    /// <summary>카드 획득 설정을 런 상태로 복사한다.</summary>
    public void Add(LevelUpData card)
    {
        var e = card.droneEffect;
        if (e == null || e.Kind == DroneSelectionKind.None) return;
        _effects[card.chooseId] = new DroneSelectionEffect { Kind=e.Kind, Interval=e.Interval,
            Value=PreviewValue(card), MaxValue=e.MaxValue, Count=e.Count };
        OnChanged?.Invoke();
    }
    /// <summary>해당 카드의 효과를 제거한다.</summary>
    public void Remove(int id)
    {
        if (_effects.Remove(id)) OnChanged?.Invoke();
    }
    /// <summary>동일 기능의 카드는 풀에 하나씩 등록한다.</summary>
    public DroneSelectionEffect Get(DroneSelectionKind kind)
    {
        foreach (var effect in _effects.Values) if (effect.Kind == kind) return effect;
        return null;
    }
}
