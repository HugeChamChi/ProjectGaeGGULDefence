using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 선택지 카드 UI (범위 그리드 없는 간소화 버전)
///
/// ─ 프리팹 구성 ──────────────────────────────────────────
///   CardRoot  (TotemSelectionCardUI + Button)
///     ├── TierBorderImage   (Image  — 등급 테두리)
///     ├── IconBorderImage   (Image  — 아이콘 테두리)
///     ├── IconImage         (Image  — 토템 아이콘)
///     ├── NameText          (TMP_Text — 토템 이름)
///     ├── TierText          (TMP_Text — 노말/레어/에픽/전설)
///     └── DescriptionText   (TMP_Text — 효과 설명)
/// </summary>
public class TotemSelectionCardUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    iconBorderImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image    tierBorderImage;
    [SerializeField] private Button   button;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float scaleDuration = 0.2f;

    [Header("Tier Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] tierBorderSprites;

    [Header("Icon Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] iconBorderSprites;

    [Header("Editor Test")]
    [SerializeField] private TotemData testData;

    private TotemData                 _data;
    private Action<TotemSelectionCardUI> _onClicked;

    private void Awake()
    {
        button?.onClick.AddListener(() => _onClicked?.Invoke(this));
    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(TotemData data, Action<TotemSelectionCardUI> onClicked)
    {
        _data      = data;
        _onClicked = onClicked;

        if (iconImage != null)
        {
            iconImage.sprite  = data?.icon;
            iconImage.enabled = data?.icon != null;
        }

        if (nameText        != null) nameText.text        = data?.totemName   ?? string.Empty;
        if (descriptionText != null) descriptionText.text = data?.description ?? string.Empty;
        if (tierText        != null) tierText.text        = TierToLabel(data?.tier ?? Tier.Normal);

        ApplyTierSprites(data?.tier ?? Tier.Normal);
    }

    public TotemData GetData() => _data;

    // ── 선택 / 해제 ────────────────────────────────────────────

    public void Select()
    {
        transform.DOKill();
        transform.DOScale(selectedScale, scaleDuration).SetEase(Ease.InOutElastic);
    }

    public void Deselect()
    {
        transform.DOKill();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.InOutQuad);
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
        if (testData == null) { Debug.LogWarning("[TotemSelectionCardUI] testData가 비어있습니다."); return; }
        Setup(testData, null);
        Debug.Log($"[TotemSelectionCardUI] Test Setup 완료: {testData.totemName} ({testData.tier})");
    }
#endif
}
