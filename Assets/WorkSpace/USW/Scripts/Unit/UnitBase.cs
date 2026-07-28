using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class UnitBase : MonoBehaviour
{
    public UnitData unitData;
    public Tier currentTier = Tier.Normal;
    public UnityEvent onSkillFull;
    public UnityEvent onAttack;
    public static event Action OnAnyUnitChanged;

    public GridCell currentCell { get; private set; }
    public BossBase Boss { get; private set; }
    
    public UnitAnimator animator;
    protected UnitSoundController _sound;
    protected UnitVisualController _visual;
    private SpriteRenderer _spriteRenderer;

    public enum UnitState { Idle, Attacking, Skilling, Sealed }
    public UnitState CurrentState { get; protected set; } = UnitState.Idle;

    public virtual bool IsFoodProductionBuffable => true;
    public virtual bool CanBasicAttack => true;
    public virtual bool CanAutoSkill => true;

    public bool IsFirstPlacement { get; private set; } = true;
    public bool IsPopulationReserved = false;

    private UnitStatsModifier _stats;
    private UnitCombatComponent _combat;
    private UnitResourceComponent _resource;
    private BuffController _buff;
    private UnitDependencies _deps;

    /// <summary>이 유닛에 걸린 버프("용기")를 보관/집계하는 컴포넌트.</summary>
    public BuffController Buffs => _buff;

    /// <summary>kind 스탯의 토템 전역/셀/버프/자기 패시브/리더 패시브 보너스 합을 반환한다.</summary>
    public float GetStatBonus(StatKind kind, float leaderPassiveBonusScale = 1f)
    {
        float globalBonus = _deps?.TotemBuffManager?.GetGlobalStatBonus(kind) ?? 0f;
        float cellBonus = currentCell?.Model.GetTotemCellBonus(kind) ?? 0f;
        float buffBonus = Buffs != null ? Buffs.GetStatMultiplier(kind) - 1f : 0f;

        float selfPassiveBonus = unitData?.passive != null
            ? unitData.passive.GetSelfBonus(kind, this, _deps, leaderPassiveBonusScale)
            : 0f;

        var leaderPassive = _deps?.ChieftainManager?.ChieftainUnit?.unitData?.passive;
        float leaderPassiveBonus = leaderPassive != null
            ? leaderPassive.GetPartyBonus(kind, this, _deps, leaderPassiveBonusScale)
            : 0f;

        return globalBonus + cellBonus + buffBonus + selfPassiveBonus + leaderPassiveBonus;
    }

    protected virtual void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<UnitAnimator>();
        if (animator == null) animator = GetComponent<UnitAnimator>();
        
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null) _visual = new UnitVisualController(_spriteRenderer);
    }

    public void Init(UnitDependencies deps)
    {
        _deps = deps;
        
        _stats = gameObject.AddComponent<UnitStatsModifier>();
        _resource = gameObject.AddComponent<UnitResourceComponent>();
        _combat = gameObject.AddComponent<UnitCombatComponent>();
        _buff = gameObject.AddComponent<BuffController>();

        _stats.Init(this, deps);
        _resource.Init(this, deps);
        _combat.Init(this, deps, _stats, _resource);
        _buff.Init(this, deps);
        deps.BuffManager?.ApplyActiveGlobalBuffsTo(this);
    }

    public void OnPlaced(CurrencyManager currency, BossBase boss, GridCell cell = null)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<UnitAnimator>();
            if (animator == null) animator = GetComponent<UnitAnimator>();
        }

        if (unitData != null && _deps?.GameDataManager != null && _deps.GameDataManager.IsLoaded)
        {
            SyncStatsWithSheet();
        }

        if (_visual != null && unitData != null)
        {
            _visual.UpdateVisual(currentTier);
        }

        Boss = boss;
        currentCell = cell;
        ApplyFacingByCell();

        _combat.StartLoops();

        OnUnitPlaced();
        
        if (!IsPopulationReserved)
        {
            _deps?.PopulationManager?.Add(unitData != null ? unitData.populationCost.Get(currentTier) : 1);
            IsPopulationReserved = true;
        }
        
        OnAnyUnitChanged?.Invoke();

        if (_deps?.AudioManager != null)
            _sound = new(this, _deps.AudioManager);

        IsFirstPlacement = false;
    }

    public void OnPlaced(CurrencyManager currency, BossBase boss) => OnPlaced(currency, boss, null);

    public void OnRemoved()
    {
        OnUnitRemoved();
        _combat.StopLoops();
        if (IsPopulationReserved)
        {
            _deps?.PopulationManager?.Remove(unitData != null ? unitData.populationCost.Get(currentTier) : 1);
            IsPopulationReserved = false;
        }
        currentCell = null;
        OnAnyUnitChanged?.Invoke();
    }

    protected virtual void OnUnitPlaced()  { }
    protected virtual void OnUnitRemoved() { }

    private void ApplyFacingByCell()
    {
        if (_spriteRenderer == null || currentCell == null || _deps?.GridManager == null) return;
        int halfColumn = _deps.GridManager.Columns / 2;
        bool faceLeft = currentCell.GridPosition.x >= halfColumn;
        var t = _spriteRenderer.transform;
        var scale = t.localScale;
        float x = Mathf.Abs(scale.x);
        if (x <= 0f) x = 1f;
        // 기존 반대이던 방향을 반전 (오른쪽/왼쪽 반대 정렬)
        t.localScale = new Vector3(faceLeft ? x : -x, scale.y, scale.z);
    }

    private void OnDestroy() 
    {
        _visual?.Cleanup();
    }

    public void PauseLoops() => _combat.PauseLoops();
    public void ResumeLoops() => _combat.ResumeLoops();
    protected virtual void SyncStatsWithSheet() { }

    public void SetState(UnitState state) => CurrentState = state;
    public void InvokeOnAttack() => onAttack?.Invoke();
    public void InvokeOnSkillFull() 
    {
        onSkillFull?.Invoke();
        OnSkillFull();
    }
    
    protected virtual void OnSkillFull() { }

    public float SkillGaugeProgress 
    {
        get 
        {
            if (unitData == null || _stats == null || _combat == null) return 0f;
            float interval = _stats.GetCurrentSkillInterval();
            return Mathf.Clamp01(_combat.SkillTimer / interval);
        }
    }

    public virtual float CurrentFoodProductionPerSecond => _resource?.CurrentFoodProductionPerSecond ?? 0f;
    public virtual float GetBaseFoodPerSecond() => unitData != null ? unitData.foodProduction.Get(currentTier) : 0f;
    
    public int GetAttackDamage() => _stats?.GetAttackDamage() ?? 0;
    public float GetUpgradedAtk() => _stats?.GetUpgradedAtk() ?? 0f;
    public int ComputeDamageFrom(float baseDamage) => _stats?.ComputeDamageFrom(baseDamage) ?? 0;
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier) => _stats?.ComputeDamageFrom(baseDamage, projAtkBonusMultiplier) ?? 0;

    /// <summary>ChiefUnit의 수동 스킬 발동 등에서 UnitCombatComponent.ExecuteSkill()(skillData 우선, 없으면 legacy 폴백) 전체 파이프라인을 그대로 태운다.</summary>
    public void TriggerSkillManually() => _combat?.TriggerSkillManually();

    // ── Backward Compatibility Wrappers for Subclasses & UI ──
    public TotemBuffManager _totemBuffManager => _deps?.TotemBuffManager;
    public LevelUpManager _levelUpManager => _deps?.LevelUpManager;
    public AudioManager _audioManager => _deps?.AudioManager;
    public BossBase _boss => Boss;
    public GridManager _gridManager => _deps?.GridManager;
    
    public void LaunchProjectile(int damage) => _combat?.LaunchProjectile(damage);

    public float GetCurrentAttackInterval() => _stats?.GetCurrentAttackInterval() ?? 1f;
    public float GetCurrentSkillInterval() => _stats?.GetCurrentSkillInterval() ?? 1f;
    
    public float _skillTimer 
    { 
        get => _combat?.SkillTimer ?? 0f; 
        set { if (_combat != null) _combat.SkillTimer = value; } 
    }
    public float CurrentSkillTimer => _combat?.SkillTimer ?? 0f;
}
