using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD conversion stone counter built next to the food counter at runtime, so no scene or prefab is edited.
/// Copies the food counter's font and size and punches when the count rises.
/// </summary>
public sealed class ConversionStoneHud : MonoBehaviour
{
    private const float IconSize = 64f;
    private const float Gap = 6f;
    private const float GroupGap = 28f;
    private const float PunchScale = 0.25f;
    private const float PunchSeconds = 0.35f;

    private ConversionStoneWallet _wallet;
    private TMP_Text _count;
    private RectTransform _icon;
    private int _shown;

    /// <summary>Creates the counter beside <paramref name="food"/>'s group and binds it to the wallet.</summary>
    public static ConversionStoneHud Create(TMP_Text food, ConversionStoneWallet wallet, Sprite icon)
    {
        var group = food.transform.parent as RectTransform;
        if (group == null || group.parent == null) return null;
        var root = new GameObject("ConversionStoneHud", typeof(RectTransform)).AddComponent<ConversionStoneHud>();
        var rect = (RectTransform)root.transform;
        rect.SetParent(group.parent, false);
        rect.anchorMin = group.anchorMin; rect.anchorMax = group.anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        float height = Mathf.Max(IconSize, group.rect.height);
        rect.sizeDelta = new Vector2(200f, height);
        // Right edge of the food group, vertically centred on it.
        Vector2 groupRight = group.anchoredPosition + new Vector2((1f - group.pivot.x) * group.rect.width, (0.5f - group.pivot.y) * group.rect.height);
        rect.anchoredPosition = groupRight + new Vector2(GroupGap, 0f);
        rect.SetSiblingIndex(group.GetSiblingIndex() + 1);

        var iconImage = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        iconImage.sprite = icon; iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        root._icon = iconImage.rectTransform;
        root._icon.SetParent(rect, false);
        root._icon.anchorMin = root._icon.anchorMax = new Vector2(0f, 0.5f);
        root._icon.pivot = new Vector2(0f, 0.5f);
        root._icon.sizeDelta = new Vector2(IconSize, IconSize);
        root._icon.anchoredPosition = Vector2.zero;

        var text = Instantiate(food, rect);
        text.name = "Count";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        foreach (var behaviour in text.GetComponents<MonoBehaviour>())
            if (behaviour != text) Destroy(behaviour);
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var textRect = text.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(140f, height);
        textRect.anchoredPosition = new Vector2(IconSize + Gap, 0f);
        textRect.localScale = Vector3.one;
        root._count = text;
        root.Bind(wallet);
        return root;
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
