using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 클릭 시 표시되는 정보 팝업
///
/// ─ Scene 구성 ──────────────────────────────────────────
///   TotemInfoPopup (이 컴포넌트)
///     ├── TotemNameText    — TMP_Text
///     ├── TotemEffectText  — TMP_Text (효과 자동 생성)
///     ├── RangeGridContainer — GridLayoutGroup, CellSize 약 36px, 7×7
///     ├── RotateButton     — Button
///     └── CloseButton      — Button
///
/// ─ Inspector 연결 ──────────────────────────────────────
///   rangeCellPrefab: 단순 Image 1×1 흰색 스프라이트 프리팹
///   나머지 레퍼런스 연결 필요
///
/// ─ 범위 그리드 ─────────────────────────────────────────
///   에디터(TotemEditorWindow)와 동일한 색상·좌표계 사용
///   7×7 그리드, 중앙(3,3) = 토템 위치
///   effectRange → 빨강(#a20000), attackDisabledRange → 검정, 토템 → 녹색(#92d050)
/// </summary>
public class TotemInfoPopupUI : InGameSingleton<TotemInfoPopupUI>
{
    [Header("Info")]
    [SerializeField] private TMP_Text totemNameText;
    [SerializeField] private TMP_Text totemEffectText;

    [Header("Range Grid")]
    [SerializeField] private Transform rangeGridContainer;
    [SerializeField] private Image     rangeCellPrefab;

    [Header("Buttons")]
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button closeButton;

    [Header("Grid Colors")]
    [SerializeField] private Color colorDefault  = new Color(0.20f, 0.20f, 0.20f, 0.90f);
    [SerializeField] private Color colorCenter   = new Color(0.57f, 0.82f, 0.31f, 0.90f);
    [SerializeField] private Color colorBuff     = new Color(0.64f, 0.00f, 0.00f, 0.90f);
    [SerializeField] private Color colorDisabled = new Color(0.10f, 0.10f, 0.10f, 0.90f);

    private const int GridHalf = 3;
    private const int GridDim  = 7;

    private readonly List<Image> _cells = new();
    private TotemBase _currentTotem;

    protected override void Awake()
    {
        base.Awake();
        rotateButton?.onClick.AddListener(OnRotateClicked);
        closeButton?.onClick.AddListener(Hide);
        BuildGrid();
        gameObject.SetActive(false);
    }

    // ── 열기/닫기 ──────────────────────────────────────────────────

    public void Toggle(TotemBase totem)
    {
        if (_currentTotem == totem && gameObject.activeSelf) Hide();
        else Show(totem);
    }

    public void Show(TotemBase totem)
    {
        _currentTotem = totem;
        Refresh();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _currentTotem = null;
        gameObject.SetActive(false);
    }

    // ── 새로고침 ───────────────────────────────────────────────────

    private void Refresh()
    {
        if (_currentTotem == null) return;
        var data = _currentTotem.Data;
        if (data == null) return;

        if (totemNameText   != null) totemNameText.text   = data.totemName;
        if (totemEffectText != null) totemEffectText.text = BuildEffectText(data);

        RefreshGrid(data);
    }

    // ── 범위 그리드 ────────────────────────────────────────────────

    private void BuildGrid()
    {
        if (rangeCellPrefab == null) return;
        for (int i = 0; i < GridDim * GridDim; i++)
            _cells.Add(Instantiate(rangeCellPrefab, rangeGridContainer));
    }

    private void RefreshGrid(TotemData data)
    {
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].color = colorDefault;

        // 토템 위치 (중앙)
        _cells[GridHalf * GridDim + GridHalf].color = colorCenter;

        // 효과 범위 — 회전 적용
        foreach (var offset in data.effectRange)
        {
            if (TryGetIndex(_currentTotem.RotateOffset(offset), out int idx))
                _cells[idx].color = colorBuff;
        }

        // 공격 불가 범위 — 회전 적용
        foreach (var offset in data.attackDisabledRange)
        {
            if (TryGetIndex(_currentTotem.RotateOffset(offset), out int idx))
                _cells[idx].color = colorDisabled;
        }
    }

    private bool TryGetIndex(Vector2Int offset, out int index)
    {
        int col = GridHalf + offset.x;
        int row = GridHalf + offset.y;
        if (col < 0 || col >= GridDim || row < 0 || row >= GridDim)
        {
            index = -1;
            return false;
        }
        index = row * GridDim + col;
        return true;
    }

    // ── 회전 버튼 ──────────────────────────────────────────────────

    private void OnRotateClicked()
    {
        _currentTotem?.Rotate();
        Refresh();
    }

    // ── 효과 텍스트 자동 생성 ──────────────────────────────────────

    private static string BuildEffectText(TotemData data)
    {
        var sb = new StringBuilder();

        if (data.attackBuffAmount     > 0f) sb.AppendLine($"공격력 +{data.attackBuffAmount * 100f:F0}%");
        if (data.speedBuffAmount      > 0f) sb.AppendLine($"공격속도 +{data.speedBuffAmount * 100f:F0}%");
        if (data.foodSpeedBuffAmount  > 0f) sb.AppendLine($"식량 생산 간격 -{data.foodSpeedBuffAmount * 100f:F0}%");
        if (data.foodAmountBuffAmount > 0f) sb.AppendLine($"식량 생산량 +{data.foodAmountBuffAmount * 100f:F0}%");
        if (data.critChanceBuffAmount > 0f) sb.AppendLine($"치명타 확률 +{data.critChanceBuffAmount * 100f:F0}%");
        if (data.critDamageBuffAmount > 0f) sb.AppendLine($"치명타 피해 +{data.critDamageBuffAmount * 100f:F0}%");

        if (!string.IsNullOrEmpty(data.description))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(data.description);
        }

        return sb.ToString().TrimEnd();
    }
}
