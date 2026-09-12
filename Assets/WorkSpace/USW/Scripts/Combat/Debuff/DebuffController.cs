using System;
using System.Collections.Generic;

/// <summary>대상 하나의 디버프 적용·조회·만료. Unity 전역 상태를 참조하지 않는다.</summary>
public sealed class DebuffController
{
    private readonly List<DebuffInstance> _instances = new List<DebuffInstance>();
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<DebuffInstance> _view;
    private readonly Action<long> _burnDamage;
    private readonly Func<bool> _alive;
    private bool _advancing;
    private double _now;
    /// <summary>디버그용 읽기 전용 활성 상태 목록.</summary>
    public IReadOnlyList<DebuffInstance> Active => _view;
    /// <summary>공통 피해 차감과 대상 생존 검사를 주입한다.</summary>
    public DebuffController(Action<long> burnDamage, Func<bool> alive)
    { _burnDamage = burnDamage; _alive = alive; _view = _instances.AsReadOnly(); }
    /// <summary>다음 부여/스냅샷 전에 기존 도래 틱을 처리한다.</summary>
    public void Advance(double now)
    {
        if (double.IsNaN(now) || double.IsInfinity(now) || now < _now) throw new ArgumentOutOfRangeException(nameof(now));
        _now = now;
        if (_advancing) return;
        _advancing = true;
        try
        {
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (i >= _instances.Count) continue;
                var instance = _instances[i];
                instance.Advance(now, _burnDamage, _alive);
                if (!_alive()) { Clear(); break; }
                if (instance.IsExpired(now)) _instances.Remove(instance);
            }
        }
        finally { _advancing = false; }
    }
    /// <summary>Advance 이후의 시각에서 적용. 다른 정의가 동일 슬롯을 차지하는 것은 거부한다.</summary>
    public bool Apply(DebuffDefinition definition, DebuffApplyContext context)
    {
        if (!_alive() || context.Stacks <= 0 || context.SnapshotHpUnits < 0) return false;
        var instance = _instances.Find(x => x.Definition.StackGroup == definition.StackGroup);
        if (instance != null && instance.Definition.Id != definition.Id)
            throw new InvalidOperationException("Different definitions cannot share an active stack group.");
        if (instance == null)
        {
            instance = definition.Kind switch
            {
                DebuffKind.Burn => new BurnDebuff(definition),
                DebuffKind.ArmorBreak => new ArmorBreakDebuff(definition),
                DebuffKind.DamageTakenIncrease => new DamageTakenIncreaseDebuff(definition),
                _ => throw new ArgumentOutOfRangeException(nameof(definition))
            };
            _instances.Add(instance);
        }
        instance.Reapply(_now, context);
        return true;
    }
    /// <summary>현재 받피증 배율.</summary>
    public decimal DamageTakenMultiplier
    {
        get { foreach (var x in _instances) if (x is DamageTakenIncreaseDebuff) return x.Definition.DamageMultiplier; return 1; }
    }
    /// <summary>아머 방어 약화 계수 q.</summary>
    public double ArmorFactor
    {
        get { foreach (var x in _instances) if (x is ArmorBreakDebuff) return 1 + x.Definition.ArmorStrength * x.Stacks / x.Definition.MaxStacks; return 1; }
    }
    /// <summary>사망·웨이브 종료·재초기화 시 호출한다.</summary>
    public void Clear() => _instances.Clear();
}
