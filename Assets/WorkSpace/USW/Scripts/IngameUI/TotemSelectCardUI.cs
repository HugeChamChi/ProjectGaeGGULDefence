using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 선택 카드 하나의 UI
///
/// ─ 프리팹 구성 ──────────────────────────────────────────
///   CardRoot  (TotemSelectCardUI + Button)
///     ├── TierBorderImage  (Image  — 등급 색 테두리)
///     ├── IconImage        (Image  — 토템 아이콘)
///     ├── NameText         (TMP_Text — 토템 이름, 등급 색)
///     ├── TierText         (TMP_Text — 노말/레어/에픽/전설)
///     └── DescriptionText  (TMP_Text — 효과 설명)
/// </summary>
public class TotemSelectCardUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image    tierBorderImage;
    [SerializeField] private Button   button;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float scaleDuration = 0.2f;

    [Header("Tier Colors")]
    [SerializeField] private Color colorNormal    = new Color(0.600f, 0.773f, 1.000f, 1f);
    [SerializeField] private Color colorRare      = new Color(0.753f, 0.627f, 0.976f, 1f);
    [SerializeField] private Color colorEpic      = new Color(0.859f, 0.588f, 0.016f, 1f);
    [SerializeField] private Color colorLegendary = new Color(0.859f, 0.588f, 0.016f, 1f);

    private TotemData                    _data;
    private Action<TotemSelectCardUI>    _onClicked;

    private void Awake()
    {
        button?.onClick.AddListener(() => _onClicked?.Invoke(this));
    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(TotemData data, Action<TotemSelectCardUI> onClicked)
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

        ApplyTierColor(data?.tier ?? Tier.Normal);
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

    // ── 등급 색상 ──────────────────────────────────────────────

    private void ApplyTierColor(Tier tier)
    {
        var c = TierToColor(tier);
        if (nameText       != null) { nameText.color       = c; }
        if (tierText       != null) { tierText.text        = TierToLabel(tier); tierText.color = c; }
        if (tierBorderImage!= null)   tierBorderImage.color = c;
    }

    private Color TierToColor(Tier tier) => tier switch
    {
        Tier.Normal => colorNormal,
        Tier.Rare   => colorRare,
        Tier.Epic   => colorEpic,
        Tier.Legend => colorLegendary,
        _           => colorNormal,
    };

    private static string TierToLabel(Tier tier) => tier switch
    {
        Tier.Normal => "노말",
        Tier.Rare   => "레어",
        Tier.Epic   => "에픽",
        Tier.Legend => "전설",
        _           => "노말",
    };

    private void OnDestroy() => transform.DOKill();
}
