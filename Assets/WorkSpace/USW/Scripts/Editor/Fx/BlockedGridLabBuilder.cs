using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>기존 인게임 에셋을 참조하는 독립 배치 불가 그리드 FxLab 씬과 네 시안 SO를 생성한다.</summary>
public static class BlockedGridLabBuilder
{
    /// <summary>사용자가 열어 비교할 실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_BlockedGrid.unity";
    private const string StyleRoot = "Assets/WorkSpace/USW/Data/Fx/BlockedGrid";
    private const string BackdropPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string FontPath = "Assets/Imports/Font/NotoSansKR-Medium SDF.asset";
    private const string UnitRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/";
    private static readonly string[] UnitNames = { "Betang_1.png", "Gammang.png", "Zetang_1.png" };
    private static readonly string[] StyleNames = { "A_HatchLock", "B_CornerClamp", "C_ChainSeal", "D_CrossMark" };

    /// <summary>새 실험실을 생성한다. 이미 존재하면 저작 내용을 덮지 않고 해당 씬을 연다.</summary>
    [MenuItem("Tools/USW/Fx/Build Blocked Grid Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene previous = SceneManager.GetActiveScene();
        if (previous.isDirty) throw new InvalidOperationException("현재 씬에 저장하지 않은 변경이 있습니다. 먼저 보존해야 합니다.");
        const string evidence = "outputs/blocked-grid-lab-20261011";
        Directory.CreateDirectory(evidence);
        string previousStart = EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "(none)";
        string before = Path.Combine(evidence, "before.txt");
        if (!File.Exists(before)) File.WriteAllText(before, "scene=" + previous.path + "\nplayStart=" + previousStart + "\ndirty=" + previous.isDirty + "\n");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.playModeStartScene = null;
            return;
        }
        EnsureFolder(StyleRoot);
        for (int i = 0; i < StyleNames.Length; i++) EnsureStyle(i);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (backdrop == null || font == null) throw new InvalidOperationException("기존 배경/한글 폰트 참조를 찾을 수 없습니다.");
        var camera = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .07f, .09f);
        camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
        new GameObject("LabEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var lab = new GameObject("BlockedGridLab").AddComponent<BlockedGridLab>();
        var so = new SerializedObject(lab);
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_font").objectReferenceValue = font;
        so.FindProperty("_background").objectReferenceValue = backdrop;
        var styles = so.FindProperty("_styles"); styles.arraySize = StyleNames.Length;
        for (int i = 0; i < StyleNames.Length; i++)
            styles.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<BlockedGridStyle>(StyleRoot + "/BlockedGridStyle_" + StyleNames[i] + ".asset");
        var units = so.FindProperty("_unitSprites"); units.arraySize = UnitNames.Length;
        for (int i = 0; i < UnitNames.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UnitRoot + UnitNames[i]);
            if (sprite == null) throw new InvalidOperationException("예시 유닛 그림 없음: " + UnitNames[i]);
            units.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.playModeStartScene = null;
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[BlockedGridLab] 네 시안과 배치 비교 실험실 생성: " + ScenePath);
    }

    private static void EnsureStyle(int index)
    {
        string path = StyleRoot + "/BlockedGridStyle_" + StyleNames[index] + ".asset";
        if (AssetDatabase.LoadAssetAtPath<BlockedGridStyle>(path) != null) return;
        var style = ScriptableObject.CreateInstance<BlockedGridStyle>();
        style.Kind = (BlockedGridStyleKind)index;
        switch (index)
        {
            case 0:
                style.DisplayName = "A · 사선 잠금  /  권장";
                style.Description = "회색 채움과 가는 사선 + 작은 자물쇠\n바닥은 보이게, 배치 불가는 분명하게";
                break;
            case 1:
                style.DisplayName = "B · 모서리 잠금";
                style.Description = "네 모서리를 붙잡는 잠금 프레임\n사선을 줄여 유닛 주변의 복잡함을 낮춤";
                style.Fill.a = .46f; style.Edge.a = .42f; style.Symbol = new Color(.83f, .90f, .89f, .88f);
                break;
            case 2:
                style.DisplayName = "C · 체인 봉인";
                style.Description = "교차하는 쇠사슬과 작은 자물쇠\n봉인 상태가 해제될 수 있다는 느낌";
                style.Fill = new Color(.11f, .16f, .20f, .62f);
                style.Pattern = new Color(.72f, .79f, .81f, .76f); style.Edge.a = .47f;
                break;
            case 3:
                style.DisplayName = "D · 큰 X 표시";
                style.Description = "얇고 큰 X + 단순한 테두리\n작은 화면에서도 한눈에 구분하는 안";
                style.Fill.a = .48f; style.Symbol = new Color(.72f, .79f, .81f, .66f);
                break;
        }
        AssetDatabase.CreateAsset(style, path);
        AssetDatabase.SaveAssetIfDirty(style);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
