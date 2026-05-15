using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 클릭 시 표시되는 정보 팝업 UI
///
/// ─ Scene 구성 예시 ─────────────────────────────────────────
///   TotemInfoPopup  (이 컴포넌트 + CanvasGroup or Image 배경)
///     ├── TierBorderImage          — Image  (팝업 테두리, 등급 색상 적용)
///     ├── LeftPanel
///     │     ├── TotemIconImage     — Image  (토템 아이콘 스프라이트)
///     │     ├── TotemNameText      — TMP_Text (등급 색상 적용)
///     │     ├── GradeText          — TMP_Text (노말 / 레어 / 에픽 / 전설)
///     │     └── RotatableIconImage — Image  (회전 가능 ↔ 불가 아이콘)
///     ├── CenterPanel
///     │     └── RangeGridContainer — GridLayoutGroup 7×7, CellSize ≈ 36px
///     ├── RightPanel               — 범례 (순수 레이아웃, 코드 연결 없음)
///     ├── BottomPanel
///     │     └── TotemEffectText    — TMP_Text (효과 설명)
///     ├── RotateButton             — Button
///     └── CloseButton              — Button
///
/// ─ Inspector 연결 필수 필드 ────────────────────────────────
///   rangeCellPrefab    : 단순 흰색 Image 프리팹 (1×1)
///   spriteRotatable    : 회전 가능 아이콘
///   spriteNotRotatable : 회전 불가 아이콘
///
/// ─ 범위 우선순위 ───────────────────────────────────────────
///   시트 EffectRange / AttackDisabledRange 값이 있으면 → 시트 우선
///   비어있으면 → TotemData SO의 effectRange / attackDisabledRange fallback
///
/// ─ 범위 그리드 좌표 규칙 ──────────────────────────────────
///   중앙 (3,3) = 토템 위치 / +x=우 / -x=좌 / -y=상 / +y=하
/// </summary>
public class TotemInfoPopupUI : InGameSingleton<TotemInfoPopupUI>
{
    // ─── Left Panel ───────────────────────────────────────────────────
    [Header("Left Panel — Icon & Info")]
    [SerializeField] private Image    totemIconImage;
    [SerializeField] private TMP_Text totemNameText;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private Image    tierBorderImage;

    [Header("Left Panel — Rotate State")]
    [SerializeField] private Image  rotatableIconImage;
    [SerializeField] private Sprite spriteRotatable;
    [SerializeField] private Sprite spriteNotRotatable;

    // ─── Center Panel ─────────────────────────────────────────────────
    [Header("Center Panel — Range Grid")]
    [SerializeField] private Transform rangeGridContainer;
    [SerializeField] private Image     rangeCellPrefab;

    // ─── Bottom ───────────────────────────────────────────────────────
    [Header("Effect Text")]
    [SerializeField] private TMP_Text totemEffectText;

    // ─── Buttons ──────────────────────────────────────────────────────
    [Header("Buttons")]
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button closeButton;

    // ─── Grid Colors ──────────────────────────────────────────────────
    [Header("Grid Colors")]
    [SerializeField] private Color colorDefault  = new Color(0.85f, 0.85f, 0.85f, 1f); // 빈 필드
    [SerializeField] private Color colorCenter   = new Color(0.57f, 0.82f, 0.31f, 1f); // 토템 위치 #92d050
    [SerializeField] private Color colorBuff     = new Color(0.64f, 0.00f, 0.00f, 1f); // 효과 범위 #a20000
    [SerializeField] private Color colorDisabled = new Color(0.10f, 0.10f, 0.10f, 1f); // 공격 불가

    // ─── Grade Colors ─────────────────────────────────────────────────
    [Header("Grade Colors")]
    [SerializeField] private Color colorNormal    = new Color(0.600f, 0.773f, 1.000f, 1f); // #99c5ff
    [SerializeField] private Color colorRare      = new Color(0.753f, 0.627f, 0.976f, 1f); // #c0a0f9
    [SerializeField] private Color colorEpic      = new Color(0.859f, 0.588f, 0.016f, 1f); // #db9604
    [SerializeField] private Color colorLegendary = new Color(0.859f, 0.588f, 0.016f, 1f); // #db9604

    // ─── Constants ────────────────────────────────────────────────────
    private const int GridHalf = 3;
    private const int GridDim  = 7;

    private readonly List<Image> _cells = new();
    private TotemBase       _currentTotem;
    private GameDataManager _gameData;

    // ─── Lifecycle ────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        rotateButton?.onClick.AddListener(OnRotateClicked);
        closeButton?.onClick.AddListener(Hide);
        BuildGrid();
        gameObject.SetActive(false);
    }

    private void Start()
    {
        _gameData = Manager.GameData;
        if (_gameData != null)
            _gameData.OnLoaded += OnSheetDataLoaded;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_gameData != null)
            _gameData.OnLoaded -= OnSheetDataLoaded;
    }

    private void OnSheetDataLoaded()
    {
        if (gameObject.activeSelf && _currentTotem != null)
            Refresh();
    }

    // ─── Show / Hide / Toggle ─────────────────────────────────────────

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

    // ─── Refresh ──────────────────────────────────────────────────────

    private void Refresh()
    {
        if (_currentTotem == null) return;
        var data = _currentTotem.Data;
        if (data == null) return;

        var sheet = _gameData?.GetTotemRow(data.totemId);

        string displayName = sheet != null && !string.IsNullOrEmpty(sheet.TotemName)
                             ? sheet.TotemName : data.totemName;
        Tier   grade       = sheet != null ? sheet.Grade       : data.tier;
        bool   canRotate   = sheet != null ? sheet.IsRotatable : data.isRotatable;

        ApplyIcon(data);
        ApplyNameAndGrade(displayName, grade);
        ApplyRotatableState(canRotate);
        RefreshEffectText(data, sheet);
        RefreshGrid(data, sheet);
    }

    // ─── Visual Helpers ───────────────────────────────────────────────

    private void ApplyIcon(TotemData data)
    {
        if (totemIconImage == null) return;
        totemIconImage.sprite  = data.icon;
        totemIconImage.enabled = data.icon != null;
    }

    private void ApplyNameAndGrade(string name, Tier grade)
    {
        var c = TierToColor(grade);

        if (totemNameText  != null) { totemNameText.text  = name;             totemNameText.color  = c; }
        if (gradeText      != null) { gradeText.text      = TierToLabel(grade); gradeText.color    = c; }
        if (tierBorderImage!= null)   tierBorderImage.color = c;
    }

    private void ApplyRotatableState(bool canRotate)
    {
        if (rotatableIconImage != null)
        {
            var sprite = canRotate ? spriteRotatable : spriteNotRotatable;
            rotatableIconImage.sprite  = sprite;
            rotatableIconImage.enabled = sprite != null;
        }
        if (rotateButton != null)
            rotateButton.interactable = canRotate;
    }

    // ─── Range Grid ───────────────────────────────────────────────────

    private void BuildGrid()
    {
        if (rangeCellPrefab == null) return;
        for (int i = 0; i < GridDim * GridDim; i++)
            _cells.Add(Instantiate(rangeCellPrefab, rangeGridContainer));
    }

    private void RefreshGrid(TotemData data, GameDataManager.TotemSheetRow sheet)
    {
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].color = colorDefault;

        _cells[GridHalf * GridDim + GridHalf].color = colorCenter;

        // 효과 범위: 시트 우선, 비어있으면 SO fallback
        var effectRange   = (sheet?.EffectRange?.Count   > 0) ? sheet.EffectRange   : data.effectRange;
        var disabledRange = (sheet?.AttackDisabledRange?.Count > 0) ? sheet.AttackDisabledRange : data.attackDisabledRange;

        foreach (var offset in effectRange)
        {
            if (TryGetIndex(_currentTotem.RotateOffset(offset), out int idx))
                _cells[idx].color = colorBuff;
        }

        foreach (var offset in disabledRange)
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

    // ─── Rotate Button ────────────────────────────────────────────────

    private void OnRotateClicked()
    {
        _currentTotem?.Rotate();
        Refresh();
    }

    // ─── Effect Text ──────────────────────────────────────────────────

    private void RefreshEffectText(TotemData data, GameDataManager.TotemSheetRow sheet)
    {
        if (totemEffectText == null) return;
        totemEffectText.text = BuildEffectText(data, sheet);
    }

    private static string BuildEffectText(TotemData data, GameDataManager.TotemSheetRow sheet)
    {
        var sb = new StringBuilder();

        // 수치 버프: 시트 우선 → SO fallback
        float atk      = sheet != null ? sheet.AttackBuff        : data.attackBuffAmount;
        float spd      = sheet != null ? sheet.SpeedBuff         : data.speedBuffAmount;
        float fProd    = sheet != null ? sheet.FoodProductionBuff: data.foodSpeedBuffAmount;
        float fAmt     = sheet != null ? sheet.FoodAmountBuff    : data.foodAmountBuffAmount;
        float cCh      = sheet != null ? sheet.CritChanceBuff    : data.critChanceBuffAmount;
        float cDmg     = sheet != null ? sheet.CritDamageBuff    : data.critDamageBuffAmount;

        // 시트 전용 추가 스탯 (SO에 대응 필드 없음)
        float atkDeb   = sheet?.AttackDebuff     ?? 0f;
        float spdDeb   = sheet?.SpeedDebuff      ?? 0f;
        float cooldown = sheet?.CooldownDecrease ?? 0f;
        float projSize = sheet?.ProjectileSizeRate?? 0f;
        float expGain  = sheet?.ExpGainRate      ?? 0f;

        if (atk    > 0f) sb.AppendLine($"공격력 +{atk    * 100f:F0}%");
        if (atkDeb > 0f) sb.AppendLine($"공격력 -{atkDeb * 100f:F0}%");
        if (spd    > 0f) sb.AppendLine($"공격속도 +{spd   * 100f:F0}%");
        if (spdDeb > 0f) sb.AppendLine($"공격속도 -{spdDeb* 100f:F0}%");
        if (fProd  > 0f) sb.AppendLine($"식량 생산 간격 -{fProd * 100f:F0}%");
        if (fAmt   > 0f) sb.AppendLine($"식량 생산량 +{fAmt:F0}");
        if (cCh    > 0f) sb.AppendLine($"치명타 확률 +{cCh   * 100f:F0}%");
        if (cDmg   > 0f) sb.AppendLine($"치명타 피해 +{cDmg  * 100f:F0}%");
        if (cooldown>0f) sb.AppendLine($"쿨타임 -{cooldown * 100f:F0}%");
        if (projSize>0f) sb.AppendLine($"투사체 크기 +{projSize* 100f:F0}%");
        if (expGain > 0f) sb.AppendLine($"경험치 획득 +{expGain* 100f:F0}%");

        // 설명 텍스트: 시트 effect 우선 → SO description fallback
        // 시트의 "{10}" 같은 템플릿 중괄호를 제거해서 "10"만 표시
        string desc = sheet != null && !string.IsNullOrEmpty(sheet.Effect)
                      ? sheet.Effect : data.description;
        if (!string.IsNullOrEmpty(desc))
        {
            desc = desc.Replace("{", "").Replace("}", "");
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(desc);
        }

        return sb.ToString().TrimEnd();
    }

    // ─── Tier Helpers ─────────────────────────────────────────────────

    private static string TierToLabel(Tier tier) => tier switch
    {
        Tier.Normal => "노말",
        Tier.Rare   => "레어",
        Tier.Epic   => "에픽",
        Tier.Legend => "전설",
        _           => "노말",
    };

    private Color TierToColor(Tier tier) => tier switch
    {
        Tier.Normal => colorNormal,
        Tier.Rare   => colorRare,
        Tier.Epic   => colorEpic,
        Tier.Legend => colorLegendary,
        _           => colorNormal,
    };
}
