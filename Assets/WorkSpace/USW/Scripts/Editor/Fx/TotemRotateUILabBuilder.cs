using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 회전 UI·카메라 시안 실험실 씬(FxLab_TotemRotateUI)을 만든다 — 사용자 10-15 "회전 UI 마름모가 애매, 트렌디하게 + 시점/카메라 시안".
/// 인게임 원근 카메라(0,3,-13 · FOV 60)와 같은 배경·칸 위치에 실제 토템 데이터·범위 머티리얼·홀드 게이지 머티리얼을 연결한다.
/// 보드와 UI는 TotemRotateUILab이 플레이 시작 때 직접 만든다. 원본 씬·프리팹은 수정하지 않는다.
/// </summary>
public static class TotemRotateUILabBuilder
{
    /// <summary>실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TotemRotateUI.unity";
    private const string BackgroundPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended_002.jpg";
    private const string CellOverlayPath = "Assets/WorkSpace/USW/Prefab/etc/GridCell_Overlay.png";
    private const string RangeMaterialPath = "Assets/WorkSpace/USW/Materials/TotemEffectRange.mat";
    private const string HoldRingMaterialPath = "Assets/WorkSpace/USW/Materials/TotemHoldRing.mat";
    private const string FontPath = "Assets/Imports/Font/NotoSansKR-Medium SDF.asset";
    private const string TotemRoot = "Assets/WorkSpace/USW/Data/TotemData/Playable/";
    private const string UnitRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/";
    private static readonly string[] Totems = { "TD1006Data", "TD1001Data", "TD1005Data" }; // 회전 가능 토템 전부
    private static readonly string[] Units = { "Betang_1.png", "Gammang.png", "Zetang_1.png", "Deltang.png", "Alphang_1.png", "Drone_Yellow_Gammang_1.png" };

    // IngameScene Main Camera 저작 값.
    private static readonly Vector3 CameraPosition = new Vector3(0f, 3f, -13f);
    private const float CameraFov = 60f;

    [MenuItem("Tools/USW/Fx/Build Totem Rotate UI Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[TotemRotateUILab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(BuildScene());
    }

    /// <summary>저장 확인 없이 씬을 만든다(CLI·MCP 실행용). 현재 씬에 저장 안 된 변경이 있으면 중단한다.</summary>
    public static string BuildScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "[TotemRotateUILab] 현재 씬에 저장 안 된 변경이 있어 중단: " + active.path;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = false;
        camera.fieldOfView = CameraFov;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.55f, 0.78f, 0.95f, 1f);
        camera.transform.position = CameraPosition;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var lab = new GameObject("TotemRotateUILab").AddComponent<TotemRotateUILab>();
        var so = new SerializedObject(lab);
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_font").objectReferenceValue = Require<TMP_FontAsset>(FontPath);
        so.FindProperty("_background").objectReferenceValue = Require<Sprite>(BackgroundPath);
        so.FindProperty("_cellOverlay").objectReferenceValue = Require<Sprite>(CellOverlayPath);
        so.FindProperty("_rangeMaterial").objectReferenceValue = Require<Material>(RangeMaterialPath);
        so.FindProperty("_holdRingMaterial").objectReferenceValue = Require<Material>(HoldRingMaterialPath);
        var totems = so.FindProperty("_totems");
        totems.arraySize = Totems.Length;
        for (int i = 0; i < Totems.Length; i++)
            totems.GetArrayElementAtIndex(i).objectReferenceValue = Require<TotemData>(TotemRoot + Totems[i] + ".asset");
        var units = so.FindProperty("_unitSprites");
        units.arraySize = Units.Length;
        for (int i = 0; i < Units.Length; i++)
            units.GetArrayElementAtIndex(i).objectReferenceValue = Require<Sprite>(UnitRoot + Units[i]);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        return "[TotemRotateUILab] 실험실 생성: " + ScenePath;
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("[TotemRotateUILab] 에셋 없음: " + path);
        return asset;
    }
}
