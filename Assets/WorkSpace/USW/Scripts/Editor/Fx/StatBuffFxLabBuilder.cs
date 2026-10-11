using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 버프 연출 실험실 씬(FxLab_StatBuff)과 시안 13종(StatBuffFxStyle SO)을 만든다 — 분위기 레퍼런스 design/버프형식.gif, design/버프예시2.gif.
/// 시안은 토템 목록(design/quick-specs/totem-description-definitions-2026-10-11.md, TD1011~1014 포함)의 효과 종류별로 하나씩 잡았다.
/// 카메라·배경·보스는 다른 FxLab과 같은 인게임 기준값. 유닛 3기에 StatBuffFx를 하나씩 붙인다(기본은 가운데만 재생).
/// 시안 SO는 없을 때만 만든다 — 실험실에서 다듬은 값을 지키려고. 처음 값으로 되돌릴 때만 Reset Stat Buff Styles.
/// </summary>
public static class StatBuffFxLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_StatBuff.unity";
    /// <summary>게임에서 버프 연출에 쓸 프리팹.</summary>
    public const string PrefabPath = "Assets/WorkSpace/USW/Prefab/Effect/StatBuff/StatBuffFx.prefab";
    private const string StyleRoot = "Assets/WorkSpace/USW/Data/Fx/StatBuff";
    private const string MaterialPath = "Assets/WorkSpace/USW/Materials/Fx/StatBuff/M_StatBuff_Add.mat";
    private const string ShaderName = "USW/Fx/AdditiveWorld";
    private const string SpriteRoot = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";

    // 인게임 IngameScene 기준값 (ChiefConvergeBeamLabBuilder와 동일)
    private static readonly Vector3 CameraPos = new Vector3(0f, 3f, -13f);
    private const float CameraPitch = 7f;
    private const float CameraFov = 60f;
    private static readonly Vector3 BgPos = new Vector3(0f, 0.88f, 0f);
    private const float BgScale = 0.8f;
    private static readonly Vector3 BossPos = new Vector3(0f, 3.28f, 0f);
    private const float BossScale = 0.56f;

    private static readonly string[] UnitSprites = { "Betang_1", "Gammang", "Zetang_1" };
    private static readonly Vector3[] UnitPos = { new Vector3(-1.7f, -1.4f, 0f), new Vector3(0f, -1.4f, 0f), new Vector3(1.7f, -1.4f, 0f) };
    private const float UnitScale = 1.6f;
    private const int UnitOrder = 5;

    // ── 시안 13종 ─────────────────────────────────────────

    private static readonly Color Mint = new Color(0.36f, 0.92f, 0.68f, 1f);
    private static readonly Color MintLight = new Color(0.72f, 1f, 0.86f, 1f);
    private static readonly Color Yellow = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color YellowLight = new Color(1f, 0.96f, 0.62f, 1f);
    private static readonly Color Cyan = new Color(0.35f, 0.85f, 1f, 1f);
    private static readonly Color CyanLight = new Color(0.8f, 0.96f, 1f, 1f);
    private static readonly Color Red = new Color(1f, 0.28f, 0.3f, 1f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.18f, 1f);
    private static readonly Color Orange = new Color(1f, 0.58f, 0.18f, 1f);
    private static readonly Color OrangeLight = new Color(1f, 0.85f, 0.5f, 1f);
    private static readonly Color Purple = new Color(0.72f, 0.48f, 1f, 1f);
    private static readonly Color Shadow = new Color(0.32f, 0.18f, 0.48f, 0.85f);
    private static readonly Color Green = new Color(0.45f, 0.85f, 0.3f, 1f);
    private static readonly Color GreenLight = new Color(0.75f, 1f, 0.55f, 1f);
    private static readonly Color Grey = new Color(0.55f, 0.6f, 0.7f, 1f);
    private static readonly Color Blue = new Color(0.35f, 0.6f, 1f, 1f);

    private static readonly (string File, Action<StatBuffFxStyle> Setup)[] Presets =
    {
        ("A_AttackMint", s =>
        {
            Name(s, "공격력 · 민트 세모", "공격력 상승 기본 (버프예시2). TD1013 같은 줄 · TD1008 안쪽 · TD1002 성장");
            s.Primary = Mint; s.Secondary = Mint; s.BadgeColor = Mint;
            s.Shape = StatBuffShape.Triangle; s.Swoosh = true;
        }),
        ("B_SpeedDouble", s =>
        {
            Name(s, "공격속도 · 겹세모+속도선", "공격속도 상승 기본 — 세모 두 개씩 + 가는 속도선. TD1008 바깥 · TD1011 아래 칸");
            s.Primary = Yellow; s.Secondary = Yellow; s.BadgeColor = Yellow;
            s.Shape = StatBuffShape.Triangle; s.Stacked = true; s.SpeedLines = true; s.Swoosh = true; s.Rise = 0.9f; s.Life = 0.7f;
        }),
        ("C_SpeedBolt", s =>
        {
            Name(s, "공격속도 · 번개", "공격속도 대안 — 빠름을 번개로. 외곽선 있음");
            s.Primary = Cyan; s.Secondary = CyanLight; s.BadgeColor = Cyan;
            s.Shape = StatBuffShape.Bolt; s.Outline = true; s.RandomTilt = true; s.SpeedLines = true;
            s.Interval = 0.15f; s.Size = new Vector2(0.26f, 0.4f); s.Rise = 0.85f;
        }),
        ("D_AttackArrow", s =>
        {
            Name(s, "공격력 · 빨간 화살표", "공격력 대안 — 버프형식.gif 분위기 (빨간 화살 + 노란 소용돌이 + 은은한 기둥)");
            s.Primary = Red; s.Secondary = Gold; s.BadgeColor = Red;
            s.Shape = StatBuffShape.Arrow; s.Outline = true; s.Swoosh = true; s.Pillar = true; s.PillarWidth = 0.8f;
            s.Interval = 0.14f; s.Size = new Vector2(0.24f, 0.38f);
        }),
        ("E_TimedSpeed", s =>
        {
            Name(s, "일시 버프 · 남은 시간 눈금", "TD1014 보스 등장 후 7초 공속 — 발밑 눈금이 시간 따라 하나씩 꺼짐");
            s.Primary = Orange; s.Secondary = OrangeLight; s.BadgeColor = Orange;
            s.Shape = StatBuffShape.Triangle; s.Stacked = true; s.SpeedLines = true; s.TimerPips = true; s.PipCount = 10;
            s.Rise = 0.9f; s.Life = 0.7f;
        }),
        ("F_Crit", s =>
        {
            Name(s, "치명타 · 금빛 별", "TD1012 치명타 확률 — 도는 별 + 반짝이 많이 + 머리 위 큰 별");
            s.Primary = Gold; s.Secondary = YellowLight; s.BadgeColor = Gold;
            s.Shape = StatBuffShape.Star; s.Outline = true; s.SpinRange = new Vector2(-200f, 200f);
            s.Interval = 0.16f; s.Size = new Vector2(0.22f, 0.36f); s.SparkleInterval = 0.09f; s.SparkleSize = 0.2f;
            s.Badge = StatBuffShape.Star; s.BadgeSize = 0.42f; s.BadgeHold = 0.6f;
        }),
        ("G_GradeUp", s =>
        {
            Name(s, "등급 상승 · 계급장", "TD1005 임시 등급 +1 — 머리 위 갈매기 세 겹 + 보랏빛 기둥 + 금빛 소용돌이");
            s.Primary = Purple; s.Secondary = Gold; s.BadgeColor = Gold;
            s.Shape = StatBuffShape.Chevron; s.Outline = true; s.Pattern = StatBuffSpawnPattern.Column;
            s.Interval = 0.18f; s.Size = new Vector2(0.22f, 0.34f); s.Rise = 1f;
            s.Pillar = true; s.Swoosh = true; s.Badge = StatBuffShape.TripleChevron; s.BadgeSize = 0.5f; s.BadgeHold = 1f;
        }),
        ("H_Shadow", s =>
        {
            Name(s, "그림자 · 보라 연기", "TD1007 그림자 부여 — 발밑에서 흔들리며 커지는 연기 + 어두운 오라");
            s.Primary = Shadow; s.Secondary = Purple; s.BadgeColor = Purple; s.SparkleColor = new Color(0.8f, 0.6f, 1f, 1f);
            s.Shape = StatBuffShape.Blob; s.Pattern = StatBuffSpawnPattern.Ring; s.Sway = 0.08f; s.EndScale = 1.4f;
            s.Interval = 0.1f; s.Size = new Vector2(0.18f, 0.32f); s.Rise = 0.6f; s.Life = 1f;
            s.GroundAura = true; s.AuraRadius = 0.65f; s.SparkleInterval = 0.3f;
        }),
        ("I_Growth", s =>
        {
            Name(s, "성장 · 잎사귀", "TD1002 뿌리내림 — 흔들리며 오르는 잎 + 연두 오라. 단계 오를 때 '다시 걸기'로 시작 고리");
            s.Primary = Green; s.Secondary = GreenLight; s.BadgeColor = Green;
            s.Shape = StatBuffShape.Leaf; s.Outline = true; s.RandomTilt = true; s.SpinRange = new Vector2(-60f, 60f);
            s.Sway = 0.1f; s.SwayFrequency = 5f; s.Interval = 0.15f; s.Size = new Vector2(0.2f, 0.32f);
            s.GroundAura = true;
        }),
        ("J_ExtraShot", s =>
        {
            Name(s, "추가 투사체 · 부채꼴 화살", "TD1001 추가 투사체 확률 — 머리 위 부채꼴 화살 배지가 주기적으로 뜸 (발동 순간용)");
            s.Primary = Cyan; s.Secondary = CyanLight; s.BadgeColor = Cyan;
            s.Shape = StatBuffShape.Arrow; s.Outline = true; s.Interval = 0.22f; s.Size = new Vector2(0.18f, 0.28f);
            s.Badge = StatBuffShape.FanArrows; s.BadgeSize = 0.5f; s.BadgeHold = 0.35f; s.BadgeRepeat = 1.1f;
        }),
        ("K_ArmorBreak", s =>
        {
            Name(s, "아머브레이크 · 파편", "TD1006 공격에 아머브레이크 부여 — 발밑 둘레에서 도는 주황 파편");
            s.Primary = Orange; s.Secondary = OrangeLight; s.BadgeColor = Orange;
            s.Shape = StatBuffShape.Shard; s.Outline = true; s.Pattern = StatBuffSpawnPattern.Ring;
            s.SpinRange = new Vector2(-360f, 360f); s.RandomTilt = true; s.Interval = 0.11f; s.Size = new Vector2(0.2f, 0.34f); s.Rise = 0.55f;
        }),
        ("L_AttackDisabled", s =>
        {
            Name(s, "공격 불가 · 회색 X", "TD1011 위 3칸 공격 불가 (디버프) — 가라앉는 회색 세모 + 머리 위 빨간 X");
            s.Primary = Grey; s.Secondary = Grey; s.BadgeColor = Red;
            s.Shape = StatBuffShape.Triangle; s.Downward = true; s.Interval = 0.16f; s.Rise = 0.6f;
            s.SparkleInterval = 0f; s.GroundAura = true; s.AuraRadius = 0.55f; s.BurstRing = false;
            s.Badge = StatBuffShape.Cross; s.BadgeSize = 0.36f; s.BadgeHold = 1.4f; s.BadgeRepeat = 2f;
        }),
        ("M_AttackUpSpeedDown", s =>
        {
            Name(s, "공격력↑ 공속↓ · 위아래 세모", "TD1009 공격력 50% 상승 · 공격 속도 20% 감소 — 빨강은 오르고 파랑은 내려감");
            s.Primary = Red; s.Secondary = Blue; s.BadgeColor = Red;
            s.Shape = StatBuffShape.Triangle; s.SecondaryShare = 0.4f; s.SecondaryDownward = true; s.Interval = 0.1f;
        }),
    };

    private static void Name(StatBuffFxStyle s, string displayName, string usage)
    {
        s.DisplayName = displayName;
        s.Usage = usage;
    }

    [MenuItem("Tools/USW/Fx/Build Stat Buff Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[StatBuffLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EnsureMaterial();
        EnsureStyles(false);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 풀림 — PenaltyFxLabBuilder 참고)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var styles = LoadStyles();
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var boss = AssetDatabase.LoadAssetAtPath<Sprite>(BossPath);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bg != null) NewSprite("Battlefield_BG", bg, BgPos, BgScale, -100);
        if (boss != null) NewSprite("Boss_Dummy", boss, BossPos, BossScale, 0);

        var units = new Transform[UnitPos.Length];
        var fxs = new StatBuffFx[UnitPos.Length];
        for (int i = 0; i < UnitPos.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/{UnitSprites[i]}.png");
            if (sprite == null) Debug.LogError($"[StatBuffLab] 유닛 그림 없음: {UnitSprites[i]}");
            units[i] = NewSprite($"Unit_{i}", sprite, UnitPos[i], UnitScale, UnitOrder).transform;
            fxs[i] = NewFx($"StatBuffFx_{i}", mat);
            fxs[i].transform.position = UnitPos[i];
        }

        var lab = new GameObject("StatBuffFxLab").AddComponent<StatBuffFxLab>();
        var so = new SerializedObject(lab);
        SetArray(so.FindProperty("_fx"), fxs);
        SetArray(so.FindProperty("_units"), units);
        SetArray(so.FindProperty("_styles"), styles);
        so.ApplyModifiedPropertiesWithoutUndo();

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = lab;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = false;
        capSo.FindProperty("_frameRate").intValue = 30;
        capSo.FindProperty("_duration").floatValue = 3.6f;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 1168);
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/StatBuff";
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[StatBuffLab] 씬 생성: {ScenePath} (시안 {styles.Length}종)");
    }

    /// <summary>시안 SO를 모두 처음 값으로 되돌린다 (실험실에서 바꾼 값이 사라진다).</summary>
    [MenuItem("Tools/USW/Fx/Reset Stat Buff Styles")]
    public static void ResetStyles()
    {
        EnsureStyles(true);
        Debug.Log("[StatBuffLab] 시안 SO를 처음 값으로 되돌림");
    }

    /// <summary>플레이 중 실험실에서 시안 전체를 차례로 캡처한다 (Temp/FxCapture/StatBuffStyles).</summary>
    [MenuItem("Tools/USW/Fx/Stat Buff Lab - Capture All Styles")]
    public static void CaptureAllStyles()
    {
        var lab = Object.FindAnyObjectByType<StatBuffFxLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[StatBuffLab] 플레이 중인 실험실에서만"); return; }
        lab.CaptureAllStyles();
    }

    /// <summary>게임용 연출 프리팹을 (다시) 만든다 — 머티리얼만 연결.</summary>
    [MenuItem("Tools/USW/Fx/Create Stat Buff Fx Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        var fx = NewFx("StatBuffFx", EnsureMaterial());
        PrefabUtility.SaveAsPrefabAsset(fx.gameObject, PrefabPath);
        Object.DestroyImmediate(fx.gameObject);
        Debug.Log($"[StatBuffLab] 프리팹 저장: {PrefabPath}");
    }

    private static void EnsureStyles(bool overwrite)
    {
        EnsureFolder(StyleRoot);
        foreach (var (file, setup) in Presets)
        {
            string path = $"{StyleRoot}/StatBuffStyle_{file}.asset";
            var style = AssetDatabase.LoadAssetAtPath<StatBuffFxStyle>(path);
            if (style != null && !overwrite) continue;
            bool created = style == null;
            var fresh = ScriptableObject.CreateInstance<StatBuffFxStyle>();
            setup(fresh);
            if (created) AssetDatabase.CreateAsset(fresh, path);
            else
            {
                fresh.name = style.name; // CopySerialized가 이름도 덮는다
                EditorUtility.CopySerialized(fresh, style);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(style);
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static StatBuffFxStyle[] LoadStyles()
    {
        var list = new StatBuffFxStyle[Presets.Length];
        for (int i = 0; i < Presets.Length; i++)
            list[i] = AssetDatabase.LoadAssetAtPath<StatBuffFxStyle>($"{StyleRoot}/StatBuffStyle_{Presets[i].File}.asset");
        return list;
    }

    private static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static StatBuffFx NewFx(string name, Material mat)
    {
        var fx = new GameObject(name).AddComponent<StatBuffFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("_additiveMaterial").objectReferenceValue = mat;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    // 번짐 공용 머티리얼 — 밝은 배경(얼음·풀밭) 위에서도 색이 보이도록 Opacity를 조금 올린다
    private static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"[StatBuffLab] 셰이더 없음: {ShaderName}"); return null; }
        EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
        mat = new Material(shader) { name = "M_StatBuff_Add" };
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Intensity", 1.6f);
        mat.SetFloat("_Opacity", 0.45f);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
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
