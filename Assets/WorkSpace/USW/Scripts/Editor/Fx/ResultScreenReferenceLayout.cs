using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Applies design/전투종료예시.png to the shared result prefab without rebuilding any scene.</summary>
public static class ResultScreenReferenceLayout
{
    private const string Art = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/Battle Outcome/";
    private const string LegacyArt = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/";
    private static readonly Color Gold = new Color(1f, .83f, .18f);

    /// <summary>Imports the supplied UI art and updates the existing prefab, preserving its scene references.</summary>
    [MenuItem("Tools/USW/Fx/Apply Result Reference Layout")]
    public static void ApplyPrefab()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring the prefab.");
        ImportArt();
        var root = PrefabUtility.LoadPrefabContents(ResultScreenLabBuilder.PrefabPath);
        try
        {
            Configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, ResultScreenLabBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Configures newly generated and existing result roots with the same maintained layout.</summary>
    public static void Configure(GameObject root)
    {
        var view = root.GetComponent<ResultScreenView>();
        var so = new SerializedObject(view);
        var font = Ref<TextMeshProUGUI>(so, "_headerTitle").font;
        var content = root.transform.Find("ReferenceContent") as RectTransform;
        if (content == null)
        {
            content = Rect("ReferenceContent", root.transform, new Vector2(1080f, 1920f), Vector2.zero);
            var fit = content.gameObject.AddComponent<SafeAreaContentFrame>();
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("_expandHeight").boolValue = false;
            fitSo.ApplyModifiedPropertiesWithoutUndo();
        }
        var header = Ref<CanvasGroup>(so, "_header");
        var chief = Ref<RectTransform>(so, "_chief");
        var record = Ref<CanvasGroup>(so, "_recordBlock");
        var build = Ref<CanvasGroup>(so, "_buildBlock");
        var reward = Ref<CanvasGroup>(so, "_rewardBlock");
        var buttons = Ref<CanvasGroup>(so, "_buttons");
        var ad = Ref<CanvasGroup>(so, "_adSlot");
        var popup = Ref<GameObject>(so, "_buildPopup");
        foreach (var t in new Transform[] { header.transform, chief, record.transform, build.transform,
            reward.transform, buttons.transform, ad.transform, popup.transform }) t.SetParent(content, false);
        Pose((RectTransform)header.transform, 950f, 300f, 0f, 780f);
        var banner = Ref<Image>(so, "_headerBanner");
        Pose(banner.rectTransform, 950f, 346f, 0f, 0f);
        banner.sprite = Sprite("SPR_UI2600_HeaderBanner.png");
        banner.preserveAspect = true;
        var title = Ref<TextMeshProUGUI>(so, "_headerTitle");
        Text(title, "전투 종료", 80f, Color.white, 760f, 100f, 0f, 10f);
        Shadow(title);
        Set(so, "_plainBanner", banner.sprite);
        Pose(chief, 550f, 440f, 0f, 335f);
        chief.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Imports/GGD_ArtWork/LHH_Artwork/Boss/Boss_Designs/Boss_Crocodile.png");
        chief.GetComponent<Image>().preserveAspect = true;

        Pose((RectTransform)record.transform, 760f, 110f, 0f, 610f);
        Text(record.transform.Find("RoundLabel").GetComponent<TextMeshProUGUI>(), "도달 라운드", 34f, Color.white, 270f, 70f, -180f, 0f);
        var round = Ref<TextMeshProUGUI>(so, "_roundValue");
        Text(round, "0", 100f, Gold, 240f, 120f, 100f, 0f);
        Shadow(round);
        var divider = Image("Divider", record.transform, null, new Vector2(3f, 60f), Color.gray, new Vector2(-10f, 0f));
        var wave = Label("Wave", record.transform, font, "Wave", 40f, Gold, new Vector2(130f, 60f), new Vector2(265f, -12f));
        var progress = Ref<ResultBossProgressView>(so, "_bossProgress");
        progress.transform.SetParent(content, false);
        Pose((RectTransform)progress.transform, 760f, 170f, 0f, -170f);
        var pso = new SerializedObject(progress);
        pso.FindProperty("_showRemainingOnly").boolValue = true;
        pso.FindProperty("_duration").floatValue = .8f;
        pso.ApplyModifiedPropertiesWithoutUndo();
        Text(progress.transform.Find("Title").GetComponent<TextMeshProUGUI>(), "마지막 보스", 34f, Color.white, 700f, 50f, 0f, 60f);
        Text(progress.transform.Find("Caption").GetComponent<TextMeshProUGUI>(), "100% 남음", 42f, Gold, 730f, 60f, 0f, -60f);
        var track = progress.transform.Find("HpSettlement/Track").GetComponent<Image>();
        Pose(track.rectTransform, 730f, 34f, 0f, 0f);
        track.sprite = Sprite("SPR_UI2600_BossHPLeftBg.png");
        track.color = Color.white;
        track.type = UnityEngine.UI.Image.Type.Sliced;
        track.preserveAspect = false;
        var fill = track.transform.Find("RemainingHp").GetComponent<Image>();
        Pose(fill.rectTransform, 730f, 34f, -365f, 0f);
        fill.rectTransform.pivot = new Vector2(0f, .5f);
        fill.sprite = Sprite("SPR_UI2600_BossHPLeftFrame.png");
        fill.type = UnityEngine.UI.Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Color.white;

        Pose((RectTransform)reward.transform, 760f, 180f, 0f, 60f);
        var row = reward.transform.Find("Row").GetComponent<RectTransform>();
        Pose(row, 700f, 140f, 0f, 0f);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 50f;
        var slot = Ref<RectTransform>(so, "_rewardSlotTemplate");
        Pose(slot, 130f, 130f, 0f, 0f);
        slot.GetComponent<Image>().sprite = Sprite("SPR_UI2600_RewardSlotBg.png");
        slot.GetComponent<Image>().color = Color.white;
        if (slot.GetComponent<CanvasGroup>() == null) slot.gameObject.AddComponent<CanvasGroup>();
        Pose(slot.Find("Icon").GetComponent<RectTransform>(), 85f, 85f, 0f, 16f);
        Text(slot.Find("Amount").GetComponent<TextMeshProUGUI>(), "—", 26f, new Color(.1f,.08f,.06f), 110f, 34f, 0f, -39f);
        var unavailable = Label("Unavailable", reward.transform, font, "보상 준비 중", 24f, Color.gray, new Vector2(700f, 36f), new Vector2(0f, -88f));
        Set(so, "_rewardUnavailableLabel", unavailable);
        var icons = so.FindProperty("_unavailableRewardIcons");
        icons.arraySize = 2;
        icons.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(LegacyArt + "SPR_UI1000_GoldIcon.png");
        icons.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(LegacyArt + "Power-up_EXp.png");
        so.FindProperty("_rewardShiftWithoutBuild").floatValue = 0f;

        Pose((RectTransform)build.transform, 800f, 200f, 0f, -420f);
        Text(build.transform.Find("Label").GetComponent<TextMeshProUGUI>(), "이번 판 빌드", 34f, Color.white, 700f, 50f, 0f, 75f);
        build.transform.Find("Hint").gameObject.SetActive(false);
        var scroll = Ref<ScrollRect>(so, "_buildScroll");
        Pose(scroll.viewport, 760f, 104f, 0f, -10f);
        Pose(scroll.content, 760f, 104f, 0f, 0f);
        scroll.content.anchorMin = scroll.content.anchorMax = new Vector2(0f, .5f);
        scroll.content.pivot = new Vector2(0f, .5f);
        scroll.horizontal = true;
        scroll.vertical = false;
        var grid = scroll.content.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(10f, 0f);
        grid.padding = new RectOffset(0, 0, 2, 2);
        var fitter = scroll.content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        Image("Divider", build.transform, null, new Vector2(760f, 3f), Color.gray, new Vector2(0f, -110f));
        var empty = Label("Empty", build.transform, font, "선택한 효과 없음", 28f, Color.gray, new Vector2(700f, 70f), new Vector2(0f, -10f));
        Set(so, "_emptyBuildLabel", empty);

        Pose((RectTransform)buttons.transform, 950f, 110f, 0f, -795f);
        ConfigureButton(Ref<Button>(so, "_retryButton"), "다시하기", -240f);
        ConfigureButton(Ref<Button>(so, "_homeButton"), "로비로", 240f);
        Pose((RectTransform)ad.transform, 530f, 92f, 10f, -665f);
        var adButton = Ref<Button>(so, "_adRewardButton");
        Pose((RectTransform)adButton.transform, 530f, 92f, 0f, 0f);
        adButton.image.sprite = Sprite("SPR_UI2600_AdButtonBg.png");
        adButton.image.color = Color.white;
        adButton.image.type = UnityEngine.UI.Image.Type.Sliced;
        var adText = adButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        Text(adText, "보상 더 받기", 46f, Color.white, 470f, 78f, 0f, 0f);
        Shadow(adText);
        adButton.transform.Find("AdIcon").gameObject.SetActive(false);
        adButton.transform.Find("Detail").gameObject.SetActive(false);
        var statsBg = Image("StatsButton", buttons.transform, Sprite("SPR_UI2600_AdButtonBg.png"),
            new Vector2(96f, 96f), new Color(.6f, .7f, 1f), new Vector2(350f, 130f));
        statsBg.type = UnityEngine.UI.Image.Type.Sliced;
        statsBg.preserveAspect = false;
        var statsIcon = Image("Icon", statsBg.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(LegacyArt + "SPR_UI2000_DealGraphIcon.png"),
            new Vector2(100f, 100f), Color.white, Vector2.zero);
        Set(so, "_statsIcon", statsIcon.sprite);
        statsBg.raycastTarget = true;
        var stats = statsBg.GetComponent<Button>() ?? statsBg.gameObject.AddComponent<Button>();
        stats.targetGraphic = statsBg;
        Set(so, "_statsButton", stats);
        var popupRt = (RectTransform)popup.transform;
        popupRt.anchorMin = Vector2.zero; popupRt.anchorMax = Vector2.one;
        popupRt.offsetMin = popupRt.offsetMax = Vector2.zero;
        Ref<Image>(so, "_dim").color = new Color(0f, 0f, 0f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
    }

    /// <summary>Sets sprite and slicing metadata on the supplied textures; the source PNGs stay intact.</summary>
    public static void ImportArt()
    {
        foreach (var file in Directory.GetFiles(Art, "*.png").Concat(new[] { LegacyArt + "SPR_UI2000_DealGraphIcon.png" }))
        {
            string path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100f;
            string name = Path.GetFileName(path);
            importer.spriteBorder = name.Contains("ButtonBg") || name.Contains("RetryButton") ? new Vector4(48f, 36f, 48f, 36f) :
                name.Contains("BossHP") ? new Vector4(8f, 4f, 8f, 4f) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static T Ref<T>(SerializedObject so, string name) where T : Object => (T)so.FindProperty(name).objectReferenceValue;
    private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name) ?? throw new InvalidOperationException(Art + name);
    private static void Pose(RectTransform rect, float w, float h, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.sizeDelta = new Vector2(w,h); rect.anchoredPosition = new Vector2(x,y); rect.localScale = Vector3.one;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) {rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);}
        Pose(rect,size.x,size.y,position.x,position.y); return rect;
    }
    private static Image Image(string name, Transform parent, Sprite sprite, Vector2 size, Color color, Vector2 position)
    {
        var rect = Rect(name,parent,size,position); var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.preserveAspect = sprite != null; image.raycastTarget = false; return image;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string value, float size, Color color, Vector2 box, Vector2 pos)
    {
        var rect = Rect(name,parent,box,pos); var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; Text(text,value,size,color,box.x,box.y,pos.x,pos.y); return text;
    }
    private static void Text(TextMeshProUGUI text, string value, float size, Color color, float w, float h, float x, float y)
    {
        Pose(text.rectTransform,w,h,x,y); text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
    }
    private static void Shadow(TextMeshProUGUI text)
    {
        var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(3f,-3f);
    }
    private static void ConfigureButton(Button button, string label, float x)
    {
        Pose((RectTransform)button.transform,430f,84f,x,0f);
        button.image.sprite = Sprite("SPR_UI2600_RetryButton_Idle.png");
        button.image.type = UnityEngine.UI.Image.Type.Sliced; button.image.preserveAspect = false;
        button.image.pixelsPerUnitMultiplier = 1f; button.image.color = Color.white;
        Text(button.GetComponentInChildren<TextMeshProUGUI>(),label,46f,Color.white,400f,80f,0f,0f);
        Shadow(button.GetComponentInChildren<TextMeshProUGUI>());
    }
}
