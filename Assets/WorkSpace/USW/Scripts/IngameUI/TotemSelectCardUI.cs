using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 선택 카드 하나의 UI
///
/// ─ 프리팹 구성 ──────────────────────────────────────────
///   CardRoot  (TotemSelectCardUI)
///     ├── Button (투명 입력 영역 — 런타임에 TotemCardPointerInput 연결)
///     ├── TierBorderImage   (Image  — 등급 테두리)
///     ├── IconBorderImage   (Image  — 아이콘 테두리)
///     ├── IconImage         (Image  — 토템 아이콘)
///     ├── NameText          (TMP_Text — 토템 이름)
///     ├── TierText          (TMP_Text — 노말/레어/에픽/전설)
///     ├── DescriptionText   (TMP_Text — 효과 설명)
///     └── RangeGridContainer (GridLayoutGroup 7×7 — 범위 그리드)
/// </summary>
public class TotemSelectCardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
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

    [Header("Range Grid Sprites")]
    [SerializeField] private Sprite spriteDefault;
    [SerializeField] private Sprite spriteCenter;
    [SerializeField] private Sprite spriteBuff;
    [SerializeField] private Sprite spriteDisabled;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float scaleDuration = 0.2f;



    [Header("Editor Test")]
    [SerializeField] private TotemData testData;

    private const int GridCols   = 6; // A~F
    private const int GridRows   = 4; // 1~4
    private const int TotemCol   = 3; // D (0-indexed)
    private const int TotemRow   = 2; // 3 (0-indexed)

    private readonly List<Image>      _cells     = new();
    private TotemData                 _data;
    private Action<TotemSelectCardUI> _onClicked;

    [Header("Hold to view field")]
    [SerializeField, Min(0.1f)] private float _holdSeconds = 0.4f;
    private UI_Peekthrough _peek;
    private int? _pointerId;
    private int? _releasedPointerId;
    private float _pressedAt;
    private bool _suppressClick;

    /// <summary>길게 누르는 동안 숨길 선택 패널을 연결한다.</summary>
    public void ConfigurePeek(UI_Peekthrough peek)
    {
        CancelPress();
        _peek = peek;
    }

    /// <summary>한 손가락의 짧은 선택 또는 긴 필드 보기를 시작한다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left ||
            _pointerId.HasValue || button == null || !button.IsActive() || !button.IsInteractable()) return;
        _pointerId = eventData.pointerId;
        _releasedPointerId = null;
        _pressedAt = Time.unscaledTime;
        _suppressClick = false;
    }

    private void Update()
    {
        if (!_pointerId.HasValue) return;
        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            CancelPress();
            return;
        }
        if (!_suppressClick && _peek != null && Time.unscaledTime - _pressedAt >= _holdSeconds)
        {
            _suppressClick = true;
            _peek.TryBeginPeek(this);
        }
    }

    /// <summary>홀드 해제는 선택하지 않고 패널 표시만 복구한다.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        if (_peek != null && Time.unscaledTime - _pressedAt >= _holdSeconds) _suppressClick = true;
        _releasedPointerId = eventData.pointerId;
        _pointerId = null;
        _peek?.EndPeek(this);
    }

    /// <summary>카드 밖으로 나간 누름을 취소한다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_pointerId == eventData.pointerId) CancelPress();
    }

    /// <summary>같은 손가락으로 완료한 짧은 탭만 한 번 선택한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_releasedPointerId != eventData.pointerId || eventData.button != PointerEventData.InputButton.Left) return;
        _releasedPointerId = null;
        if (isActiveAndEnabled && !_suppressClick && button != null && button.IsActive() && button.IsInteractable())
            _onClicked?.Invoke(this);
    }

    /// <summary>입력 취소 또는 자식 버튼 비활성화 시 필드 보기를 해제한다.</summary>
    public void CancelPress()
    {
        _pointerId = null;
        _releasedPointerId = null;
        _suppressClick = true;
        _peek?.EndPeek(this);
    }

    private void OnDisable() => CancelPress();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelPress();
    }

    private void Awake()
    {
        if (button != null && button.gameObject != gameObject)
        {
            var input = button.GetComponent<TotemCardPointerInput>();
            if (input == null) input = button.gameObject.AddComponent<TotemCardPointerInput>();
            input.Configure(this);
        }
        CollectOrBuildGrid();
    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(TotemData data, Action<TotemSelectCardUI> onClicked)
    {
        CancelPress();
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
        if (descriptionText != null) descriptionText.text = data?.GetDisplayDescription() ?? string.Empty;
        if (tierText        != null) tierText.text        = TierToLabel(data?.tier ?? Tier.Normal);

        RefreshGrid(data);
    }

    public TotemData GetData() => _data;

    // ── 선택 / 해제 ────────────────────────────────────────────

    public void Select()
    {
        transform.SetAsLastSibling();
        transform.DOKill();
        transform.DOScale(selectedScale, scaleDuration).SetEase(Ease.InOutElastic).SetUpdate(true);
    }

    public void Deselect()
    {
        transform.DOKill();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
    }


    // ── 범위 그리드 ────────────────────────────────────────────

    /// <summary>프리팹에 미리 구성된 셀을 재사용하거나, 없으면 동적으로 생성.</summary>
    private void CollectOrBuildGrid()
    {
        if (rangeGridContainer == null) return;

        int expected = GridCols * GridRows;

        // 기존 자식 Image 수집 시도 (파괴 없이 재사용)
        _cells.Clear();
        for (int i = 0; i < rangeGridContainer.childCount; i++)
        {
            var img = rangeGridContainer.GetChild(i).GetComponent<Image>();
            if (img != null) _cells.Add(img);
        }

        if (_cells.Count == expected) return;

        // 수가 맞지 않으면 새로 빌드
        BuildGrid();
    }

    private void BuildGrid()
    {
        if (rangeGridContainer == null) return;

        ClearGrid();

        for (int i = 0; i < GridCols * GridRows; i++)
        {
            var go  = new GameObject($"Cell_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rangeGridContainer, false);
            var img = go.GetComponent<Image>();
            img.sprite = spriteDefault;
            img.color  = Color.white;
            _cells.Add(img);
        }
    }

    private void EnsureGridBuilt()
    {
        if (_cells.Count == GridCols * GridRows) return;
        CollectOrBuildGrid();
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
        {
            cell.sprite = spriteDefault;
            cell.color  = Color.white;
        }

        // 토템 위치 D3
        _cells[TotemRow * GridCols + TotemCol].sprite = spriteCenter;

        if (data == null) return;

        foreach (var offset in data.GetEffectPreviewOffsets())
        {
            if (TryGetIndex(offset, out int idx))
            {
                _cells[idx].sprite = spriteBuff;
                _cells[idx].color  = Color.white;
            }
        }

        if (!data.HasEffectGroups) return;
        foreach (var group in data.EffectGroups)
        {
            if (group == null) continue;
            foreach (var offset in group.GetPreviewOffsets())
            {
                if (!TryGetIndex(offset, out int idx)) continue;
                _cells[idx].sprite = spriteDefault;
                _cells[idx].color = group.Color;
            }
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
