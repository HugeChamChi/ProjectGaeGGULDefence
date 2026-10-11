using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 드론 배치/크기 실험실 씬(FxLab_DroneLayout)을 만든다 — 사용자 피드백 13 "그리드에서 드론과 유닛이 서로 가림, 대형 유지 불필요".
/// 카메라·배경·보스는 디시그망 레일건 실험실과 같은 인게임 기준값, 칸은 IngameScene GridCell 실측 좌표(간격 1.088).
/// 유닛·드론은 실제 프리팹을 풀어 스크립트만 뺀 외형 템플릿을 쓴다. 다시 실행하면 씬을 새로 만든다.
/// </summary>
public static class DroneLayoutLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_DroneLayout.unity";
    private const string PrefabRoot = "Assets/WorkSpace/USW/Prefab/Unit/DronUnit";
    private const string CellPrefabPath = "Assets/WorkSpace/USW/Prefab/etc/GridCell_Prefab.prefab";
    private const string FormationPath = "Assets/WorkSpace/USW/Data/DroneUnits/BetanFormationSettings.asset";
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";
    private const string BossPath = "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string CaptureFolder = "outputs/drone-layout-20261012";
    private const string BoltRoot = "Assets/WorkSpace/USW/Prefab/Effect/DroneLaser";
    private const string BoltDataPath = "Assets/WorkSpace/USW/Data/Projectile/ProjectileData_DroneLaser_Mint.asset";
    private const string BetanDataPath = "Assets/WorkSpace/USW/Data/DroneUnits/UnitData_Betan.asset";
    private static readonly string[] BoltPrefabs = { "DroneSkyBolt_Mint", "DroneSkyBolt_Yellow", "DroneSkyBolt_Red" };
    private const int StartVariant = 8; // H 궤도 회전 — 사용자 10-12 "H 진짜 괜찮다"
    private const float StatusHeight = 130f;
    private const int CaptureWidth = 1080;
    private const int CaptureHeight = 2340;

    // 인게임 IngameScene 기준값 (DisigmanRailgunFxLabBuilder와 동일)
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
    private const float VariantFontSize = 20f;
    private const int VariantsPerRow = 8;

    private static readonly string[] UnitPrefabs = { "DroneUnit_Betan", "DroneUnit_Gamman", "DroneUnit_Deltan", "DroneUnit_Zeltan", "Disigman" };
    private static readonly string[] DronePrefabs = { "Drone_Normal_Prefab", "Drone_Buffer_Prefab", "Drone_Debuffer_Prefab" };

    [MenuItem("Tools/USW/Fx/Build Drone Layout Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[DroneLayoutLab] 플레이 중에는 실행하지 않는다"); return; }
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
        var formation = AssetDatabase.LoadAssetAtPath<DroneFormationSettings>(FormationPath);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = false;
        cam.fieldOfView = CameraFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.SetPositionAndRotation(CameraPos, Quaternion.Euler(CameraPitch, 0f, 0f));

        if (bg != null) DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bg, BgPos, BgScale, -100);
        if (boss != null) DisigmanRailgunFxLabBuilder.NewSprite("Boss_Dummy", boss, BossPos, BossScale, 0);

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
        for (int i = 0; i < UnitPrefabs.Length; i++)
            unitTemplates[i] = Template(UnitPrefabs[i], templates);
        var droneTemplates = new GameObject[DronePrefabs.Length];
        for (int i = 0; i < DronePrefabs.Length; i++)
            droneTemplates[i] = Template(DronePrefabs[i], templates);

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

        var lab = new GameObject("DroneLayoutLab").AddComponent<DroneLayoutLab>();
        var variants = Variants();

        var rowControls = Row(canvasRt, "Controls", RowBottom);
        var rowB = Row(canvasRt, "VariantsB", RowBottom + RowStep);
        var rowA = Row(canvasRt, "VariantsA", RowBottom + RowStep * 2f);
        var images = new Image[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            var b = TimerBonusFxLabBuilder.NewButton(i < VariantsPerRow ? rowA : rowB, ShortLabel(variants[i].Label), font);
            b.GetComponentInChildren<TMP_Text>().fontSize = VariantFontSize;
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectVariant, i);
            images[i] = b.GetComponent<Image>();
        }
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "판 바꾸기", font).onClick, lab.NextBoard);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "베탕 드론 4/5", font).onClick, lab.ToggleBetanDrones);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "드론 크기", font).onClick, lab.NextDroneScale);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "부유 켬/끔", font).onClick, lab.ToggleBob);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "사격 끔/2/6/전체", font).onClick, lab.NextFireMode);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "궤도 좌우", font).onClick, lab.ToggleOrbitFacing);
        foreach (var label in rowControls.GetComponentsInChildren<TMP_Text>()) label.fontSize = VariantFontSize + 2f;

        var status = TimerBonusFxLabBuilder.NewText("Status", canvasRt, font, 24f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, StatusHeight);
        status.fontSize = 22f;
        status.rectTransform.anchoredPosition = new Vector2(0f, RowBottom + RowStep * 3f);

        var so = new SerializedObject(lab);
        SetArray(so.FindProperty("_unitTemplates"), unitTemplates);
        SetArray(so.FindProperty("_droneTemplates"), droneTemplates);
        so.FindProperty("_cellOrigin").vector2Value = CellOrigin;
        so.FindProperty("_cellStep").floatValue = CellStep;
        var slots = so.FindProperty("_currentSlots");
        var source = formation != null ? formation.SlotOffsets : System.Array.Empty<Vector2>();
        slots.arraySize = source.Length;
        for (int i = 0; i < source.Length; i++) slots.GetArrayElementAtIndex(i).vector2Value = source[i];
        var list = so.FindProperty("_variants");
        list.arraySize = variants.Length;
        for (int i = 0; i < variants.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Label").stringValue = variants[i].Label;
            e.FindPropertyRelative("Description").stringValue = variants[i].Description;
            e.FindPropertyRelative("Kind").enumValueIndex = (int)variants[i].Kind;
            e.FindPropertyRelative("DroneScale").floatValue = variants[i].DroneScale;
            e.FindPropertyRelative("UnitScale").floatValue = variants[i].UnitScale;
            e.FindPropertyRelative("Spread").floatValue = variants[i].Spread;
        }
        so.FindProperty("_variant").intValue = StartVariant;
        CopyBoltSettings(so);
        so.FindProperty("_font").objectReferenceValue = font;
        so.FindProperty("_status").objectReferenceValue = status;
        SetArray(so.FindProperty("_variantButtons"), images);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        string msg = $"[DroneLayoutLab] 씬 생성: {ScenePath} (시안 {variants.Length}개, 슬롯 {source.Length}개)";
        Debug.Log(msg);
        return msg;
    }

    /// <summary>시안 목록. 크기는 드론 프리팹(0.9) 기준 배율.</summary>
    private static DroneLayoutVariant[] Variants() => new[]
    {
        // 사용자 10-12: 크기는 줄여도 되지만 과하게는 말 것 → 드론 최소 0.6배, 유닛 축소는 88%까지.
        V("현재", "게임 그대로 — 5슬롯(BetanFormationSettings), 드론 0.9, 오너 바로 앞", DroneLayoutKind.Current, 1f),
        V("A 축소", "현재 슬롯을 75%로 안쪽으로 당기고 드론 75%", DroneLayoutKind.Compact, 0.75f, spread: 0.75f),
        V("B 머리 위 일렬", "드론을 머리 위 가로 한 줄로 — 옆 칸 유닛과 안 겹침, 위 칸 발밑과만 만남", DroneLayoutKind.HeadRow, 0.7f, spread: 1.25f),
        V("C 후광 아치", "머리 위 반원 — 유닛 실루엣은 다 보이고 드론은 왕관처럼", DroneLayoutKind.HeadArc, 0.7f, spread: 1.15f),
        V("D 발밑 줄", "칸 아래쪽에 한 줄로 — 유닛 앞, 아래 칸 머리와 만나는지 확인", DroneLayoutKind.FootRow, 0.65f, spread: 1.2f),
        V("E 등 뒤", "유닛 뒤에 부채꼴로 숨김 — 날개처럼 어깨 너머로만 보임", DroneLayoutKind.BehindFan, 0.8f),
        V("F 칸 모서리", "자기 칸 네 모서리 + 위 가운데 — 칸 경계를 넘지 않음", DroneLayoutKind.CellCorners, 0.65f),
        V("G 등쪽 세로열", "보는 방향 반대(바깥쪽) 옆에 세로로 쌓기", DroneLayoutKind.BackColumn, 0.65f, spread: 1.1f),
        V("H 궤도 회전", "유닛 둘레를 납작하게 회전, 위쪽 반은 유닛 뒤로", DroneLayoutKind.Orbit, 0.7f),
        V("I 유닛 축소", "유닛 88% + 현재 슬롯 85% + 드론 85% — 둘 다 조금씩 줄임", DroneLayoutKind.ShrinkUnit, 0.85f, unitScale: 0.88f, spread: 0.85f),
        V("J 대표1+숫자", "드론 1기만 그리고 ×4 숫자 — 사격은 대표 드론에서", DroneLayoutKind.SingleWithCount, 0.85f),
        V("K 하늘 격납", "드론이 그리드를 떠나 그리드 위 하늘 띠에 칸별로 모임 — 그리드엔 유닛만", DroneLayoutKind.SkyHangar, 0.6f, spread: 1.2f),
        V("L 어깨 탑승", "유닛 어깨 위에 작게 — 자기 몸 범위 밖으로 안 나감", DroneLayoutKind.ShoulderRide, 0.6f, spread: 1.4f),
        V("M 축소+등 뒤", "유닛 88% + 등 뒤 부채꼴 드론 80% — I와 E 조합", DroneLayoutKind.BehindFan, 0.8f, unitScale: 0.88f),
        V("N 축소+머리 위", "유닛 88% + 머리 위 일렬 드론 70% — I와 B 조합", DroneLayoutKind.HeadRow, 0.7f, unitScale: 0.88f, spread: 1.25f),
    };

    /// <summary>게임 드론 레이저 프리팹·투사체 데이터·베탕 공속을 실험실 사격 값으로 복사한다.</summary>
    private static void CopyBoltSettings(SerializedObject lab)
    {
        var tints = lab.FindProperty("_boltTints");
        tints.arraySize = BoltPrefabs.Length;
        for (int i = 0; i < BoltPrefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<DroneLaserProjectile>($"{BoltRoot}/{BoltPrefabs[i]}.prefab");
            if (prefab == null) { Debug.LogWarning($"[DroneLayoutLab] 레이저 프리팹 없음: {BoltPrefabs[i]}"); continue; }
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
        var betan = AssetDatabase.LoadAssetAtPath<UnitData>(BetanDataPath);
        if (betan != null) lab.FindProperty("_attacksPerSecond").floatValue = betan.attackSpeed.Get(Tier.Legend);
    }

    private static DroneLayoutVariant V(string label, string desc, DroneLayoutKind kind, float droneScale, float unitScale = 1f, float spread = 1f)
        => new DroneLayoutVariant { Label = label, Description = desc, Kind = kind, DroneScale = droneScale, UnitScale = unitScale, Spread = spread };

    private static string ShortLabel(string label)
    {
        int space = label.IndexOf(' ');
        return space > 0 && label.Length > 6 ? label.Substring(0, space) + "\n" + label.Substring(space + 1) : label;
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    /// <summary>Play 중 모든 시안 × 판 구성(3종)을 PNG로 저장한다. 실제 저장은 실험실 코루틴이 몇 프레임에 걸쳐 한다.</summary>
    [MenuItem("Tools/USW/Fx/Drone Layout Lab - Capture All (Play)")]
    public static string CaptureAll()
    {
        var lab = Object.FindAnyObjectByType<DroneLayoutLab>();
        if (!EditorApplication.isPlaying || lab == null) { Debug.LogError("[DroneLayoutLab] 플레이 중인 실험실에서만"); return "not playing"; }
        string folder = Path.GetFullPath(CaptureFolder);
        lab.CaptureAll(folder, 3, CaptureWidth, CaptureHeight);
        return "started: " + folder;
    }

    // ── 외형 복사 ────────────────────────────────────────────────────

    /// <summary>프리팹을 풀어 Spine 외 모든 스크립트·Animator를 뺀 비활성 템플릿을 만든다.</summary>
    private static GameObject Template(string prefabName, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabName}.prefab");
        if (prefab == null) { Debug.LogError($"[DroneLayoutLab] 프리팹 없음: {prefabName}"); return null; }
        var go = VisualCopy(prefab, parent);
        go.name = prefabName;
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
