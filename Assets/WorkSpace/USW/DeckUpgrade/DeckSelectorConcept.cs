using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class DeckSelectorConcept : MonoBehaviour
    {
        public DroneUpgradeConceptScreen screen;
        public Sprite frogPortrait;
        public Sprite attackIcon, attackSpeedIcon;
        private RectTransform page, banner, collection, grid;
        private RectTransform unownedHeader;
        private RectTransform detailChiefFrame, detailUnitFrame;
        private Image detailChiefPortrait, detailUnitPortrait;
        private TMP_Text detailName, detailLevel, requiredCost;
        private Image detailSkillTab, detailBonusTab;
        private TMP_Text chiefCardLevel;
        private readonly System.Collections.Generic.List<RectTransform> unitCards = new System.Collections.Generic.List<RectTransform>();
        private ResearchViewSettings Style => screen.style;
        private static Color C(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        /// <summary>Raised after the ownership filter has changed the collection layout.</summary>
        public event System.Action OnFilterChanged;

        /// <summary>Builds the collection after the screen has created its base layout.</summary>
        public void BuildView()
        {
            page = (RectTransform)screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page");
            page.Find("Header/Title").GetComponent<TMP_Text>().text = "\uC720\uB2DB \uAC15\uD654";
            BuildBanner(); BuildCollection(); ConfigureDetails();
            page.Find("DeckScroll").gameObject.SetActive(false);
            page.Find("UnitDetailsPopup").SetAsLastSibling();
        }
        private void BuildBanner()
        {
            banner = Box("CollectionBanner", page, C("705247"), 0, 112, 1080, 400);
            for (int i = 0; i < 5; i++) Box("ArtworkPanel_" + i, banner, C("86634F"), i * 238 - 40, 290 + i % 2 * 20, 206, 82);
            var host = Box("BannerChief", banner, Color.clear, 342, 10, 396, 380);
            host.gameObject.AddComponent<DroneSpinePreview>().Show(screen.chiefSpine, screen.chiefIdle, screen.spineMaterial, screen.spineBlendMaterials, screen.chief.Icon);
            for (int i = 0; i < 3; i++)
            {
                var spots = new[] { new Vector2(230, 150), new Vector2(785, 70), new Vector2(868, 250) };
                var drone = Box("BannerCombatDrone_" + i, banner, Color.white, spots[i].x, spots[i].y, 78, 78, screen.combatDroneSprites[i]);
                drone.GetComponent<Image>().type = Image.Type.Simple; drone.GetComponent<Image>().preserveAspect = true;
            }
            Box("BannerEdge", banner, C("506D77"), 0, 390, 1080, 10);
        }

        private void BuildCollection()
        {
            collection = Box("DeckCollection", page, Color.clear, 0, 534, 1032, 100);
            collection.anchorMin = Vector2.zero; collection.anchorMax = Vector2.one;
            collection.offsetMin = new Vector2(24, 28); collection.offsetMax = new Vector2(-24, -534);
            var summary = Box("DeckSummaryStrip", collection, C("17232B"), 0, 0, 1032, 64);
            Label("CollectionTitle", "<b>\uC804\uCCB4 \uC720\uB2DB</b>", summary, 24, 0, 280, 64, 29, Color.white);
            Label("CollectionCount", "<b>\uBCF4\uC720 6 / 12</b>", summary, 356, 0, 320, 64, 29, C("FFCE54"), TextAlignmentOptions.Center);
            var toggleRoot = Box("OwnedOnlyToggle", summary, Color.clear, 728, 0, 280, 64);
            toggleRoot.GetComponent<Image>().raycastTarget = true;
            var checkBox = Box("CheckBox", toggleRoot, C("455560"), 0, 12, 40, 40);
            var check = Box("Checkmark", checkBox, C("91E6A6"), 8, 8, 24, 24);
            Label("ToggleLabel", "<b>\uBCF4\uC720\uB9CC \uBCF4\uAE30</b>", toggleRoot, 56, 0, 224, 64, 27, Color.white, TextAlignmentOptions.Right);
            var toggle = toggleRoot.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = checkBox.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(false); toggle.onValueChanged.AddListener(FilterUnits);
            var panel = Box("UnitListPanel", collection, C("5B4038"), 0, 86, 1032, 100);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = Vector2.zero; panel.offsetMax = new Vector2(0, -86);
            var viewport = Box("UnitViewport", panel, Color.clear, 0, 0, 1032, 100); Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-16, 0, -16, 0); viewport.GetComponent<Image>().raycastTarget = true;
            grid = Box("UnitGrid", viewport, Color.clear, 0, 0, 1032, 1280);
            var scroll = panel.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = grid;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var body = page.Find("DeckScroll/Viewport/DeckContent/UnfoldingUnitList");
            var chiefCard = Button("ChiefUnit", grid, C("222836"), 0, 0, 232, 376, () => SelectFromGrid(-1));
            Box("ChiefFrame", chiefCard, C("FFE16B"), 5, 5, 222, 282);
            Box("ChiefInset", chiefCard, C("D9950D"), 12, 12, 208, 268);
            var host = Box("ChiefPortrait", chiefCard, Color.clear, 14, 10, 204, 215);
            host.gameObject.AddComponent<DroneSpinePreview>().Show(screen.chiefSpine, screen.chiefIdle, screen.spineMaterial, screen.spineBlendMaterials, screen.chief.Icon);
            chiefCardLevel = Label("Level", "Lv.1", chiefCard, 7, 204, 218, 42, 30, C("FFF3E4"), TextAlignmentOptions.Center);
            Box("SlotNamePlate", chiefCard, C("222836"), 5, 296, 222, 73);
            Label("Name", screen.chief.DisplayName, chiefCard, 0, 310, 232, 48, 32, C("FFF3E4"), TextAlignmentOptions.Center);
            Move((RectTransform)body.Find("ChiefGoldGauge"), chiefCard, 26, 249, 180, 34);
            unitCards.Clear(); unitCards.Add(chiefCard);
            for (int i = 0; i < screen.units.Length; i++)
            {
                var card = (RectTransform)body.Find("Unit_" + i); card.SetParent(grid, false); unitCards.Add(card);
                int index = i; var button = card.GetComponent<Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => SelectFromGrid(index));
            }
            for (int i = 0; i < unitCards.Count; i++)
            {
                var card = unitCards[i];
                if (i > 0)
                {
                    card.Find("UnitFrame/ColoredEdge/PortraitInset").GetComponent<Image>().color = C("384C89");
                    card.Find("UnitFrame/ColoredEdge").GetComponent<Image>().color = C("50A6F1");
                }
                var gauge = (RectTransform)(i == 0 ? card.Find("ChiefGoldGauge") : card.Find("UnitFrame/CostStrip"));
                gauge.anchorMin = gauge.anchorMax = new Vector2(.5f, 1);
                gauge.pivot = new Vector2(.5f, 1);
                gauge.anchoredPosition = new Vector2(0, -241); gauge.sizeDelta = new Vector2(180, 34);
                var price = (RectTransform)gauge.Find(i == 0 ? "ChiefCost" : "Cost");
                CenterGaugeText(price, 180, 34);
                DecorateDroneFrame(card);
                if (i == 0) DecorateChiefFrame(card);
            }
            var popupPrice = (RectTransform)page.Find("UnitDetailsPopup/UnitInfoDialog/PopupGoldGauge/UpgradeCost");
            CenterGaugeText(popupPrice, 606, 62);
            string[] examples = { "\uAC1C\uAD6C\uB9AC \uC871\uC7A5", "\uAC1C\uAD6C\uB9AC \uC804\uC0AC", "\uAC1C\uAD6C\uB9AC \uC220\uC0AC", "\uAE30\uC0AC \uC871\uC7A5", "\uAC80\uC0AC", "\uBC29\uD328\uBCD1" };
            for (int i = 0; i < examples.Length; i++)
            {
                var card = Box("LockedUnit_" + i, grid, C("222836"), 0, 0, 232, 376);
                Box("UnitFrame", card, C(i % 3 == 0 ? "D9950D" : "384C89"), 5, 5, 222, 282);
                Label("DeckMark", i < 3 ? "\uAC1C" : "\uAC80", card, 10, 20, 212, 200, 88, C("6B725D"), TextAlignmentOptions.Center);
                Box("LockedShade", card, new Color(0, 0, 0, .4f), 5, 5, 222, 282);
                Box("LockShackle", card, C("D5D9D7"), 94, 84, 44, 54);
                Box("LockHole", card, C("333B40"), 104, 94, 24, 30);
                Box("UnitLock", card, C("D5D9D7"), 80, 118, 72, 60);
                Box("Keyhole", card, C("333B40"), 111, 137, 10, 23);
                Label("Name", examples[i], card, 0, 302, 232, 64, 26, Color.white, TextAlignmentOptions.Center);
                unitCards.Add(card);
            }
            unownedHeader = Box("UnownedHeader", grid, C("263947"), 25, 0, 982, 64);
            unownedHeader.GetComponent<Image>().sprite = null;
            Label("UnownedTitle", "<b>보유하지 않음</b>", unownedHeader, 24, 0, 934, 64, 30, C("FFF3E4"), TextAlignmentOptions.Center);
            FilterUnits(false);
        }
        private static void CenterGaugeText(RectTransform text, float width, float height)
        {
            text.anchorMin = text.anchorMax = new Vector2(.5f, .5f); text.pivot = new Vector2(.5f, .5f);
            text.anchoredPosition = Vector2.zero; text.sizeDelta = new Vector2(width - 48, height);
            var label = text.GetComponent<TMP_Text>(); label.alignment = TextAlignmentOptions.Center; label.margin = Vector4.zero;
        }
        private void SelectFromGrid(int index)
        {
            screen.SelectUnit(index);
        }

        /// <summary>Displays typed state supplied by the screen's presenter.</summary>
        public void Render(DeckUpgradeItemViewData chief, DeckUpgradeItemViewData selected)
        {
            chiefCardLevel.text = "Lv." + chief.Level;
            detailName.text = selected.Name;
            detailLevel.text = "Lv." + selected.Level + "/" + DeckUpgradeProgress.MaxLevel;
            requiredCost.text = selected.IsMaxLevel ? "MAX" : selected.Cost.ToString("N0");
            detailChiefFrame.gameObject.SetActive(selected.IsChief);
            detailUnitFrame.gameObject.SetActive(!selected.IsChief);
            if (!selected.IsChief) detailUnitPortrait.sprite = screen.units[selected.Index].icon;
        }
        private void ConfigureDetails()
        {
            var dialog = page.Find("UnitDetailsPopup/UnitInfoDialog");
            var stage = (RectTransform)dialog.Find("PreviewStage");
            Move(stage, dialog, 54, -180, 848, 368);
            Move((RectTransform)stage.Find("PopupSpinePreview"), stage, 165, -25, 518, 394);
            Move((RectTransform)stage.Find("PreviewShadow"), stage, 252, 293, 344, 42);
            var stats = dialog.Find("StatsPanel");
            Move((RectTransform)stats, dialog, 48, 235, 860, 310); stats.GetComponent<Image>().color = C("00434C");
            dialog.GetComponent<Image>().color = C("17232B"); dialog.Find("DialogPaper").GetComponent<Image>().color = C("253B47");
            dialog.Find("CloseDetails").GetComponent<Image>().color = C("728BA2");
            var slot = Box("SelectedUnitSlot", stats, Color.clear, 12, 12, 226, 286);
            detailChiefFrame = BuildDetailFrame(slot, true, out detailChiefPortrait);
            detailUnitFrame = BuildDetailFrame(slot, false, out detailUnitPortrait);
            detailUnitFrame.gameObject.SetActive(false);
            foreach (var frame in new[] { detailChiefFrame, detailUnitFrame }) { frame.localScale = Vector3.one * .95f; frame.anchoredPosition = new Vector2(0, -4); }
            Remove(dialog.Find("SelectedName").gameObject);
            detailName = Label("DetailUnitName", "", stats, 250, 10, 584, 55, 38, C("FFF3E4"));
            detailLevel = Label("DetailUnitLevel", "", stats, 250, 74, 164, 42, 29, C("FFF3E4"));
            var rows = Box("UpgradeStatRows", stats, Color.clear, 250, 134, 586, 144);
            string[] icons = { "AttackIcon", "AttackSpeedIcon" };
            string[] values = { "Stat", "SpeedStat" };
            for (int i = 0; i < 2; i++)
            {
                var row = Box(i == 0 ? "AttackRow" : "AttackSpeedRow", rows, C("31505D"), 0, i * 74, 586, 70);
                var well = Box("IconSlot", row, C("1C3039"), 8, 8, 54, 54);
                Move((RectTransform)stats.Find(icons[i]), well, 4, 4, 46, 46);
                var icon = well.Find(icons[i]).GetComponent<Image>(); icon.sprite = i == 0 ? attackIcon : attackSpeedIcon;
                icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.color = Color.white;
                foreach (Transform child in icon.transform) child.gameObject.SetActive(false);
                var value = (RectTransform)stats.Find(values[i]); Move(value, row, 78, 5, 494, 60);
                var label = value.GetComponent<TMP_Text>(); label.alignment = TextAlignmentOptions.MidlineRight; label.fontSize = 27; label.color = C("FFF3E4");
            }
            var button = (RectTransform)dialog.Find("UpgradeButton"); button.sizeDelta = new Vector2(680, 128);
            Move((RectTransform)dialog.Find("ButtonShadow"), dialog, 138, 1008, 680, 128);
            Move((RectTransform)button.Find("UpgradeLabel"), button, 0, 4, 680, 48);
            button.Find("UpgradeLabel").GetComponent<TMP_Text>().fontSize = 37;
            var gauge = (RectTransform)dialog.Find("PopupGoldGauge"); Move(gauge, stats, 430, 80, 384, 36);
            CenterGaugeText((RectTransform)gauge.Find("UpgradeCost"), 384, 36);
            gauge.Find("UpgradeCost").GetComponent<TMP_Text>().fontSize = 24;
            var coin = (RectTransform)gauge.Find("GoldIcon"); coin.sizeDelta = new Vector2(44, 44);
            gauge.GetComponent<UpgradeCostGauge>().Arrow.sizeDelta = new Vector2(70, 90);
            var costPlate = Box("RequiredGoldCost", button, C("483328"), 170, 72, 340, 40);
            var gold = Box("GoldIcon", costPlate, Color.white, 26, 0, 40, 40, Style.GoldIcon); gold.GetComponent<Image>().preserveAspect = true;
            requiredCost = Label("RequiredCost", "", costPlate, 76, 0, 238, 40, 30, Color.white, TextAlignmentOptions.Center);
            var ability = dialog.Find("SpecialAbilityScroll"); ability.GetComponent<Image>().color = C("00434C");
            detailSkillTab = dialog.Find("SkillTab").GetComponent<Image>(); detailBonusTab = dialog.Find("LevelBonusTab").GetComponent<Image>();
            detailSkillTab.GetComponentInChildren<TMP_Text>().color = C("FFF3E4"); detailBonusTab.GetComponentInChildren<TMP_Text>().color = C("FFF3E4");
            ability.Find("AbilityViewport/AbilityContent/Description").GetComponent<TMP_Text>().color = C("FFF3E4");
            foreach (var image in ability.GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("LevelBonus_")) image.color = C("31505D");
        }
        private RectTransform BuildDetailFrame(RectTransform slot, bool chief, out Image portrait)
        {
            var frame = Box(chief ? "ChiefSlotFrame" : "UnitSlotFrame", slot, Color.clear, 20, 5, 232, 292);
            frame.localScale = Vector3.one * .48f;
            Box("FrameEdge", frame, C(chief ? "FFE16B" : "50A6F1"), 5, 5, 222, 282);
            Box("PortraitInset", frame, C(chief ? "D9950D" : "384C89"), 12, 12, 208, 268);
            portrait = Box("SlotPortrait", frame, Color.white, 14, 58, 204, 242, screen.chief.Icon).GetComponent<Image>();
            portrait.type = Image.Type.Simple; portrait.preserveAspect = true;
            DecorateDroneFrame(frame); if (chief) DecorateChiefFrame(frame);
            return frame;
        }
        private void DecorateChiefFrame(RectTransform card)
        {
            var trim = (RectTransform)card.Find("DroneFrameTrim");
            foreach (var image in trim.GetComponentsInChildren<Image>())
                if (image.name.Contains("MetalCorner")) image.color = C("FFE16B");
            Box("ChiefLeftRail", trim, C("FFDE62"), 1, 38, 9, 233);
            Box("ChiefRightRail", trim, C("FFDE62"), 222, 38, 9, 233);
            Box("ChiefInnerLeft", trim, C("8F5106"), 12, 43, 4, 223);
            Box("ChiefInnerRight", trim, C("8F5106"), 216, 43, 4, 223);
            Box("ChiefCrestBase", trim, C("FFE16B"), 83, -6, 66, 22);
            for (int i = 0; i < 3; i++)
                Box("ChiefCrestPoint_" + i, trim, C("FFE16B"), 83 + i * 25, i == 1 ? -20 : -14, 16, 23);
            Box("ChiefCrestInset", trim, C("A66610"), 92, 1, 48, 8);
        }
        private void DecorateDroneFrame(RectTransform card)
        {
            var decor = Box("DroneFrameTrim", card, Color.clear, 0, 0, 232, 292);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 3 : 191;
                Box("UpperMetalCorner_" + side, decor, C("89969B"), x, 3, 38, 8);
                Box("SideMetalCorner_" + side, decor, C("89969B"), side == 0 ? 3 : 221, 3, 8, 34);
                Box("LowerMetalCorner_" + side, decor, C("647279"), x, 280, 38, 8);
                var rivet = Box("FrameRivet_" + side, decor, C("D0AD63"), side == 0 ? 15 : 207, 15, 10, 10, Style.CircleFill);
                rivet.GetComponent<Image>().type = Image.Type.Simple;
            }
            Box("CircuitRail", decor, C("39464C"), 78, 5, 76, 12);
            for (int i = 0; i < 3; i++) Box("AmberIndicator_" + i, decor, C("F4BC59"), 87 + i * 21, 8, 12, 5);
            var drone = Box("CombatDroneBadge", decor, Color.white, 14, 14, 72, 72, screen.combatDroneSprites[0]);
            drone.GetComponent<Image>().type = Image.Type.Simple; drone.GetComponent<Image>().preserveAspect = true;
        }
        private void FilterUnits(bool ownedOnly)
        {
            int ownedCount = screen.units.Length + 1;
            float headerY = 40 + Mathf.CeilToInt(ownedCount / 3f) * 490;
            unownedHeader.anchoredPosition = new Vector2(25, -headerY);
            unownedHeader.gameObject.SetActive(!ownedOnly);
            for (int i = 0; i < unitCards.Count; i++)
            {
                bool owned = i < ownedCount;
                bool visible = !ownedOnly || owned; unitCards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var card = unitCards[i]; card.anchorMin = card.anchorMax = new Vector2(0, 1); card.pivot = new Vector2(0, 1);
                int index = owned ? i : i - ownedCount;
                float top = owned ? 40 : headerY + 104;
                card.anchoredPosition = new Vector2(47 + index % 3 * 330, -top - index / 3 * 490); card.localScale = Vector3.one * 1.2f;
            }
            float bottom = ownedOnly ? headerY + 20 : headerY + 104 + Mathf.CeilToInt((unitCards.Count - ownedCount) / 3f) * 490 + 20;
            grid.sizeDelta = new Vector2(1032, bottom);
            OnFilterChanged?.Invoke();
        }
        private static void Move(RectTransform item, Transform parent, float x, float y, float width, float height)
        {
            item.SetParent(parent, false); item.anchorMin = item.anchorMax = new Vector2(0, 1); item.pivot = new Vector2(0, 1);
            item.anchoredPosition = new Vector2(x, -y); item.sizeDelta = new Vector2(width, height);
        }
        private RectTransform Button(string name, Transform parent, Color color, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rt = Box(name, parent, color, x, y, w, h); rt.GetComponent<Image>().raycastTarget = true;
            rt.gameObject.AddComponent<Button>().onClick.AddListener(action); return rt;
        }
        private void Remove(GameObject go)
        {
            go.name = "Discarded"; go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        private RectTransform Box(string name, Transform parent, Color color, float x, float y, float w, float h, Sprite sprite = null)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(Image)).transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var image = rt.GetComponent<Image>(); image.color = color; image.sprite = sprite != null ? sprite : Style.RoundedFill;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = Style.RoundedCornerScale; image.raycastTarget = false; return rt;
        }
        private TMP_Text Label(string name, string value, Transform parent, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var text = rt.GetComponent<TextMeshProUGUI>(); text.font = Style.FontBold; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false; return text;
        }
    }
}
