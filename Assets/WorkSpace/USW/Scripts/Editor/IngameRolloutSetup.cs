
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 테스트 씬에서 만든 기능을 IngameScene에 적용한다.
/// 1) TotemSelectTest의 TotemRewardCanvas를 프리팹으로 만들고 연결
/// 2) IngameScene에 토템 보상 화면 프리팹 + 모비노기형 보스 피해 숫자(Lilita One) 배치, InGameInstaller/LifetimeScope 연결
/// 이미 있는 오브젝트는 다시 만들지 않는다.
/// </summary>
public static class IngameRolloutSetup
{
    private const string TotemTestScene   = "Assets/WorkSpace/USW/TotemSelectTest.unity";
    private const string IngameScene      = "Assets/Scenes/IngameScene.unity";
    private const string RewardPrefabPath = "Assets/WorkSpace/USW/Prefab/UI/TotemRewardCanvas.prefab";
    private const string LabSettingsPath  = "Assets/WorkSpace/USW/Data/UI/DamageStyleLab/DamageStyleLabSettings.asset";
    private const string DamageSettingsPath = "Assets/WorkSpace/USW/Data/UI/BossDamageNumberSettings.asset";
    private const string RewardName = "TotemRewardCanvas";
    private const string DamageName = "BossDamageNumbersCanvas";
    private const string LilitaOne  = "Lilita One";

    [MenuItem("Tools/USW/Ingame Rollout/1. Prefab TotemRewardCanvas")]
    public static void PrefabRewardCanvas()
    {
        var scene = EditorSceneManager.OpenScene(TotemTestScene, OpenSceneMode.Single);
        var go = FindRoot(scene, RewardName);
        if (go == null) { Debug.LogError($"[IngameRollout] {RewardName} 없음"); return; }
        if (PrefabUtility.IsPartOfPrefabInstance(go)) { Debug.Log("[IngameRollout] 이미 프리팹 인스턴스"); return; }

        EnsureFolder("Assets/WorkSpace/USW/Prefab/UI");
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, RewardPrefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[IngameRollout] {RewardPrefabPath} 생성, TotemSelectTest 연결");
    }

    [MenuItem("Tools/USW/Ingame Rollout/2. Apply To IngameScene")]
    public static void ApplyToIngame()
    {
        var scene = EditorSceneManager.OpenScene(IngameScene, OpenSceneMode.Single);
        // 씬을 연 뒤에 불러와야 한다 (OpenScene이 앞서 로드한 에셋 참조를 끊는다).
        var damageSettings = EnsureDamageSettings();

        var reward = FindRoot(scene, RewardName);
        if (reward == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RewardPrefabPath);
            if (prefab == null) { Debug.LogError("[IngameRollout] 보상 화면 프리팹이 없음 — 1번 먼저"); return; }
            reward = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        var damage = FindRoot(scene, DamageName);
        if (damage == null) damage = CreateDamageCanvas(damageSettings);
        var numbersSo = new SerializedObject(damage.GetComponent<BossDamageNumbers>());
        var settingsProp = numbersSo.FindProperty("_settings");
        if (settingsProp.objectReferenceValue == null)
        {
            settingsProp.objectReferenceValue = damageSettings;
            numbersSo.ApplyModifiedProperties();
        }

        var installer = Object.FindFirstObjectByType<InGameInstaller>();
        if (installer != null)
        {
            var so = new SerializedObject(installer);
            so.FindProperty("_totemRewardUI").objectReferenceValue = reward.GetComponent<TotemRewardUI>();
            so.ApplyModifiedProperties();
        }
        else Debug.LogError("[IngameRollout] InGameInstaller 없음");

        var scope = Object.FindFirstObjectByType<InGameLifetimeScope>();
        if (scope != null)
        {
            var so = new SerializedObject(scope);
            var list = so.FindProperty("autoInjectGameObjects");
            AddUnique(list, reward);
            AddUnique(list, damage);
            so.ApplyModifiedProperties();
        }
        else Debug.LogError("[IngameRollout] InGameLifetimeScope 없음");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[IngameRollout] IngameScene 적용 완료 (토템 보상 화면 + 모비노기 피해 숫자)");
    }

    // ── 플레이 모드 검증용 ──────────────────────────────────────────

    /// <summary>글자가 "시작"인 버튼을 누른다.</summary>
    [MenuItem("Tools/USW/Ingame Rollout/Check - Press Start")]
    public static void PressStart()
    {
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (label == null || label.text.Trim() != "시작") continue;
            button.onClick.Invoke();
            Debug.Log("[IngameRollout] 시작 버튼 누름");
            return;
        }
        Debug.LogWarning("[IngameRollout] 시작 버튼 없음");
    }

    /// <summary>현재 보스에 일반/치명타/화상 피해를 섞어 넣는다 (데미지 숫자 확인).</summary>
    [MenuItem("Tools/USW/Ingame Rollout/Check - Hit Boss")]
    public static void HitBoss()
    {
        var boss = Object.FindFirstObjectByType<BossBase>();
        if (boss == null) { Debug.LogWarning("[IngameRollout] 보스 없음"); return; }
        boss.TakeDamage(1234m, null, BossDamageKind.Normal);
        boss.TakeDamage(3456m, null, BossDamageKind.Critical);
        boss.TakeDamage(321m, null, BossDamageKind.Burn);
        Debug.Log("[IngameRollout] 보스 피해 3종 적용");
    }

    /// <summary>토템 보상 화면을 연다.</summary>
    [MenuItem("Tools/USW/Ingame Rollout/Check - Open Totem Reward")]
    public static void OpenReward()
    {
        var ui = Object.FindFirstObjectByType<TotemRewardUI>();
        if (ui == null) { Debug.LogWarning("[IngameRollout] TotemRewardUI 없음"); return; }
        ui.Show(() => Debug.Log("[IngameRollout] 토템 보상 선택 완료 콜백"));
        Debug.Log("[IngameRollout] 토템 보상 화면 열기");
    }

    /// <summary>Game 뷰를 Temp/IngameRollout_*.png 로 저장.</summary>
    [MenuItem("Tools/USW/Ingame Rollout/Check - Capture")]
    public static void Capture() => ScreenCapture.CaptureScreenshot($"Temp/IngameRollout_{System.DateTime.Now:HHmmss_fff}.png");

    private static GameObject CreateDamageCanvas(DamageStyleLabSettings settings)
    {
        var go = new GameObject(DamageName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = settings.SortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        var numbers = go.AddComponent<BossDamageNumbers>();
        var so = new SerializedObject(numbers);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_fontIndex").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // 테스트 설정(사용자가 맞춘 색 포함)을 복사하고, 폰트는 Lilita One 하나만 남긴다.
    private static DamageStyleLabSettings EnsureDamageSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(DamageSettingsPath);
        if (settings != null) return settings;

        AssetDatabase.CopyAsset(LabSettingsPath, DamageSettingsPath);
        settings = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(DamageSettingsPath);
        var lilita = System.Array.Find(settings.Fonts, f => f.DisplayName == LilitaOne);
        if (lilita != null) settings.Fonts = new[] { lilita };
        else Debug.LogWarning("[IngameRollout] Lilita One 폰트 항목을 찾지 못함 — 첫 번째 폰트 사용");
        settings.SampleLoopInEditor = false;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static void AddUnique(SerializedProperty list, GameObject go)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == go) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = go;
    }

    private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
