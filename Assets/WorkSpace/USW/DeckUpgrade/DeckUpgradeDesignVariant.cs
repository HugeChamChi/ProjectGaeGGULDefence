using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class DeckUpgradeDesignVariant : MonoBehaviour
    {
        [Range(1, 3)] public int variant = 1;
        public GameObject lobbyTopTemplate;
        [SerializeField] private TMP_FontAsset _popupFont;
        [SerializeField] private Sprite _popupRound;
        [SerializeField] private Sprite _upgradeButtonSprite;
        private Transform page, grid;
        private bool lastOwnedOnly;
        private DeckSelectorConcept _selector;
        /// <summary>Detail tabs created during explicit layout composition.</summary>
        public DeckUpgradeStatsTabs Tabs { get; private set; }

        /// <summary>Applies the selected design after the collection layout has been built.</summary>
        public void Apply()
        {
            if (_selector != null) _selector.OnFilterChanged -= Arrange;
            _selector = GetComponent<DeckSelectorConcept>();
            page = transform.Find("DroneUpgradeCanvas/SafeArea/Page");
            page.Find("Header").gameObject.SetActive(false);
            var banner = page.Find("CollectionBanner").GetComponent<Image>();
            page.GetComponent<Image>().color = banner.color;
            transform.Find("DroneUpgradeCanvas").GetComponent<Image>().color = banner.color;
            banner.sprite = null;
            var divider = (RectTransform)page.Find("CollectionBanner/BannerEdge");
            divider.anchoredPosition = new Vector2(0, -390); divider.sizeDelta = new Vector2(1080, 32);
            divider.GetComponent<Image>().sprite = null;
            if (lobbyTopTemplate != null)
            {
                var top = (RectTransform)Instantiate(lobbyTopTemplate, page, false).transform;
                top.name = "LobbyTopPreview"; top.anchorMin = top.anchorMax = new Vector2(.5f, 1); top.pivot = new Vector2(.5f, 1);
                top.sizeDelta = new Vector2(1080, 213); top.anchoredPosition = new Vector2(0, 12); top.localScale = Vector3.one;
                top.SetAsLastSibling(); page.Find("UnitDetailsPopup").SetAsLastSibling();
            }
            var summary = (RectTransform)page.Find("DeckCollection/DeckSummaryStrip");
            summary.GetComponent<Image>().sprite = null;
            summary.anchoredPosition = new Vector2(-24, 0); summary.sizeDelta = new Vector2(1080, 64);
            foreach (RectTransform item in summary) item.anchoredPosition += new Vector2(24, 0);
            grid = page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
            foreach (var image in page.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "DroneFrameTrim" || image.name == "SlotNamePlate") image.gameObject.SetActive(false);
                if (image.name == "ChiefUnit" || image.name.StartsWith("Unit_") || image.name.StartsWith("LockedUnit_")) image.color = Color.clear;
                if (variant == 3 && image.name == "CardShadow") image.gameObject.SetActive(false);
                if (image.name == "PortraitInset" && image.transform.parent.name == "ColoredEdge")
                {
                    var rect = image.rectTransform;
                    float inset = variant == 2 ? 12 : variant == 3 ? 3 : 7;
                    rect.anchoredPosition = new Vector2(inset, -inset);
                    rect.sizeDelta = new Vector2(222 - inset * 2, 282 - inset * 2);
                }
            }
            foreach (var label in page.GetComponentsInChildren<TMP_Text>(true)) label.fontStyle |= FontStyles.Bold;
            foreach (var trim in page.GetComponentsInChildren<Transform>(true))
                if (trim.name == "DroneFrameTrim" && trim.Find("ChiefCrestBase") != null) AddChiefEffects(trim);
            Arrange();
            RemoveUnusedViews();
            var dialog = page.Find("UnitDetailsPopup/UnitInfoDialog");
            var tabs = dialog.gameObject.AddComponent<DeckUpgradeStatsTabs>();
            Tabs = tabs;
            tabs.Build(GetComponent<DeckSelectorConcept>(), variant != 1);
            if (variant == 1) ApplyCleanLayout((RectTransform)dialog, tabs);
            _selector.OnFilterChanged += Arrange;
        }

        private void ApplyCleanLayout(RectTransform dialog, DeckUpgradeStatsTabs tabs)
        {
            var banner = page.Find("CollectionBanner");
            for (int i = banner.childCount - 1; i >= 0; i--)
            {
                var child = banner.GetChild(i);
                if (child.name.StartsWith("ArtworkPanel_") || child.name.StartsWith("BannerCombatDrone_")) RemoveView(child.gameObject);
            }
            Paint(page, LobbyUtilityVisuals.Sheet);
            Paint(transform.Find("DroneUpgradeCanvas"), LobbyUtilityVisuals.Sheet);
            Paint(banner, LobbyUtilityVisuals.Card);
            Paint(banner.Find("BannerEdge"), LobbyUtilityVisuals.Line);
            Paint(page.Find("DeckCollection/DeckSummaryStrip"), LobbyUtilityVisuals.Sheet);
            Paint(page.Find("DeckCollection/UnitListPanel"), LobbyUtilityVisuals.Surface);
            foreach (Transform card in grid)
            {
                bool chief = card.name == "ChiefUnit";
                if (!chief && !card.name.StartsWith("Unit_")) continue;
                var cost = card.Find(chief ? "ChiefGoldGauge" : "UnitFrame/CostStrip");
                if (cost != null) RemoveView(cost.gameObject);
                var level = (RectTransform)card.Find(chief ? "Level" : "UnitFrame/Level");
                Place(level, 7, 246, 218, 42);
                level.GetComponent<TMP_Text>().color = Color.white;
                if (!chief)
                {
                    Paint(card.Find("UnitFrame/ColoredEdge"), LobbyUtilityVisuals.Line);
                    Paint(card.Find("UnitFrame/ColoredEdge/PortraitInset"), LobbyUtilityVisuals.Card);
                }
            }

            dialog.sizeDelta = new Vector2(956, 1540);
            Paint(dialog, LobbyUtilityVisuals.Line);
            Place((RectTransform)dialog.Find("DialogPaper"), 2, 2, 952, 1536);
            Paint(dialog.Find("DialogPaper"), LobbyUtilityVisuals.Sheet);
            Paint(dialog.parent, new Color(0, 0, 0, .62f));
            var header = NewBox("PopupHeader", dialog, LobbyUtilityVisuals.Card);
            Place(header, 2, 2, 952, 94);
            var close = (RectTransform)dialog.Find("CloseDetails");
            Place(close, 860, 14, 72, 72); Paint(close, LobbyUtilityVisuals.CardFeatured); close.SetAsLastSibling();
            close.GetComponentInChildren<TMP_Text>().color = LobbyUtilityVisuals.Muted;

            var stage = (RectTransform)dialog.Find("PreviewStage"); Place(stage, 54, -180, 848, 356);
            var motes = stage.GetComponent<RisingUpgradeMotes>();
            if (motes != null) motes.enabled = false;
            for (int i = stage.childCount - 1; i >= 0; i--)
                if (stage.GetChild(i).name != "PopupSpinePreview" && stage.GetChild(i).name != "PreviewShadow") RemoveView(stage.GetChild(i).gameObject);
            Place((RectTransform)stage.Find("PopupSpinePreview"), 216, 0, 416, 328);
            Place((RectTransform)stage.Find("PreviewShadow"), 282, 310, 284, 26);
            stage.SetAsLastSibling(); close.SetAsLastSibling();
            var stats = (RectTransform)dialog.Find("StatsPanel"); Place(stats, 48, 224, 860, 264); Paint(stats, LobbyUtilityVisuals.Card);
            RemoveView(stats.Find("PopupGoldGauge").gameObject);
            Place((RectTransform)stats.Find("DetailUnitName"), 252, 48, 576, 62);
            Place((RectTransform)stats.Find("DetailUnitLevel"), 252, 128, 576, 48);
            stats.Find("DetailUnitLevel").GetComponent<TMP_Text>().color = LobbyUtilityVisuals.Muted;
            Place((RectTransform)stats.Find("SelectedUnitSlot"), 12, 4, 226, 256);
            stats.Find("InfoDivider").gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                var tab = (RectTransform)dialog.Find(i == 0 ? "SkillTab" : i == 1 ? "LevelBonusTab" : "StatsTab");
                Place(tab, 48 + i * 291, 528, 278, 64);
                Place((RectTransform)tab.Find("TabLabel"), 0, 0, 278, 64);
            }
            var scroll = (RectTransform)dialog.Find("SpecialAbilityScroll"); Place(scroll, 48, 620, 860, 612); Paint(scroll, LobbyUtilityVisuals.Sheet);
            var viewport = (RectTransform)scroll.Find("AbilityViewport"); Place(viewport, 16, 16, 828, 580);
            tabs.UseLobbyStyle(580);
            foreach (var image in scroll.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "SkillNamePanel" || image.name == "RequiredLevel") image.color = LobbyUtilityVisuals.CardFeatured;
                else if (image.name.StartsWith("LevelBonus_") || image.name.EndsWith("CurrentStat")) image.color = LobbyUtilityVisuals.Card;
                else if (image.name == "IconSlot") image.color = LobbyUtilityVisuals.Sheet;
            }

            var button = (RectTransform)dialog.Find("UpgradeButton");
            var price = (RectTransform)button.Find("RequiredGoldCost"); Place(price, 198, 82, 364, 44);
            var priceBackground = price.GetComponent<Image>(); priceBackground.color = new Color32(68, 47, 14, 255); priceBackground.sprite = _popupRound;
            priceBackground.type = Image.Type.Sliced; priceBackground.pixelsPerUnitMultiplier = 1f;
            var coin = NewBox("GoldIcon", price, Color.white); Place(coin, 105, 6, 32, 32);
            coin.GetComponent<Image>().sprite = GetComponent<DroneUpgradeConceptScreen>().style.GoldIcon;
            coin.GetComponent<Image>().type = Image.Type.Simple; coin.GetComponent<Image>().preserveAspect = true;
            var value = price.Find("RequiredCost").GetComponent<TMP_Text>(); Place(value.rectTransform, 145, 0, 150, 44);
            value.alignment = TextAlignmentOptions.MidlineLeft; value.fontSize = 29; value.color = Color.white;
            Place(button, 98, 1342, 760, 150); button.pivot = Vector2.one * .5f; button.anchoredPosition += new Vector2(380, -75);
            var buttonImage = button.GetComponent<Image>(); buttonImage.sprite = _upgradeButtonSprite; buttonImage.type = Image.Type.Simple;
            buttonImage.color = Color.white; buttonImage.preserveAspect = true; buttonImage.pixelsPerUnitMultiplier = 1f;
            dialog.Find("ButtonShadow").gameObject.SetActive(false);
            var action = button.Find("UpgradeLabel").GetComponent<TMP_Text>(); Place(action.rectTransform, 32, 17, 696, 56);
            action.alignment = TextAlignmentOptions.Center; action.fontSize = 38; action.color = LobbyUtilityVisuals.Sheet;
            foreach (var label in page.GetComponentsInChildren<TMP_Text>(true))
            {
                if (_popupFont != null) { label.font = _popupFont; label.fontSharedMaterial = _popupFont.material; label.fontStyle = FontStyles.Normal; }
                foreach (var outline in label.GetComponents<Outline>()) outline.enabled = false;
            }
            var description = viewport.Find("TabPageTrack/SkillPage/AbilityContent/Description").GetComponent<TMP_Text>();
            description.fontSize = 30; description.lineSpacing = 8; description.color = Color.white;
            GetComponent<DroneUpgradeConceptScreen>().RefreshPopupLayout(180);
        }

        private void Paint(Transform target, Color color)
        {
            var image = target.GetComponent<Image>(); image.color = color;
            if (_popupRound != null && image.sprite != null) { image.sprite = _popupRound; image.type = Image.Type.Sliced; }
        }

        private RectTransform NewBox(string name, Transform parent, Color color)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
            rect.SetParent(parent, false); var image = rect.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            image.sprite = _popupRound; image.type = Image.Type.Sliced; return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private void RemoveUnusedViews()
        {
            foreach (string path in new[] { "Header", "DeckScroll" })
            {
                var old = page.Find(path); if (old != null) RemoveView(old.gameObject);
            }
            foreach (var old in page.GetComponentsInChildren<Transform>(true))
                if (old != null && (old.name == "DroneFrameTrim" || old.name == "SlotNamePlate")) RemoveView(old.gameObject);
        }
        private static void RemoveView(GameObject old)
        {
            old.name = "RemovedUnusedView"; old.SetActive(false);
            if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
        private void AddChiefEffects(Transform originalTrim)
        {
            var frame = originalTrim.parent;
            var crown = (RectTransform)new GameObject("FloatingGoldCrown", typeof(RectTransform)).transform;
            crown.SetParent(frame, false); crown.anchorMin = crown.anchorMax = new Vector2(0, 1); crown.pivot = new Vector2(0, 1);
            crown.sizeDelta = new Vector2(232, 292); crown.anchoredPosition = Vector2.zero;
            foreach (Transform part in originalTrim)
                if (part.name.StartsWith("ChiefCrest")) Instantiate(part.gameObject, crown, false).name = part.name;
            crown.gameObject.AddComponent<DeckUpgradeCrownFloat>();
            var motes = (RectTransform)new GameObject("ChiefGoldMotes", typeof(RectTransform)).transform;
            motes.SetParent(frame, false); motes.anchorMin = motes.anchorMax = new Vector2(0, 1); motes.pivot = new Vector2(.5f, .5f);
            motes.sizeDelta = new Vector2(232, 292); motes.anchoredPosition = new Vector2(116, -146); motes.localScale = Vector3.one * .45f;
            motes.gameObject.AddComponent<RisingUpgradeMotes>().Build(GetComponent<DeckSelectorConcept>().screen.style.CircleFill);
        }
        /// <summary>Detaches layout callbacks before the screen replaces its generated hierarchy.</summary>
        internal void ReleaseView()
        {
            if (_selector != null) _selector.OnFilterChanged -= Arrange;
            _selector = null; grid = null; Tabs = null;
        }

        private void OnDestroy() => ReleaseView();
        private void Arrange()
        {
            var divider = (RectTransform)grid.Find("UnownedHeader"); lastOwnedOnly = !divider.gameObject.activeSelf;
            float scale = variant == 2 ? 1.16f : variant == 3 ? 1.12f : 1.2f;
            float rowStep = variant == 2 ? 510 : variant == 3 ? 455 : 490;
            int ownedCount = GetComponent<DeckSelectorConcept>().screen.units.Length + 1;
            float dividerY = 40 + Mathf.CeilToInt(ownedCount / 3f) * rowStep;
            divider.anchoredPosition = new Vector2(25, -dividerY);
            int count = 0;
            foreach (Transform child in grid)
            {
                if (child == divider) continue;
                bool owned = count < ownedCount; int index = owned ? count : count - ownedCount;
                var card = (RectTransform)child;
                card.localScale = Vector3.one * scale;
                card.anchoredPosition = new Vector2((1032 - (232 * scale + 660)) * .5f + index % 3 * 330, -(owned ? 40 : dividerY + 104) - index / 3 * rowStep);
                count++;
            }
            ((RectTransform)grid).sizeDelta = new Vector2(1032, lastOwnedOnly ? dividerY + 20 : dividerY + 104 + Mathf.CeilToInt((count - ownedCount) / 3f) * rowStep + 20);
        }
    }
}
