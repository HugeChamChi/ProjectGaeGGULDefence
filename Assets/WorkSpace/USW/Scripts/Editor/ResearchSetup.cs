using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 아웃게임 강화 화면 세팅 도구 (코드 이름 Research, 화면 문구 "강화").
/// - Setup: 흑백 화면 설정 SO(Modern UI Pack 스프라이트, Noto Sans KR)와 레퍼런스(그거예시.gif) 모양 트리 SO를 만들고,
///   OutgameUpgrade 씬에 Canvas / EventSystem / ResearchScreen을 배치한다.
///   앞서 만든 3줄기·갈림길 트리 에셋은 지우지 않고 남겨 두지만 화면에는 연결하지 않는다.
///   트리 SO가 이미 있으면 덮어쓰지 않는다 (직접 고친 내용 보존). 새로 만들려면 에셋을 지우고 다시 실행.
/// - 플레이 중 도구: 노드 클릭·강화·추천 강화·구성 전환·탭 클릭·캡처 (검증용).
/// </summary>
public static class ResearchSetup
{
    private const string DataDir = "Assets/WorkSpace/USW/Data/OutgameUpgrade";
    private const string BasicPath = DataDir + "/UpgradeTree_Basic.asset";
    private const string SettingsPath = DataDir + "/UpgradeViewSettings.asset";
    private const string OldDataDir = "Assets/WorkSpace/USW/Data/Research";
    private const string ScenePath = "Assets/WorkSpace/USW/Data/OutgameUpgrade.unity";
    private const string FontBoldPath = "Assets/Imports/Font/NotoSansKR-Bold SDF.asset";
    private const string FontRegularPath = "Assets/Imports/Font/NotoSansKR-Medium SDF.asset";
    private const string MuiDir = "Assets/Imports/Modern UI Pack/Textures/";
    private const string PictoDir = "Assets/Imports/Layer Lab/GUI Pro-FantasyHero/ResourcesData/Sptites/Components/Icon_PictoIcons/128/";
    private const string EvidenceDir = "production/qa/evidence";

    // 스탯 표 — 이름, 퍼센트 여부, 아이콘(mui:Modern UI / picto:Layer Lab)
    private static readonly (ResearchStat Stat, string Name, bool Percent, string Icon)[] StatTable =
    {
        (ResearchStat.AttackPercent, "공격력", true, "picto:Attack_Power"),
        (ResearchStat.AttackSpeedPercent, "공격 속도", true, "picto:Thunder"),
        (ResearchStat.CritChance, "치명타 확률", true, "picto:Critical"),
        (ResearchStat.CritDamage, "치명타 피해", true, "mui:Icon/Common/Star Filled"),
        (ResearchStat.SkillCooldownReduction, "스킬 쿨타임 감소", true, "mui:Icon/Time/Clock"),
        (ResearchStat.FoodProduction, "식량 생산", true, "picto:Support"),
        (ResearchStat.StartFood, "시작 식량", false, "mui:Icon/Common/Store Bag"),
        (ResearchStat.BossDamage, "보스 피해", true, "picto:Boss"),
        (ResearchStat.BurnDamage, "화상 피해", true, "picto:Fire"),
        (ResearchStat.TotemEffect, "토템 효과", true, "picto:Temple"),
    };

    [MenuItem("Tools/USW/Research/Setup OutgameUpgrade")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[ResearchSetup] 플레이 중에는 실행하지 않는다"); return; }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.isDirty) { Debug.LogError($"[ResearchSetup] 열린 씬({scene.name})에 저장 안 된 변경이 있어 중단한다. 저장하거나 OutgameUpgrade를 먼저 열 것"); return; }
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        Directory.CreateDirectory(DataDir);
        // 첫 버전(파란 레퍼런스 스타일) 에셋 정리 — 이 도구가 만든 것만 있다.
        if (AssetDatabase.IsValidFolder(OldDataDir)) AssetDatabase.DeleteAsset(OldDataDir);

        var settings = LoadOrCreate<ResearchViewSettings>(SettingsPath);
        settings.FontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
        settings.FontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegularPath);
        settings.RoundedFill = Mui("Border/Rounded/128px/Rounded Filled 128px");
        settings.RoundedOutline = Mui("Border/Rounded/128px/Rounded Outline 128px - 1x");
        settings.CircleFill = Mui("Border/Radial/128px/Radial Filled 128px");
        settings.CircleOutline = Mui("Border/Radial/128px/Radial Outline 128px - 3x");
        settings.Shadow = Mui("Shadow/Radial Shadow");
        settings.BlockedIcon = Mui("Icon/Navigation/Cancel Bold");
        settings.UpgradeArrow = Picto("Upgrade");
        var stats = new List<ResearchViewSettings.StatInfo>();
        foreach (var row in StatTable)
            stats.Add(new ResearchViewSettings.StatInfo { Stat = row.Stat, DisplayName = row.Name, IsPercent = row.Percent });
        settings.Stats = stats.ToArray();
        EditorUtility.SetDirty(settings);

        var basic = AssetDatabase.LoadAssetAtPath<ResearchTreeData>(BasicPath) ?? CreateTree(BasicPath, BuildBasicTree);
        AssetDatabase.SaveAssets();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = GameObject.Find("ResearchCanvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("ResearchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
        }

        var screenTf = canvasGo.transform.Find("ResearchScreen");
        GameObject screenGo;
        if (screenTf == null)
        {
            screenGo = new GameObject("ResearchScreen", typeof(RectTransform));
            screenGo.transform.SetParent(canvasGo.transform, false);
            ResearchUi.Stretch((RectTransform)screenGo.transform);
            screenGo.AddComponent<SafeAreaApplier>();
        }
        else screenGo = screenTf.gameObject;

        var screen = screenGo.GetComponent<ResearchScreen>() ?? screenGo.AddComponent<ResearchScreen>();
        var so = new SerializedObject(screen);
        var trees = so.FindProperty("_trees");
        trees.arraySize = 1;
        trees.GetArrayElementAtIndex(0).objectReferenceValue = basic;
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ResearchSetup] 완료: 기본 트리 {basic.Nodes.Count}개 노드, 씬 {ScenePath}");
    }

    private static ResearchTreeData CreateTree(string path, System.Action<ResearchTreeData> build)
    {
        var tree = ScriptableObject.CreateInstance<ResearchTreeData>();
        build(tree);
        AssetDatabase.CreateAsset(tree, path);
        return tree;
    }

    // 레퍼런스(그거예시.gif) 모양: 가운데 줄기 → 두 갈래(2줄) → 원형 노드로 합류 + 오른쪽 옆가지 마름모(특별).
    // 4줄 묶음을 3번 반복하고, 묶음마다 최대 레벨이 커진다. 추천 순서는 따로 두지 않는다 (아래 줄, 왼쪽부터).
    private static void BuildBasicTree(ResearchTreeData tree)
    {
        tree.TreeKey = "BASIC";
        tree.DisplayName = "기본";
        var n = tree.Nodes;
        ResearchStat[] cycle =
        {
            ResearchStat.AttackPercent, ResearchStat.CritChance, ResearchStat.AttackSpeedPercent, ResearchStat.CritDamage,
            ResearchStat.FoodProduction, ResearchStat.AttackPercent, ResearchStat.SkillCooldownReduction, ResearchStat.BurnDamage,
            ResearchStat.BossDamage, ResearchStat.StartFood, ResearchStat.TotemEffect,
        };
        int[] maxLevels = { 3, 5, 7 };
        ResearchStat[] specials = { ResearchStat.BossDamage, ResearchStat.StartFood, ResearchStat.TotemEffect };
        int statIndex = 0;
        ResearchStat Next() => cycle[statIndex++ % cycle.Length];

        string previous = null;
        for (int block = 0; block < 3; block++)
        {
            int b = block * 4, max = maxLevels[block];
            string trunk = $"{b + 1}-1", l1 = $"{b + 2}-1", r1 = $"{b + 2}-2", l2 = $"{b + 3}-1", r2 = $"{b + 3}-2", core = $"{b + 4}-1";
            AddPerLevel(n, trunk, Next(), ResearchNodeShape.Square, b, 0, max, previous != null ? new[] { previous } : new string[0]);
            AddPerLevel(n, l1, Next(), ResearchNodeShape.Square, b + 1, -1, max, trunk);
            AddPerLevel(n, r1, Next(), ResearchNodeShape.Square, b + 1, 1, max, trunk);
            AddPerLevel(n, l2, Next(), ResearchNodeShape.Square, b + 2, -1, max, l1);
            AddPerLevel(n, r2, Next(), ResearchNodeShape.Square, b + 2, 1, max, r1);
            AddPerLevel(n, core, ResearchStat.AttackPercent, ResearchNodeShape.Circle, b + 3, 0, max + 2, l2, r2);
            var special = Add(n, $"S-{block + 1}", specials[block], ResearchNodeShape.Special, b + 3, 2, 1,
                PerLevel(specials[block], 10f), 0, core);
            special.Icon = Mui("Icon/Reward/Medal");
            previous = core;
        }
    }

    private static void AddPerLevel(List<ResearchNodeData> list, string id, ResearchStat stat, ResearchNodeShape shape, int row, int column,
        int maxLevel, params string[] parents) =>
        Add(list, id, stat, shape, row, column, maxLevel, PerLevel(stat, 2f), 0, parents);

    private static float PerLevel(ResearchStat stat, float percentDefault) => stat == ResearchStat.StartFood ? 30f : percentDefault / 100f;

    private static ResearchNodeData Add(List<ResearchNodeData> list, string id, ResearchStat stat, ResearchNodeShape shape, int row, int column,
        int maxLevel, float perLevel, int recommendOrder, params string[] parents)
    {
        var node = new ResearchNodeData
        {
            Id = id, Stat = stat, Shape = shape, Row = row, Column = column, MaxLevel = maxLevel, ValuePerLevel = perLevel,
            RecommendOrder = recommendOrder, Icon = IconOf(stat), Parents = new List<string>(parents),
        };
        list.Add(node);
        return node;
    }

    private static Sprite IconOf(ResearchStat stat)
    {
        foreach (var row in StatTable)
        {
            if (row.Stat != stat) continue;
            return row.Icon.StartsWith("mui:") ? Mui(row.Icon.Substring(4)) : Picto(row.Icon.Substring(6));
        }
        return null;
    }

    private static Sprite Mui(string relative)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MuiDir + relative + ".png");
        if (sprite == null) Debug.LogWarning($"[ResearchSetup] Modern UI 스프라이트 없음: {relative}");
        return sprite;
    }

    private static Sprite Picto(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PictoDir + "PictoIcon_" + name + ".Png");
        if (sprite == null) Debug.LogWarning($"[ResearchSetup] 아이콘 없음: {name}");
        return sprite;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // ---------------------------------------------------------------- 플레이 중 검증 도구

    [MenuItem("Tools/USW/Research/Upgrade Selected")]
    public static void UpgradeSelected() => WithScreen(s =>
    {
        s.Upgrade(s.Selected);
        Log(s, "강화");
    });

    [MenuItem("Tools/USW/Research/Recommended Upgrade")]
    public static void RecommendedUpgrade() => WithScreen(s =>
    {
        s.UpgradeRecommended();
        Log(s, "추천 강화");
    });

    [MenuItem("Tools/USW/Research/Recommended Upgrade x10")]
    public static void RecommendedUpgrade10() => WithScreen(s =>
    {
        for (int i = 0; i < 10; i++) s.UpgradeRecommended();
        Log(s, "추천 강화 x10");
    });

    [MenuItem("Tools/USW/Research/Next Layout")]
    public static void NextLayout() => WithScreen(s => { s.NextLayout(); Log(s, "구성 전환"); });

    /// <summary>검증용: 이름으로 노드 버튼을 눌러(실제 onClick 경로) 선택한다. 선택 안 된 첫 "막힌/열린" 노드.</summary>
    [MenuItem("Tools/USW/Research/Click Blocked Node")]
    public static void ClickBlocked() => ClickNode(ResearchNodeState.Blocked);

    [MenuItem("Tools/USW/Research/Click Maxed Node")]
    public static void ClickMaxed() => ClickNode(ResearchNodeState.Maxed);

    [MenuItem("Tools/USW/Research/Click Next Available Node")]
    public static void ClickNextAvailable() => ClickNode(ResearchNodeState.Available);

    private static void ClickNode(ResearchNodeState wanted) => WithScreen(s =>
    {
        foreach (var view in Object.FindObjectsByType<ResearchNodeView>(FindObjectsSortMode.None))
        {
            var button = view.GetComponent<Button>();
            if (button == null || view.Node == s.Selected || s.Progress.Tree.Find(view.Node.Id) != view.Node) continue;
            if (s.Progress.GetState(view.Node) != wanted) continue;
            button.onClick.Invoke();
            Log(s, $"클릭 {view.Node.Id}");
            return;
        }
        Debug.LogWarning($"[Research] {wanted} 노드 없음");
    });

    /// <summary>검증용: [추천 강화] 버튼을 누른다 (실제 onClick 경로).</summary>
    [MenuItem("Tools/USW/Research/Click Recommend Button")]
    public static void ClickRecommend() => ClickButton("RecommendButton");

    [MenuItem("Tools/USW/Research/Swipe Mode Next")]
    public static void SwipeNext() => WithScreen(s => { s.SwipeMode(1); Log(s, "방식 다음"); });

    [MenuItem("Tools/USW/Research/Swipe Mode Prev")]
    public static void SwipePrev() => WithScreen(s => { s.SwipeMode(-1); Log(s, "방식 이전"); });

    /// <summary>검증용: 토글형의 [추천 따라가기] 토글을 누른다 (실제 onClick 경로).</summary>
    [MenuItem("Tools/USW/Research/Click Follow Toggle")]
    public static void ClickFollow() => ClickButton("FollowToggle");

    /// <summary>검증용: 하단 가운데 버튼([강화] / [선택 바꾸기])을 누른다.</summary>
    [MenuItem("Tools/USW/Research/Click Main Button")]
    public static void ClickMain() => WithScreen(s => ClickButton("MainButton" + s.RecommendMode));

    private static void ClickButton(string name) => WithScreen(s =>
    {
        var go = GameObject.Find(name);
        if (go != null && go.TryGetComponent<Button>(out var button)) { button.onClick.Invoke(); Log(s, name + " 누름"); }
        else Debug.LogWarning($"[Research] {name} 없음");
    });

    [MenuItem("Tools/USW/Research/Capture")]
    public static void Capture() => WithScreen(_ =>
    {
        Directory.CreateDirectory(EvidenceDir);
        string file = $"{EvidenceDir}/upgrade-{System.DateTime.Now:HHmmss}.png";
        ScreenCapture.CaptureScreenshot(file);
        Debug.Log($"[Research] 캡처: {file}");
    });

    private static void Log(ResearchScreen s, string what)
    {
        var node = s.Selected;
        Debug.Log($"[Research] {what} → 선택 {node?.Id} Lv.{s.Progress.GetLevel(node)}/{node?.MaxLevel} {s.Progress.GetState(node)}, " +
                  $"전체 {s.Progress.TotalLevel}, 추천 {s.Progress.GetRecommended()?.Id}, 방식 {s.RecommendMode}");
    }

    private static void WithScreen(System.Action<ResearchScreen> action)
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[Research] 플레이 중에만 쓸 수 있다"); return; }
        var screen = Object.FindAnyObjectByType<ResearchScreen>();
        if (screen == null) { Debug.LogError("[Research] 씬에 ResearchScreen 없음"); return; }
        action(screen);
    }
}
