using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [테스트] 레벨업 등장 흐름 시험 씬 생성: 속도선 전환 → 검은 화면 이펙트 → "빵빵빵" 카드 팝.
///   1. UI_DiagonalFlowBackground 머티리얼 생성 (없을 때만)
///   2. LevelUpUI.prefab 복제 → LevelUpUI_FlowTest.prefab (배경을 검은 단색으로, 자리표시 카드 제거, UnscaledShaderTime 부착)
///      — 실행할 때마다 원본에서 다시 복제한다 (테스트 프리팹에 직접 한 수정은 사라짐).
///   3. LevelUpFlowTest.unity 새로 생성 (카메라, EventSystem, 1080x1920 Canvas, 프리팹 인스턴스,
///      최상단 전환 오버레이 FlowTransition, 이펙트 위치 BurstPoint, LevelUpPopTest 연결)
/// 원본 LevelUpUI.prefab / LevelUp_Selection.prefab 은 수정하지 않는다.
/// </summary>
public static class LevelUpFlowTestSetup
{
    private const string Root = "Assets/WorkSpace/USW/";
    private const string ShaderName = "USW/UI/DiagonalFlowBackground";
    private const string MaterialPath = Root + "Shaders/UI_DiagonalFlowBackground.mat";
    private const string SourcePrefabPath = Root + "Prefab/UI/LevelUpUI.prefab";
    private const string TestPrefabPath = Root + "Prefab/UI/LevelUpUI_FlowTest.prefab";
    private const string CardPrefabPath = Root + "Prefab/UI/LevelUp_Selection.prefab";
    private const string ScenePath = Root + "LevelUpFlowTest.unity";
    private const string TransitionSpritePath = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/In-game/SPR_UI2100_ScreenBg.png";
    private const string SampleFolder = Root + "Data/SelectionData";
    private const int SampleCount = 3;

    [MenuItem("Tools/USW/LevelUp Flow Test/Build Scene")]
    public static void Build()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("[LevelUpFlowTest] 열린 씬에 저장 안 된 변경이 있습니다. 저장/정리 후 다시 실행하세요.");
                return;
            }
        }

        EnsureMaterial();
        EnsureTestPrefab();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 씬 전환 뒤에 에셋을 로드한다 (전환 전에 로드한 참조가 무효화되는 문제 회피).
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath);
        var cardPrefab = AssetDatabase.LoadAssetAtPath<LevelUpCardUI>(CardPrefabPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var transitionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TransitionSpritePath);

        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
        var panelRt = (RectTransform)panel.transform;
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

        var burstPoint = new GameObject("BurstPoint", typeof(RectTransform)).GetComponent<RectTransform>();
        burstPoint.SetParent(canvasGo.transform, false);
        burstPoint.anchoredPosition = Vector2.zero;

        // 전환 오버레이: 스프라이트는 비율(45도 보정)용, 그림은 셰이더가 그린다. 전환 중 입력 차단.
        var transition = new GameObject("FlowTransition", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var transitionRt = transition.rectTransform;
        transitionRt.SetParent(canvasGo.transform, false);
        transitionRt.anchorMin = Vector2.zero;
        transitionRt.anchorMax = Vector2.one;
        transitionRt.offsetMin = transitionRt.offsetMax = Vector2.zero;
        transition.sprite = transitionSprite;
        transition.material = material;
        transitionRt.SetAsLastSibling();

        var driver = new GameObject("LevelUpPopTest", typeof(LevelUpPopTest));
        var so = new SerializedObject(driver.GetComponent<LevelUpPopTest>());
        so.FindProperty("_panel").objectReferenceValue = panel.GetComponent<CanvasGroup>();
        so.FindProperty("_cardArea").objectReferenceValue = FindChild(panel.transform, "LevelUp_UIArea");
        so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
        so.FindProperty("_transition").objectReferenceValue = transition;
        so.FindProperty("_burstPoint").objectReferenceValue = burstPoint;
        var samples = PickSamples();
        var dataProp = so.FindProperty("_sampleData");
        dataProp.arraySize = samples.Count;
        for (int i = 0; i < samples.Count; i++) dataProp.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[LevelUpFlowTest] 씬 생성: {ScenePath} (샘플 {samples.Count}개: {string.Join(", ", samples.Select(s => s.name))})");
    }

    /// <summary>플레이 중 Game 뷰(오버레이 UI 포함)를 PNG로 저장한다. 저장 경로는 콘솔에 출력.</summary>
    [MenuItem("Tools/USW/LevelUp Flow Test/Capture Game View")]
    public static void Capture()
    {
        if (!Application.isPlaying) { Debug.LogError("[LevelUpFlowTest] 플레이 모드에서만 캡처합니다."); return; }
        string path = System.IO.Path.GetFullPath($"Temp/LevelUpFlowTest_{System.DateTime.Now:HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[LevelUpFlowTest] 캡처: {path}");
    }

    private static readonly float[] SequenceTimes = { 0.1f, 0.25f, 0.4f, 0.6f, 0.8f, 1.0f, 1.3f, 1.6f, 2.0f, 2.6f };

    /// <summary>플레이 중 LevelUpPopTest를 다시 재생하며 정해진 시점마다 Game 뷰를 Temp/LevelUpFlowSeq_*.png로 저장한다.</summary>
    [MenuItem("Tools/USW/LevelUp Flow Test/Capture Sequence")]
    public static void CaptureSequence()
    {
        var driver = Object.FindAnyObjectByType<LevelUpPopTest>();
        if (!Application.isPlaying || driver == null) { Debug.LogError("[LevelUpFlowTest] 플레이 중인 LevelUpFlowTest 씬에서만 실행합니다."); return; }
        driver.Replay();
        double start = EditorApplication.timeSinceStartup;
        int next = 0;
        void Tick()
        {
            if (!Application.isPlaying || next >= SequenceTimes.Length) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup - start < SequenceTimes[next]) return;
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($"Temp/LevelUpFlowSeq_{next:00}_{SequenceTimes[next]:0.00}s.png"));
            next++;
        }
        EditorApplication.update += Tick;
        Debug.Log("[LevelUpFlowTest] 시퀀스 캡처 시작 → Temp/LevelUpFlowSeq_*.png");
    }

    private static void EnsureMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"[LevelUpFlowTest] 셰이더 없음: {ShaderName}"); return; }
        AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
    }

    private static void EnsureTestPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) != null) AssetDatabase.DeleteAsset(TestPrefabPath);
        if (!AssetDatabase.CopyAsset(SourcePrefabPath, TestPrefabPath))
        {
            Debug.LogError("[LevelUpFlowTest] 프리팹 복제 실패");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(TestPrefabPath);
        try
        {
            var background = FindChild(root.transform, "Background");
            // 메인 화면은 검은 배경 (레퍼런스: design/levelup-reveal-reference-frames.png). 주황 속도선은 전환 오버레이가 담당.
            if (background != null && background.TryGetComponent<Image>(out var image))
            {
                image.sprite = null;
                image.material = null;
                image.color = Color.black;
            }

            var area = FindChild(root.transform, "LevelUp_UIArea");
            if (area != null)
                for (int i = area.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(area.GetChild(i).gameObject);

            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            if (root.GetComponent<UnscaledShaderTime>() == null) root.AddComponent<UnscaledShaderTime>();
            PrefabUtility.SaveAsPrefabAsset(root, TestPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 등급이 겹치지 않게 우선 고르고, 모자라면 나머지로 채운다.
    private static List<LevelUpData> PickSamples()
    {
        var all = AssetDatabase.FindAssets("t:LevelUpData", new[] { SampleFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && !string.IsNullOrEmpty(d.chooseName) && d.icon != null)
            .ToList();
        var picked = all.GroupBy(d => d.tier).Select(g => g.First()).OrderByDescending(d => d.tier).Take(SampleCount).ToList();
        foreach (var d in all)
        {
            if (picked.Count >= SampleCount) break;
            if (!picked.Contains(d)) picked.Add(d);
        }
        return picked;
    }

    private static RectTransform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            if (t.name == name) return t;
        return null;
    }
}
