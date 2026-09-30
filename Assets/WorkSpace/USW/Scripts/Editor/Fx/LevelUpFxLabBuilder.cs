using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 레벨업 이펙트 실험실 씬(FxLab_LevelUp)을 만든다.
///   1. IngameScene의 레벨업 패널(LevelUpUI 오브젝트)과 소환 버튼을 복사한다 — IngameScene은 읽기만 하고 저장하지 않는다.
///   2. 복사본에서 게임 의존 스크립트(DI 주입 등)를 떼어 내고, 그림 요소와 LevelUpRevealSequence만 남긴다.
///      → 실험실에서 슬롯에 꽂은 이펙트가 게임과 같은 코드·타이밍으로 재생된다.
///   2-1. LevelUpFxPrefabBuilder가 만든 새 이펙트 프리팹이 있으면 복사본 슬롯에만 꽂는다 (게임 연결 전 미리보기).
///        ① 경험치 MAX = 슬롯 A, ② 등급 연출 = 패널 배경 위(카드 뒤)에 인스턴스 + _tierReveal.
///   3. 드라이버(LevelUpFxLab) + 등급 버튼(레어/에픽/레전더리) + 실시간 캡처(FxLabCapture)를 붙인다.
/// 다시 실행하면 씬을 새로 만든다 (씬에 직접 한 수정은 사라짐 — 연출 값은 IngameScene/프리팹 쪽을 고칠 것).
/// </summary>
public static class LevelUpFxLabBuilder
{
    private const string IngameScenePath = "Assets/Scenes/IngameScene.unity";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_LevelUp.unity";
    private const string SampleFolder = "Assets/WorkSpace/USW/Data/SelectionData";
    private const string SummonButtonName = "SummonButton";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Color FieldColor = new Color(0.08f, 0.13f, 0.21f, 1f);
    private static readonly string[] TierButtonLabels = { "RARE", "EPIC", "LEGEND" };
    private const float CaptureDuration = 4.2f; // 레전더리 공개(~2.3초) + 자동 선택(④)까지

    [MenuItem("Tools/USW/Fx/Build LevelUp Fx Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[LevelUpFxLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 이전 실험실 씬이 열려 있으면 같은 경로로 저장할 수 없으므로 IngameScene 단독으로 연다 (실험실 씬은 매번 새로 만든다).
        if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            EditorSceneManager.OpenScene(IngameScenePath, OpenSceneMode.Single);
        var ingame = SceneManager.GetSceneByPath(IngameScenePath);
        if (!ingame.isLoaded) ingame = EditorSceneManager.OpenScene(IngameScenePath, OpenSceneMode.Additive);

        var sourceUi = FindInScene<LevelUpUI>(ingame);
        var sourceSummon = FindByName(ingame, SummonButtonName);
        if (sourceUi == null || sourceSummon == null)
        {
            Debug.LogError($"[LevelUpFxLab] IngameScene에서 LevelUpUI({sourceUi != null}) / {SummonButtonName}({sourceSummon != null})를 찾지 못함");
            return;
        }

        var uiSo = new SerializedObject(sourceUi);
        var sourcePanel = uiSo.FindProperty("obj").objectReferenceValue as GameObject;
        var sourceContainer = uiSo.FindProperty("cardContainer").objectReferenceValue as Transform;
        var cardPrefab = uiSo.FindProperty("cardPrefab").objectReferenceValue as LevelUpCardUI;
        if (sourcePanel == null || sourceContainer == null || !sourceContainer.IsChildOf(sourcePanel.transform))
        {
            Debug.LogError("[LevelUpFxLab] LevelUpUI의 obj(패널)/cardContainer 연결을 확인할 것 (cardContainer는 패널 안에 있어야 함)");
            return;
        }
        // LevelUpUI 컴포넌트 오브젝트와 패널(obj)은 IngameScene에서 서로 다른 곳에 있을 수 있다 → 각각 복사한다.
        bool panelInsideUi = sourcePanel.transform.IsChildOf(sourceUi.transform);
        string panelPath = panelInsideUi ? RelativePath(sourceUi.transform, sourcePanel.transform) : string.Empty;
        string containerPath = RelativePath(sourcePanel.transform, sourceContainer);
        // 선택 타이머는 게임에서 없앰 (사용자 결정 2026-09-29, IngameScene _disableSelectionTimer = 1) → 복사본에서도 숨긴다
        var timerText = uiSo.FindProperty("selectionTimerText").objectReferenceValue as Component;
        string timerPath = timerText != null && timerText.transform.IsChildOf(sourcePanel.transform)
            ? RelativePath(sourcePanel.transform, timerText.transform) : null;
        var summonPlacement = MeasureBottomAnchored((RectTransform)sourceSummon.transform);

        var lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(lab);

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        // 필드 자리 (전투 화면 대신 어두운 바탕 — 이펙트가 필드 위에서 어떻게 보이는지 판단용)
        var field = new GameObject("FieldStandIn", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        field.rectTransform.SetParent(canvasRt, false);
        Stretch(field.rectTransform);
        field.color = FieldColor;
        field.raycastTarget = false;

        // 소환 버튼 복사 (하단 기준 위치 유지)
        var summon = Object.Instantiate(sourceSummon);
        summon.name = SummonButtonName;
        var summonRt = (RectTransform)summon.transform;
        summonRt.SetParent(canvasRt, false);
        summonRt.anchorMin = summonRt.anchorMax = new Vector2(0.5f, 0f);
        summonRt.pivot = new Vector2(0.5f, 0.5f);
        summonRt.sizeDelta = summonPlacement.Size;
        summonRt.localScale = Vector3.one * summonPlacement.Scale;
        summonRt.anchoredPosition = summonPlacement.Center;
        summonRt.localRotation = Quaternion.identity;
        Strip(summon);
        var gaugeFill = summon.GetComponentsInChildren<Graphic>(true)
            .FirstOrDefault(g => g.material != null && g.material.HasProperty("_Fill"));

        // 레벨업 패널 복사 (LevelUpRevealSequence 설정·슬롯 포함)
        var ui = Object.Instantiate(sourceUi.gameObject);
        ui.name = "LevelUpUI";
        var uiRt = (RectTransform)ui.transform;
        uiRt.SetParent(canvasRt, false);
        Stretch(uiRt);
        uiRt.localScale = Vector3.one;
        GameObject panel;
        if (panelInsideUi)
            panel = string.IsNullOrEmpty(panelPath) ? ui : ui.transform.Find(panelPath).gameObject;
        else
        {
            panel = Object.Instantiate(sourcePanel);
            panel.name = sourcePanel.name;
            var panelRt = (RectTransform)panel.transform;
            panelRt.SetParent(canvasRt, false);
            Stretch(panelRt);
            panelRt.localScale = Vector3.one;
            Strip(panel);
        }
        var container = string.IsNullOrEmpty(containerPath) ? panel.transform : panel.transform.Find(containerPath);
        if (!string.IsNullOrEmpty(timerPath)) panel.transform.Find(timerPath)?.gameObject.SetActive(false);
        var reveal = ui.GetComponent<LevelUpRevealSequence>();
        if (reveal == null) reveal = ui.AddComponent<LevelUpRevealSequence>();
        var revealSo = new SerializedObject(reveal);
        revealSo.FindProperty("_origin").objectReferenceValue = summonRt;
        // 새 이펙트 프리팹이 있으면 실험실 복사본의 슬롯에만 꽂는다 (IngameScene 연결은 별도 단계)
        var gaugeBurst = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpFxPrefabBuilder.GaugeBurstGlowPath); // 사용자 선택: B 빛 번짐
        if (gaugeBurst != null) revealSo.FindProperty("_gaugeBurstPrefab").objectReferenceValue = gaugeBurst;
        var cardReveal = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpFxPrefabBuilder.CardRevealPath);
        if (cardReveal != null) revealSo.FindProperty("_cardRevealPrefab").objectReferenceValue = cardReveal;
        var tierReveal = AddTierReveal(panel, container as RectTransform);
        if (tierReveal != null) revealSo.FindProperty("_tierReveal").objectReferenceValue = tierReveal;
        var effectRoot = revealSo.FindProperty("_effectRoot");
        if (effectRoot.objectReferenceValue is Component c && !c.transform.IsChildOf(ui.transform))
            effectRoot.objectReferenceValue = null;
        revealSo.ApplyModifiedPropertiesWithoutUndo();
        var select = ui.GetComponent<LevelUpSelectSequence>();
        if (select == null) select = ui.AddComponent<LevelUpSelectSequence>();
        var cardSelect = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpFxPrefabBuilder.CardSelectPath);
        if (cardSelect != null)
        {
            var selectSo = new SerializedObject(select);
            selectSo.FindProperty("_cardSelectPrefab").objectReferenceValue = cardSelect;
            selectSo.ApplyModifiedPropertiesWithoutUndo();
        }
        Strip(ui, typeof(LevelUpRevealSequence), typeof(LevelUpSelectSequence));
        ui.SetActive(true);

        // 드라이버
        var driverGo = new GameObject("LevelUpFxLab", typeof(LevelUpFxLab), typeof(UnscaledShaderTime));
        var driver = driverGo.GetComponent<LevelUpFxLab>();
        var dSo = new SerializedObject(driver);
        dSo.FindProperty("_reveal").objectReferenceValue = reveal;
        dSo.FindProperty("_select").objectReferenceValue = select;
        dSo.FindProperty("_tierReveal").objectReferenceValue = tierReveal;
        dSo.FindProperty("_panelObject").objectReferenceValue = panel;
        dSo.FindProperty("_cardContainer").objectReferenceValue = container;
        dSo.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
        dSo.FindProperty("_gaugeFill").objectReferenceValue = gaugeFill;
        var variants = dSo.FindProperty("_gaugeBurstVariants");
        var variantAssets = new[] { LevelUpFxPrefabBuilder.GaugeBurstPath, LevelUpFxPrefabBuilder.GaugeBurstGlowPath }
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
        variants.arraySize = variantAssets.Length;
        for (int i = 0; i < variantAssets.Length; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = variantAssets[i];
        var cardVariants = dSo.FindProperty("_cardRevealVariants");
        var cardVariantAssets = new[] { LevelUpFxPrefabBuilder.CardRevealPath, LevelUpFxPrefabBuilder.CardRevealShapePath }
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
        cardVariants.arraySize = cardVariantAssets.Length;
        for (int i = 0; i < cardVariantAssets.Length; i++) cardVariants.GetArrayElementAtIndex(i).objectReferenceValue = cardVariantAssets[i];
        var samples = AssetDatabase.FindAssets("t:LevelUpData", new[] { SampleFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.icon != null)
            .OrderBy(d => d.chooseId)
            .ToArray();
        var samplesProp = dSo.FindProperty("_samples");
        samplesProp.arraySize = samples.Length;
        for (int i = 0; i < samples.Length; i++) samplesProp.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];
        dSo.ApplyModifiedPropertiesWithoutUndo();

        BuildButtons(canvasRt, driver);

        var capture = cam.gameObject.AddComponent<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_target").objectReferenceValue = driver;
        capSo.FindProperty("_camera").objectReferenceValue = cam;
        capSo.FindProperty("_captureOnStart").boolValue = false;
        capSo.FindProperty("_realtime").boolValue = true;
        capSo.FindProperty("_duration").floatValue = CaptureDuration;
        capSo.FindProperty("_resolution").vector2IntValue = new Vector2Int(540, 1170);
        capSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(lab, ScenePath);
        EditorSceneManager.CloseScene(ingame, true);
        var tierCounts = string.Join(", ", samples.GroupBy(s => s.tier).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
        Debug.Log($"[LevelUpFxLab] 완료: {ScenePath} (샘플 {tierCounts}, 게이지 {(gaugeFill != null ? gaugeFill.name : "없음")}, " +
                  $"소환 버튼 하단 {summonPlacement.Center} 크기 {summonPlacement.Size}×{summonPlacement.Scale:0.##})");
    }

    /// <summary>플레이 중 실험실 캡처를 시작한다 (결과: Temp/FxCapture).</summary>
    [MenuItem("Tools/USW/Fx/Capture Lab (Play Mode)")]
    public static void CaptureLab()
    {
        var capture = Object.FindAnyObjectByType<FxLabCapture>();
        if (!Application.isPlaying || capture == null) { Debug.LogError("[FxLab] 플레이 중인 실험실 씬에서만 실행한다"); return; }
        capture.Capture();
    }

    [MenuItem("Tools/USW/Fx/Lab Gauge Burst A (Cartoon)")]
    public static void UseBurstA() => SetBurstVariant(0);

    [MenuItem("Tools/USW/Fx/Lab Gauge Burst B (Glow)")]
    public static void UseBurstB() => SetBurstVariant(1);

    [MenuItem("Tools/USW/Fx/Lab Card Reveal A (Line)")]
    public static void CardRevealA() => WithLab(l => l.SetCardRevealVariant(0));

    [MenuItem("Tools/USW/Fx/Lab Card Reveal B (Shape)")]
    public static void CardRevealB() => WithLab(l => l.SetCardRevealVariant(1));

    // 화면의 LEGEND 버튼과 같다 — 재생 도중 다시 누르는 경우(취소 → 재시작)를 재현할 때 쓴다.
    [MenuItem("Tools/USW/Fx/Lab Play Legend (Button)")]
    public static void PlayLegendButton() => WithLab(l => l.PlayTierIndex(2));

    [MenuItem("Tools/USW/Fx/Lab Capture Tier Rare")]
    public static void CaptureTierRare() => WithLab(l => l.SetCaptureTier(0));

    [MenuItem("Tools/USW/Fx/Lab Capture Tier Epic")]
    public static void CaptureTierEpic() => WithLab(l => l.SetCaptureTier(1));

    [MenuItem("Tools/USW/Fx/Lab Capture Tier Legend")]
    public static void CaptureTierLegend() => WithLab(l => l.SetCaptureTier(2));

    [MenuItem("Tools/USW/Fx/Lab Legend Upgrade - Random (10%)")]
    public static void LegendUpgradeRandom() => SetForceUpgrade(null);

    [MenuItem("Tools/USW/Fx/Lab Legend Upgrade - Always")]
    public static void LegendUpgradeAlways() => SetForceUpgrade(true);

    [MenuItem("Tools/USW/Fx/Lab Legend Upgrade - Never")]
    public static void LegendUpgradeNever() => SetForceUpgrade(false);

    // 레전더리 보라 → 빨강 반전(확률 연출)을 실험실에서 강제로 확인한다
    private static void SetForceUpgrade(bool? force)
    {
        WithLab(l => l.SetLegendUpgrade(force));
        Debug.Log($"[FxLab] 레전더리 반전: {(force == null ? "확률" : force.Value ? "항상" : "없음")}");
    }

    private static void WithLab(Action<LevelUpFxLab> action)
    {
        var lab = Object.FindAnyObjectByType<LevelUpFxLab>();
        if (!Application.isPlaying || lab == null) { Debug.LogError("[FxLab] 플레이 중인 FxLab_LevelUp 씬에서만 실행한다"); return; }
        action(lab);
    }

    private static void SetBurstVariant(int index)
    {
        var lab = Object.FindAnyObjectByType<LevelUpFxLab>();
        if (!Application.isPlaying || lab == null) { Debug.LogError("[FxLab] 플레이 중인 FxLab_LevelUp 씬에서만 실행한다"); return; }
        lab.SetGaugeBurstVariant(index);
    }

    // ② 등급 연출 프리팹을 패널 배경 바로 위(카드 뒤)에 넣고, Anchor를 카드 컨테이너 중심에 맞춘다.
    private static TierRevealFx AddTierReveal(GameObject panel, RectTransform container)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpFxPrefabBuilder.TierRevealPath);
        if (prefab == null || container == null) return null;
        var panelRt = (RectTransform)panel.transform;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.scene);
        var rt = (RectTransform)go.transform;
        rt.SetParent(panelRt, false);
        Stretch(rt);
        var background = panel.transform.Find("Background");
        rt.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

        Canvas.ForceUpdateCanvases();
        Vector2 local = panelRt.InverseTransformPoint(container.TransformPoint(container.rect.center));
        var anchor = (RectTransform)go.transform.Find("Anchor");
        if (anchor != null) anchor.anchoredPosition = local - panelRt.rect.center;
        var fx = go.GetComponent<TierRevealFx>();
        if (background != null && background.TryGetComponent<Graphic>(out var bgGraphic))
        {
            var so = new SerializedObject(fx);
            so.FindProperty("_replacedBackground").objectReferenceValue = bgGraphic;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return fx;
    }

    private struct Placement
    {
        public Vector2 Center; // 하단 중앙 기준 (캔버스 단위)
        public Vector2 Size;   // 자기 로컬 단위
        public float Scale;    // 루트 캔버스 대비 배율
    }

    // 게임 하단 UI는 SafeArea 하단 기준이므로, 루트 캔버스 하단 중앙에서의 거리로 옮긴다.
    private static Placement MeasureBottomAnchored(RectTransform rt)
    {
        var root = (RectTransform)rt.GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector2 local = root.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
        var rect = root.rect;
        return new Placement
        {
            Center = new Vector2(local.x - rect.center.x, local.y - rect.yMin),
            Size = rt.rect.size,
            Scale = rt.lossyScale.x / root.lossyScale.x,
        };
    }

    // 그림·레이아웃(UnityEngine/TMPro/Graphic 파생)과 keep 타입만 남기고 게임 스크립트를 제거한다.
    private static void Strip(GameObject root, params Type[] keep)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                string ns = type.Namespace ?? string.Empty;
                if (keep.Contains(type) || typeof(Graphic).IsAssignableFrom(type) || typeof(BaseMeshEffect).IsAssignableFrom(type)
                    || ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro"))
                    continue;
                Object.DestroyImmediate(mb);
            }
        }
    }

    private static void BuildButtons(RectTransform canvasRt, LevelUpFxLab driver)
    {
        // 1줄: 등급 재생 / 초기화
        var bar = NewButtonRow(canvasRt, "LabButtons", -20f);
        for (int i = 0; i < TierButtonLabels.Length; i++)
        {
            var button = NewButton(bar, TierButtonLabels[i]);
            UnityEventTools.AddIntPersistentListener(button.onClick, driver.PlayTierIndex, i);
        }
        var reset = NewButton(bar, "RESET");
        UnityEventTools.AddVoidPersistentListener(reset.onClick, driver.HidePanel);

        // 2줄: 연출 A/B 전환 (누를 때마다 바뀜, 글자가 현재 상태) — 다음 재생부터 반영
        var toggles = NewButtonRow(canvasRt, "LabToggles", -122f);
        var burst = NewButton(toggles, "BURST B");
        UnityEventTools.AddVoidPersistentListener(burst.onClick, driver.ToggleGaugeBurst);
        var card = NewButton(toggles, "CARD A");
        UnityEventTools.AddVoidPersistentListener(card.onClick, driver.ToggleCardReveal);
        var upgrade = NewButton(toggles, "UPG 10%");
        UnityEventTools.AddVoidPersistentListener(upgrade.onClick, driver.CycleLegendUpgrade);

        var so = new SerializedObject(driver);
        so.FindProperty("_burstLabel").objectReferenceValue = burst.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("_cardLabel").objectReferenceValue = card.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("_upgradeLabel").objectReferenceValue = upgrade.GetComponentInChildren<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();
        bar.SetAsLastSibling();
        toggles.SetAsLastSibling();
    }

    private static RectTransform NewButtonRow(RectTransform canvasRt, string name, float y)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(canvasRt, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-40f, 90f);
        row.anchoredPosition = new Vector2(0f, y);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = layout.childControlHeight = true;
        return row;
    }

    private static Button NewButton(RectTransform parent, string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(go.transform, false);
        Stretch(text.rectTransform);
        text.text = label;
        text.fontSize = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return go.GetComponent<Button>();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    private static string RelativePath(Transform root, Transform target)
    {
        if (target == null || target == root) return string.Empty;
        return AnimationUtility.CalculateTransformPath(target, root);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
