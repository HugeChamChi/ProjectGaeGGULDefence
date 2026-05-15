#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GaeGGUL.UI.Totem;

/// <summary>
/// 메뉴: Tools > USW > Build TotemSelect Card Prefab
///        Tools > USW > Build TotemSelect Panel Prefab
/// 실행 순서: Card 먼저 → Panel
/// 저장 경로: Assets/WorkSpace/USW/Prefabs/UI/
/// </summary>
public static class TotemSelectPrefabBuilder
{
    private const string SaveDir  = "Assets/WorkSpace/USW/Prefabs/UI";
    private const string CardPath = "Assets/WorkSpace/USW/Prefabs/UI/TotemSelectCardPrefab.prefab";
    private const string PanelPath = "Assets/WorkSpace/USW/Prefabs/UI/TotemSelectPanel.prefab";

    // ─────────────────────────────────────────────
    [MenuItem("Tools/USW/Build TotemSelect Card Prefab")]
    public static void BuildCardPrefab()
    {
        EnsureDirectory(SaveDir);
        var root = BuildCardHierarchy();
        PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[TotemSelectPrefabBuilder] 카드 프리팹 생성 완료: {CardPath}");
    }

    [MenuItem("Tools/USW/Build TotemSelect Panel Prefab")]
    public static void BuildPanelPrefab()
    {
        EnsureDirectory(SaveDir);
        var cardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (cardAsset == null)
            Debug.LogWarning("[TotemSelectPrefabBuilder] 카드 프리팹 없음 — Card Prefab을 먼저 생성하세요.");

        var root = BuildPanelHierarchy(cardAsset);
        PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[TotemSelectPrefabBuilder] 패널 프리팹 생성 완료: {PanelPath}");
    }

    // ─────────────────────────────────────────────
    // 카드 계층구조  (340 × 460)
    // ─────────────────────────────────────────────
    private static GameObject BuildCardHierarchy()
    {
        var root = new GameObject("TotemSelectCardPrefab");
        SetRect(root, new Vector2(340, 460));

        // CardBackground — 카드 전체 배경 (tier.GetBGColor() 적용 대상)
        var bgGO  = Child("CardBackground", root.transform);
        Fill(bgGO.GetComponent<RectTransform>());
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.13f, 0.13f, 0.15f, 1f);

        // Button — 배경 위에 투명하게 덮음
        var btnGO  = Child("Button", root.transform);
        Fill(btnGO.GetComponent<RectTransform>());
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = Color.clear;
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // TierBorder — 카드 테두리 (tier.GetFrame() 스프라이트 적용 대상)
        var borderGO  = Child("TierBorder", root.transform);
        Fill(borderGO.GetComponent<RectTransform>());
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = Color.white;
        borderImg.type  = Image.Type.Sliced;

        // TierBanner — 상단 등급명 띠 (높이 48)
        var bannerGO  = Child("TierBanner", root.transform);
        var bannerRect = bannerGO.GetComponent<RectTransform>();
        bannerRect.anchorMin        = new Vector2(0f, 1f);
        bannerRect.anchorMax        = new Vector2(1f, 1f);
        bannerRect.pivot            = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = Vector2.zero;
        bannerRect.sizeDelta        = new Vector2(0f, 48f);
        bannerGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

        // TierText — 등급명
        var tierTMP = ChildTMP("TierText", bannerGO.transform, 20, FontStyles.Bold);
        Fill(tierTMP.GetComponent<RectTransform>());

        // IconImage — 160×160, 배너 바로 아래 중앙
        var iconGO  = Child("IconImage", root.transform);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin        = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot            = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -56f);
        iconRect.sizeDelta        = new Vector2(160f, 160f);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color          = Color.white;

        // NameText — 아이콘 아래
        var nameTMP = ChildTMP("NameText", root.transform, 18, FontStyles.Bold);
        AnchorTop(nameTMP.GetComponent<RectTransform>(), -224f, 28f);

        // Divider 효과
        Divider("Divider_Effect", root.transform, -262f, "효과");

        // DescriptionText
        var descTMP = ChildTMP("DescriptionText", root.transform, 15, FontStyles.Normal);
        var descRect = descTMP.GetComponent<RectTransform>();
        AnchorTop(descRect, -298f, 80f);
        descRect.offsetMin = new Vector2(12f, descRect.offsetMin.y);
        descRect.offsetMax = new Vector2(-12f, descRect.offsetMax.y);
        descTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;

        // Divider 범위
        Divider("Divider_Range", root.transform, -386f, "범위");

        // TotemSelectCardUI 연결
        var card = root.AddComponent<TotemSelectCardUI>();
        Bind(card, "iconImage",           iconImg);
        Bind(card, "tierBorderImage",     borderImg);
        Bind(card, "cardBackgroundImage", bgImg);
        Bind(card, "nameText",            nameTMP.GetComponent<TextMeshProUGUI>());
        Bind(card, "tierText",            tierTMP.GetComponent<TextMeshProUGUI>());
        Bind(card, "descriptionText",     descTMP.GetComponent<TextMeshProUGUI>());
        Bind(card, "button",              btn);

        return root;
    }

    // ─────────────────────────────────────────────
    // 패널 계층구조
    // ─────────────────────────────────────────────
    private static GameObject BuildPanelHierarchy(GameObject cardPrefabAsset)
    {
        var root = new GameObject("TotemSelectPanel");
        SetRect(root, new Vector2(1280f, 720f));

        // 딤 배경
        var dimGO  = Child("Dim", root.transform);
        Fill(dimGO.GetComponent<RectTransform>());
        dimGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Window
        var winGO  = Child("Window", root.transform);
        Fill(winGO.GetComponent<RectTransform>());
        winGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

        // Header — 타이머
        var headerGO = Child("Header", winGO.transform);
        var headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot     = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -16f);
        headerRect.sizeDelta        = new Vector2(0f, 56f);
        var timerTMP = ChildTMP("TimerText", headerGO.transform, 28, FontStyles.Bold);
        Fill(timerTMP.GetComponent<RectTransform>());
        timerTMP.GetComponent<TextMeshProUGUI>().text = "30";

        // CardContainer — GridLayoutGroup
        var containerGO  = Child("CardContainer", winGO.transform);
        var containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.anchorMin    = new Vector2(0f, 0.12f);
        containerRect.anchorMax    = new Vector2(1f, 0.88f);
        containerRect.offsetMin    = containerRect.offsetMax = Vector2.zero;
        var grid = containerGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(340f, 460f);
        grid.spacing         = new Vector2(24f, 0f);
        grid.childAlignment  = TextAnchor.MiddleCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        // Footer — 확인 버튼
        var footerGO  = Child("Footer", winGO.transform);
        var footerRect = footerGO.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot     = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 20f);
        footerRect.sizeDelta        = new Vector2(0f, 64f);

        var confirmGO  = Child("ConfirmButton", footerGO.transform);
        var confirmRect = confirmGO.GetComponent<RectTransform>();
        confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.sizeDelta = new Vector2(200f, 52f);
        var confirmImg = confirmGO.AddComponent<Image>();
        confirmImg.color = new Color(0.2f, 0.6f, 1f);
        var confirmBtn = confirmGO.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        var confirmLbl = ChildTMP("Label", confirmGO.transform, 18, FontStyles.Bold);
        Fill(confirmLbl.GetComponent<RectTransform>());
        confirmLbl.GetComponent<TextMeshProUGUI>().text = "확인";

        // TotemSelectUI 연결
        var ui = root.AddComponent<TotemSelectUI>();
        Bind(ui, "cardContainer",      containerGO.transform);
        Bind(ui, "cardPrefab",         cardPrefabAsset);
        Bind(ui, "confirmButton",      confirmBtn);
        Bind(ui, "selectionTimerText", timerTMP.GetComponent<TextMeshProUGUI>());

        return root;
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    private static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject ChildTMP(string name, Transform parent, int size, FontStyles style)
    {
        var go  = Child(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    private static void SetRect(GameObject go, Vector2 size)
    {
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTop(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(0f, height);
    }

    private static void Divider(string name, Transform parent, float yPos, string label)
    {
        var go  = Child(name, parent);
        var rt  = go.GetComponent<RectTransform>();
        AnchorTop(rt, yPos, 30f);
        go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);

        var lbl = ChildTMP("Label", go.transform, 14, FontStyles.Normal);
        Fill(lbl.GetComponent<RectTransform>());
        lbl.GetComponent<TextMeshProUGUI>().text = label;
    }

    private static void EnsureDirectory(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Bind(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public    |
            System.Reflection.BindingFlags.Instance);
        if (f == null) return;

        // GameObject를 넘겼는데 필드 타입이 다르면 GetComponent로 자동 변환
        if (value is GameObject go && f.FieldType != typeof(GameObject))
            value = go.GetComponent(f.FieldType);

        f.SetValue(target, value);
    }
}
#endif
