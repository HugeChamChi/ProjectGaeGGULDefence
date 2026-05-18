using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 선택 카드 하나의 UI
///
/// ─ 프리팹 구성 ──────────────────────────────────────────
///   CardRoot  (TotemSelectCardUI + Button)
///     ├── TierBorderImage   (Image  — 등급 테두리)
///     ├── IconBorderImage   (Image  — 아이콘 테두리)
///     ├── IconImage         (Image  — 토템 아이콘)
///     ├── NameText          (TMP_Text — 토템 이름)
///     ├── TierText          (TMP_Text — 노말/레어/에픽/전설)
///     ├── DescriptionText   (TMP_Text — 효과 설명)
///     └── RangeGridContainer (GridLayoutGroup 7×7 — 범위 그리드)
/// </summary>
public class TotemSelectCardUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    iconBorderImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image    tierBorderImage;
    [SerializeField] private Button   button;

    [Header("Range Grid")]
    [SerializeField] private Transform rangeGridContainer;
    [SerializeField] private Sprite    rangeCellSprite;

    [Header("Range Grid Colors")]
    [SerializeField] private Color colorDefault  = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color colorCenter   = new Color(0.57f, 0.82f, 0.31f, 1f);
    [SerializeField] private Color colorBuff     = new Color(0.64f, 0.00f, 0.00f, 1f);
    [SerializeField] private Color colorDisabled = new Color(0.10f, 0.10f, 0.10f, 1f);

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float scaleDuration = 0.2f;

    [Header("Tier Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] tierBorderSprites;

    [Header("Icon Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] iconBorderSprites;

    [Header("Editor Test")]
    [SerializeField] private TotemData testData;

    private const int GridCols   = 6; // A~F
    private const int GridRows   = 4; // 1~4
    private const int TotemCol   = 3; // D (0-indexed)
    private const int TotemRow   = 2; // 3 (0-indexed)

    private readonly List<Image>      _cells     = new();
    private TotemData                 _data;
    private Action<TotemSelectCardUI> _onClicked;

    private void Awake()
    {
        button?.onClick.AddListener(() => _onClicked?.Invoke(this));
        BuildGrid();
    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(TotemData data, Action<TotemSelectCardUI> onClicked)
    {
        _data      = data;
        _onClicked = onClicked;
        EnsureGridBuilt();

        if (iconImage != null)
        {
            var sprite = data != null ? data.DisplaySprite : null;
            iconImage.sprite  = sprite;
            iconImage.enabled = sprite != null;
        }

        if (nameText        != null) nameText.text        = data?.totemName   ?? string.Empty;
        if (descriptionText != null) descriptionText.text = data?.description ?? string.Empty;
        if (tierText        != null) tierText.text        = TierToLabel(data?.tier ?? Tier.Normal);

        ApplyTierSprites(data?.tier ?? Tier.Normal);
        RefreshGrid(data);
    }

    public TotemData GetData() => _data;

    // ── 선택 / 해제 ────────────────────────────────────────────

    public void Select()
    {
        transform.DOKill();
        transform.DOScale(selectedScale, scaleDuration).SetEase(Ease.InOutElastic).SetUpdate(true);
    }

    public void Deselect()
    {
        transform.DOKill();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
    }

    // ── 등급 스프라이트 ────────────────────────────────────────

    private void ApplyTierSprites(Tier tier)
    {
        int idx = (int)tier;

        if (tierBorderImage != null && tierBorderSprites != null && idx < tierBorderSprites.Length)
            tierBorderImage.sprite = tierBorderSprites[idx];

        if (iconBorderImage != null && iconBorderSprites != null && idx < iconBorderSprites.Length)
            iconBorderImage.sprite = iconBorderSprites[idx];
    }

    // ── 범위 그리드 ────────────────────────────────────────────

    private void BuildGrid()
    {
        if (rangeGridContainer == null) return;

        ClearGrid();

        for (int i = 0; i < GridCols * GridRows; i++)
        {
            var go  = new GameObject($"Cell_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rangeGridContainer, false);
            var img = go.GetComponent<Image>();
            img.sprite = rangeCellSprite;
            img.color  = colorDefault;
            _cells.Add(img);
        }
    }

    private void EnsureGridBuilt()
    {
        int expectedCount = GridCols * GridRows;
        if (_cells.Count == expectedCount && rangeGridContainer != null && rangeGridContainer.childCount == expectedCount)
            return;

        BuildGrid();
    }

    private void ClearGrid()
    {
        if (rangeGridContainer == null) return;

        for (int i = rangeGridContainer.childCount - 1; i >= 0; i--)
        {
            var child = rangeGridContainer.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false);
                Destroy(child);
            }
            else DestroyImmediate(child);
        }
        _cells.Clear();
    }

    private void RefreshGrid(TotemData data)
    {
        if (_cells.Count == 0) return;

        foreach (var cell in _cells)
            cell.color = colorDefault;

        // 토템 위치 D3
        _cells[TotemRow * GridCols + TotemCol].color = colorCenter;

        if (data == null) return;

        foreach (var offset in data.effectRange)
        {
            if (TryGetIndex(offset, out int idx))
                _cells[idx].color = colorBuff;
        }

        foreach (var offset in data.attackDisabledRange)
        {
            if (TryGetIndex(offset, out int idx))
                _cells[idx].color = colorDisabled;
        }
    }

    private bool TryGetIndex(Vector2Int offset, out int index)
    {
        int col = TotemCol + offset.x;
        int row = TotemRow + offset.y;
        if (col < 0 || col >= GridCols || row < 0 || row >= GridRows)
        {
            index = -1;
            return false;
        }
        index = row * GridCols + col;
        return true;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────

    private static string TierToLabel(Tier tier) => tier switch
    {
        Tier.Normal => "노말",
        Tier.Rare   => "레어",
        Tier.Epic   => "에픽",
        Tier.Legend => "전설",
        _           => "노말",
    };

    private void OnDestroy() => transform.DOKill();

#if UNITY_EDITOR
    [ContextMenu("Test - Setup with testData")]
    private void EditorTestSetup()
    {
        if (testData == null) { Debug.LogWarning("[TotemSelectCardUI] testData가 비어있습니다."); return; }

        // 기존 셀 정리 후 재생성 (에디터에서 여러 번 실행 대비)
        foreach (var c in _cells) if (c != null) DestroyImmediate(c.gameObject);
        _cells.Clear();
        BuildGrid();

        Setup(testData, null);
        Debug.Log($"[TotemSelectCardUI] Test Setup 완료: {testData.totemName} ({testData.tier})");
    }

    [ContextMenu("Test - Clear Grid")]
    private void EditorTestClear()
    {
        if (rangeGridContainer == null) return;
        for (int i = rangeGridContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(rangeGridContainer.GetChild(i).gameObject);
        _cells.Clear();
    }
#endif
}
