using UnityEngine;
using System.Collections;
using UnityEngine.Events;

/// <summary>
/// 모든 고블린의 기반 — 게이지 루프, 식량 생산, 선택적 보스 공격
///
/// 변경 사항:
///   - TotemBuffManager.Instance → Manager.Buff
///   - 보스 디버프 상태(셀 봉인/공격불가/속도감소) 반영
/// </summary>
public abstract class UnitBase : MonoBehaviour
{
    public UnitData unitData;

    public UnityEvent onGaugeFull;  // VFX/애니메이션 훅
    public UnityEvent onAttack;     // 공격 VFX 훅

    /// <summary>유닛 배치/제거 시 전역 알림 — TotemProximityBuff 등에서 구독</summary>
    public static event System.Action OnAnyUnitChanged;

    protected CurrencyManager _currency;
    protected BossBase        _boss;
    private   Coroutine       _gaugeCoroutine;
    private   WaitForSeconds  _cachedWait;
    private   float           _cachedInterval = -1f;

    // 현재 배치된 셀 참조 (보스 패턴 디버프 체크용)
    protected GridCell _currentCell;

    // ── 배치/제거 ──────────────────────────────────────────────

    /// <summary>UnitSpawner 또는 DragHandler.PlaceSelfAt() 이 셀에 배치한 뒤 호출</summary>
    public void OnPlaced(CurrencyManager currency, BossBase boss, GridCell cell = null)
    {
        if (currency == null)
        {
            Debug.LogError($"UnitBase({name}): CurrencyManager가 null입니다.");
            return;
        }

        _currency    = currency;
        _boss        = boss;
        _currentCell = cell;

        if (_gaugeCoroutine != null)
            StopCoroutine(_gaugeCoroutine);

        _gaugeCoroutine = StartCoroutine(GaugeLoop());
        OnUnitPlaced();
        OnAnyUnitChanged?.Invoke();
    }

    /// <summary>OnPlaced() 완료 후 호출되는 훅 — 서브클래스에서 배치 시 추가 처리</summary>
    protected virtual void OnUnitPlaced() { }

    /// <summary>
    /// 하위 호환 오버로드 — 셀 참조 없이 호출하는 기존 코드 지원
    /// WaveManager, BossRewardUI, LevelUpUI 등에서 cell 없이 호출 시 사용
    /// </summary>
    public void OnPlaced(CurrencyManager currency, BossBase boss)
        => OnPlaced(currency, boss, null);

    /// <summary>셀 제거 또는 게임 종료 시 호출</summary>
    public void OnRemoved()
    {
        OnUnitRemoved();
        if (_gaugeCoroutine != null)
        {
            StopCoroutine(_gaugeCoroutine);
            _gaugeCoroutine = null;
        }
        _currentCell = null;
        OnAnyUnitChanged?.Invoke();
    }

    /// <summary>OnRemoved() 시작 시 호출되는 훅 — 서브클래스에서 제거 시 추가 처리</summary>
    protected virtual void OnUnitRemoved() { }

    // ── 공격력 계산 ────────────────────────────────────────────

    /// <summary>
    /// unitData.attackDamage × TotemBuffManager.AttackMultiplier
    /// × 셀 DamageModifier (보스 패턴 데미지 감소 반영)
    /// </summary>
    public int GetAttackDamage()
    {
        if (unitData == null) return 0;

        float cellModifier  = _currentCell != null
            ? (_currentCell.Model.NullifyDamageDebuff ? 1f : _currentCell.Model.DamageModifier)
            : 1f;
        float totemModifier = _currentCell != null ? _currentCell.Model.TotemAttackModifier : 1f;
        int   row           = _currentCell != null ? _currentCell.GridPosition.y            : 0;
        float rowModifier   = Manager.LevelUp != null ? Manager.LevelUp.GetRowAttackMultiplier(row) : 1f;

        float damage = unitData.attackDamage
                     * Manager.Buff.AttackMultiplier
                     * cellModifier
                     * totemModifier
                     * rowModifier;

        // 치명타 판정 — LevelUp 확률 + 토템 보너스 합산
        float critChance = (Manager.LevelUp?.CritChance ?? 0f) + Manager.Buff.CritChanceBonus;
        if (critChance > 0f && Random.value < critChance)
        {
            float critDmgMult = (Manager.LevelUp?.CritDamageMultiplier ?? 1.5f) + Manager.Buff.CritDamageBonus;
            damage *= critDmgMult;
        }

        return Mathf.RoundToInt(damage);
    }

    // ── 게이지 루프 ────────────────────────────────────────────

    private IEnumerator GaugeLoop()
    {
        if (unitData == null)
        {
            Debug.LogError($"UnitBase({name}): unitData가 null입니다.");
            yield break;
        }

        while (true)
        {
            // 셀이 봉인되어 있으면 게이지 정지
            if (_currentCell != null && _currentCell.Model.IsSealed)
            {
                yield return null;
                continue;
            }

            // 대기 시간 = gaugeDuration × 개인배율 × 속도배율 × 식량속도 × 셀디버프 ÷ 줄별속도배율
            int   row         = _currentCell != null ? _currentCell.GridPosition.y : 0;
            float rowSpeedMult = Manager.LevelUp != null ? Manager.LevelUp.GetRowSpeedMultiplier(row) : 1f;

            float interval = unitData.gaugeDuration
                           * unitData.gaugeMultiplier
                           * Manager.Buff.SpeedMultiplier
                           * Manager.Buff.FoodSpeedMultiplier
                           * (_currentCell != null ? _currentCell.Model.SpeedModifier : 1f)
                           / rowSpeedMult;

            if (!Mathf.Approximately(interval, _cachedInterval))
            {
                _cachedInterval = interval;
                _cachedWait     = new WaitForSeconds(interval);
            }
            yield return _cachedWait;
            OnGaugeFull();
        }
    }

    protected virtual void OnGaugeFull()
    {
        onGaugeFull?.Invoke();

        // 식량 생산 — FoodAmountMultiplier: 마법사 아우라 등으로 감소 가능
        _currency.AddCurrency(unitData.foodPerTick * Manager.Buff.FoodAmountMultiplier);

        // 보스 공격 — 공격 불가 상태 체크 (보스 패턴 + OverWelm 토템)
        bool attackDisabled = _currentCell != null &&
            (_currentCell.Model.IsAttackDisabled || _currentCell.Model.TotemAttackDisabled);

        if (unitData.attackDamage > 0 && !attackDisabled && _boss != null && !_boss.IsDead)
        {
            var bossArea = Manager.Boss?.CurrentBoss?.GetComponent<BossAreaTarget>();
            if (bossArea != null && Manager.Projectile != null)
                Manager.Projectile.Launch(transform.position, bossArea.GetRandomWorldPosition());

            _boss.TakeDamage(GetAttackDamage());
            onAttack?.Invoke();
        }
    }
}
