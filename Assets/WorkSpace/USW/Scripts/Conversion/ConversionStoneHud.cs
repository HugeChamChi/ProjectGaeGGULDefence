using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD conversion stone counter. At runtime it widens the food bar above the summon button and splits it into two slots,
/// food on the left and conversion stones on the right, so no scene or prefab is edited. Punches the icon when the count rises.
/// </summary>
public sealed class ConversionStoneHud : MonoBehaviour
{
    // Canvas units inside the food bar (reference 1080 wide). Food text fits 6 digits at the scene's font size 40.
    private const float SidePadding = 14f;
    private const float IconTextGap = 6f;
    private const float FoodTextWidth = 132f;
    private const float StoneIconSize = 52f;      // the gem art fills ~74% of its square, so it reads as wide as the 36 food icon
    private const float StoneIconInset = -7f;     // pulls the gem's transparent margin toward the divider
    private const float StoneTextWidth = 52f;
    private const float SlotGap = 12f;            // each side of the divider
    private const float DividerWidth = 2f;
    private const float DividerHeightRatio = 0.55f;
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.28f);
    private const float PunchScale = 0.25f;
    private const float PunchSeconds = 0.35f;

    private ConversionStoneWallet _wallet;
    private TMP_Text _count;
    private RectTransform _icon;
    private int _shown;

    /// <summary>Widens <paramref name="food"/>'s bar, moves food into its left slot and builds the stone slot on the right.</summary>
    public static ConversionStoneHud Create(TMP_Text food, ConversionStoneWallet wallet, Sprite icon)
    {
        var bar = food.transform.parent as RectTransform;
        if (bar == null) return null;
        float height = bar.rect.height;
        RectTransform foodIcon = null;
        foreach (Transform child in bar)
            if (child != food.transform && child.TryGetComponent<Image>(out _)) { foodIcon = (RectTransform)child; break; }
        float foodIconWidth = foodIcon != null ? foodIcon.rect.width * Mathf.Abs(foodIcon.localScale.x) : 0f;

        float leftWidth = SidePadding + foodIconWidth + IconTextGap + FoodTextWidth + SlotGap;
        float rightWidth = SlotGap + StoneIconInset + StoneIconSize + IconTextGap + StoneTextWidth + SidePadding;
        float width = leftWidth + DividerWidth + rightWidth;
        // Keep the authored centre (aligned with the summon button) and grow both ways.
        bar.sizeDelta = new Vector2(width, bar.sizeDelta.y);

        if (foodIcon != null) Place(foodIcon, SidePadding, foodIcon.sizeDelta);
        var foodRect = food.rectTransform;
        food.alignment = TextAlignmentOptions.MidlineLeft;
        food.textWrappingMode = TextWrappingModes.NoWrap;
        Place(foodRect, SidePadding + foodIconWidth + IconTextGap, new Vector2(FoodTextWidth, height));

        var divider = new GameObject("ConversionDivider", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        divider.color = DividerColor; divider.raycastTarget = false;
        divider.rectTransform.SetParent(bar, false);
        Place(divider.rectTransform, leftWidth, new Vector2(DividerWidth, height * DividerHeightRatio));

        var root = new GameObject("ConversionStoneHud", typeof(RectTransform)).AddComponent<ConversionStoneHud>();
        var rect = (RectTransform)root.transform;
        rect.SetParent(bar, false);
        Place(rect, leftWidth + DividerWidth, new Vector2(rightWidth, height));

        var iconImage = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        iconImage.sprite = icon; iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        root._icon = iconImage.rectTransform;
        root._icon.SetParent(rect, false);
        Place(root._icon, SlotGap + StoneIconInset, new Vector2(StoneIconSize, StoneIconSize));

        var text = Instantiate(food, rect);
        text.name = "Count";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        foreach (var behaviour in text.GetComponents<MonoBehaviour>())
            if (behaviour != text) Destroy(behaviour);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.localScale = Vector3.one;
        Place(text.rectTransform, SlotGap + StoneIconInset + StoneIconSize + IconTextGap, new Vector2(StoneTextWidth, height));
        root._count = text;
        root.Bind(wallet);
        return root;
    }

    // Left-anchored, vertically centred placement inside the bar or a slot.
    private static void Place(RectTransform rect, float x, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    private void Bind(ConversionStoneWallet wallet)
    {
        _wallet = wallet;
        _shown = wallet.Stones;
        _count.text = _shown.ToString();
        wallet.OnChanged += Refresh;
    }

    private void Refresh(int stones)
    {
        if (_count == null) return;
        bool rose = stones > _shown;
        _shown = stones;
        _count.text = stones.ToString();
        if (!rose || _icon == null) return;
        _icon.DOKill(true);
        _icon.localScale = Vector3.one;
        _icon.DOPunchScale(Vector3.one * PunchScale, PunchSeconds, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        if (_wallet != null) _wallet.OnChanged -= Refresh;
        _icon?.DOKill();
    }
}
