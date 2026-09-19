using UnityEngine;

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
    [SerializeField] private SpriteRenderer cellRenderer;
    [Header("Totem Range Preview")]
    [SerializeField] private Material _effectPreviewMaterial;
    [Header("Unit Drop Preview")]
    [SerializeField] private Material _unitDropPreviewMaterial;
    [SerializeField] private Color _unitDropPreviewColor = new Color(0.2f, 0.9f, 1f, 0.85f);
    private Material _originalMaterial;
    private MaterialPropertyBlock _originalProperties;
    private MaterialPropertyBlock _previewProperties;
    private static readonly int StripeColorId = Shader.PropertyToID("_StripeColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int CellRectId = Shader.PropertyToID("_CellRect");

    // ── 색상 정의 ──────────────────────────────────────────────
    // 토템 범위 프리뷰
    private static readonly Color ColorTotemPreviewEffect   = new Color(1.0f, 0.0f, 0.0f, 0.85f);

    // 보스 패턴 디버프
    private static readonly Color ColorSealed  = new Color(0.3f, 0.3f, 0.3f, 0.90f); // 짙은 회색 (봉인)
    private static readonly Color ColorDebuff  = new Color(0.5f, 0.0f, 0.8f, 0.75f); // 보라 (데미지/속도 감소)
    private static readonly Color ColorDisable = new Color(0.1f, 0.1f, 0.5f, 0.85f); // 짙은 파랑 (공격 불가)

    private Color _originalColor;
    private bool  _originalColorCaptured;
    private GridCellModel _model;

    private void Awake()
    {
        if (cellRenderer == null)
            cellRenderer = GetComponent<SpriteRenderer>();

        if (cellRenderer == null)
        {
            Debug.LogWarning($"GridCellView({name}): SpriteRenderer 컴포넌트 없음");
            return;
        }

        CaptureOriginalColor();
    }

    private void CaptureOriginalColor()
    {
        if (_originalColorCaptured || cellRenderer == null) return;
        _originalColor         = cellRenderer.color;
        _originalMaterial = cellRenderer.sharedMaterial;
        _originalProperties = new MaterialPropertyBlock();
        _previewProperties = new MaterialPropertyBlock();
        cellRenderer.GetPropertyBlock(_originalProperties);
        _originalColorCaptured = true;
    }

    /// <summary>GridCell.Awake()에서 Model 주입 후 구독 등록</summary>
    public void SetModel(GridCellModel model)
    {
        // GridCell.Awake()가 먼저 실행될 경우를 대비해 여기서도 초기화
        if (cellRenderer == null)
            cellRenderer = GetComponent<SpriteRenderer>();
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
        if (cellRenderer == null || _model == null) return;

        if (_model.IsUnitDropPreviewed && _model.IsAvailable)
        {
            ApplyDropPreview();
            return;
        }

        if (_model.IsTotemRangePreviewed)
        {
            ApplyPreview(_effectPreviewMaterial, ColorTotemPreviewEffect, _model.TotemPreviewColor);
            return;
        }

        cellRenderer.sharedMaterial = _originalMaterial;
        cellRenderer.SetPropertyBlock(_originalProperties);
        // 보스 패턴 디버프 우선순위 높음
        if (_model.IsSealed)
        {
            cellRenderer.color = ColorSealed;
            return;
        }

        if (_model.IsAttackDisabled)
        {
            cellRenderer.color = ColorDisable;
            return;
        }

        if (_model.DamageModifier < 1f || _model.SpeedModifier > 1f)
        {
            cellRenderer.color = ColorDebuff;
            return;
        }

        cellRenderer.color = _originalColor;
    }

    private void ApplyPreview(Material material, Color fallback, Color? previewColor)
    {
        // Shared materials keep all cells in phase without allocating material instances.
        cellRenderer.sharedMaterial = material != null ? material : _originalMaterial;
        cellRenderer.color = material != null ? Color.white : fallback;
        if (material != null && previewColor.HasValue)
        {
            var tint = previewColor.Value;
            var stripe = tint; stripe.a *= material.GetColor(StripeColorId).a;
            var fill = tint; fill.a *= material.GetColor(FillColorId).a;
            cellRenderer.GetPropertyBlock(_previewProperties);
            _previewProperties.SetColor(StripeColorId, stripe);
            _previewProperties.SetColor(FillColorId, fill);
            cellRenderer.SetPropertyBlock(_previewProperties);
        }
        else cellRenderer.SetPropertyBlock(_originalProperties);
    }

    private void ApplyDropPreview()
    {
        cellRenderer.sharedMaterial = _unitDropPreviewMaterial != null ? _unitDropPreviewMaterial : _originalMaterial;
        cellRenderer.color = _unitDropPreviewMaterial != null ? Color.white : _unitDropPreviewColor;
        cellRenderer.SetPropertyBlock(_originalProperties);
        if (_unitDropPreviewMaterial == null || cellRenderer.sprite == null) return;
        var bounds = cellRenderer.sprite.bounds;
        cellRenderer.GetPropertyBlock(_previewProperties);
        _previewProperties.SetVector(CellRectId, new Vector4(bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y));
        cellRenderer.SetPropertyBlock(_previewProperties);
    }
}
