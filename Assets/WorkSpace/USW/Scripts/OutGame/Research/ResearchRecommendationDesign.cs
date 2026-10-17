using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>최종 골드 팝 추천 배지. 배지는 고정하고 화살표만 움직인다.</summary>
public static class ResearchRecommendationDesign
{
    public static void Build(RectTransform root, ResearchViewSettings settings)
    {
        float half = settings.NodeSize * 0.5f;
        var ink = new Color32(29, 29, 34, 255);
        Color fill = new Color32(255, 195, 12, 255);
        root.sizeDelta = Vector2.one * settings.NodeSize;
        var floating = ResearchUi.NewRect(root, "FloatingBadge");
        floating.sizeDelta = new Vector2(settings.NodeSize + 28f, 56f);
        floating.anchoredPosition = new Vector2(0f, half - 2f);


        var badge = ResearchUi.NewRect(floating, "CasualLabel");
        badge.sizeDelta = new Vector2(114f, 44f);
        badge.anchoredPosition = new Vector2(-half + 35f, 0f);
        float radius = 9f;
        Plate(badge, "Shadow", badge.sizeDelta, new Vector2(0f, -4f), ink, radius);
        Plate(badge, "BrightEdge", badge.sizeDelta, Vector2.zero, Color.Lerp(fill, Color.white, 0.55f), radius);
        Plate(badge, "Fill", badge.sizeDelta - new Vector2(4f, 4f), Vector2.zero, fill, radius - 2f);
        Plate(badge, "Highlight", new Vector2(86f, 2f), new Vector2(0f, 16f), new Color(1f, 1f, 1f, 0.55f), 1f);
        var label = ResearchUi.NewText(badge, "Label", "추천", 29f, settings.FontBold, TextAlignmentOptions.Center, settings.FontMaterial);
        label.color = Color.white;
        label.outlineColor = ink;
        label.outlineWidth = 0.3f;
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.anchoredPosition = new Vector2(0f, -1f);

        var arrowRect = ResearchUi.NewRect(floating, "CasualUpgradeArrow");
        arrowRect.sizeDelta = new Vector2(48f, 54f);
        arrowRect.anchoredPosition = new Vector2(half - 14f, 1f);
        arrowRect.DOAnchorPosY(7f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(arrowRect.gameObject);
        var arrow = arrowRect.gameObject.AddComponent<ResearchRecommendationArrow>();
        arrow.raycastTarget = false;
        var gleam = Plate(arrowRect, "Gleam", new Vector2(8f, 12f), new Vector2(-4f, 14f), Color.white, 4f);
        gleam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);

    }

    private static Graphic Plate(RectTransform root, string name, Vector2 size, Vector2 position, Color color, float radius)
    {
        var rect = ResearchUi.NewRect(root, name);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<ResearchRecommendationCapsule>();
        image.Radius = radius;
        image.raycastTarget = false;
        image.color = color;
        return image;
    }
}
