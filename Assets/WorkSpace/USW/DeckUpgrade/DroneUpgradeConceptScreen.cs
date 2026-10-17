using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class DroneUpgradeConceptScreen : MonoBehaviour
    {
        [SerializeField] private bool _embeddedInLobby;

        /// <summary>Whether the screen uses the lobby canvas and its safe area.</summary>
        public bool IsEmbeddedInLobby => _embeddedInLobby;

        public ResearchViewSettings style;
        public UnitData[] units;
        public Sprite[] combatDroneSprites;
        public ChieftainData chief;
        public SkeletonDataAsset chiefSpine;
        public string chiefIdle;
        public SkeletonDataAsset[] spineData;
        public string[] idleAnimations;
        public string[] attackAnimations;
        public Material spineMaterial;
        public Material[] spineBlendMaterials;

        private static readonly Color Gold = C("FFCE54"), Coral = C("F78A62"), Ink = C("17232B");
        private static readonly Color CardColor = C("97733D");
        private static readonly int[] BonusLevels = { 3, 6, 9, 12, 15 };
        private readonly List<GameObject> bonusCovers = new List<GameObject>();
        private readonly List<GameObject> bonusLocks = new List<GameObject>();
        private RectTransform abilityContent, bonusContent;
        private ScrollRect abilityScroll;
        private Image skillTabFace, bonusTabFace;
        private int abilityTab;
        private RectTransform chiefTravelIcon, chiefCircle, sectionReveal;
        private float revealAmount;
        private Color Background => C("151923");
        private Color Surface => C("222836");
        private Color Foreground => C("FFF3E4");
        private Color Muted => C("C2ADB9");
        private Color Accent => C("506D77");
        private readonly List<RectTransform> cards = new List<RectTransform>();
        private readonly List<Vector2> positions = new List<Vector2>();
        private readonly List<TMP_Text> levels = new List<TMP_Text>();
        private readonly List<TMP_Text> prices = new List<TMP_Text>();
        private readonly List<UpgradeCostGauge> moneyGauges = new List<UpgradeCostGauge>();
        private UpgradeCostGauge popupGauge;
        private RectTransform body, tab, artwork, detail, safe, deckContent, upgradeRect;
        private Vector2 upgradeRestPosition;
        private GameObject popup;
        private CanvasGroup popupGroup;
        private DroneSpinePreview popupPreview, chiefPreview;
        private TMP_Text toggleText, wallet, title, skillName, description, stat, speedStat, cost, chiefLevel, chiefCost, chiefBonus;
        private UpgradeCostGauge chiefGauge;
        private Button upgrade;
        private DeckUpgradeService _service;
        private DeckUpgradePresenter _presenter;
        private IObjectResolver _previewScope;
        private DeckSelectorConcept _selector;
        private DeckUpgradeStatsTabs _statsTabs;
        private Coroutine motion, popupMotion, punchMotion;
        private bool expanded = true;
        private float bodyHeight;
        private int selected;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private bool _fitPopup;
        private float _popupFitScale = 1f;
        private float _popupTopOverhang;
        private Vector2 _popupAreaSize;
        private RectTransform _popupArea;

        public bool Expanded => expanded;
        public int Selected => selected;
        public bool IsAnimating => motion != null;
        public bool IsPopupAnimating => popupMotion != null;
        public float PopupScale => detail.localScale.x;
        public DroneSpinePreview PopupPreview => popupPreview;
        public DroneSpinePreview ChiefPreview => chiefPreview;
        /// <summary>Current detail overlay for lobby decoration, without hierarchy-path lookup.</summary>
        public RectTransform DetailsPopup => _popupArea;
        private static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var value); return value; }
        /// <summary>Receives the lobby-owned preview service before the screen starts.</summary>
        [Inject]
        public void Construct(DeckUpgradeService service) => _service = service;

        private void Start()
        {
            BuildView();
            if (_selector != null) return;
            expanded = false; SetReveal(0); ToggleDeck();
        }

        /// <summary>Builds the base view and composes optional collection styling in an explicit order.</summary>
        public void BuildView()
        {
            if (_presenter != null) { _presenter.OnChanged -= Refresh; _presenter.Dispose(); }
            var design = GetComponent<DeckUpgradeDesignVariant>();
            if (design != null) design.ReleaseView();
            EnsureService();
            _presenter = new DeckUpgradePresenter(_service, units, chief);
            _selector = null; _statsTabs = null;
            _fitPopup = false; _popupFitScale = 1f; _popupTopOverhang = 0f;
            StopAllCoroutines(); motion = popupMotion = punchMotion = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject; child.SetActive(false);
                child.name = "ReplacedUpgradeView";
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            cards.Clear(); positions.Clear(); levels.Clear(); prices.Clear(); moneyGauges.Clear(); selected = 0;
            bonusCovers.Clear(); bonusLocks.Clear(); abilityTab = 0;
            var root = Box("DroneUpgradeCanvas", transform, Background);
            Stretch(root);
            if (!_embeddedInLobby)
            {
                root.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
                root.gameObject.AddComponent<GraphicRaycaster>();
            }
            safe = Box("SafeArea", root, Color.clear); Stretch(safe); ApplySafeArea();
            var page = Box("Page", safe, Background);
            page.anchorMin = new Vector2(.5f, 0); page.anchorMax = new Vector2(.5f, 1); page.sizeDelta = new Vector2(1080, 0);
            BuildHeader(page);

            var scrollRoot = Box("DeckScroll", page, Color.clear);
            scrollRoot.anchorMin = Vector2.zero; scrollRoot.anchorMax = Vector2.one;
            scrollRoot.offsetMin = new Vector2(34, 45); scrollRoot.offsetMax = new Vector2(-34, -146);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var viewport = Box("Viewport", scrollRoot, Color.clear); Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.GetComponent<Image>().raycastTarget = true;
            var content = Box("DeckContent", viewport, Color.clear);
            deckContent = content;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 1030); scroll.viewport = viewport; scroll.content = content;
            tab = Box("DroneDeckTab", content, Ink, style.RoundedFill); Place(tab, 0, 0, 1012, 300);
            tab.GetComponent<Image>().raycastTarget = true; tab.gameObject.AddComponent<Button>().onClick.AddListener(ToggleDeck);
            var tabFace = Box("TabFace", tab, Surface, style.RoundedFill); Place(tabFace, 5, 5, 1002, 280);
            var artFrame = Box("DroneTabFrame", content, Color.clear);
            chiefTravelIcon = artFrame; artFrame.anchorMin = artFrame.anchorMax = new Vector2(0, 1);
            artFrame.pivot = new Vector2(.5f, .5f); artFrame.sizeDelta = new Vector2(240, 240); artFrame.anchoredPosition = new Vector2(506, -120);
            artFrame.GetComponent<Image>().raycastTarget = true;
            artFrame.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (expanded) SelectUnit(-1); else ToggleDeck(); });
            chiefCircle = Box("ChiefGrowingCircle", artFrame, C("87652F"), style.CircleFill);
            chiefCircle.anchorMin = chiefCircle.anchorMax = new Vector2(.5f, .5f); chiefCircle.sizeDelta = new Vector2(240, 240);
            artwork = Box("ArtworkSlot", artFrame, Color.clear);
            Place(artwork, 10, 10, 220, 220);
            chiefPreview = artwork.gameObject.AddComponent<DroneSpinePreview>();
            Text("TabTitle", "드론", tab, 216, 232, 580, 64, 56, Foreground, TextAlignmentOptions.Center);
            var droneFx = tab.gameObject.AddComponent<DroneDeckTabEffects>();
            droneFx.Build(combatDroneSprites);
            toggleText = Text("ExpandIndicator", "−", tab, 899, 232, 72, 64, 48, Gold, TextAlignmentOptions.Center);
            bodyHeight = 1410;
            body = Box("UnfoldingUnitList", content, Color.clear); Place(body, 0, 330, 1012, bodyHeight);
            body.gameObject.AddComponent<RectMask2D>();
            sectionReveal = Box("SectionDrawClip", body, Color.clear); Place(sectionReveal, 0, 200, 1012, bodyHeight - 200);
            sectionReveal.gameObject.AddComponent<RectMask2D>();
            var sectionFill = Box("DroneSectionFill", sectionReveal, Ink, style.RoundedFill); Place(sectionFill, 0, 0, 1012, bodyHeight - 200);
            var sectionBorder = Box("DroneSectionOutline", sectionReveal, Accent, style.RoundedOutline); Place(sectionBorder, 0, 0, 1012, bodyHeight - 200);
            BuildChief();
            for (int i = 0; i < units.Length; i++) BuildUnit(i);
            BuildDetail(page);
            chiefTravelIcon.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            ShowPreview(chiefPreview, -1);
            expanded = true; SetReveal(1);
            _selector = GetComponent<DeckSelectorConcept>();
            if (_selector != null)
            {
                _selector.BuildView();
                if (design != null) { design.Apply(); _statsTabs = design.Tabs; }
            }
            _presenter.OnChanged += Refresh;
            Refresh(); ForceText();
        }

        private void EnsureService()
        {
            if (_service != null) return;
            if (Application.isPlaying && _embeddedInLobby)
                throw new System.InvalidOperationException("The lobby must inject DeckUpgradeService before building its character screen.");
            // Standalone concepts and Edit-mode authoring use an explicitly owned preview container.
            var builder = new ContainerBuilder();
            builder.Register<DeckUpgradePreviewStore>(Lifetime.Scoped).As<IDeckUpgradeStore>();
            builder.Register<DeckUpgradeService>(Lifetime.Scoped);
            _previewScope = builder.Build();
            _service = _previewScope.Resolve<DeckUpgradeService>();
        }

        private void OnDestroy()
        {
            if (_presenter != null) { _presenter.OnChanged -= Refresh; _presenter.Dispose(); }
            _previewScope?.Dispose();
        }

        private void BuildHeader(RectTransform page)
        {
            var header = Box("Header", page, Ink); Place(header, 0, 0, 1080, 112);
            Text("Title", "유닛 강화", header, 35, 15, 600, 72, 46, C("FFF3E4"));
            var balance = Box("Wallet", header, C("201A29"), style.RoundedFill); Place(balance, 760, 20, 286, 68);
            var coin = Box("Gold", balance, Color.white, style.GoldIcon); Place(coin, 12, 10, 48, 48); coin.GetComponent<Image>().preserveAspect = true;
            wallet = Text("Amount", "", balance, 74, 5, 190, 58, 36, Gold, TextAlignmentOptions.Right);
        }

        private void BuildUnit(int index)
        {
            float x = 70 + index % 3 * 320;
            float y = 530 + index / 3 * 450;
            var card = Box("Unit_" + index, body, C("201B29"), style.RoundedFill); Place(card, x, y, 232, 376);
            card.pivot = new Vector2(.5f, .5f); card.anchoredPosition += new Vector2(116, -188);
            cards.Add(card); positions.Add(card.anchoredPosition); card.gameObject.AddComponent<CanvasGroup>();
            card.GetComponent<Image>().raycastTarget = true;
            int captured = index; card.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectUnit(captured));
            var namePlate = Box("SlotNamePlate", card, Surface, style.RoundedFill); Place(namePlate, 5, 296, 222, 73);
            var shadow = Box("CardShadow", card, C("251927"), style.RoundedFill); Place(shadow, 0, 8, 232, 292);
            var frame = Box("UnitFrame", card, C("221927"), style.RoundedFill); Place(frame, 0, 0, 232, 292);
            var edge = Box("ColoredEdge", frame, CardColor, style.RoundedFill); Place(edge, 5, 5, 222, 282);
            var inset = Box("PortraitInset", edge, C("493E32"), style.RoundedFill); Place(inset, 7, 7, 208, 266);
            var topLight = Box("CardLight", inset, new Color(1, 1, 1, .08f), style.RoundedFill); Place(topLight, 0, 0, 208, 122);
            var icon = Box("Portrait", inset, Color.white, units[index].icon); Place(icon, 14, 15, 180, 184);
            icon.GetComponent<Image>().type = Image.Type.Simple; icon.GetComponent<Image>().preserveAspect = true;
            var level = Text("Level", "", frame, 7, 204, 218, 42, 30, Color.white, TextAlignmentOptions.Center);
            OutlineText(level, 1.5f); levels.Add(level);
            var strip = Box("CostStrip", frame, C("301B2B"), style.RoundedFill); Place(strip, 22, 248, 180, 34);
            moneyGauges.Add(UpgradeCostGauge.Create(strip, style.RoundedFill, style.UpgradeArrow));
            var price = Text("Cost", "", strip, 26, -2, 128, 37, 23, Color.white, TextAlignmentOptions.Center);
            price.enableAutoSizing = true; price.fontSizeMin = 15; price.fontSizeMax = 23;
            OutlineText(price, 1); prices.Add(price);
            var name = Text("Name", units[index].unitName, card, 0, 310, 232, 48, 32, Foreground, TextAlignmentOptions.Center);
            OutlineText(name, 1);
        }

        private void AddGoldIcon(RectTransform parent, float size)
        {
            var coin = Box("GoldIcon", parent, Color.white, style.GoldIcon); coin.anchorMin = coin.anchorMax = new Vector2(0, .5f); coin.pivot = new Vector2(.5f, .5f);
            coin.sizeDelta = Vector2.one * size * 1.15f; coin.anchoredPosition = new Vector2(-7, 0);
            coin.GetComponent<Image>().preserveAspect = true;
        }

        private void BuildChief()
        {
            var slot = Box("ChiefUpgradeSlot", body, Color.clear, style.CircleFill); Place(slot, 346, -10, 320, 320);
            slot.GetComponent<Image>().raycastTarget = true;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectUnit(-1));
            slot.pivot = new Vector2(.5f, .5f); slot.anchoredPosition += new Vector2(160, -160);
            chiefLevel = Text("ChiefLevel", "", body, 286, 334, 440, 46, 32, Foreground, TextAlignmentOptions.Center);
            chiefBonus = Text("ChiefBonus", "", body, 150, 382, 712, 38, 25, C("8AE69A"), TextAlignmentOptions.Center);
            var gauge = Box("ChiefGoldGauge", body, C("301B2B"), style.RoundedFill); Place(gauge, 391, 424, 230, 34);
            chiefGauge = UpgradeCostGauge.Create(gauge, style.RoundedFill, style.UpgradeArrow);
            chiefCost = Text("ChiefCost", "", gauge, 48, -2, 138, 37, 23, Color.white, TextAlignmentOptions.Center);
            chiefCost.enableAutoSizing = true; chiefCost.fontSizeMin = 15; chiefCost.fontSizeMax = 23;
        }

        private void LayoutDeckSections(bool open)
        {
            float bottom = open ? 330 + bodyHeight : 300;
            float gap = 22;
            deckContent.sizeDelta = new Vector2(0, bottom + gap);
        }

        private void BuildDetail(RectTransform page)
        {
            var overlay = Box("UnitDetailsPopup", page, new Color(.05f, .025f, .07f, .8f)); Stretch(overlay);
            popup = overlay.gameObject; popupGroup = popup.AddComponent<CanvasGroup>();
            _popupArea = overlay;
            overlay.GetComponent<Image>().raycastTarget = true; popup.AddComponent<Button>().onClick.AddListener(CloseDetails);
            detail = Box("UnitInfoDialog", overlay, Ink, style.RoundedFill);
            detail.anchorMin = detail.anchorMax = new Vector2(.5f, .5f); detail.sizeDelta = new Vector2(956, 1220);
            detail.GetComponent<Image>().raycastTarget = true; detail.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var paper = Box("DialogPaper", detail, C("005968"), style.RoundedFill); Place(paper, 9, 9, 938, 1198);
            var close = Box("CloseDetails", detail, Coral, style.RoundedFill); Place(close, 850, 24, 77, 71);
            close.GetComponent<Image>().raycastTarget = true; close.gameObject.AddComponent<Button>().onClick.AddListener(CloseDetails);
            Text("CloseLabel", "×", close, 0, -1, 77, 71, 48, Ink, TextAlignmentOptions.Center);
            var stage = Box("PreviewStage", detail, Color.clear); Place(stage, 54, -180, 848, 368);
            stage.gameObject.AddComponent<RisingUpgradeMotes>().Build(style.CircleFill);
            var shadow = Box("PreviewShadow", stage, new Color(.2f, .12f, .2f, .16f), style.CircleFill); Place(shadow, 252, 293, 344, 42);
            var host = Box("PopupSpinePreview", stage, Color.clear); Place(host, 208, 8, 432, 328);
            popupPreview = host.gameObject.AddComponent<DroneSpinePreview>();
            title = Text("SelectedName", "", detail, 70, 190, 816, 74, 44, Foreground, TextAlignmentOptions.Center);
            var stats = Box("StatsPanel", detail, C("00434C"), style.RoundedFill); Place(stats, 48, 280, 860, 168);
            var attackIcon = Box("AttackIcon", stats, Coral, style.RoundedFill); Place(attackIcon, 24, 17, 56, 56);
            Text("AttackSymbol", "ATK", attackIcon, 0, 0, 56, 56, 20, Ink, TextAlignmentOptions.Center);
            var speedIcon = Box("AttackSpeedIcon", stats, Gold, style.RoundedFill); Place(speedIcon, 24, 94, 56, 56);
            Text("SpeedSymbol", "SPD", speedIcon, 0, 0, 56, 56, 20, Ink, TextAlignmentOptions.Center);
            stat = Text("Stat", "", stats, 96, 15, 738, 60, 33, Foreground);
            speedStat = Text("SpeedStat", "", stats, 96, 92, 738, 60, 33, Foreground);
            skillTabFace = BuildAbilityTab("SkillTab", "스킬", 48, 0);
            bonusTabFace = BuildAbilityTab("LevelBonusTab", "레벨 보너스", 486, 1);
            var scrollRoot = Box("SpecialAbilityScroll", detail, C("003E47"), style.RoundedFill); Place(scrollRoot, 48, 624, 860, 300);
            var viewport = Box("AbilityViewport", scrollRoot, Color.clear); Place(viewport, 16, 14, 828, 272);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.GetComponent<Image>().raycastTarget = true;
            var content = Box("AbilityContent", viewport, Color.clear); Place(content, 0, 0, 828, 490);
            abilityContent = content;
            var namePlate = Box("SkillNamePanel", content, C("79582D"), style.RoundedFill); Place(namePlate, 12, 8, 804, 72);
            skillName = Text("SkillName", "", namePlate, 22, 0, 760, 72, 32, C("FFE39D"));
            description = Text("Description", "", content, 20, 104, 788, 450, 31, C("D5E3E5"), TextAlignmentOptions.TopLeft);
            description.textWrappingMode = TextWrappingModes.Normal;
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            abilityScroll = scroll;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            bonusContent = Box("LevelBonusContent", viewport, Color.clear); Place(bonusContent, 0, 0, 828, 570);
            for (int i = 0; i < BonusLevels.Length; i++)
            {
                var row = Box("LevelBonus_" + BonusLevels[i], bonusContent, C("00515B"), style.RoundedFill); Place(row, 8, 8 + i * 112, 812, 100);
                var badge = Box("RequiredLevel", row, C("C48B36"), style.RoundedFill); Place(badge, 8, 10, 126, 80);
                Text("LevelLabel", "Lv." + BonusLevels[i], badge, 0, 0, 126, 80, 31, Ink, TextAlignmentOptions.Center);
                Text("BonusDescription", "공격력 10% 증가", row, 156, 20, 520, 60, 32, Foreground);
                var cover = Box("LockedCover", row, new Color(0, 0, 0, .48f), style.RoundedFill); Stretch(cover); bonusCovers.Add(cover.gameObject);
                var lockRoot = Box("BonusLock", row, Color.clear); Place(lockRoot, 730, 22, 52, 56); bonusLocks.Add(lockRoot.gameObject);
                var loop = Box("LockLoop", lockRoot, C("FFF2D4"), style.RoundedOutline); Place(loop, 11, 0, 30, 33);
                var lockBody = Box("LockBody", lockRoot, C("FFF2D4"), style.RoundedFill); Place(lockBody, 3, 23, 46, 32);
                var hole = Box("Keyhole", lockBody, Ink, style.CircleFill); Place(hole, 18, 8, 10, 14);
            }
            bonusContent.gameObject.AddComponent<DeckUpgradeBonusView>().Build(style, detail);
            var buttonBase = Box("ButtonShadow", detail, C("D9883F"), style.RoundedFill); Place(buttonBase, 48, 1005, 860, 151);
            var button = Box("UpgradeButton", detail, Gold, style.RoundedFill); Place(button, 48, 990, 860, 151);
            upgradeRect = button;
            button.pivot = new Vector2(.5f, .5f); button.anchoredPosition += new Vector2(430, -75.5f);
            upgradeRestPosition = button.anchoredPosition;
            button.GetComponent<Image>().raycastTarget = true; upgrade = button.gameObject.AddComponent<Button>(); upgrade.onClick.AddListener(UpgradeSelected);
            Text("UpgradeLabel", "강화하기", button, 20, 4, 820, 139, 44, Ink, TextAlignmentOptions.Center);
            var goldGauge = Box("PopupGoldGauge", detail, C("49374A"), style.RoundedFill); Place(goldGauge, 175, 478, 606, 62);
            popupGauge = UpgradeCostGauge.Create(goldGauge, style.RoundedFill, style.UpgradeArrow);
            AddGoldIcon(goldGauge, 76);
            cost = Text("UpgradeCost", "", goldGauge, 86, -2, 450, 62, 29, Color.white, TextAlignmentOptions.Center);
            OutlineText(cost, 1);
            popup.SetActive(false);
        }

        public void SelectUnit(int index)
        {
            selected = index; abilityTab = 0; popup.SetActive(true); Refresh();
            Canvas.ForceUpdateCanvases(); ShowPreview(popupPreview, selected); ForceText();
            if (popupMotion != null) StopCoroutine(popupMotion);
            if (punchMotion != null) { StopCoroutine(punchMotion); punchMotion = null; }
            upgradeRect.localScale = Vector3.one; upgradeRect.anchoredPosition = upgradeRestPosition;
            popupMotion = StartCoroutine(PopOpen());
        }

        public void CloseDetails()
        {
            if (popupMotion != null) { StopCoroutine(popupMotion); popupMotion = null; }
            if (punchMotion != null) { StopCoroutine(punchMotion); punchMotion = null; }
            popup.SetActive(false); detail.localScale = Vector3.one * _popupFitScale;
            upgradeRect.localScale = Vector3.one; upgradeRect.anchoredPosition = upgradeRestPosition;
        }

        private IEnumerator PopOpen()
        {
            popupGroup.interactable = false;
            for (float time = 0; time < .28f; time += Time.unscaledDeltaTime)
            {
                SamplePopup(time / .28f); yield return null;
            }
            SamplePopup(1); popupGroup.interactable = true; popupMotion = null;
        }

        // Fast impact, visible overshoot, squash, then a short second rebound.
        public void SamplePopup(float t)
        {
            detail.localScale = Vector3.one * (_popupFitScale * KeyBounce(t, .12f));
        }

        /// <summary>Fits the authored detail panel inside its page and captures the action button's new resting position.</summary>
        public void RefreshPopupLayout()
        {
            _fitPopup = true;
            Canvas.ForceUpdateCanvases();
            _popupAreaSize = _popupArea.rect.size;
            const float edgeMargin = 48f;
            _popupFitScale = Mathf.Min(1f, Mathf.Max(1f, _popupAreaSize.x - edgeMargin) / detail.rect.width,
                Mathf.Max(1f, _popupAreaSize.y - edgeMargin) / (detail.rect.height + _popupTopOverhang));
            detail.anchoredPosition = new Vector2(0, -_popupTopOverhang * _popupFitScale * .5f);
            if (popupMotion == null) detail.localScale = Vector3.one * _popupFitScale;
            upgradeRestPosition = upgradeRect.anchoredPosition;
        }

        /// <summary>Includes artwork extending above the panel when fitting and centering the complete popup.</summary>
        public void RefreshPopupLayout(float topOverhang)
        {
            _popupTopOverhang = Mathf.Max(0, topOverhang);
            RefreshPopupLayout();
        }

        private static float KeyBounce(float t, float start)
        {
            t = Mathf.Clamp01(t);
            if (t < .72f) return Mathf.Lerp(start, 1.06f, 1 - Mathf.Pow(1 - t / .72f, 3));
            return Mathf.Lerp(1.06f, 1, Mathf.SmoothStep(0, 1, (t - .72f) / .28f));
        }

        public void UpgradeSelected()
        {
            if (!_presenter.TryUpgrade(selected)) return;
            _statsTabs?.ShowUpgradeResult();
            if (selected >= 0) popupPreview.PlayAction(attackAnimations[selected]);

            if (punchMotion != null) StopCoroutine(punchMotion);
            punchMotion = StartCoroutine(UpgradePunch());
        }

        private IEnumerator UpgradePunch()
        {
            for (float time = 0; time < .22f; time += Time.unscaledDeltaTime)
            {
                float bump = Mathf.Sin(time / .22f * Mathf.PI);
                float scale = 1 + bump * .12f;
                upgradeRect.localScale = new Vector3(scale, scale, 1);
                upgradeRect.anchoredPosition = upgradeRestPosition + new Vector2(0, bump * 8);
                yield return null;
            }
            upgradeRect.localScale = Vector3.one; upgradeRect.anchoredPosition = upgradeRestPosition; punchMotion = null;
        }

        private void Refresh()
        {
            for (int i = 0; i < units.Length; i++)
            {
                var item = _presenter.GetItem(i);
                levels[i].text = "Lv." + item.Level;
                if (prices[i] != null) prices[i].text = item.IsMaxLevel ? "MAX" : item.Gold + "/" + item.Cost;
                if (moneyGauges[i] != null) moneyGauges[i].SetAmount(item.Gold, item.Cost, !item.IsMaxLevel);
            }
            var chiefData = _presenter.GetItem(-1);
            var data = _presenter.GetItem(selected);
            if (wallet != null) wallet.text = data.Gold.ToString("N0");
            if (chiefLevel != null) chiefLevel.text = chiefData.Name + "  Lv." + chiefData.Level;
            if (chiefBonus != null) chiefBonus.text = $"전체 유닛 능력치 +{chiefData.ChiefBonus * 100:0}%";
            if (chiefCost != null) chiefCost.text = chiefData.IsMaxLevel ? "MAX" : chiefData.Gold + "/" + chiefData.Cost;
            if (chiefGauge != null) chiefGauge.SetAmount(chiefData.Gold, chiefData.Cost, !chiefData.IsMaxLevel);
            upgrade.interactable = data.CanUpgrade;
            if (cost != null) cost.text = data.IsMaxLevel ? "MAX" : data.Gold.ToString("N0") + " / " + data.Cost.ToString("N0");
            if (popupGauge != null) popupGauge.SetAmount(data.Gold, data.Cost, !data.IsMaxLevel);
            if (title != null) title.text = data.Name + "  Lv." + data.Level;
            skillName.text = data.SkillName;
            description.text = data.Description;
            if (stat != null) stat.text = data.StatComparison;
            if (speedStat != null) speedStat.text = data.SpeedComparison;
            ResizeAbility();
            _selector?.Render(chiefData, data);
            _statsTabs?.Render(data);
        }

        private void ResizeAbility()
        {
            var content = (RectTransform)description.transform.parent;
            float height = Mathf.Max(272, description.GetPreferredValues(description.text, 788, 0).y + 128);
            content.sizeDelta = new Vector2(828, height); content.anchoredPosition = Vector2.zero;
            ((RectTransform)description.transform).sizeDelta = new Vector2(788, height - 116);
            SetAbilityTab(abilityTab);
        }

        private Image BuildAbilityTab(string name, string label, float x, int index)
        {
            var face = Box(name, detail, Gold, style.RoundedFill); Place(face, x, 562, 422, 56);
            face.GetComponent<Image>().raycastTarget = true;
            face.gameObject.AddComponent<Button>().onClick.AddListener(() => SetAbilityTab(index));
            Text("TabLabel", label, face, 0, 0, 422, 56, 29, Foreground, TextAlignmentOptions.Center);
            return face.GetComponent<Image>();
        }

        public void SetAbilityTab(int index)
        {
            abilityTab = index;
            bool paged = _statsTabs != null;
            abilityContent.gameObject.SetActive(paged || index == 0); bonusContent.gameObject.SetActive(paged || index == 1);
            if (!paged)
            {
                abilityScroll.content = index == 0 ? abilityContent : bonusContent;
                abilityScroll.StopMovement(); abilityScroll.content.anchoredPosition = Vector2.zero;
            }
            skillTabFace.color = index == 0 ? C("0094A6") : C("003B44");
            bonusTabFace.color = index == 1 ? C("0094A6") : C("003B44");
            int level = _presenter.GetItem(selected).Level;
            bonusContent.GetComponent<DeckUpgradeBonusView>().Refresh(selected < 0 ? "알팡" : units[selected].unitName);
            for (int i = 0; i < BonusLevels.Length; i++)
            {
                bool locked = level < BonusLevels[i];
                bonusCovers[i].SetActive(locked); bonusLocks[i].SetActive(locked);
            }
        }

        private void ShowPreview(DroneSpinePreview preview, int index)
        {
            if (index < 0) { preview.Show(chiefSpine, chiefIdle, spineMaterial, spineBlendMaterials, chief.Icon); return; }
            preview.Show(spineData[index], idleAnimations[index], spineMaterial, spineBlendMaterials, units[index].icon);
        }

        public void ToggleDeck()
        {
            expanded = !expanded; toggleText.text = expanded ? "−" : "+";
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(AnimateDeck());
        }

        private IEnumerator AnimateDeck()
        {
            float duration = expanded ? .42f : .20f;
            float start = revealAmount;
            if (expanded) { body.sizeDelta = new Vector2(1012, bodyHeight); LayoutDeckSections(true); }
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                float t = time / duration;
                revealAmount = Mathf.Lerp(start, expanded ? 1 : 0, t);
                ApplyCards(revealAmount, !expanded); MoveChiefIcon(revealAmount);
                yield return null;
            }
            SetReveal(expanded ? 1 : 0); artwork.localRotation = Quaternion.identity; motion = null;
        }

        public void SetReveal(float amount)
        {
            amount = Mathf.Clamp01(amount); body.sizeDelta = new Vector2(1012, amount > 0 ? bodyHeight : 0);
            revealAmount = amount; MoveChiefIcon(amount);
            LayoutDeckSections(amount > 0);
            ApplyCards(amount, false);
        }

        private void MoveChiefIcon(float amount)
        {
            float travel = Mathf.SmoothStep(0, 1, Mathf.Clamp01(amount / .3f));
            chiefTravelIcon.anchoredPosition = new Vector2(506, Mathf.Lerp(-120, -480, travel));
            float size = Mathf.Lerp(240, 320, travel);
            chiefTravelIcon.localScale = Vector3.one * (size / 240);
            float circle = Mathf.SmoothStep(0, 1, Mathf.Clamp01((amount - .04f) / .32f));
            chiefCircle.gameObject.SetActive(amount > .01f);
            chiefCircle.localScale = Vector3.one * (320 * circle / size);
            sectionReveal.sizeDelta = new Vector2(1012, (bodyHeight - 200) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((amount - .31f) / .36f)));
            chiefGauge.transform.localScale = Vector3.one * KeyBounce(Mathf.Clamp01((amount - .36f) / .18f), 0);
            chiefLevel.alpha = chiefBonus.alpha = Mathf.Clamp01((amount - .3f) / .14f);
            chiefTravelIcon.GetComponent<Button>().interactable = amount < .001f || amount > .999f;
        }

        private void ApplyCards(float amount, bool closing)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                float delay = .45f + i * .065f;
                float p = Mathf.Clamp01((amount - delay) / .24f);
                var group = cards[i].GetComponent<CanvasGroup>(); group.alpha = p > .01f ? 1 : 0;
                group.blocksRaycasts = amount >= .999f;
                float scale = closing ? Mathf.Lerp(.12f, 1, p) : KeyBounce(p, .12f);
                cards[i].localScale = Vector3.one * scale;
                moneyGauges[i].transform.localScale = Vector3.one * KeyBounce(Mathf.Clamp01((p - .28f) / .72f), 0);
                cards[i].anchoredPosition = positions[i];
                cards[i].localRotation = Quaternion.identity;
            }
        }

        private void ForceText()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var label in GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        }
        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreen != new Vector2Int(Screen.width, Screen.height)) ApplySafeArea();
            if (_fitPopup && _popupArea != null && _popupArea.rect.size != _popupAreaSize) RefreshPopupLayout();
        }
        private void ApplySafeArea()
        {
            if (safe == null || Screen.width == 0 || Screen.height == 0) return;
            lastSafeArea = Screen.safeArea; lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (_embeddedInLobby) { Stretch(safe); return; }
            safe.GetComponentInParent<CanvasScaler>().matchWidthOrHeight = Screen.width / (float)Screen.height > 1080f / 1920 ? 1 : 0;
            safe.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safe.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        private RectTransform Box(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
            rect.SetParent(parent, false); var image = rect.GetComponent<Image>();
            image.color = color; image.sprite = sprite; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = style.RoundedCornerScale; image.raycastTarget = false; return rect;
        }
        private TMP_Text Text(string name, string text, Transform parent, float x, float y, float width, float height,
            float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform;
            rect.SetParent(parent, false); Place(rect, x, y, width, height);
            var label = rect.GetComponent<TextMeshProUGUI>(); label.font = style.FontBold; label.text = text;
            label.fontSize = size; label.color = color; label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; return label;
        }
        private static void OutlineText(TMP_Text text, float size)
        {
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .7f); outline.effectDistance = new Vector2(size, -size);
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
