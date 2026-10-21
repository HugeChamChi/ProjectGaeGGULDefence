using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lizard-owned mechanics. Scheduling and the combat/pause clock remain scene-controller responsibilities.</summary>
[RequireComponent(typeof(LizardBossPresentation))]
public sealed class BossLizard : BossBase, IBossPatternRuntime
{
    private GridManager _grid;
    private LizardBossPresentation _presentation;
    private LizardPatternData _casting;
    private LizardPatternData _slowPattern;
    private readonly List<UnitBase> _slowed = new();
    private float _elapsed;
    private float _slowRemaining;
    private bool _impactApplied;
    private bool _healed;
    private bool _burrowed;
    private decimal _damageFactor = 1m;

    /// <summary>Burrow excludes new targeting and all incoming damage, including already flying projectiles.</summary>
    public override bool IsTargetable => base.IsTargetable && !_burrowed;
    /// <summary>Whether the temporary thorn barrier is currently reducing damage.</summary>
    public bool BarrierActive => _damageFactor < 1m;
    /// <summary>Whether the authored three-second burrow window is active.</summary>
    public bool IsBurrowed => _burrowed;
    /// <summary>The active pose/effect sequence, including entry and exit animation.</summary>
    public LizardPatternData CastingPattern => _casting;
    /// <summary>Remaining combat seconds on the unit debuff.</summary>
    public float ThreatRemaining => _slowRemaining;
    protected override decimal PatternDamageMultiplier => _damageFactor;

    /// <inheritdoc/>
    public void ConfigurePatternContext(GridManager grid)
    {
        ClearPatternEffects();
        _grid = grid;
        _presentation = GetComponent<LizardBossPresentation>();
    }

    /// <summary>Starts only authored Lizard effects; the controller has already selected and locked this cast.</summary>
    public override void ExecutePattern(BossPatternData patternData)
    {
        if (patternData is not LizardPatternData pattern)
            throw new ArgumentException("Lizard requires LizardPatternData.", nameof(patternData));
        _presentation ??= GetComponent<LizardBossPresentation>();
        _casting = pattern;
        _elapsed = 0f;
        _impactApplied = _healed = false;
        _burrowed = pattern.patternType == BossPatternType.LizardBurrow;
        _presentation.Begin(pattern);
        ApplyDueEffects();
        Render();
    }

    /// <inheritdoc/>
    public void AdvancePatternEffects(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        if (IsDead) { ClearPatternEffects(); return; }
        if (_slowRemaining > 0f)
        {
            _slowRemaining = Mathf.Max(0f, _slowRemaining - deltaTime);
            if (_slowRemaining <= 0f) ClearThreat();
        }
        if (_casting != null)
        {
            _elapsed += deltaTime;
            ApplyDueEffects();
            _presentation.Sample(_casting, _elapsed);
            if (_elapsed >= _casting.CastDuration)
            {
                _casting = null;
                _damageFactor = 1m;
                _burrowed = false;
                _presentation.End();
            }
        }
        Render();
    }

    private void ApplyDueEffects()
    {
        var pattern = _casting;
        if (pattern == null) return;
        if (!_impactApplied && _elapsed >= pattern.ImpactTimeSec)
        {
            _impactApplied = true;
            switch (pattern.patternType)
            {
                case BossPatternType.LizardThreat:
                    ClearThreat();
                    _slowPattern = pattern;
                    _slowRemaining = Mathf.Max(0f, pattern.duration - (_elapsed - pattern.ImpactTimeSec));
                    if (_slowRemaining > 0f && _grid != null)
                        foreach (var cell in _grid.AllCells())
                        {
                            var unit = cell?.OccupyingUnit;
                            if (unit == null || _slowed.Contains(unit)) continue;
                            unit.SetPatternAttackSpeed(this, 1f - pattern.speedReductionRate);
                            _slowed.Add(unit);
                        }
                    break;
                case BossPatternType.LizardThornBarrier:
                    _damageFactor = 1m - (decimal)pattern.damageReductionRate;
                    AddShield(MaxHp * (decimal)pattern.ShieldFraction);
                    break;
            }
        }
        if (pattern.patternType == BossPatternType.LizardThornBarrier
            && _elapsed >= pattern.ImpactTimeSec + pattern.duration) _damageFactor = 1m;
        if (pattern.patternType == BossPatternType.LizardBurrow)
        {
            if (!_healed && _elapsed >= pattern.HealDelay)
            {
                _healed = true;
                Heal(MaxHp * (decimal)pattern.HealFraction);
            }
            _burrowed = _elapsed < pattern.duration;
        }
    }

    private void Render() => _presentation?.RenderEffects(_casting, _elapsed, _slowed,
        _slowPattern != null ? _slowRemaining / _slowPattern.duration : 0f);

    private void ClearThreat()
    {
        foreach (var unit in _slowed) if (unit != null) unit.SetPatternAttackSpeed(this, 1f);
        _slowed.Clear();
        _slowRemaining = 0f;
        _slowPattern = null;
    }

    /// <inheritdoc/>
    public void ClearPatternEffects()
    {
        ClearThreat();
        ClearShield();
        _casting = null;
        _burrowed = false;
        _damageFactor = 1m;
        _presentation?.End();
        _presentation?.ClearEffects();
    }

    private void OnDisable() => ClearPatternEffects();
}
