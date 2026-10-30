using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class DeckUpgradeEffectTooltip : MonoBehaviour
    {
        private RectTransform root, bubble;
        private CanvasGroup group;
        public bool IsOpen => gameObject.activeSelf;
        public string Body { get; private set; }
        public static DeckUpgradeEffectTooltip Create(RectTransform canvas, TMP_Text source, ResearchViewSettings style)
        {
            var root = Rect("UpgradeEffectTooltip", canvas, 0, 0, 0, 0);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var view = root.gameObject.AddComponent<DeckUpgradeEffectTooltip>(); view.root = root;
            var shield = root.gameObject.AddComponent<Image>(); shield.color = Color.clear;
            root.gameObject.AddComponent<Button>().onClick.AddListener(view.Hide);
            view.bubble = Rect("EffectBubble", root, 0, 0, 700, 220);
            var borderColor = new Color32(55, 38, 39, 255); var faceColor = new Color32(125, 103, 105, 255);
            ImageBox("TailBorder", view.bubble, 0, 220, 26, 26, borderColor, null).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            ImageBox("TailFill", view.bubble, 0, 220, 19, 19, faceColor, null).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            ImageBox("Border", view.bubble, 0, 0, 700, 220, borderColor, style.RoundedFill, style.RoundedCornerScale);
            ImageBox("Face", view.bubble, 4, 4, 692, 212, faceColor, style.RoundedFill, style.RoundedCornerScale);
            var title = Label("EffectTitle", view.bubble, source, 22, 16, 656, 50, 32);
            title.text = "공격속도 증가"; title.color = new Color32(255, 226, 146, 255);
            var body = Label("EffectBody", view.bubble, source, 22, 84, 656, 120, 29);
            var bonuses = new UnitLevelBonusController(UnitBonusChart.Resolve("AlphanChief", new System.Collections.Generic.List<UnitLevelBonusDefinition>()), 12);
            view.Body = $"{bonuses.GetStat(UnitBonusStat.ChiefSpeedDuration):0.##}초간 전체 유닛 공격속도 {bonuses.GetStat(UnitBonusStat.ChiefSpeedPercent)*100f:0.##}% 증가.\n중첩 없이 지속시간만 갱신.";
            body.text = DeckUpgradeBonusView.HighlightNumbers(view.Body);
            view.group = view.bubble.gameObject.AddComponent<CanvasGroup>();
            view.Hide(); return view;
        }
        public void Show(TermLinkTapTarget source, string id)
        {
            if (!source.TryGetLinkWorldBounds(id, out var min, out var max)) return;
            gameObject.SetActive(true); transform.SetAsLastSibling(); Canvas.ForceUpdateCanvases();
            Vector3 top = root.InverseTransformPoint(max);
            Vector3 center = root.InverseTransformPoint((min + max) * .5f);
            float centerX = center.x - root.rect.xMin;
            float left = Mathf.Clamp(centerX - 350, 16, root.rect.width - 716);
            float y = Mathf.Max(12, root.rect.yMax - top.y - 238);
            bubble.anchoredPosition = new Vector2(left, -y);
            foreach (string name in new[] { "TailBorder", "TailFill" })
            {
                var tail = (RectTransform)bubble.Find(name); tail.pivot = new Vector2(.5f, .5f);
                tail.anchoredPosition = new Vector2(Mathf.Clamp(centerX - left, 30, 670), -220);
            }
            bubble.DOKill(); group.DOKill(); bubble.localScale = Vector3.one * .95f; group.alpha = 0;
            bubble.DOScale(1, .12f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            group.DOFade(1, .12f).SetUpdate(true).SetLink(gameObject);
        }
        public void Hide()
        {
            if (bubble != null) bubble.DOKill(); if (group != null) group.DOKill(); gameObject.SetActive(false);
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private static Image ImageBox(string name, Transform parent, float x, float y, float w, float h, Color color, Sprite sprite, float scale = 1)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.sprite = sprite;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = scale; image.raycastTarget = false; return image;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_Text source, float x, float y, float w, float h, float size)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial; text.fontStyle = FontStyles.Bold;
            text.fontSize = size; text.color = new Color32(255, 243, 228, 255); text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; return text;
        }
    }
}
