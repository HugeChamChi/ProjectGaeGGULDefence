using UnityEngine;
using System.Collections;

/// <summary>
/// 그리드 셀 — GridCellModel의 래퍼 역할
/// 
/// 변경 사항:
///   - 색상/시각 로직 → GridCellView로 이동
///   - 상태 데이터 → GridCellModel로 이동
///   - 이 클래스는 배치 로직 + Model 보유만 담당
/// 
/// Inspector 구성:
///   GridCell 오브젝트에 GridCellView 컴포넌트도 함께 부착
/// </summary>
public class GridCell : MonoBehaviour
{
    // ── Model ──────────────────────────────────────────────────
    public GridCellModel Model { get; private set; }

    // ── 배치 상태 ──────────────────────────────────────────────
    public Vector2Int GridPosition   { get; private set; }
    public UnitBase   OccupyingUnit  { get; private set; }
    public TotemBase  OccupyingTotem { get; private set; }

    /// <summary>봉인된 셀은 배치 불가</summary>
    public bool IsOccupied => OccupyingUnit != null || OccupyingTotem != null;
    public bool IsAvailable => !IsOccupied && Model.IsAvailable;

    // ── 하위 호환 프로퍼티 (기존 코드 호환) ───────────────────
    public bool HasAttackBuff => Model.HasAttackBuff;
    public bool HasSpeedBuff  => Model.HasSpeedBuff;

    // ── 초기화 ─────────────────────────────────────────────────
    private void Awake()
    {
        Model = new GridCellModel();

        // GridCellView에 Model 주입
        var view = GetComponent<GridCellView>();
        if (view != null)
            view.SetModel(Model);
        else
            Debug.LogWarning($"GridCell({name}): GridCellView 컴포넌트 없음 — Inspector에서 추가 필요");
    }

    public void Init(Vector2Int pos)
    {
        GridPosition = pos;
    }

    // ── 유닛 배치/제거 ─────────────────────────────────────────
    public bool TryPlaceUnit(UnitBase unit)
    {
        if (!IsAvailable) return false;
        OccupyingUnit = unit;
        return true;
    }

    public UnitBase RemoveUnit()
    {
        var u = OccupyingUnit;
        OccupyingUnit = null;
        return u;
    }

    // ── 토템 배치/제거 ─────────────────────────────────────────
    public bool TryPlaceTotem(TotemBase totem)
    {
        if (!IsAvailable) return false;
        OccupyingTotem = totem;
        return true;
    }

    public TotemBase RemoveTotem()
    {
        var t = OccupyingTotem;
        OccupyingTotem = null;
        return t;
    }

    // ── 버프 플래그 (TotemBuffManager에서 호출) ────────────────
    public void SetBuffFlags(bool atk, bool spd)
    {
        Model.SetBuffFlags(atk, spd);
    }

    public void SetFoodBuff(bool value)              => Model.SetFoodBuff(value);
    public void SetTotemAttackDisabled(bool value)   => Model.SetTotemAttackDisabled(value);
    public void SetTotemAttackModifier(float value)  => Model.SetTotemAttackModifier(value);
    public void SetNullifyDamageDebuff(bool value)   => Model.SetNullifyDamageDebuff(value);

    /// <summary>RebuildCellBuffFlags()에서 토템 전용 효과 일괄 초기화 시 호출</summary>
    public void ClearTotemEffects()                  => Model.ClearTotemEffects();

    // ── 보스 패턴 상태 지속시간 코루틴 ────────────────────────
    /// <summary>
    /// duration 초 후 상태 자동 해제
    /// BossPatternController에서 패턴 발동 후 호출
    /// </summary>
    public void StartDebuffTimer(float duration, System.Action onExpire)
    {
        if (duration <= 0f) return;
        StartCoroutine(DebuffTimerRoutine(duration, onExpire));
    }

    private IEnumerator DebuffTimerRoutine(float duration, System.Action onExpire)
    {
        yield return new WaitForSeconds(duration);
        onExpire?.Invoke();
    }
}
