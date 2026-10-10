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
///   3. 드라이버(TermPopupLab) + MODE/RESET 버튼을 붙이고, 카드 3장·설명·용어 예시 문구를 채운다.
/// 다시 실행하면 씬을 새로 만든다 (씬에 직접 한 수정은 사라짐).
/// </summary>
public static class TermPopupLabBuilder
{
    private const string IngameScenePath = "Assets/Scenes/IngameScene.unity";
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TermPopup.unity";
    private const string SampleFolder = "Assets/WorkSpace/USW/Data/SelectionData";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Color FieldColor = new Color(0.08f, 0.13f, 0.21f, 1f);

    // 위·가운데·아래 카드. 이름이 없으면 등급 순 아무 카드나 쓴다.
    private static readonly string[] CardNames = { "전술적 프로토콜", "다 쏴 제껴버려!!!!", "만나서 반가워!" };
    private static readonly string[] Descriptions =
    {
        "[[액티브 스킬]]의 피해량이 40% 증가합니다.",
        "베탕의 [[전투 드론]] 공격 속도가 20% 증가하고, 적중 시 [[화상]]을 1중첩 부여합니다.",
        "10초마다 [[자폭 드론]] 1기를 추가로 발사하고, 폭발한 보스에게 [[아머브레이크]]를 부여합니다.",
    };

    private static readonly Color Debuff = new Color(0.886f, 0.408f, 0.235f);
    private static readonly Color Unit = new Color(0.298f, 0.553f, 0.941f);
    private static readonly Color Rule = new Color(0.42f, 0.44f, 0.53f);

    // 실험실 예시 문구 — 수치는 화면 확인용이며 실제 값은 디버프 SO에서 온다.
    private static TermPopupLabTerm[] SampleTerms() => new[]
    {
        Term("액티브 스킬", "규칙", Rule, "스킬 게이지가 차면 자동으로 쓰는 유닛 고유 기술.",
             "드론 유닛의 스킬 게이지가 가득 차면 자동으로 발동하는 고유 기술입니다. 유닛마다 효과가 다르며, 쿨타임·피해량 선택지의 대상이 됩니다.", ""),
        Term("전투 드론", "유닛", Unit, "베탕 주위를 맴돌며 자동 공격하는 드론.",
             "베탕을 배치하면 함께 나와 주위를 맴도는 드론입니다. 베탕의 공격력을 따라 보스를 자동 공격하고, 베탕을 회수하면 사라집니다.", ""),
        Term("화상", "디버프", Debuff, "일정 간격으로 피해를 주는 상태 이상.",
             "걸린 보스에게 일정 간격으로 피해를 줍니다. 중첩이 쌓일수록 피해가 커지고, 지속 시간이 끝나면 사라집니다.", "최대 5중첩 · 지속 4초 · 0.5초마다"),
        Term("자폭 드론", "유닛", Unit, "보스에게 날아가 터지는 일회용 드론.",
             "보스를 향해 날아가 도착하자마자 폭발하는 일회용 드론입니다. 폭발 피해는 발사한 유닛의 공격력을 따릅니다.", ""),
        Term("아머브레이크", "디버프", Debuff, "보스 방어를 깎아 받는 피해를 늘림.",
             "보스의 방어를 깎아 모든 공격에 받는 피해가 늘어납니다. 중첩이 쌓일수록 효과가 커집니다.", "최대 10중첩 · 지속 6초 · 중첩당 피해 +3%"),
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
        var modeLabel = BuildButtons(canvasRt, driver, out var punchLabel);
        var so = new SerializedObject(driver);
        so.FindProperty("_popupCanvas").objectReferenceValue = canvasRt;
        so.FindProperty("_panelObject").objectReferenceValue = panel;
        so.FindProperty("_cardContainer").objectReferenceValue = container;
        so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
        so.FindProperty("_roundedSprite").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        so.FindProperty("_modeLabel").objectReferenceValue = modeLabel;
        so.FindProperty("_punchLabel").objectReferenceValue = punchLabel;
        so.FindProperty("_tierReveal").objectReferenceValue = tierReveal;

        var cards = PickCards();
        var cardProp = so.FindProperty("_cardData");
        cardProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) cardProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        var descProp = so.FindProperty("_descriptions");
        descProp.arraySize = Descriptions.Length;
        for (int i = 0; i < Descriptions.Length; i++) descProp.GetArrayElementAtIndex(i).stringValue = Descriptions[i];
        var terms = SampleTerms();
        var termProp = so.FindProperty("_terms");
        termProp.arraySize = terms.Length;
        for (int i = 0; i < terms.Length; i++)
        {
            var t = termProp.GetArrayElementAtIndex(i);
            t.FindPropertyRelative("Id").stringValue = terms[i].Id;
            t.FindPropertyRelative("Kind").stringValue = terms[i].Kind;
            t.FindPropertyRelative("KindColor").colorValue = terms[i].KindColor;
            t.FindPropertyRelative("Short").stringValue = terms[i].Short;
            t.FindPropertyRelative("Body").stringValue = terms[i].Body;
            t.FindPropertyRelative("Facts").stringValue = terms[i].Facts;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(lab, ScenePath);
        if (openedIngame) EditorSceneManager.CloseScene(ingame, true);
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

    private static TermPopupLabTerm Term(string id, string kind, Color color, string shortText, string body, string facts) =>
        new TermPopupLabTerm { Id = id, Kind = kind, KindColor = color, Short = shortText, Body = body, Facts = facts };

    private static TMP_Text BuildButtons(RectTransform canvasRt, TermPopupLab driver, out TMP_Text punchLabel)
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

        var mode = NewButton(row, "MODE A+B");
        UnityEventTools.AddVoidPersistentListener(mode.onClick, driver.CycleMode);
        var punch = NewButton(row, "B NEW");
        UnityEventTools.AddVoidPersistentListener(punch.onClick, driver.TogglePunch);
        punchLabel = punch.GetComponentInChildren<TextMeshProUGUI>();
        var reset = NewButton(row, "RESET");
        UnityEventTools.AddVoidPersistentListener(reset.onClick, driver.Respawn);
        return mode.GetComponentInChildren<TextMeshProUGUI>();
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
