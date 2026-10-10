using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 족장 스킬 수렴 빔 실험실 씬(FxLab_ChiefConvergeBeam)을 만든다 — 레퍼런스 design/엥이런드론연출이있었다고.gif.
/// 카메라·배경·보스는 디시그망 레일건 실험실과 같은 인게임 기준값. 베탕 2기를 좌우로 띄워 두고 드론 8기가 그 주위를 맴돈다.
/// 연출 값은 씬의 ChiefConvergeBeamFx / ChiefConvergeBeamLab 인스펙터에서 Play 중에 조절한다. 다시 실행하면 씬을 새로 만든다.
/// </summary>
public static class ChiefConvergeBeamLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_ChiefConvergeBeam.unity";
    /// <summary>인게임 DroneManager._convergeBeamPrefab이 참조하는 연출 프리팹.</summary>
    public const string PrefabPath = "Assets/WorkSpace/USW/Prefab/Effect/ChiefConvergeBeam/ChiefConvergeBeamFx.prefab";
    private const string MatRoot = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser";
    private const string SpriteRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";

    // 인게임 IngameScene 기준값 (DisigmanRailgunFxLabBuilder와 동일)
    private static readonly Vector3 CameraPos = new Vector3(0f, 3f, -13f);
    private const float CameraPitch = 7f;
    private const float CameraFov = 60f;
    private static readonly Vector3 BgPos = new Vector3(0f, 0.88f, 0f);
    private const float BgScale = 0.8f;
    private static readonly Vector3 BossPos = new Vector3(0f, 3.28f, 0f);
    private const float BossScale = 0.56f;

    // 베탕 2기 — 좌우로 넓게 (사용자 요청: 붙어 있지 않게)
    private static readonly Vector3[] UnitPos = { new Vector3(-1.5f, -1.4f, 0f), new Vector3(1.5f, -1.4f, 0f) };
    private const float UnitScale = 1.6f;
    // 베탕 1기당 8기 (16기 대형 확인용). 실험실 메뉴로 8기/16기를 바꿔 본다.
    private const int DronesPerUnit = 8;
    private const float DroneOrbit = 0.6f;

    [MenuItem("Tools/USW/Fx/Build Chief Converge Beam Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[ChiefConvergeBeamLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 풀림 — PenaltyFxLabBuilder 참고)
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var boss = AssetDatabase.LoadAssetAtPath<Sprite>(BossPath);
        var betang = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/Betang_1.png");
        var droneIdle = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/Drone_Green_Betang_1.png");
        var droneFire = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/Drone_Green_Betang_2.png");

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bg != null) NewSprite("Battlefield_BG", bg, BgPos, BgScale, -100);
        var target = new GameObject("Target").transform;
        target.position = BossPos;
        if (boss != null) NewSprite("Boss_Dummy", boss, BossPos, BossScale, 0).transform.SetParent(target, true);

        var drones = new SpriteRenderer[UnitPos.Length * DronesPerUnit];
        for (int u = 0; u < UnitPos.Length; u++)
        {
            if (betang != null) NewSprite($"Betang_{u}", betang, UnitPos[u], UnitScale, 5);
            for (int k = 0; k < DronesPerUnit; k++)
            {
                // 두 겹 고리 (안쪽 4 · 바깥 4)
                bool outer = k >= DronesPerUnit / 2;
                int slot = outer ? k - DronesPerUnit / 2 : k;
                float a = (slot / (DronesPerUnit / 2f)) * Mathf.PI * 2f + (outer ? 0.8f : 0.4f);
                float r = DroneOrbit * (outer ? 1.55f : 1f);
                var pos = UnitPos[u] + new Vector3(Mathf.Cos(a) * r, 0.35f + Mathf.Sin(a) * r * 0.55f, 0f);
                var go = NewSprite($"Drone_{u}_{k}", droneIdle, pos, 1f, 8);
                drones[u * DronesPerUnit + k] = go.GetComponent<SpriteRenderer>();
            }
        }

        var fx = NewFx();

        var lab = new GameObject("ChiefConvergeBeamLab").AddComponent<ChiefConvergeBeamLab>();
        var so = new SerializedObject(lab);
        so.FindProperty("_fx").objectReferenceValue = fx;
        so.FindProperty("_target").objectReferenceValue = target;
        so.FindProperty("_idleSprite").objectReferenceValue = droneIdle;
        so.FindProperty("_fireSprite").objectReferenceValue = droneFire;
        var list = so.FindProperty("_drones");
        list.arraySize = drones.Length;
        for (int i = 0; i < drones.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = drones[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = false;
        capSo.FindProperty("_frameRate").intValue = 30;
        capSo.FindProperty("_duration").floatValue = 2.6f;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 1168);
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/ChiefConvergeBeam";
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[ChiefConvergeBeamLab] 씬 생성: {ScenePath} (드론 {drones.Length}기)");
    }

    [MenuItem("Tools/USW/Fx/Chief Beam Lab - 8 Drones (Play Mode)")]
    public static void Drones8() => SetDrones(8);

    [MenuItem("Tools/USW/Fx/Chief Beam Lab - 16 Drones (Play Mode)")]
    public static void Drones16() => SetDrones(16);

    private static void SetDrones(int count)
    {
        var lab = Object.FindAnyObjectByType<ChiefConvergeBeamLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[ChiefConvergeBeamLab] 플레이 중인 실험실에서만"); return; }
        lab.SetDroneCount(count);
        Debug.Log($"[ChiefConvergeBeamLab] 드론 {count}기");
    }

    /// <summary>인게임용 연출 프리팹을 (다시) 만든다 — 머티리얼만 연결, 값은 코드 기본값.</summary>
    [MenuItem("Tools/USW/Fx/Create Chief Converge Beam Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        var fx = NewFx();
        PrefabUtility.SaveAsPrefabAsset(fx.gameObject, PrefabPath);
        Object.DestroyImmediate(fx.gameObject);
        Debug.Log($"[ChiefConvergeBeamLab] 프리팹 저장: {PrefabPath}");
    }

    private static ChiefConvergeBeamFx NewFx()
    {
        var fx = new GameObject("ChiefConvergeBeamFx").AddComponent<ChiefConvergeBeamFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("_beamMaterial").objectReferenceValue = Mat("M_SkyLaser_Beam");
        so.FindProperty("_glowMaterial").objectReferenceValue = Mat("M_SkyLaser_Glow");
        so.FindProperty("_flareMaterial").objectReferenceValue = Mat("M_SkyLaser_Flare");
        so.FindProperty("_ringMaterial").objectReferenceValue = Mat("M_SkyLaser_Ring");
        so.FindProperty("_pointMaterial").objectReferenceValue = Mat("M_SkyLaser_Point");
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    private static Material Mat(string name)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatRoot}/{name}.mat");
        if (mat == null) Debug.LogError($"[ChiefConvergeBeamLab] 머티리얼 없음: {name}");
        return mat;
    }

    private static GameObject NewSprite(string name, Sprite sprite, Vector3 pos, float scale, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
