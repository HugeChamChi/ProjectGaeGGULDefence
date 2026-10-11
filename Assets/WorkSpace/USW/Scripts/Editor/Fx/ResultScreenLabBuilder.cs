using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 프리팹(UI_ResultScreen)과 실험실 씬(FxLab_Result)을 만든다. 현행 배치는 design/전투종료예시.png.
///   FxLab_Result 한 씬에서 담백 결과 화면의 HP 정산·도장·결승선을 비교한다.
///   아트: UI2600 배너·배경, 알팡 일러스트, 선택지 아이콘, 골드/EXP 아이콘, UI2200 버튼(9-slice).
/// 다시 실행하면 프리팹/씬을 이 값으로 덮어쓴다 — 인스펙터에서 튜닝한 뒤에는 이 파일 값도 갱신할 것.
/// </summary>
public static class ResultScreenLabBuilder
{
    public const string PrefabDir = "Assets/WorkSpace/USW/Prefab/UI/Result";
    public const string PrefabPath = PrefabDir + "/UI_ResultScreen.prefab";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_Result.unity";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)

    private const string ArtDir = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/";
    private const string PlainBannerPath = ArtDir + "SPR_UI2600_ClearBanner.png";      // 파란 배경 + 노려보는 몬스터
    private const string ButtonPath = ArtDir + "SPR_UI2200_Button_Idle.png";
    private const string SlotBgPath = ArtDir + "Shared_Use_background.png";
    private const string GoldIconPath = ArtDir + "SPR_UI1000_GoldIcon.png";
    private const string ExpIconPath = ArtDir + "Power-up_EXp.png";
    private const string AdIconPath = ArtDir + "SPR_UI1000_AdIcon.png";
    private const string ChiefPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Alphang+HoverPad_1.png";
    private const string SelectionDataDir = "Assets/WorkSpace/USW/Data/SelectionData";
    private const int BuildChoiceCount = 12;
    private const int BuildColumns = 6;

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Color LabelDim = new Color(1f, 1f, 1f, 0.75f);
    private const float CaptureDuration = 5.4f;

    private static Scene _stage;

    [MenuItem("Tools/USW/Fx/Build Result Screen Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[ResultScreenLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ResultScreenReferenceLayout.ImportArt();
        var art = Art.Load();
        if (!art.IsValid(out string missing))
        {
            Debug.LogError($"[ResultScreenLab] 아트 없음: {missing} — Assets/Imports 확인");
            return;
        }

        Directory.CreateDirectory(PrefabDir);
        _stage = EditorSceneManager.NewPreviewScene();
        try { BuildPrefab(art); }
        finally { EditorSceneManager.ClosePreviewScene(_stage); }
        AssetDatabase.SaveAssets();
        BuildScene();
        Debug.Log($"[ResultScreenLab] 완료: {PrefabPath}, {ScenePath}");
    }

    // ── 아트 ──────────────────────────────────────────────────

    private sealed class Art
    {
        public TMP_FontAsset Font;
        public Sprite PlainBanner, Button, SlotBg, Gold, Exp, AdIcon, Chief;
        public LevelUpData[] BuildChoices;

        public static Art Load() => new Art
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid)),
            PlainBanner = LoadSprite(PlainBannerPath),
            Button = LoadSprite(ButtonPath),
            SlotBg = LoadSprite(SlotBgPath),
            Gold = LoadSprite(GoldIconPath),
            Exp = LoadSprite(ExpIconPath),
            AdIcon = LoadSprite(AdIconPath),
            Chief = LoadSprite(ChiefPath),
            BuildChoices = AssetDatabase.FindAssets("t:LevelUpData", new[] { SelectionDataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => AssetDatabase.LoadAssetAtPath<LevelUpData>(p))
                .Where(s => s != null && s.icon != null).OrderBy(s => s.chooseId)
                .Take(BuildChoiceCount).ToArray(),
        };

        public bool IsValid(out string missing)
        {
            var checks = new (string name, Object value)[]
            {
                ("font", Font), ("plainBanner", PlainBanner),
                ("button", Button), ("slotBg", SlotBg), ("gold", Gold), ("exp", Exp), ("adIcon", AdIcon), ("chief", Chief),
            };
            missing = string.Join(", ", checks.Where(c => c.value == null).Select(c => c.name));
            if (BuildChoices.Length == 0) missing += (missing.Length > 0 ? ", " : "") + "selectionData";
            return missing.Length == 0;
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ── 프리팹 ────────────────────────────────────────────────

    private static void BuildPrefab(Art art)
    {
        var root = new GameObject("UI_ResultScreen", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, _stage);
        var rootRt = (RectTransform)root.transform;
        Stretch(rootRt);
        var view = root.AddComponent<ResultScreenView>();

        var dim = NewImage("Dim", rootRt, null, Vector2.zero, Color.black);
        Stretch(dim.rectTransform);

        // 재생 중 화면을 누르면 건너뛰기 (버튼보다 뒤에 둔다)
        var skip = NewImage("SkipCatcher", rootRt, null, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        Stretch(skip.rectTransform);
        skip.raycastTarget = true;
        var skipButton = skip.gameObject.AddComponent<Button>();
        skipButton.transition = Selectable.Transition.None;

        // 헤더 + 족장
        var header = NewGroup("Header", rootRt, new Vector2(860f, 230f), new Vector2(0f, 680f));
        var banner = NewImage("Banner", (RectTransform)header.transform, art.PlainBanner, new Vector2(860f, 230f), Color.white);
        var title = NewText("Title", (RectTransform)header.transform, art.Font, 68f, Color.white, new Vector2(760f, 100f));
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        var chief = NewImage("Chief", rootRt, art.Chief, new Vector2(260f, 260f), Color.white).rectTransform;
        chief.anchoredPosition = new Vector2(0f, 435f);

        // 기록
        var record = NewGroup("RecordBlock", rootRt, new Vector2(1000f, 380f), new Vector2(0f, 170f));
        var recordRt = (RectTransform)record.transform;
        Place(NewText("RoundLabel", recordRt, art.Font, 32f, LabelDim, new Vector2(600f, 46f)), 0f, 100f).text = "도달 라운드";
        var roundValue = Place(NewText("RoundValue", recordRt, art.Font, 112f, Color.white, new Vector2(600f, 130f)), 0f, 5f);
        var bossProgress = BuildBossProgress(recordRt, art);

        // 이번 판 빌드
        var build = NewGroup("BuildBlock", rootRt, new Vector2(1000f, 280f), new Vector2(0f, -230f));
        var buildRt = (RectTransform)build.transform;
        Place(NewText("Label", buildRt, art.Font, 34f, LabelDim, new Vector2(600f, 50f)), 0f, 90f).text = "이번 판 빌드";
        Place(NewText("Hint", buildRt, art.Font, 24f, LabelDim, new Vector2(900f, 40f)), 0f, 50f).text = "선택지를 눌러 효과 확인";
        var buildViewport = NewImage("Viewport", buildRt, null, new Vector2(700f, 204f), new Color(0f, 0f, 0f, 0f));
        buildViewport.rectTransform.anchoredPosition = new Vector2(0f, -76f);
        buildViewport.raycastTarget = true;
        buildViewport.gameObject.AddComponent<RectMask2D>();
        var buildRow = NewRect("Choices", buildViewport.rectTransform, new Vector2(700f, 204f));
        buildRow.anchorMin = buildRow.anchorMax = new Vector2(0.5f, 1f);
        buildRow.pivot = new Vector2(0.5f, 1f);
        var buildGrid = buildRow.gameObject.AddComponent<GridLayoutGroup>();
        buildGrid.cellSize = new Vector2(92f, 92f);
        buildGrid.spacing = new Vector2(12f, 12f);
        buildGrid.padding = new RectOffset(44, 44, 4, 4);
        buildGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        buildGrid.constraintCount = BuildColumns;
        var buildSize = buildRow.gameObject.AddComponent<ContentSizeFitter>();
        buildSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var buildScroll = buildViewport.gameObject.AddComponent<ScrollRect>();
        buildScroll.viewport = buildViewport.rectTransform;
        buildScroll.content = buildRow;
        buildScroll.horizontal = false;
        buildScroll.movementType = ScrollRect.MovementType.Clamped;
        var buildIcon = NewImage("IconTemplate", buildRow, null, new Vector2(92f, 92f), Color.white);
        buildIcon.raycastTarget = true;
        buildIcon.gameObject.AddComponent<Button>();

        // 빌드 → 골드·EXP → 광고 보상 → 다시하기·로비 순서로 중앙 정렬.
        var reward = NewGroup("RewardBlock", rootRt, new Vector2(1000f, 330f), new Vector2(0f, -500f));
        var rewardRt = (RectTransform)reward.transform;
        var rewardRow = NewRow("Row", rewardRt, new Vector2(700f, 150f), -30f, 40f);
        var slot = NewImage("SlotTemplate", rewardRow, art.SlotBg, new Vector2(150f, 150f), Color.white).rectTransform;
        Place(NewImage("Icon", slot, art.Gold, new Vector2(92f, 92f), Color.white), 0f, 14f);
        Place(NewText("Amount", slot, art.Font, 32f, Color.white, new Vector2(140f, 44f)), 0f, -48f);
        var ad = NewGroup("AdSlot", rewardRt, new Vector2(640f, 104f), new Vector2(0f, -195f));
        var adRt = (RectTransform)ad.transform;
        var adBg = NewImage("ClaimButton", adRt, art.SlotBg, new Vector2(640f, 104f), new Color(0.12f, 0.9f, 0.62f, 1f));
        adBg.preserveAspect = false;
        adBg.raycastTarget = true;
        var adButton = adBg.gameObject.AddComponent<Button>();
        var adButtonRt = adBg.rectTransform;
        Place(NewText("Label", adButtonRt, art.Font, 38f, Color.white, new Vector2(600f, 46f)), 0f, 21f).text = "보상 더 받기";
        Place(NewImage("AdIcon", adButtonRt, art.AdIcon, new Vector2(40f, 40f), Color.white), -126f, -26f);
        Place(NewText("Detail", adButtonRt, art.Font, 28f, Color.white, new Vector2(250f, 40f)), 30f, -26f).text = "광고 보고 2배";

        // 버튼
        var buttons = NewGroup("Buttons", rootRt, new Vector2(1000f, 140f), new Vector2(0f, -850f));
        var buttonsRt = (RectTransform)buttons.transform;
        var retry = NewButton("RetryButton", buttonsRt, art, "다시하기", Color.white, -215f);
        var home = NewButton("HomeButton", buttonsRt, art, "로비로", new Color(0.78f, 0.88f, 1f, 1f), 215f);

        // 설명 팝업은 실험실 조작 버튼보다 위에 표시하고 뒤의 모든 입력을 막는다.
        var popup = NewRect("BuildPopup", rootRt, Vector2.zero);
        Stretch(popup);
        var popupCanvas = popup.gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 20;
        popup.gameObject.AddComponent<GraphicRaycaster>();
        var popupBackdrop = NewImage("Backdrop", popup, null, Vector2.zero, Color.clear);
        Stretch(popupBackdrop.rectTransform);
        popupBackdrop.raycastTarget = true;
        var popupBackdropButton = popupBackdrop.gameObject.AddComponent<Button>();
        popupBackdropButton.transition = Selectable.Transition.None;
        var popupPanel = NewImage("Panel", popup, null, new Vector2(640f, 250f), new Color(0.075f, 0.09f, 0.10f, 1f));
        var popupGroup = popupPanel.gameObject.AddComponent<CanvasGroup>();
        popupPanel.raycastTarget = true;
        var popupBorder = popupPanel.gameObject.AddComponent<Outline>();
        popupBorder.effectColor = new Color(0.7f, 0.7f, 0.65f, 1f);
        popupBorder.effectDistance = new Vector2(2f, -2f);
        var popupIcon = Place(NewImage("Icon", popupPanel.rectTransform, null, new Vector2(64f, 64f), Color.white), -270f, 75f);
        var popupTitle = Place(NewText("Title", popupPanel.rectTransform, art.Font, 34f, Color.white, new Vector2(420f, 64f)), -2f, 75f);
        popupIcon.rectTransform.anchorMin = popupIcon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        popupIcon.rectTransform.anchoredPosition = new Vector2(-270f, -50f);
        popupTitle.rectTransform.anchorMin = popupTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        popupTitle.rectTransform.anchoredPosition = new Vector2(-2f, -50f);
        popupTitle.textWrappingMode = TextWrappingModes.Normal;
        popupTitle.enableAutoSizing = true;
        popupTitle.fontSizeMin = 26f;
        popupTitle.fontSizeMax = 34f;
        var descriptionViewport = NewImage("DescriptionViewport", popupPanel.rectTransform, null, new Vector2(576f, 132f), Color.clear);
        descriptionViewport.rectTransform.anchorMin = descriptionViewport.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        descriptionViewport.rectTransform.pivot = new Vector2(0.5f, 1f);
        descriptionViewport.rectTransform.anchoredPosition = new Vector2(0f, -96f);
        descriptionViewport.raycastTarget = true;
        descriptionViewport.gameObject.AddComponent<RectMask2D>();
        var popupDescription = NewText("Description", descriptionViewport.rectTransform, art.Font, 28f, Color.white, new Vector2(576f, 132f));
        popupDescription.rectTransform.anchorMin = popupDescription.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        popupDescription.rectTransform.pivot = new Vector2(0.5f, 1f);
        popupDescription.textWrappingMode = TextWrappingModes.Normal;
        popupDescription.alignment = TextAlignmentOptions.TopLeft;
        popupDescription.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var popupScroll = descriptionViewport.gameObject.AddComponent<ScrollRect>();
        popupScroll.viewport = descriptionViewport.rectTransform;
        popupScroll.content = popupDescription.rectTransform;
        popupScroll.horizontal = false;
        popupScroll.movementType = ScrollRect.MovementType.Clamped;
        var popupCloseBg = NewImage("Close", popupPanel.rectTransform, null, new Vector2(88f, 64f), new Color(1f, 1f, 1f, 0.08f));
        popupCloseBg.rectTransform.anchorMin = popupCloseBg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        popupCloseBg.rectTransform.anchoredPosition = new Vector2(264f, -36f);
        popupCloseBg.raycastTarget = true;
        var popupClose = popupCloseBg.gameObject.AddComponent<Button>();
        NewText("Label", popupCloseBg.rectTransform, art.Font, 26f, LabelDim, new Vector2(88f, 64f)).text = "닫기";
        popup.gameObject.SetActive(false);

        var so = new SerializedObject(view);
        Set(so, "_dim", dim);
        Set(so, "_skipCatcher", skipButton);
        Set(so, "_header", header);
        Set(so, "_headerBanner", banner);
        Set(so, "_headerTitle", title);
        Set(so, "_plainBanner", art.PlainBanner);
        Set(so, "_chief", chief);
        Set(so, "_recordBlock", record);
        Set(so, "_roundValue", roundValue);
        Set(so, "_bossProgress", bossProgress);
        Set(so, "_buildBlock", build);
        Set(so, "_buildIconTemplate", buildIcon);
        Set(so, "_buildScroll", buildScroll);
        Set(so, "_buildPopup", popup.gameObject);
        Set(so, "_buildPopupPanel", popupPanel.rectTransform);
        Set(so, "_buildPopupGroup", popupGroup);
        Set(so, "_buildPopupBackdrop", popupBackdropButton);
        Set(so, "_buildPopupClose", popupClose);
        Set(so, "_buildPopupIcon", popupIcon);
        Set(so, "_buildPopupTitle", popupTitle);
        Set(so, "_buildPopupDescription", popupDescription);
        Set(so, "_buildPopupScroll", popupScroll);
        Set(so, "_rewardBlock", reward);
        Set(so, "_rewardSlotTemplate", slot);
        Set(so, "_adSlot", ad);
        Set(so, "_adRewardButton", adButton);
        Set(so, "_buttons", buttons);
        Set(so, "_retryButton", retry);
        Set(so, "_homeButton", home);
        so.ApplyModifiedPropertiesWithoutUndo();

        int uiLayer = LayerMask.NameToLayer("UI");
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = uiLayer;
        ResultScreenReferenceLayout.Configure(root);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    // ── 실험실 씬 ─────────────────────────────────────────────

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 열면 먼저 불러 둔 에셋 참조가 풀려 저장 시 비므로 씬을 연 뒤 다시 불러온다
        var art = Art.Load();

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var viewGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var viewRt = (RectTransform)viewGo.transform;
        viewRt.SetParent(canvasRt, false);
        Stretch(viewRt);
        var view = viewGo.GetComponent<ResultScreenView>();

        // 실험실 조작 버튼은 결과 화면 위에 그린다
        var controls = NewRect("LabControls", canvasRt, Vector2.zero);
        Stretch(controls);
        var controlsCanvas = controls.gameObject.AddComponent<Canvas>();
        controlsCanvas.overrideSorting = true;
        controlsCanvas.sortingOrder = 10;
        controls.gameObject.AddComponent<GraphicRaycaster>();

        var driver = new GameObject("ResultScreenLab", typeof(ResultScreenLab)).GetComponent<ResultScreenLab>();
        var row = NewRect("LabTones", controls, new Vector2(980f, 70f));
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -12f);
        var plainButton = NewLabButton(row, "담백", art.Font);
        ((RectTransform)plainButton.transform).sizeDelta = new Vector2(100f, 64f);
        ((RectTransform)plainButton.transform).anchoredPosition = new Vector2(-430f, -35f);
        UnityEventTools.AddVoidPersistentListener(plainButton.onClick, driver.Play);
        string[] candidateNames = { "① HP 정산", "② 도장", "③ 결승선" };
        var progressButtons = new Button[candidateNames.Length];
        for (int i = 0; i < progressButtons.Length; i++)
        {
            var button = NewLabButton(row, candidateNames[i], art.Font);
            progressButtons[i] = button;
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(240f, 64f);
            rt.anchoredPosition = new Vector2(-220f + i * 260f, -35f);
            UnityEventTools.AddIntPersistentListener(button.onClick, driver.SetBossProgressStyle, i);
        }
        var samples = NewRect("HpSamples", controls, new Vector2(800f, 56f));
        samples.anchorMin = samples.anchorMax = new Vector2(0.5f, 1f);
        samples.pivot = new Vector2(0.5f, 1f);
        samples.anchoredPosition = new Vector2(0f, -92f);
        int[] sampleDamage = { 15, 75, 95 };
        var damageButtons = new Button[sampleDamage.Length];
        for (int i = 0; i < sampleDamage.Length; i++)
        {
            var button = NewLabButton(samples, $"예시 HP {sampleDamage[i]}% 감소", art.Font);
            damageButtons[i] = button;
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(250f, 52f);
            rt.anchoredPosition = new Vector2((i - 1) * 260f, -28f);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 26f;
            UnityEventTools.AddIntPersistentListener(button.onClick, driver.SetBossDamagePercent, sampleDamage[i]);
        }

        var dSo = new SerializedObject(driver);
        Set(dSo, "_view", view);
        Set(dSo, "_goldIcon", art.Gold);
        Set(dSo, "_expIcon", art.Exp);
        var progressRefs = dSo.FindProperty("_progressButtons");
        progressRefs.arraySize = progressButtons.Length;
        for (int i = 0; i < progressButtons.Length; i++) progressRefs.GetArrayElementAtIndex(i).objectReferenceValue = progressButtons[i];
        var damageRefs = dSo.FindProperty("_damageButtons");
        damageRefs.arraySize = damageButtons.Length;
        for (int i = 0; i < damageButtons.Length; i++) damageRefs.GetArrayElementAtIndex(i).objectReferenceValue = damageButtons[i];
        var choices = dSo.FindProperty("_buildChoices");
        choices.arraySize = art.BuildChoices.Length;
        for (int i = 0; i < art.BuildChoices.Length; i++) choices.GetArrayElementAtIndex(i).objectReferenceValue = art.BuildChoices[i];
        dSo.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        Set(capSo, "_target", driver);
        Set(capSo, "_camera", cam);
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = CaptureDuration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 960);
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ── 도우미 ────────────────────────────────────────────────

    private static ResultBossProgressView BuildBossProgress(RectTransform parent, Art art)
    {
        var group = NewGroup("BossProgress", parent, new Vector2(760f, 170f), new Vector2(0f, -160f));
        group.blocksRaycasts = false;
        var root = (RectTransform)group.transform;
        var view = group.gameObject.AddComponent<ResultBossProgressView>();
        var mint = new Color(0.45f, 0.95f, 0.8f, 1f);
        Place(NewText("Title", root, art.Font, 28f, LabelDim, new Vector2(700f, 40f)), 0f, 63f).text = "마지막 보스";
        var caption = Place(NewText("Caption", root, art.Font, 30f, Color.white, new Vector2(730f, 42f)), 0f, -68f);
        var variants = new RectTransform[3];
        string[] names = { "HpSettlement", "PartialStamp", "FinishLine" };
        for (int i = 0; i < variants.Length; i++) variants[i] = NewRect(names[i], root, new Vector2(660f, 88f));

        // HP 정산: 꽉 찬 HP가 실제 잔량까지 줄어든다.
        var hpTrack = NewImage("Track", variants[0], null, new Vector2(560f, 26f), new Color(0.18f, 0.21f, 0.22f));
        var hpFill = NewImage("RemainingHp", hpTrack.rectTransform, null, new Vector2(560f, 26f), mint).rectTransform;
        hpFill.pivot = new Vector2(0f, 0.5f);
        hpFill.anchoredPosition = new Vector2(-280f, 0f);

        // 부분 도장: 희미한 도장을 왼쪽부터 피해 비율만큼 완성한다.
        var stamp = NewRect("Stamp", variants[1], new Vector2(300f, 76f));
        StampOutline(stamp, new Color(0.25f, 0.3f, 0.29f), art.Font);
        var reveal = NewRect("Reveal", stamp, new Vector2(300f, 76f));
        reveal.pivot = new Vector2(0f, 0.5f);
        reveal.anchoredPosition = new Vector2(-150f, 0f);
        reveal.gameObject.AddComponent<RectMask2D>();
        var ink = NewRect("Ink", reveal, new Vector2(300f, 76f));
        ink.anchorMin = ink.anchorMax = new Vector2(0f, 0.5f);
        ink.pivot = new Vector2(0f, 0.5f);
        StampOutline(ink, mint, art.Font);

        // 결승선: 작은 점이 피해 진행도를 따라 이동하고 거의 처치했을 때만 한 번 강조한다.
        var line = NewImage("Track", variants[2], null, new Vector2(560f, 5f), new Color(0.25f, 0.28f, 0.29f));
        var lineFill = NewImage("Progress", line.rectTransform, null, new Vector2(560f, 5f), mint).rectTransform;
        lineFill.pivot = new Vector2(0f, 0.5f);
        lineFill.anchoredPosition = new Vector2(-280f, 0f);
        var marker = NewImage("Marker", variants[2], art.SlotBg, new Vector2(20f, 20f), mint).rectTransform;
        var finish = Place(NewImage("Finish", variants[2], null, new Vector2(4f, 42f), Color.white), 280f, 0f);
        Place(NewText("Zero", variants[2], art.Font, 20f, LabelDim, new Vector2(80f, 28f)), -280f, -28f).text = "0";
        Place(NewText("Goal", variants[2], art.Font, 20f, LabelDim, new Vector2(100f, 28f)), 280f, -28f).text = "처치";

        var so = new SerializedObject(view);
        Set(so, "_group", group);
        Set(so, "_caption", caption);
        Set(so, "_hpFill", hpFill);
        Set(so, "_stamp", stamp);
        Set(so, "_stampReveal", reveal);
        Set(so, "_lineFill", lineFill);
        Set(so, "_marker", marker);
        Set(so, "_finish", finish);
        var variantArray = so.FindProperty("_variants");
        variantArray.arraySize = variants.Length;
        for (int i = 0; i < variants.Length; i++) variantArray.GetArrayElementAtIndex(i).objectReferenceValue = variants[i].gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < variants.Length; i++) variants[i].gameObject.SetActive(i == 0);
        return view;
    }

    private static void StampOutline(RectTransform parent, Color color, TMP_FontAsset font)
    {
        var size = parent.sizeDelta;
        Place(NewImage("Top", parent, null, new Vector2(size.x, 3f), color), 0f, size.y * 0.5f - 1.5f);
        Place(NewImage("Bottom", parent, null, new Vector2(size.x, 3f), color), 0f, -size.y * 0.5f + 1.5f);
        Place(NewImage("Left", parent, null, new Vector2(3f, size.y), color), -size.x * 0.5f + 1.5f, 0f);
        Place(NewImage("Right", parent, null, new Vector2(3f, size.y), color), size.x * 0.5f - 1.5f, 0f);
        NewText("Label", parent, font, 32f, color, size).text = "LAST BOSS";
    }

    private static void Set(SerializedObject so, string field, Object value) =>
        so.FindProperty(field).objectReferenceValue = value;

    private static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    private static CanvasGroup NewGroup(string name, RectTransform parent, Vector2 size, Vector2 position)
    {
        var rt = NewRect(name, parent, size);
        rt.anchoredPosition = position;
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        return group;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        return img;
    }

    private static Image NewSliced(string name, RectTransform parent, Sprite sprite, Vector2 size, Color color, float borderScale)
    {
        var img = NewImage(name, parent, sprite, size, color);
        img.preserveAspect = false;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = borderScale; // 원본 테두리(약 185px)를 작은 크기에 맞게 줄인다
        return img;
    }

    private static TextMeshProUGUI NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color, Vector2 box)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.sizeDelta = box;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static T Place<T>(T graphic, float x, float y) where T : Graphic
    {
        graphic.rectTransform.anchoredPosition = new Vector2(x, y);
        return graphic;
    }

    private static RectTransform NewRow(string name, RectTransform parent, Vector2 size, float y, float spacing)
    {
        var row = NewRect(name, parent, size);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return row;
    }

    private static Button NewButton(string name, RectTransform parent, Art art, string label, Color tint, float x)
    {
        var bg = NewSliced(name, parent, art.Button, new Vector2(390f, 130f), tint, 3f);
        bg.raycastTarget = true;
        bg.rectTransform.anchoredPosition = new Vector2(x, 0f);
        var text = NewText("Label", bg.rectTransform, art.Font, 46f, Color.white, new Vector2(370f, 120f));
        text.text = label;
        return bg.gameObject.AddComponent<Button>();
    }

    private static RectTransform NewButtonRow(RectTransform parent, string name, float y)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-40f, 90f);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = layout.childControlHeight = true;
        return row;
    }

    private static Button NewLabButton(RectTransform parent, string label, TMP_FontAsset font)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var text = NewText("Label", (RectTransform)go.transform, font, 32f, Color.white, Vector2.zero);
        text.text = label;
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
