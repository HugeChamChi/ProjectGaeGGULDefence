using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 출격 실험실 씬(FxLab_Sortie)을 만든다 — 사용자 10-14 "고정 그리드 위에서 너무 정적, 동적으로 보일 필요".
/// 카메라·배경·보스·칸 좌표는 드론 배치 실험실과 같은 인게임 기준값. 유닛·드론·토템은 실제 프리팹을 풀어 스크립트만 뺀 외형 템플릿.
/// 다시 실행하면 씬을 새로 만든다.
/// </summary>
public static class SortieLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_Sortie.unity";
    private const string UnitPrefabRoot = "Assets/WorkSpace/USW/Prefab/Unit/DronUnit";
    private const string TotemPrefabRoot = "Assets/WorkSpace/USW/Data/TotemData/Playable";
    private const string CellPrefabPath = "Assets/WorkSpace/USW/Prefab/etc/GridCell_Prefab.prefab";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string CaptureFolder = "outputs/sortie-lab-20261014/captures";
    private const string BoltRoot = "Assets/WorkSpace/USW/Prefab/Effect/DroneLaser";
    private const string BoltDataPath = "Assets/WorkSpace/USW/Data/Projectile/ProjectileData_DroneLaser_Mint.asset";
    private static readonly string[] BoltPrefabs = { "DroneSkyBolt_Mint", "DroneSkyBolt_Yellow", "DroneSkyBolt_Red" };
    private const int StartVariant = 5; // 혼합
    private const float StatusHeight = 110f;
    private const int CaptureWidth = 1080;
    private const int CaptureHeight = 2340;

    // 인게임 IngameScene 기준값 (DroneLayoutLabBuilder와 동일)
    private static readonly Vector3 CameraPos = new Vector3(0f, 3f, -13f);
    private const float CameraPitch = 7f;
    private const float CameraFov = 60f;
    private static readonly Vector3 BgPos = new Vector3(0f, 0.88f, 0f);
    private const float BgScale = 0.8f;
    private static readonly Vector3 BossPos = new Vector3(0f, 3.28f, 0f);
    private const float BossScale = 0.56f;
    private static readonly Vector2 CellOrigin = new Vector2(-2.72f, -3.03f);
    private const float CellStep = 1.088f;

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const float CanvasPlaneDistance = 1f;
    private const float RowHeight = 64f;
    private const float RowStep = 70f;
    private const float RowBottom = 16f;
    private const float ButtonFontSize = 21f;

    private static readonly string[] UnitPrefabs = { "DroneUnit_Betan", "DroneUnit_Gamman", "DroneUnit_Deltan", "DroneUnit_Zeltan", "Disigman" };
    private static readonly string[] DronePrefabs = { "Drone_Normal_Prefab", "Drone_Buffer_Prefab", "Drone_Debuffer_Prefab" };
    private static readonly string[] TotemPrefabs = { "TD1001", "TD1008" };

    [MenuItem("Tools/USW/Fx/Build Sortie Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[SortieLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>저장 확인 없이 씬을 만든다(CLI 실행용). 현재 씬에 저장 안 된 변경이 있으면 중단한다.</summary>
    public static string BuildScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "현재 씬에 저장 안 된 변경이 있어 중단: " + active.path;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 풀림 — PenaltyFxLabBuilder 참고)
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var boss = AssetDatabase.LoadAssetAtPath<Sprite>(BossPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var lineMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bg != null) DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bg, BgPos, BgScale, -100);
        var bossGo = boss != null ? DisigmanRailgunFxLabBuilder.NewSprite("Boss_Dummy", boss, BossPos, BossScale, 0) : null;

        // ── 칸: 실제 GridCell 프리팹 외형 ──
        var gridRoot = new GameObject("Grid").transform;
        var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 6; c++)
            {
                var cell = VisualCopy(cellPrefab, gridRoot);
                if (cell == null) continue;
                cell.name = $"GridCell_{c}_{r}";
                cell.transform.position = new Vector3(CellOrigin.x + c * CellStep, CellOrigin.y + r * CellStep, -0.017f);
            }

        // ── 외형 템플릿 (비활성) ──
        var templates = new GameObject("Templates").transform;
        var unitTemplates = new GameObject[UnitPrefabs.Length];
        for (int i = 0; i < UnitPrefabs.Length; i++) unitTemplates[i] = Template($"{UnitPrefabRoot}/{UnitPrefabs[i]}.prefab", templates);
        var droneTemplates = new GameObject[DronePrefabs.Length];
        for (int i = 0; i < DronePrefabs.Length; i++) droneTemplates[i] = Template($"{UnitPrefabRoot}/{DronePrefabs[i]}.prefab", templates);
        var totemTemplates = new GameObject[TotemPrefabs.Length];
        for (int i = 0; i < TotemPrefabs.Length; i++) totemTemplates[i] = Template($"{TotemPrefabRoot}/{TotemPrefabs[i]}.prefab", templates);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ── UI: 카메라 캔버스 (캡처가 카메라 렌더로 찍으므로 오버레이 대신) ──
        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = CanvasPlaneDistance;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        var lab = new GameObject("SortieLab").AddComponent<SortieLab>();
        var variants = Variants();

        var rowControls = Row(canvasRt, "Controls", RowBottom);
        var rowVariants = Row(canvasRt, "Variants", RowBottom + RowStep);
        var images = new Image[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            var b = TimerBonusFxLabBuilder.NewButton(rowVariants, variants[i].Label, font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectVariant, i);
            images[i] = b.GetComponent<Image>();
        }
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "토템", font).onClick, lab.ToggleTotems);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "버프색", font).onClick, lab.NextBuffMode);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "발판", font).onClick, lab.ToggleHomeMarks);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "보스 반응", font).onClick, lab.ToggleBossReact);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "카메라", font).onClick, lab.ToggleCameraPunch);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "속도", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "도약!", font).onClick, lab.TriggerLeap);
        foreach (var label in canvasRt.GetComponentsInChildren<TMP_Text>()) label.fontSize = ButtonFontSize;

        var status = TimerBonusFxLabBuilder.NewText("Status", canvasRt, font, 22f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = true;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, StatusHeight);
        status.rectTransform.anchoredPosition = new Vector2(0f, RowBottom + RowStep * 2f);

        var so = new SerializedObject(lab);
        SetArray(so.FindProperty("_unitTemplates"), unitTemplates);
        SetArray(so.FindProperty("_droneTemplates"), droneTemplates);
        SetArray(so.FindProperty("_totemTemplates"), totemTemplates);
        so.FindProperty("_cellOrigin").vector2Value = CellOrigin;
        so.FindProperty("_cellStep").floatValue = CellStep;
        so.FindProperty("_boss").objectReferenceValue = bossGo != null ? bossGo.transform : null;
        so.FindProperty("_lineMaterial").objectReferenceValue = lineMaterial;
        var list = so.FindProperty("_variants");
        list.arraySize = variants.Length;
        for (int i = 0; i < variants.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Label").stringValue = variants[i].Label;
            e.FindPropertyRelative("Description").stringValue = variants[i].Description;
            e.FindPropertyRelative("Kind").enumValueIndex = (int)variants[i].Kind;
        }
        so.FindProperty("_variant").intValue = StartVariant;
        CopyBoltSettings(so);
        so.FindProperty("_status").objectReferenceValue = status;
        SetArray(so.FindProperty("_variantButtons"), images);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        string msg = $"[SortieLab] 씬 생성: {ScenePath} (시안 {variants.Length}개)";
        Debug.Log(msg);
        return msg;
    }

    /// <summary>시안 목록. 모든 시안에서 판정·점유는 칸 그대로이고 보이는 몸만 움직인다.</summary>
    private static SortieLabVariant[] Variants() => new[]
    {
        V("현재", "게임 그대로 — 칸에 선 채 드론/유닛이 쏜다(비교 기준)", SortieLabMotionKind.Static),
        V("앞발", "공격마다 보스 쪽으로 1/4칸 튀어나가 쏘고 돌아온다", SortieLabMotionKind.Lunge),
        V("드론 출격", "유닛은 진지에 남고 드론만 보스 앞까지 날아가 쏘고 귀환", SortieLabMotionKind.DroneSortie),
        V("도약", "평소 제자리, 2.4초마다 한 기가 보스 앞으로 도약해 내려찍고 복귀", SortieLabMotionKind.SkillLeap),
        V("맥박", "칸을 안 떠남 — 판 전체가 아래에서 위(보스 쪽)로 출렁임", SortieLabMotionKind.Pulse),
        V("혼합", "앞발 + 베탕 3타마다 드론 출격 + 가끔 도약 (추천 조합 후보)", SortieLabMotionKind.Mixed),
    };

    /// <summary>게임 드론 레이저 프리팹·투사체 데이터를 실험실 사격 값으로 복사한다.</summary>
    private static void CopyBoltSettings(SerializedObject lab)
    {
        var tints = lab.FindProperty("_boltTints");
        tints.arraySize = BoltPrefabs.Length;
        for (int i = 0; i < BoltPrefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<DroneLaserProjectile>($"{BoltRoot}/{BoltPrefabs[i]}.prefab");
            if (prefab == null) { Debug.LogWarning($"[SortieLab] 레이저 프리팹 없음: {BoltPrefabs[i]}"); continue; }
            var src = new SerializedObject(prefab);
            tints.GetArrayElementAtIndex(i).colorValue = src.FindProperty("_tint").colorValue;
            if (i != 0) continue;
            lab.FindProperty("_boltBeamMaterial").objectReferenceValue = src.FindProperty("_beamMaterial").objectReferenceValue;
            lab.FindProperty("_boltGlowMaterial").objectReferenceValue = src.FindProperty("_glowMaterial").objectReferenceValue;
            lab.FindProperty("_boltWidth").floatValue = src.FindProperty("_width").floatValue;
            lab.FindProperty("_boltSegment").floatValue = src.FindProperty("_segmentLength").floatValue;
        }
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(BoltDataPath);
        if (data != null && data.movement is StraightMovement straight) lab.FindProperty("_boltDuration").floatValue = straight.duration;
    }

    private static SortieLabVariant V(string label, string desc, SortieLabMotionKind kind)
        => new SortieLabVariant { Label = label, Description = desc, Kind = kind };

    // ── 캡처 ─────────────────────────────────────────────────────────

    /// <summary>Play 중 시안마다 2초 돌린 뒤 0.15초 간격 8장씩 PNG로 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Sortie Lab - Capture All (Play)")]
    public static string CaptureAll()
    {
        var lab = Object.FindAnyObjectByType<SortieLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[SortieLab] 플레이 중인 실험실에서만"); return "not playing"; }
        string folder = Path.GetFullPath(CaptureFolder);
        lab.CaptureAll(folder, 2f, 8, 0.15f, CaptureWidth, CaptureHeight);
        return "started: " + folder;
    }

    // ── 외형 복사 ────────────────────────────────────────────────────

    /// <summary>프리팹을 풀어 Spine 외 모든 스크립트·Animator를 뺀 비활성 템플릿을 만든다.</summary>
    private static GameObject Template(string path, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogError($"[SortieLab] 프리팹 없음: {path}"); return null; }
        var go = VisualCopy(prefab, parent);
        go.name = prefab.name;
        go.SetActive(false);
        return go;
    }

    private static GameObject VisualCopy(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(parent, false);
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        for (int pass = 0; pass < 4; pass++)
        {
            bool removed = false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || IsSpine(mb) || IsRequired(mb)) continue;
                Object.DestroyImmediate(mb);
                removed = true;
            }
            if (!removed) break;
        }
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(c);
        return go;
    }

    private static bool IsSpine(Component c) => (c.GetType().Namespace ?? string.Empty).StartsWith("Spine");

    private static bool IsRequired(Component target)
    {
        foreach (var other in target.GetComponents<MonoBehaviour>())
        {
            if (other == null || other == target) continue;
            foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if (Requires(req.m_Type0, target) || Requires(req.m_Type1, target) || Requires(req.m_Type2, target)) return true;
        }
        return false;
    }

    private static bool Requires(System.Type type, Component target) => type != null && type.IsInstanceOfType(target);

    private static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static RectTransform Row(RectTransform canvasRt, string name, float y)
    {
        var row = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, name, 0f);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.sizeDelta = new Vector2(-24f, RowHeight);
        row.anchoredPosition = new Vector2(0f, y);
        return row;
    }
}
