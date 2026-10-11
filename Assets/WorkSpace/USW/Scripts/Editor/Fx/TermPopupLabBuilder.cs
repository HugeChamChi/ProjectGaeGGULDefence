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
/// 용어 팝업 실험실 씬(FxLab_TermPopup)을 만든다.
///   1. IngameScene의 레벨업 패널을 복사한다 — IngameScene은 읽기만 하고 저장하지 않는다.
///   2. 복사본에서 게임 스크립트를 떼어 내고 그림·레이아웃만 남긴다. 선택 타이머는 게임처럼 숨긴다.
///   3. 드라이버(TermPopupLab) + RESET 버튼을 붙이고, 용어 사전(TermGlossary SO)과 4개 키워드가 들어간 카드 3장을 연결한다.
/// 다시 실행하면 씬을 새로 만든다 (씬에 직접 한 수정은 사라짐).
/// </summary>
public static class TermPopupLabBuilder
{
    private const string IngameScenePath = "Assets/Scenes/IngameScene.unity";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TermPopup.unity";
    private const string SampleFolder = "Assets/WorkSpace/USW/Data/SelectionData";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Color FieldColor = new Color(0.08f, 0.13f, 0.21f, 1f);

    // 위·가운데·아래 카드 — 4개 키워드(족장 스킬·자폭 드론·해킹스택·시간조작)가 모두 나오게 고른다.
    // 설명이 비면 카드 SO 설명을 그대로 쓴다 (용어는 사전 별칭으로 자동 링크). 이름이 없으면 아무 카드나 쓴다.
    private static readonly string[] CardNames = { "전술적 프로토콜", "해킹 자폭", "오류코드 0x00" };
    private static readonly string[] Descriptions =
    {
        "",
        "베탕의 모든 자폭 드론을 해킹 드론으로 전환합니다. 폭발 피해 대신 보스 적중 시 해킹 스택 +1. 수리 키트도 발동합니다.",
        "[실험실 예시] 디시그망 스킬의 시간 회복이 0.1초 늘어납니다.",
    };

    [MenuItem("Tools/USW/Fx/Build Term Popup Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[TermPopupLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            EditorSceneManager.OpenScene(IngameScenePath, OpenSceneMode.Single);
        var ingame = SceneManager.GetSceneByPath(IngameScenePath);
        bool openedIngame = !ingame.isLoaded;
        if (openedIngame) ingame = EditorSceneManager.OpenScene(IngameScenePath, OpenSceneMode.Additive);

        var sourceUi = FindInScene<LevelUpUI>(ingame);
        if (sourceUi == null) { Debug.LogError("[TermPopupLab] IngameScene에서 LevelUpUI를 찾지 못함"); return; }
        var uiSo = new SerializedObject(sourceUi);
        var sourcePanel = uiSo.FindProperty("obj").objectReferenceValue as GameObject;
        var sourceContainer = uiSo.FindProperty("cardContainer").objectReferenceValue as Transform;
        var cardPrefab = uiSo.FindProperty("cardPrefab").objectReferenceValue as LevelUpCardUI;
        if (sourcePanel == null || sourceContainer == null || cardPrefab == null || !sourceContainer.IsChildOf(sourcePanel.transform))
        {
            Debug.LogError("[TermPopupLab] LevelUpUI의 obj(패널)/cardContainer/cardPrefab 연결을 확인할 것");
            return;
        }
        string containerPath = RelativePath(sourcePanel.transform, sourceContainer);
        var timerText = uiSo.FindProperty("selectionTimerText").objectReferenceValue as Component;
        string timerPath = timerText != null && timerText.transform.IsChildOf(sourcePanel.transform)
            ? RelativePath(sourcePanel.transform, timerText.transform) : null;

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

        var field = new GameObject("FieldStandIn", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        field.rectTransform.SetParent(canvasRt, false);
        Stretch(field.rectTransform);
        field.color = FieldColor;
        field.raycastTarget = false;

        // 레벨업 패널 복사 (그림·레이아웃만)
        var panel = Object.Instantiate(sourcePanel);
        panel.name = sourcePanel.name;
        var panelRt = (RectTransform)panel.transform;
        panelRt.SetParent(canvasRt, false);
        Stretch(panelRt);
        panelRt.localScale = Vector3.one;
        var container = (RectTransform)(string.IsNullOrEmpty(containerPath) ? panel.transform : panel.transform.Find(containerPath));
        for (int i = container.childCount - 1; i >= 0; i--)
            if (container.GetChild(i).GetComponent<LevelUpCardUI>() != null) Object.DestroyImmediate(container.GetChild(i).gameObject);
        if (!string.IsNullOrEmpty(timerPath)) panel.transform.Find(timerPath)?.gameObject.SetActive(false);
        // 게임 레벨업 화면의 검은 배경 + 글로우 + 올라오는 입자(TierRevealFx)는 남긴다. 패널 안에 없으면 실험실 복사본에만 프리팹을 넣는다.
        Strip(panel, typeof(TierRevealFx));
        var tierReveal = panel.GetComponentInChildren<TierRevealFx>(true) ?? AddTierReveal(panel, container);
        foreach (var group in panel.GetComponentsInChildren<CanvasGroup>(true)) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true; }
        panel.SetActive(true);

        // 드라이버
        var driver = new GameObject("TermPopupLab", typeof(TermPopupLab)).GetComponent<TermPopupLab>();
        BuildButtons(canvasRt, driver);
        var so = new SerializedObject(driver);
        so.FindProperty("_popupCanvas").objectReferenceValue = canvasRt;
        so.FindProperty("_panelObject").objectReferenceValue = panel;
        so.FindProperty("_cardContainer").objectReferenceValue = container;
        so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
        so.FindProperty("_roundedSprite").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        so.FindProperty("_blurShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/WorkSpace/USW/Shaders/UI/UI_SnapshotBlur.shader");
        TermGlossarySetup.CreateOrFill();
        so.FindProperty("_glossary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TermGlossary>(TermGlossarySetup.AssetPath);
        so.FindProperty("_tierReveal").objectReferenceValue = tierReveal;

        var cards = PickCards();
        var cardProp = so.FindProperty("_cardData");
        cardProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) cardProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        var descProp = so.FindProperty("_descriptions");
        descProp.arraySize = Descriptions.Length;
        for (int i = 0; i < Descriptions.Length; i++) descProp.GetArrayElementAtIndex(i).stringValue = Descriptions[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(lab, ScenePath);
        // IngameScene이 원래 열려 있었어도 닫는다 — 함께 열린 채 Play하면 두 씬이 같이 돈다 (저장 확인은 위에서 끝남).
        if (ingame.isLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(ingame, true);
        Debug.Log($"[TermPopupLab] 완료: {ScenePath} (카드 {string.Join(", ", cards.Select(c => c.chooseName))}, 등급 연출 {(tierReveal != null ? tierReveal.name : "없음")})");
    }

    private static LevelUpData[] PickCards()
    {
        var all = AssetDatabase.FindAssets("t:LevelUpData", new[] { SampleFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.icon != null)
            .OrderBy(d => d.chooseId)
            .ToList();
        var picked = new LevelUpData[CardNames.Length];
        for (int i = 0; i < CardNames.Length; i++)
        {
            picked[i] = all.FirstOrDefault(d => d.chooseName == CardNames[i])
                     ?? all.FirstOrDefault(d => !picked.Contains(d));
            if (picked[i] == null) Debug.LogWarning($"[TermPopupLab] 카드 {i} 샘플 없음");
        }
        return picked.Where(d => d != null).ToArray();
    }

    private static void BuildButtons(RectTransform canvasRt, TermPopupLab driver)
    {
        var row = new GameObject("LabButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(canvasRt, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-40f, 90f);
        row.anchoredPosition = new Vector2(0f, -20f);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = layout.childControlHeight = true;
        var reset = NewButton(row, "RESET");
        UnityEventTools.AddVoidPersistentListener(reset.onClick, driver.Respawn);
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

    // 게임 IngameScene에 TierReveal이 패널 밖에 있을 때: 패널 배경 바로 위(카드 뒤)에 프리팹을 넣고 Anchor를 카드 묶음 중심에 맞춘다.
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
        if (fx != null && background != null && background.TryGetComponent<Graphic>(out var bgGraphic))
        {
            var so = new SerializedObject(fx);
            so.FindProperty("_replacedBackground").objectReferenceValue = bgGraphic;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return fx;
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

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
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
