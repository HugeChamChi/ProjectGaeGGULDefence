using System;
using System.Collections.Generic;

/// <summary>UI와 분리된 종류별 최종 피해 집계. 피해 입력자는 실제 적용된 양만 전달한다.</summary>
public sealed class DamageContributionLedger
{
    private readonly Dictionary<(UnitTribe, int, UnitData), DamageContributionEntry> _byKind =
        new Dictionary<(UnitTribe, int, UnitData), DamageContributionEntry>();
    private readonly List<DamageContributionEntry> _entries = new List<DamageContributionEntry>();
    private readonly Dictionary<(Type, int, UnityEngine.Object), DamageContributionEntry> _otherSources =
        new Dictionary<(Type, int, UnityEngine.Object), DamageContributionEntry>();
    private readonly double _window;

    /// <summary>SO에서 최근 DPS 집계 창을 받는다.</summary>
    public DamageContributionLedger(DamageChartLabSettings settings)
    {
        if (settings == null || settings.RecentDpsWindow <= 0)
            throw new ArgumentException("Positive DPS window is required.");
        _window = settings.RecentDpsWindow;
    }
    /// <summary>발견된 유닛 종류. 개체별 행을 생성하지 않는다.</summary>
    public IReadOnlyList<DamageContributionEntry> Entries => _entries;
    /// <summary>모든 유닛·토템·족장의 누적 최종 피해.</summary>
    public decimal TotalDamage { get; private set; }
    /// <summary>일시정지를 제외한 누적 전투 시간.</summary>
    public double CombatSeconds { get; private set; }
    /// <summary>승패가 확정되어 이번 런의 피해와 전투 시간이 고정된 상태.</summary>
    public bool IsFinalized { get; private set; }
    /// <summary>최근 전체 DPS.</summary>
    public decimal RecentDps
    {
        get { decimal result = 0m; foreach (var entry in _entries) result += entry.RecentDps(CombatSeconds, _window); return result; }
    }

    /// <summary>같은 종류의 현재 배치 수를 설정한다. 판매/합성 뒤에도 누적 피해는 남는다.</summary>
    public void SetUnitCount(UnitData unit, int count) => GetOrCreate(unit).UnitCount = Math.Max(0, count);
    /// <summary>최종 피해를 종류별로 합산한다. 피해량이 0 이하인 호출은 무시한다.</summary>
    public void RecordDamage(UnityEngine.Object source, decimal appliedDamage)
    {
        if (IsFinalized || source == null || appliedDamage <= 0m) return;
        DamageContributionEntry entry;
        if (source is UnitData unit) entry = GetOrCreate(unit);
        else
        {
            int id = source is TotemData totem ? totem.totemId : source is ChieftainData chief ? chief.ChieftainId : 0;
            var key = (source.GetType(), id, id > 0 ? null : source);
            if (!_otherSources.TryGetValue(key, out entry))
            {
                entry = new DamageContributionEntry(source);
                _otherSources.Add(key, entry); _entries.Add(entry);
            }
        }
        entry.Add(appliedDamage, CombatSeconds);
        TotalDamage += appliedDamage;
    }
    /// <summary>재생 중인 전투 시간만 전진하여 최근 DPS를 만료시킨다.</summary>
    public void Advance(double combatDelta)
    {
        if (IsFinalized) return;
        if (double.IsNaN(combatDelta) || double.IsInfinity(combatDelta) || combatDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(combatDelta));
        CombatSeconds += combatDelta;
        foreach (var entry in _entries) entry.Advance(CombatSeconds, _window);
    }
    /// <summary>한 종류의 전체 피해 비중. 전체 피해가 0이면 0을 반환한다.</summary>
    public decimal Share(DamageContributionEntry entry) => TotalDamage > 0m && entry != null ? entry.TotalDamage / TotalDamage : 0m;
    /// <summary>한 종류의 최근 DPS.</summary>
    public decimal RecentDpsFor(DamageContributionEntry entry) => entry?.RecentDps(CombatSeconds, _window) ?? 0m;
    /// <summary>마지막 유효 타격까지 보존하고 종료 연출 중 뒤늦은 피해 입력을 차단한다.</summary>
    public void FinalizeRun() => IsFinalized = true;
    /// <summary>배치 종류/수를 보존하고 피해와 전투 시간만 초기화한다.</summary>
    public void Reset()
    {
        IsFinalized = false;
        TotalDamage = 0m; CombatSeconds = 0;
        foreach (var entry in _entries) entry.Clear();
    }
    private DamageContributionEntry GetOrCreate(UnitData unit)
    {
        if (unit == null) throw new ArgumentNullException(nameof(unit));
        int type = unit.unitType.Get(Tier.Normal);
        var key = (unit.unitTribe, type, type > 0 ? null : unit);
        if (_byKind.TryGetValue(key, out var entry)) return entry;
        entry = new DamageContributionEntry(unit);
        _byKind.Add(key, entry); _entries.Add(entry);
        return entry;
    }
}
