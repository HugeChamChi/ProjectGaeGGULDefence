using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그리드 셀의 시각적 표현 담당 (View)
/// 
/// 책임:
///   - GridCellModel.OnStateChanged 구독
///   - 상태에 따른 색상 변경만 처리
///   - 게임 로직 없음
/// 
/// GridCell이 Model을 보유하고, GridCellView는 Model을 구독
/// </summary>
public class GridCellView : MonoBehaviour
{
    [SerializeField] private Image cellImage;

    // ── 색상 정의 ──────────────────────────────────────────────
    // 토템 버프
    private static readonly Color ColorAtk    = new Color(1.0f, 0.15f, 0.15f, 0.85f); // 빨강 (공격력)
    private static readonly Color ColorSpd    = new Color(1.0f, 0.55f, 0.0f,  0.85f); // 주황 (속도)
    private static readonly Color ColorBoth   = new Color(1.0f, 0.39f, 0.0f,  0.85f); // 중간 (둘 다)
    private static readonly Color ColorFood   = new Color(0.1f, 0.8f,  0.1f,  0.85f); // 초록 (식량 속도)
    private static readonly Color ColorTotemBoost   = new Color(1.0f, 0.85f, 0.0f,  0.85f); // 금색 (OverWelm 하1칸 공격력 증폭)
    private static readonly Color ColorTotemDisable = new Color(0.0f, 0.75f, 0.75f, 0.85f); // 청록 (OverWelm 상3칸 공격불가)

    // 보스 패턴 디버프
    private static readonly Color ColorSealed  = new Color(0.3f, 0.3f, 0.3f, 0.90f); // 짙은 회색 (봉인)
    private static readonly Color ColorDebuff  = new Color(0.5f, 0.0f, 0.8f, 0.75f); // 보라 (데미지/속도 감소)
    private static readonly Color ColorDisable = new Color(0.1f, 0.1f, 0.5f, 0.85f); // 짙은 파랑 (공격 불가)

    private Color _originalColor;
    private bool  _originalColorCaptured;
    private GridCellModel _model;

    private void Awake()
    {
        if (cellImage == null)
            cellImage = GetComponent<Image>();

        if (cellImage == null)
        {
            Debug.LogWarning($"GridCellView({name}): Image 컴포넌트 없음");
            return;
        }

        CaptureOriginalColor();
    }

    private void CaptureOriginalColor()
    {
        if (_originalColorCaptured || cellImage == null) return;
        _originalColor         = cellImage.color;
        _originalColorCaptured = true;
    }

    /// <summary>GridCell.Awake()에서 Model 주입 후 구독 등록</summary>
    public void SetModel(GridCellModel model)
    {
        // GridCell.Awake()가 먼저 실행될 경우를 대비해 여기서도 초기화
        if (cellImage == null)
            cellImage = GetComponent<Image>();
        CaptureOriginalColor();

        // 기존 구독 해제 후 재등록
        if (_model != null)
            _model.OnStateChanged -= RefreshColor;

        _model = model;
        _model.OnStateChanged += RefreshColor;

        RefreshColor();
    }

    private void OnDestroy()
    {
        if (_model != null)
            _model.OnStateChanged -= RefreshColor;
    }

    // ── 색상 갱신 ──────────────────────────────────────────────
    private void RefreshColor()
    {
        if (cellImage == null || _model == null) return;

        // 보스 패턴 디버프 우선순위 높음
        if (_model.IsSealed)
        {
            cellImage.color = ColorSealed;
            return;
        }

        if (_model.IsAttackDisabled)
        {
            cellImage.color = ColorDisable;
            return;
        }

        if (_model.DamageModifier < 1f || _model.SpeedModifier > 1f)
        {
            cellImage.color = ColorDebuff;
            return;
        }

        // 토템 전용 효과 (보스 패턴 없을 때)
        if (_model.TotemAttackDisabled)
        {
            cellImage.color = ColorTotemDisable;
            return;
        }

        if (_model.TotemAttackModifier > 1f)
        {
            cellImage.color = ColorTotemBoost;
            return;
        }

        if (_model.HasFoodBuff)
        {
            cellImage.color = ColorFood;
            return;
        }

        // 기존 공격력/속도 버프
        if      (_model.HasAttackBuff && _model.HasSpeedBuff) cellImage.color = ColorBoth;
        else if (_model.HasAttackBuff)                        cellImage.color = ColorAtk;
        else if (_model.HasSpeedBuff)                         cellImage.color = ColorSpd;
        else                                                  cellImage.color = _originalColor;
    }
}
