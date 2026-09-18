using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class UnitBase : MonoBehaviour, IDebuffSource
{
    /// <summary>툴팁·디버그가 현재 등급의 연결을 조회한다.</summary>
    public bool TryGetDebuffBinding(out DebuffBinding binding)
    {
        binding = unitData != null ? unitData.DebuffBindings.Get(currentTier) : default;
        return binding.IsConfigured;
    }

    /// <summary>기존 스킬 훅에서 시트 설정을 사용해 현재 보스에 부여한다.</summary>
    protected virtual bool ApplySkillDebuff()
    {
        if (!TryGetDebuffBinding(out var binding) || binding.Trigger != DebuffTrigger.SkillActivated) return false;
        var target = _deps?.BossManager?.CurrentBoss;
        return target != null && target.TryApplyDebuff(binding, GetInstanceID());
    }
    /// <summary>스킬 디버프가 적용될 현재 보스.</summary>
    protected BossBase SkillDebuffTarget => _deps?.BossManager?.CurrentBoss;
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
    /// <summary>자동 및 수동 스킬, 스킬 이벤트와 게이지를 사용할 수 있는지 여부.</summary>
    public virtual bool CanUseSkill => true;
    public virtual bool CanAutoSkill => CanUseSkill;

    public bool IsFirstPlacement { get; private set; } = true;

    private bool _hasTemporaryTier;
    private UnityEngine.Object _temporaryTierSource;
    private UnitData _originalData;
    private Tier _originalTier;
    /// <summary>조커형 노말 합성 재료인지 여부.</summary>
    public virtual bool IsWildcardMergeUnit => false;
    /// <summary>합성/판매에 사용하는 실제 소유 등급.</summary>
    public Tier OriginalTier => _hasTemporaryTier ? _originalTier : currentTier;
    /// <summary>합성/판매에 사용하는 실제 소유 데이터.</summary>
    public UnitData OriginalData => _hasTemporaryTier ? _originalData : unitData;
    /// <summary>범위 효과에 의해 임시 등급을 사용 중인지 여부.</summary>
    public bool HasTemporaryTier => _hasTemporaryTier;

    /// <summary>전투 데이터만 한 등급 올린다. 중복 출처/전설/족장에는 적용하지 않는다.</summary>
    public bool TryApplyTemporaryTier(UnityEngine.Object source, UnitData upgradedData = null)
    {
        if (source == null || unitData == null || _hasTemporaryTier ||
            currentTier < Tier.Normal || currentTier >= Tier.Legend) return false;
        _originalData = unitData;
        _originalTier = currentTier;
        _temporaryTierSource = source;
        _hasTemporaryTier = true;
        unitData = upgradedData != null ? upgradedData : unitData;
        currentTier = (Tier)((int)_originalTier + 1);
        _visual?.UpdateVisual(currentTier);
        OnCombatTierChanged();
        return true;
    }

    /// <summary>해당 출처의 임시 등급만 해제하고 원래 데이터/등급을 복구한다.</summary>
    public void RemoveTemporaryTier(UnityEngine.Object source)
    {
        if (!_hasTemporaryTier || !ReferenceEquals(_temporaryTierSource, source)) return;
        unitData = _originalData;
        currentTier = _originalTier;
        _hasTemporaryTier = false;
        _temporaryTierSource = null;
        _originalData = null;
        _visual?.UpdateVisual(currentTier);
        OnCombatTierChanged();
    }

    /// <summary>임시 전투 등급의 적용/복구 후 등급에 종속된 소환물을 갱신한다.</summary>
    protected virtual void OnCombatTierChanged() { }

    /// <summary>판매/영구 제거 직전에 원래 전투 데이터를 복구한다.</summary>
    public void RestoreOriginalTier() => RemoveTemporaryTier(_temporaryTierSource);

    private UnitStatsModifier _stats;
    private UnitCombatComponent _combat;
    /// <summary>일반 공격 기록 구독 등 전투 확장을 위한 기존 컴포넌트.</summary>
    public UnitCombatComponent Combat => _combat;
    private UnitResourceComponent _resource;
    private BuffController _buff;
    private UnitDependencies _deps;
    /// <summary>팩토리가 주입한 런별 드론 선택지 상태.</summary>
    public DroneSelectionState DroneSelections => _deps?.LevelUpManager?.DroneSelections;
    /// <summary>개별 유닛의 선택지 쿨타임 배율.</summary>
    public virtual float SkillCooldownMultiplier => 1f;
    /// <summary>식량 지급을 묶는 기본 초 간격. 초당 생산량과 별개다.</summary>
    public virtual float FoodPayoutInterval => 1f;

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
        // 첫 공격/스킬 전에 새 셀의 토템 효과와 공격 구독을 확정한다.
        _deps?.TotemBuffManager?.RebuildCellBuffFlags();
        ApplyFacingByCell();

        _combat.StartLoops();

        OnUnitPlaced();

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
        RemoveTemporaryTier(_temporaryTierSource);
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
        if (!CanUseSkill) return;
        onSkillFull?.Invoke();
        OnSkillFull();
        ApplySkillDebuff();
    }
    
    protected virtual void OnSkillFull() { }

    public float SkillGaugeProgress 
    {
        get 
        {
            if (!CanUseSkill || unitData == null || _stats == null || _combat == null) return 0f;
            float interval = _stats.GetCurrentSkillInterval();
            return Mathf.Clamp01(_combat.SkillTimer / interval);
        }
    }

    public virtual float CurrentFoodProductionPerSecond => _resource?.CurrentFoodProductionPerSecond ?? 0f;
    public virtual float GetBaseFoodPerSecond() => unitData != null ? unitData.foodProduction.Get(currentTier) : 0f;
    
    public int GetAttackDamage() => _stats?.GetAttackDamage() ?? 0;
    /// <summary>강화와 버프를 포함한 비치명타 공격력. 난수 상태를 변경하지 않는다.</summary>
    public int GetNonCriticalAttackDamage() => _stats?.GetNonCriticalAttackDamage() ?? 0;
    /// <summary>정보창 공격력. 소환자는 소유 드론 공격력 합계로 재정의한다.</summary>
    public virtual float GetDisplayAttackDamage() => GetNonCriticalAttackDamage();
    /// <summary>정보창에 초 단위로 표시하는 현재 공격간격.</summary>
    public virtual float GetDisplayAttackInterval() => GetCurrentAttackInterval();
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
