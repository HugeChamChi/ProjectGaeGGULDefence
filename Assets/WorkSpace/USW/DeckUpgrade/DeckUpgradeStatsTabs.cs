using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    [DefaultExecutionOrder(1100)]
    public sealed class DeckUpgradeStatsTabs : MonoBehaviour
    {
        private RectTransform track;
        private readonly RectTransform[] pages = new RectTransform[3];
        private readonly Image[] faces = new Image[3];
        private readonly TMP_Text[] _faceLabels = new TMP_Text[3];
        private TMP_Text sourceAttack, sourceSpeed, attackValue, speedValue, sourceTitle;
        private Transform skillContent, bonusContent;
        private Coroutine motion;
        private string observedTitle;
        private TMP_Text upgradePrice, sourcePrice;
        private UpgradeCostGauge upgradeGauge, sourceGauge;
        public int SelectedTab { get; private set; }
        public bool Sliding => motion != null;
        private DroneUpgradeConceptScreen screen;
        private ResearchViewSettings style;
        private bool _useLobbyStyle;
        public void Build(DeckSelectorConcept selector)
        {
            screen = selector.screen; style = screen.style;
            var dialog = (RectTransform)transform;
            var stats = dialog.Find("StatsPanel");
            var rows = stats.Find("UpgradeStatRows");
            sourceAttack = rows.Find("AttackRow/Stat").GetComponent<TMP_Text>();
            sourceSpeed = rows.Find("AttackSpeedRow/SpeedStat").GetComponent<TMP_Text>();
            var bindings = dialog.parent.parent.Find("RuntimeBindings");
            sourceAttack.transform.SetParent(bindings, false); sourceSpeed.transform.SetParent(bindings, false);
            Remove(rows.gameObject);
            foreach (string frameName in new[] { "ChiefSlotFrame", "UnitSlotFrame" })
            {
                var frame = (RectTransform)stats.Find("SelectedUnitSlot/" + frameName);
                frame.localScale = Vector3.one * .82f; frame.anchoredPosition = new Vector2(12, -12);
            }
            var name = (RectTransform)stats.Find("DetailUnitName"); name.anchoredPosition = new Vector2(240, -28); name.sizeDelta = new Vector2(584, 62);
            name.GetComponent<TMP_Text>().fontSize = 44; name.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.TopLeft;
            var level = (RectTransform)stats.Find("DetailUnitLevel"); level.anchoredPosition = new Vector2(240, -109); level.sizeDelta = new Vector2(584, 42);
            var gauge = (RectTransform)stats.Find("PopupGoldGauge"); gauge.anchoredPosition = new Vector2(240, -172); gauge.sizeDelta = new Vector2(584, 36);
            ((RectTransform)gauge.Find("UpgradeCost")).sizeDelta = new Vector2(536, 36);
            Box("InfoDivider", stats, 24, 283, 812, 2, new Color32(80, 109, 119, 255)).sprite = null;
            Remove(stats.Find("PopupGoldGauge/GoldIcon").gameObject);
            Remove(dialog.Find("UpgradeButton/RequiredGoldCost/GoldIcon").gameObject);
            var costText = (RectTransform)dialog.Find("UpgradeButton/RequiredGoldCost/RequiredCost");
            costText.anchorMin = costText.anchorMax = costText.pivot = new Vector2(.5f, .5f);
            costText.anchoredPosition = Vector2.zero; costText.sizeDelta = new Vector2(468, 40);
            var buttonGauge = (RectTransform)costText.parent; buttonGauge.anchoredPosition = new Vector2(100, -72); buttonGauge.sizeDelta = new Vector2(480, 40);
            upgradePrice = costText.GetComponent<TMP_Text>(); sourcePrice = gauge.Find("UpgradeCost").GetComponent<TMP_Text>();
            sourceGauge = gauge.GetComponent<UpgradeCostGauge>();
            upgradeGauge = UpgradeCostGauge.Create(buttonGauge, style.RoundedFill, style.UpgradeArrow); costText.SetAsLastSibling();
            sourceTitle = dialog.Find("SelectedName").GetComponent<TMP_Text>();
            var scrollRoot = dialog.Find("SpecialAbilityScroll");
            scrollRoot.GetComponent<ScrollRect>().enabled = false;
            var viewport = (RectTransform)scrollRoot.Find("AbilityViewport");
            skillContent = viewport.Find("AbilityContent"); bonusContent = viewport.Find("LevelBonusContent");
            track = Rect("TabPageTrack", viewport, 0, 0, 2484, 272);
            for (int i = 0; i < 3; i++)
            {
                var page = Rect(i == 0 ? "SkillPage" : i == 1 ? "BonusPage" : "StatsPage", track, i * 828, 0, 828, 272);
                pages[i] = page;
                var image = page.gameObject.AddComponent<Image>(); image.color = Color.clear;
                page.gameObject.AddComponent<RectMask2D>();
            }
            skillContent.SetParent(pages[0], false); bonusContent.SetParent(pages[1], false);
            var statsContent = Rect("CurrentStats", pages[2], 0, 0, 828, 272);
            attackValue = StatRow(statsContent, "Attack", selector.attackIcon, 20);
            speedValue = StatRow(statsContent, "Speed", selector.attackSpeedIcon, 142);
            var contents = new[] { (RectTransform)skillContent, (RectTransform)bonusContent, statsContent };
            for (int i = 0; i < 3; i++)
            {
                var scroll = pages[i].gameObject.AddComponent<ScrollRect>(); scroll.viewport = pages[i]; scroll.content = contents[i];
                scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            faces[2] = Box("StatsTab", dialog, 48, 562, 278, 56, new Color32(0, 148, 166, 255));
            Label("TabLabel", "\uC2A4\uD0EF", faces[2].transform, 0, 0, 278, 56, 29);
            faces[0] = dialog.Find("SkillTab").GetComponent<Image>(); faces[1] = dialog.Find("LevelBonusTab").GetComponent<Image>();
            for (int i = 0; i < 3; i++)
            {
                int index = i; var rt = faces[i].rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(48 + i * 291, -562); rt.sizeDelta = new Vector2(278, 56);
                var label = (RectTransform)faces[i].transform.Find("TabLabel"); label.sizeDelta = new Vector2(278, 56);
                _faceLabels[i] = label.GetComponent<TMP_Text>();
                var button = faces[i].GetComponent<Button>() ?? faces[i].gameObject.AddComponent<Button>();
                faces[i].raycastTarget = true; button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => Select(index, true));
            }
            dialog.Find("UpgradeButton").GetComponent<Button>().onClick.AddListener(AfterUpgrade);
            Select(0, false);
        }
        private TMP_Text StatRow(Transform parent, string name, Sprite icon, float y)
        {
            var row = Box(name + "CurrentStat", parent, 12, y, 804, 102, new Color32(49, 80, 93, 255));
            var well = Box("IconSlot", row.transform, 16, 20, 62, 62, new Color32(28, 48, 57, 255));
            var symbol = Box("Icon", well.transform, 8, 8, 46, 46, Color.white); symbol.sprite = icon; symbol.type = Image.Type.Simple; symbol.preserveAspect = true;
            return Label("CurrentValue", "", row.transform, 98, 0, 676, 102, 34);
        }
        private void OnEnable() { if (track != null) { Select(0, false); observedTitle = sourceTitle.text; } }
        private void OnDisable() { if (motion != null) StopCoroutine(motion); motion = null; }
        private void AfterUpgrade()
        {
            if (sourceTitle.text != observedTitle) Select(2, true);
            observedTitle = sourceTitle.text;
        }
        private void LateUpdate()
        {
            if (track == null) return;
            skillContent.gameObject.SetActive(true); bonusContent.gameObject.SetActive(true);
            attackValue.text = CurrentOnly(sourceAttack.text);
            speedValue.text = CurrentOnly(sourceSpeed.text) + (sourceSpeed.text.StartsWith("\uACF5\uACA9\uC18D\uB3C4") ? " \uD68C/\uCD08" : "");
            for (int i = 0; i < 3; i++)
            {
                faces[i].color = _useLobbyStyle ? (i == SelectedTab ? LobbyUtilityVisuals.CardFeatured : LobbyUtilityVisuals.Sheet)
                    : i == SelectedTab ? new Color32(0, 148, 166, 255) : new Color32(38, 57, 71, 255);
                if (_useLobbyStyle) _faceLabels[i].color = i == SelectedTab ? LobbyUtilityVisuals.Accent : LobbyUtilityVisuals.Muted;
            }
            int slash = sourcePrice.text.IndexOf('/');
            upgradePrice.text = _useLobbyStyle && slash >= 0 ? sourcePrice.text.Substring(slash + 1).Trim() : sourcePrice.text;
            if (slash >= 0 && int.TryParse(sourcePrice.text.Substring(0, slash).Trim().Replace(",", ""), out int available) && int.TryParse(sourcePrice.text.Substring(slash + 1).Trim().Replace(",", ""), out int required))
                upgradeGauge.SetAmount(available, required, sourceGauge.Ready);
            else upgradeGauge.SetAmount(0, 1, false);
            observedTitle = sourceTitle.text;
        }
        public void Select(int index, bool animate)
        {
            if (motion != null) StopCoroutine(motion);
            SelectedTab = index;
            if (animate && Application.isPlaying && gameObject.activeInHierarchy) motion = StartCoroutine(Slide(-index * 828));
            else { track.anchoredPosition = new Vector2(-index * 828, 0); motion = null; }
        }

        /// <summary>Uses the shared lobby tab palette and gives each content page the taller viewport.</summary>
        public void UseLobbyStyle(float viewportHeight)
        {
            _useLobbyStyle = true;
            track.sizeDelta = new Vector2(track.sizeDelta.x, viewportHeight);
            foreach (var item in pages) item.sizeDelta = new Vector2(item.sizeDelta.x, viewportHeight);
            for (int i = 0; i < 3; i++)
            {
                faces[i].color = i == SelectedTab ? LobbyUtilityVisuals.CardFeatured : LobbyUtilityVisuals.Sheet;
                _faceLabels[i].color = i == SelectedTab ? LobbyUtilityVisuals.Accent : LobbyUtilityVisuals.Muted;
            }
        }
        private IEnumerator Slide(float target)
        {
            float start = track.anchoredPosition.x;
            for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
            {
                track.anchoredPosition = new Vector2(Mathf.Lerp(start, target, 1 - Mathf.Pow(1 - t / .22f, 3)), 0); yield return null;
            }
            track.anchoredPosition = new Vector2(target, 0); motion = null;
        }
        private static string CurrentOnly(string text)
        {
            int arrow = text.IndexOf('\u2192'); int chevron = text.IndexOf('\u203A');
            int end = arrow < 0 ? chevron : chevron < 0 ? arrow : Mathf.Min(arrow, chevron);
            return Regex.Replace(end >= 0 ? text.Substring(0, end).Trim() : text, "<[^>]+>", "");
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        private Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rt = Rect(name, parent, x, y, w, h); var image = rt.gameObject.AddComponent<Image>(); image.color = color;
            image.sprite = style.RoundedFill; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = style.RoundedCornerScale; image.raycastTarget = false; return image;
        }
        private TMP_Text Label(string name, string text, Transform parent, float x, float y, float w, float h, float size)
        {
            var rt = Rect(name, parent, x, y, w, h); var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style.FontBold; label.fontStyle = FontStyles.Bold; label.text = text; label.fontSize = size;
            label.alignment = TextAlignmentOptions.MidlineRight; label.color = new Color32(255, 243, 228, 255); label.raycastTarget = false;
            if (name == "TabLabel") label.alignment = TextAlignmentOptions.Center; return label;
        }
        private static void Remove(GameObject old) { old.name = "RemovedUnusedView"; old.SetActive(false); if (Application.isPlaying) Destroy(old); else DestroyImmediate(old); }
    }
}
