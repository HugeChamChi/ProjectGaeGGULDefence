using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 디시그망 임시 레일건 실험실 씬(FxLab_DisigmanRailgun)을 만든다. 카메라·배경·보스 배치는 하늘 레이저 실험실(인게임 기준값)과 같다.
/// 유닛은 임시 베탕 그림, 연출은 RailgunBeamFx 템플릿 2종(일반/스킬)을 씬 안에 비활성으로 두고 DisigmanRailgunFxLab이 복제해 쓴다.
/// 씬은 매번 덮어쓴다. 머티리얼은 하늘 레이저(SkyLaser) 공용 머티리얼을 재사용한다.
/// </summary>
public static class DisigmanRailgunFxLabBuilder
{
    private const string ScenePath  = "Assets/WorkSpace/USW/Scene/FxLab_DisigmanRailgun.unity";
    private const string MatRoot    = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser";
    private const string SpriteRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs";
    private const string IdleSprite = "Betang_1";
    private const string FireSprite = "Betang_2";
    private const string BgPath     = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath   = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";

    // 인게임 IngameScene 기준값 (SkyLaserFxLabBuilder와 동일)
    private static readonly Vector3 CameraPos = new Vector3(0f, 3f, -13f);
    private const float CameraPitch           = 7f;
    private const float CameraFov             = 60f;
    private static readonly Vector3 BgPos     = new Vector3(0f, 0.88f, 0f);
    private const float BgScale               = 0.8f;
    private static readonly Vector3 BossPos   = new Vector3(0f, 3.28f, 0f);
    private const float BossScale             = 0.56f;

    // 임시 베탕 외형 — 512px / 687ppu ≈ 0.75 → 약 1.19 월드 (인게임 감망·델탕·젤탕 렌더 높이와 맞춤)
    private static readonly Vector3 UnitPos = new Vector3(-0.9f, -1.4f, 0f);
    private const float UnitScale           = 1.6f;
    private const int UnitOrder             = 5;

    private static readonly Color BasicTint = new Color(0.3f, 0.5f, 1f, 1f);
    private static readonly Color SkillTint = new Color(0.4f, 0.85f, 1f, 1f);

    [MenuItem("Tools/USW/Fx/Build Disigman Railgun Lab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬(Single)을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var bgSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossPath);
        var idle       = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/{IdleSprite}.png");
        var fire       = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/{FireSprite}.png");

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bgSprite != null) NewSprite("Battlefield_BG", bgSprite, BgPos, BgScale, -100);
        else Debug.LogWarning($"[DisigmanRailgunLab] 배경 없음: {BgPath}");

        var target = new GameObject("Target").transform;
        target.position = BossPos;
        if (bossSprite != null) NewSprite("Boss_Dummy", bossSprite, BossPos, BossScale, 0).transform.SetParent(target, true);
        else Debug.LogWarning($"[DisigmanRailgunLab] 보스 스프라이트 없음: {BossPath}");

        var unit = NewSprite("Disigman_TempBetang", idle, UnitPos, UnitScale, UnitOrder).GetComponent<SpriteRenderer>();
        if (idle == null) Debug.LogWarning($"[DisigmanRailgunLab] 베탕 그림 없음: {IdleSprite}");

        var lab = new GameObject("DisigmanRailgunFxLab").AddComponent<DisigmanRailgunFxLab>();
        var basic = Template(lab.transform, "Fx_Basic", BasicTint);
        var skill = Template(lab.transform, "Fx_Skill", SkillTint);

        var so = new SerializedObject(lab);
        so.FindProperty("_unit").objectReferenceValue = unit;
        so.FindProperty("_idleSprite").objectReferenceValue = idle;
        so.FindProperty("_fireSprite").objectReferenceValue = fire;
        so.FindProperty("_target").objectReferenceValue = target;
        so.FindProperty("_basicFx").objectReferenceValue = basic;
        so.FindProperty("_skillFx").objectReferenceValue = skill;
        so.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = false;
        capSo.FindProperty("_frameRate").intValue = 30;
        capSo.FindProperty("_duration").floatValue = 4f;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 1168);
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/DisigmanRailgun";
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[DisigmanRailgunLab] 씬 생성: {ScenePath}");
    }

    private static RailgunBeamFx Template(Transform parent, string name, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        var fx = go.AddComponent<RailgunBeamFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("_beamMaterial").objectReferenceValue  = Mat("M_SkyLaser_Beam");
        so.FindProperty("_glowMaterial").objectReferenceValue  = Mat("M_SkyLaser_Glow");
        so.FindProperty("_pointMaterial").objectReferenceValue = Mat("M_SkyLaser_Point");
        so.FindProperty("_ringMaterial").objectReferenceValue  = Mat("M_SkyLaser_Ring");
        so.FindProperty("_flareMaterial").objectReferenceValue = Mat("M_SkyLaser_Flare");
        so.FindProperty("_tint").colorValue = tint;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    private static Material Mat(string name)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatRoot}/{name}.mat");
        if (mat == null) Debug.LogError($"[DisigmanRailgunLab] 머티리얼 없음: {name}");
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
