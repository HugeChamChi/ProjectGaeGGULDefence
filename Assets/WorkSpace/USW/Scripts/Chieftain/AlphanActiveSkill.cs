using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

/// <summary>씬 수명의 알팡 액티브. UnitBase/그리드/등급 없이 충전과 발동을 관리한다.</summary>
public sealed class AlphanActiveSkill : IChiefActiveSkill, IInitializable, ITickable, IDisposable
{
    private readonly ChieftainSelection _selector;
    private readonly DroneManager _drones;
    private readonly IChiefSelectionReader _effects;
    private readonly GameManager _game;
    private readonly AudioManager _audio;
    private AlphanSkillData _data;
    private Sprite _fallbackIcon;
    private float _elapsed;
    private UnitLevelBonusController _bonuses;
    private float _speedRemaining;
    private float Bonus(UnitBonusStat stat) => _bonuses?.GetStat(stat) ?? 0f;
    public float GlobalAttackBonus => IsAvailable ? Bonus(UnitBonusStat.ChiefGlobalAttackPercent) : 0f;
    public float AttackSpeedBonus => IsAvailable && _speedRemaining > 0f ? Bonus(UnitBonusStat.ChiefSpeedPercent) : 0f;
    public void ConfigureLevel(UnitLevelSnapshot levels)
    {
        _bonuses = new UnitLevelBonusController(UnitBonusChart.Resolve("AlphanChief", new List<UnitLevelBonusDefinition>()), levels?.GetLevel("AlphanChief") ?? 1);
    }
    private void OnRallyImpact(BossBase boss)
    {
        float ratio=Bonus(UnitBonusStat.ChiefDamageTakenPercent), duration=Bonus(UnitBonusStat.ChiefDamageTakenDuration);
        if (!IsAvailable || boss == null || boss.IsDead || ratio <= 0f || duration <= 0f) return;
        boss.Debuffs?.Apply(new DebuffDefinition(190015, "alphan_level15", DebuffKind.DamageTakenIncrease, "alphan_level15", duration, 1, damageMultiplier: 1m+(decimal)ratio), new DebuffApplyContext(0, 1, 0));
    }
    private int _charges;
    private bool _casting;
    private bool _disposed;
    private bool _initialized;
    private CancellationTokenSource _castCts;

    /// <summary>씬 서비스 주입. 드론 매니저가 없는 씬에서도 등록 가능하나 사용은 비활성이다.</summary>
    public AlphanActiveSkill(ChieftainSelection selector, IEnumerable<DroneManager> drones,
        IChiefSelectionReader effects, GameManager game, AudioManager audio)
    {
        _selector=selector; _effects=effects; _game=game; _audio=audio;
        foreach(var manager in drones) { _drones=manager; break; }
    }
    /// <inheritdoc />
    public Sprite Icon => _data != null && _data.Icon != null ? _data.Icon : _fallbackIcon;
    /// <inheritdoc />
    public bool IsAvailable => !_disposed && _data != null && _drones != null;
    /// <summary>알팡 선택지의 쿨타임 감소만 적용한다. 유닛/셀 스탯을 사용하지 않는다.</summary>
    public float CooldownSeconds => _data == null ? 0 : Mathf.Max(0.05f, _data.CooldownSeconds
        * (1f-(_effects?.ChiefCooldownReduction ?? 0f)) * Mathf.Max(0f, 1f-Bonus(UnitBonusStat.ChiefCooldownPercent)));
    private bool IsPlaying => (_game == null || _game.CurrentState == GameManager.GameState.Playing) && Time.timeScale > 0;
    /// <inheritdoc />
    public float CooldownProgress
    {
        get
        {
            SynchronizeCharges();
            return !IsAvailable ? 0f : _charges >= MaximumCharges ? 1f : Mathf.Clamp01(_elapsed / CooldownSeconds);
        }
    }
    /// <inheritdoc />
    public float CooldownRemaining
    {
        get
        {
            SynchronizeCharges();
            return IsAvailable && _charges < MaximumCharges ? Mathf.Max(0f, CooldownSeconds - _elapsed) : 0f;
        }
    }
    /// <inheritdoc />
    public int AvailableCharges
    {
        get { SynchronizeCharges(); return IsAvailable ? _charges : 0; }
    }
    /// <inheritdoc />
    public int MaximumCharges => Mathf.Max(1, _effects?.ChiefChargeCapacity ?? 1);
    /// <inheritdoc />
    public bool CanActivate => IsAvailable && IsPlaying && !_casting && !_drones.IsRallying
        && _drones.RallyAvailableDroneCount > 0 && AvailableCharges > 0;

    /// <summary>초기화는 GameInitializer의 족장 선택보다 먼저 실행된다.</summary>
    public void Initialize()
    {
        if (_disposed || _initialized || _selector == null) return;
        _initialized = true;
        ConfigureLevel(new UnitLevelSnapshot((Player.PlayerData.Data?.UnitLevels ?? new List<PlayerData.UnitLevelEntry>()).ConvertAll(entry => new KeyValuePair<string,int>(entry.UnitKey, entry.Level))));
        if (_drones != null) _drones.OnRallyImpact += OnRallyImpact;
        _selector.OnAlphanSkillSelected += Configure;
        Configure(_selector.SelectedAlphanSkill, _selector.SelectedAlphanIcon);
    }
    /// <summary>선택된 알팡 설정으로 초기화한다. 처음에는 충전이 필요하다.</summary>
    public void Configure(AlphanSkillData data, Sprite fallbackIcon)
    {
        if (_disposed) return;
        CancelCast();
        _data=data; _fallbackIcon=fallbackIcon; _elapsed=0; _speedRemaining=0;
        _charges=Bonus(UnitBonusStat.ChiefStartsReady)>0f ? 1 : 0;
        if (_selector != null) _selector.SetActiveSkill(IsAvailable ? this : null);
    }
    /// <inheritdoc />
    public void Tick() => Advance(Time.deltaTime);
    /// <summary>플레이 중에만 충전한다. 드론 0기여도 충전은 유지한다.</summary>
    public void Advance(float seconds)
    {
        if (!IsAvailable || !IsPlaying || seconds <= 0) return;
        _speedRemaining=Mathf.Max(0f,_speedRemaining-seconds);
        SynchronizeCharges();
        if (_charges >= MaximumCharges) return;
        _elapsed += Mathf.Min(seconds, CooldownSeconds * (MaximumCharges - _charges));
        SynchronizeCharges();
    }

    // Keep existing completed charges and partial seconds when cards change.
    // A full bank never accumulates time beyond its authored capacity.
    private void SynchronizeCharges()
    {
        if (_data == null) return;
        int capacity = MaximumCharges;
        _charges = Mathf.Min(_charges, capacity);
        float cooldown = CooldownSeconds;
        while (_charges < capacity && _elapsed >= cooldown)
        {
            _elapsed -= cooldown;
            _charges++;
        }
        if (_charges >= capacity) _elapsed = 0f;
    }
    /// <inheritdoc />
    public bool TryActivate()
    {
        if (!CanActivate) return false;
        _charges--; _casting=true;
        _speedRemaining=Bonus(UnitBonusStat.ChiefSpeedDuration);
        _castCts=new CancellationTokenSource();
        var cts=_castCts;
        ExecuteAsync(cts).Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
        ChiefActiveSkillSignals.RaiseActivated(); // 보스 예고 패턴 카운터
        return true;
    }
    private async UniTask ExecuteAsync(CancellationTokenSource cts)
    {
        try
        {
            _drones.RallyBeamPrefab = _data.BeamFx;
            if (!string.IsNullOrEmpty(_data.SoundAddress)) _audio?.PlaySFX(_data.SoundAddress);
            await _drones.ExecuteChiefRallyAsync(_data, cts.Token, _selector?.SelectedChieftain);
        }
        finally
        {
            if (_castCts == cts) { _castCts=null; _casting=false; }
            if (!cts.IsCancellationRequested) cts.Cancel();
            cts.Dispose();
        }
    }
    private void CancelCast()
    {
        var cts=_castCts; _castCts=null; _casting=false;
        cts?.Cancel(); // ExecuteAsync의 finally에서 Dispose.
    }
    /// <summary>씬 종료/컨테이너 해제 시 이벤트와 실행 중 연출을 정리한다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed=true;
        if (_drones != null) _drones.OnRallyImpact -= OnRallyImpact;
        if (_selector != null) _selector.OnAlphanSkillSelected-=Configure;
        CancelCast();_data=null;
        if (_selector != null && ReferenceEquals(_selector.ActiveSkill, this))
            _selector.SetActiveSkill(null);
    }
}
