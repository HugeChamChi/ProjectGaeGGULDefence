using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 그리드 셀의 순수 상태 모델 (MonoBehaviour 미사용)
/// 
/// 책임:
///   - 셀 상태 데이터 보유 (봉인, 디버프, 버프 플래그)
///   - 상태 변경 시 이벤트 발행
///   - 지속시간 기반 상태 자동 해제 (코루틴은 GridCell에서 실행)
/// 
/// GridCellView가 OnStateChanged를 구독하여 시각적 반영
/// </summary>
public class GridCellModel
{
    // ── 이벤트 ─────────────────────────────────────────────────
    /// <summary>상태 변경 시 — GridCellView가 구독하여 색상/UI 갱신</summary>
    public event Action OnStateChanged;

    // ── 토템 버프 플래그 (기존 유지) ───────────────────────────
    public bool HasAttackBuff { get; private set; }
    public bool HasSpeedBuff  { get; private set; }

    // ── 토템 전용 추가 효과 ────────────────────────────────────
    public bool  HasFoodBuff          { get; private set; }         // FoodBuff 장판
    public bool  TotemAttackDisabled  { get; private set; }         // OverWelm 상3칸 공격불가
    public float TotemAttackModifier  { get; private set; } = 1f;   // OverWelm 하1칸 공격력 증폭
    public bool  NullifyDamageDebuff  { get; private set; }         // 제한해제 토템 — 보스 데미지 감소 무효

    // ── 보스 패턴 상태 (신규) ──────────────────────────────────
    public bool  IsSealed         { get; private set; }  // 셀 봉인 — 배치 불가
    public bool  IsAttackDisabled { get; private set; }  // 공격 불가
    public float DamageModifier   { get; private set; } = 1f;  // 1.0 = 정상, 0.7 = 30% 감소
    public float SpeedModifier    { get; private set; } = 1f;  // 1.0 = 정상, 1.3 = 30% 느려짐

    // ── 토템 버프 플래그 설정 ──────────────────────────────────
    public void SetBuffFlags(bool atk, bool spd)
    {
        HasAttackBuff = atk;
        HasSpeedBuff  = spd;
        OnStateChanged?.Invoke();
    }

    public void SetFoodBuff(bool value)
    {
        HasFoodBuff = value;
        OnStateChanged?.Invoke();
    }

    public void SetTotemAttackDisabled(bool value)
    {
        TotemAttackDisabled = value;
        OnStateChanged?.Invoke();
    }

    public void SetTotemAttackModifier(float value)
    {
        TotemAttackModifier = Mathf.Max(0f, value);
        OnStateChanged?.Invoke();
    }

    public void SetNullifyDamageDebuff(bool value)
    {
        NullifyDamageDebuff = value;
        OnStateChanged?.Invoke();
    }

    /// <summary>토템 제거 시 RebuildCellBuffFlags()에서 호출 — 토템 전용 효과 초기화</summary>
    public void ClearTotemEffects()
    {
        HasFoodBuff         = false;
        TotemAttackDisabled = false;
        TotemAttackModifier = 1f;
        NullifyDamageDebuff = false;
        OnStateChanged?.Invoke();
    }

    // ── 보스 패턴 상태 설정 ────────────────────────────────────

    /// <summary>셀 봉인 설정 — duration 0이면 영구</summary>
    public void SetSealed(bool value, float duration = 0f)
    {
        IsSealed = value;
        OnStateChanged?.Invoke();
        // duration 기반 해제는 GridCell(MonoBehaviour)에서 코루틴으로 처리
    }

    /// <summary>공격 불가 설정</summary>
    public void SetAttackDisabled(bool value, float duration = 0f)
    {
        IsAttackDisabled = value;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 데미지 배율 설정
    /// modifier: 0.7 = 30% 감소, 1.0 = 정상
    /// </summary>
    public void SetDamageModifier(float modifier, float duration = 0f)
    {
        DamageModifier = Mathf.Clamp(modifier, 0f, 1f);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 속도 배율 설정
    /// modifier: 1.3 = 30% 느려짐, 1.0 = 정상
    /// </summary>
    public void SetSpeedModifier(float modifier, float duration = 0f)
    {
        SpeedModifier = Mathf.Max(0.1f, modifier);
        OnStateChanged?.Invoke();
    }

    /// <summary>보스 패턴으로 인한 상태 전체 초기화</summary>
    public void ClearBossDebuffs()
    {
        IsSealed         = false;
        IsAttackDisabled = false;
        DamageModifier   = 1f;
        SpeedModifier    = 1f;
        OnStateChanged?.Invoke();
    }

    /// <summary>봉인 + 배치 불가 여부 통합 체크</summary>
    public bool IsAvailable => !IsSealed;
}
