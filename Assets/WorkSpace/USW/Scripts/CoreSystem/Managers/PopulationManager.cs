using UnityEngine;

/// <summary>
/// 인구수 추적 — 현재 배치 인구 / 최대 인구 관리
///
/// 유닛: UnitBase.OnPlaced/OnRemoved 에서 자동 호출
/// 토템: TotemBase.OnPlaced/OnRemoved 에서 동일하게 Add/Remove 호출하면 확장 가능
/// </summary>
public class PopulationManager : InGameSingleton<PopulationManager>
{
    [SerializeField] private GameConfig _config;

    public int Current    { get; private set; }
    public int MaxBonus   { get; private set; }
    public int Max        => (_config != null ? _config.maxPopulation : 21) + MaxBonus;

    /// <summary>레벨업 선택지(풍요로운 영토 등)가 최대 인구수를 증가시킬 때 호출</summary>
    public void AddMaxBonus(int amount)
    {
        MaxBonus += amount;
        OnPopulationChanged?.Invoke(Current, Max);
    }

    public event System.Action<int, int> OnPopulationChanged;

    public bool CanAdd(int cost = 1) => Current + cost <= Max;

    public void Add(int cost)
    {
        Current += cost;
        OnPopulationChanged?.Invoke(Current, Max);
    }

    public void Remove(int cost)
    {
        Current = Mathf.Max(0, Current - cost);
        OnPopulationChanged?.Invoke(Current, Max);
    }
}
