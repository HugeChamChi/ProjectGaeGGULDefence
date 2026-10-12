using GaeGGUL.UI.Unit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 획득 선택지 보관함 프리팹 2종을 만든다 (Tools/USW/UI/Build Selection Inventory Prefabs).
/// FxLab_SelectionInventory A안(사용자 10-13 선택)을 그대로 옮긴 계층이며, 이후 수정은 프리팹에서 직접 한다.
/// 다시 실행하면 같은 경로(GUID 유지)에 덮어쓰므로 프리팹을 손으로 고친 뒤에는 실행하지 않는다.
/// </summary>
public static class SelectionInventoryPrefabBuilder
{
    /// <summary>프리팹 폴더.</summary>
    public const string Folder = "Assets/WorkSpace/USW/Prefab/UI/SelectionInventory";
    /// <summary>보관함 창 프리팹.</summary>
    public const string PrefabPath = Folder + "/SelectionInventory.prefab";
    /// <summary>칸 프리팹.</summary>
    public const string SlotPath = Folder + "/SelectionInventorySlot.prefab";

    private const string GlossaryPath = "Assets/WorkSpace/USW/Data/UI/TermGlossary.asset";
    private const string BlurShaderPath = "Assets/WorkSpace/USW/Shaders/UI/UI_SnapshotBlur.shader";
    private const string UnitFolder = "Assets/WorkSpace/USW/Data/DroneUnits";
    private const string ChiefPath = "Assets/WorkSpace/USW/Data/Chieftain/AlphanChieftain.asset";
    private const string DroneIconPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Drone_1.png";
    private const string TitleFontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string BodyFontGuid = "34571da45f3f3d8419493b418172203f";  // NotoSansKR-Bold SDF

    // 인게임 '# MainUI'(1)·SubUI(4) 위, Unit_Info_Panel(30) 아래. 용어 시트는 정보창 위.
    private const int SortingOrder = 25;
    private const int TermSortingOrder = 40;
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    private static readonly Color PanelFill = new Color32(0x2A, 0x27, 0x66, 0xFF);
    private static readonly Color PanelWell = new Color32(0x1B, 0x19, 0x45, 0xFF);
    private static readonly Color Ink = new Color32(0x0E, 0x0C, 0x24, 0xFF);
    private static readonly Color Muted = new Color32(0xB8, 0xB5, 0xE6, 0xFF);
    private static readonly Color CloseRed = new Color32(0xE8, 0x53, 0x4D, 0xFF);
    private static readonly Color FaceBack = new Color32(0x3A, 0x37, 0x86, 0xFF);

    private const float PanelWidth = 980f;
    private const float PanelHeight = 1380f;
    private const float PanelY = 40f;
    private const float HeaderHeight = 130f;
    private const float DetailHeight = 330f;
    private const float Pad = 28f;
    private const float Cell = 200f;
    private const float Gap = 16f;
    private const int Columns = 4;
    private const float Outline = 4f;
    private const float FaceBadge = 66f;

    private static TMP_FontAsset _title, _body;
    private static Sprite _rounded, _circle;

    [MenuItem("Tools/USW/UI/Build Selection Inventory Prefabs")]
    public static void BuildMenu() => Debug.Log(Build());

    /// <summary>두 프리팹을 만들거나 덮어쓴다.</summary>
    public static string Build()
    {
        _title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TitleFontGuid));
        _body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(BodyFontGuid));
        _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        EnsureFolder(Folder);

        var slot = BuildSlot();
        BuildWindow(slot);
        AssetDatabase.SaveAssets();
        return $"[SelectionInventory] 프리팹 생성: {PrefabPath}, {SlotPath}";
    }

    // ── 칸 ───────────────────────────────────────────────────────────

    private static SelectionInventorySlot BuildSlot()
    {
        var root = new GameObject("SelectionInventorySlot", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(Cell, Cell);
        var hit = root.AddComponent<Image>();
        hit.color = Color.clear;
        var button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var group = root.AddComponent<CanvasGroup>();
        var border = Img("Border", root.transform, _rounded, Color.white);
        Stretch(border.rectTransform, -Outline);
        border.raycastTarget = false;
        var fill = Img("Fill", root.transform, _rounded, PanelWell);
        Stretch(fill.rectTransform, 0f);
        fill.raycastTarget = false;
        var icon = Img("Icon", root.transform, null, Color.white);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(Cell * 0.78f, Cell * 0.78f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var face = Face(root.transform, FaceBadge);
        Place((RectTransform)face.transform, new Vector2(1f, 0f), new Vector2(-FaceBadge * 0.32f, FaceBadge * 0.32f), new Vector2(FaceBadge, FaceBadge));

        var component = root.AddComponent<SelectionInventorySlot>();
        var so = new SerializedObject(component);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_border").objectReferenceValue = border;
        so.FindProperty("_fill").objectReferenceValue = fill;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_face").objectReferenceValue = face;
        so.ApplyModifiedPropertiesWithoutUndo();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
        Object.DestroyImmediate(root);
        return saved.GetComponent<SelectionInventorySlot>();
    }

    // ── 창 ───────────────────────────────────────────────────────────

    private static void BuildWindow(SelectionInventorySlot slotPrefab)
    {
        var root = new GameObject("SelectionInventory", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;

        var window = Rect("Window", root.transform);
        Stretch(window, 0f);
        var windowGroup = window.gameObject.AddComponent<CanvasGroup>();
        var dim = Img("Dim", window, null, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform, 0f);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;

        var panel = Framed("Panel", window, PanelFill, Ink, out _, out _);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, PanelY), new Vector2(PanelWidth, PanelHeight));

        var title = Txt("Title", panel, _title, 50f, Color.white, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad + 230f, -70f), new Vector2(460f, 80f));
        title.outlineWidth = 0.2f;
        title.outlineColor = Ink;
        title.text = "획득한 선택지 0";
        var tally = Txt("Tally", panel, _body, 24f, Muted, TextAlignmentOptions.Right);
        Place(tally.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad - 230f, -72f), new Vector2(360f, 50f));
        var close = Framed("Close", panel, CloseRed, Ink, out _, out _, _circle);
        Place(close, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(76f, 76f));
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.transition = Selectable.Transition.None;
        var x = Txt("X", close, _title, 40f, Color.white, TextAlignmentOptions.Center);
        Stretch(x.rectTransform, 0f);
        x.text = "X";

        // 보관함: 4열, 왼쪽 위부터. 4열 묶음은 가운데 정렬되도록 양옆 여백을 계산한다.
        var well = Img("Well", panel, _rounded, PanelWell);
        well.rectTransform.anchorMin = Vector2.zero;
        well.rectTransform.anchorMax = Vector2.one;
        well.rectTransform.offsetMin = new Vector2(Pad, Pad + DetailHeight + 16f);
        well.rectTransform.offsetMax = new Vector2(-Pad, -HeaderHeight);
        var viewport = Rect("Viewport", well.transform);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(Cell, Cell);
        grid.spacing = new Vector2(Gap, Gap + 4f);
        int side = Mathf.RoundToInt((PanelWidth - Pad * 2f - (Columns * Cell + (Columns - 1) * Gap)) * 0.5f);
        grid.padding = new RectOffset(side, side, 24, 24);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = well.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        // 상세
        var detail = Img("Detail", panel, _rounded, PanelWell);
        detail.rectTransform.anchorMin = new Vector2(0f, 0f);
        detail.rectTransform.anchorMax = new Vector2(1f, 0f);
        detail.rectTransform.pivot = new Vector2(0.5f, 0f);
        detail.rectTransform.offsetMin = new Vector2(Pad, Pad);
        detail.rectTransform.offsetMax = new Vector2(-Pad, Pad + DetailHeight);
        var hint = Txt("Hint", detail.transform, _body, 26f, Muted, TextAlignmentOptions.Center);
        Stretch(hint.rectTransform, 0f);
        hint.text = "레벨업에서 고른 선택지가 여기에 모입니다";
        var detailContent = Rect("Content", detail.transform);
        Stretch(detailContent, 0f);
        var detailGroup = detailContent.gameObject.AddComponent<CanvasGroup>();
        var iconFrame = Framed("IconFrame", detailContent, PanelWell, Color.white, out var detailBorder, out var detailFill);
        Place(iconFrame, new Vector2(0f, 1f), new Vector2(26f + 85f, -26f - 85f), new Vector2(170f, 170f));
        var detailIcon = Img("Icon", iconFrame, null, Color.white);
        Place(detailIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        var name = Txt("Name", detailContent, _title, 40f, Color.white, TextAlignmentOptions.TopLeft);
        name.rectTransform.anchorMin = new Vector2(0f, 1f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.offsetMin = new Vector2(222f, -80f);
        name.rectTransform.offsetMax = new Vector2(-24f, -24f);
        var desc = Txt("Description", detailContent, _body, 26f, Color.white, TextAlignmentOptions.TopLeft);
        desc.rectTransform.anchorMin = Vector2.zero;
        desc.rectTransform.anchorMax = Vector2.one;
        desc.rectTransform.offsetMin = new Vector2(222f, 96f);
        desc.rectTransform.offsetMax = new Vector2(-24f, -86f);
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.gameObject.AddComponent<TermLinkTapTarget>();
        var who = Txt("WhoLabel", detailContent, _body, 22f, Muted, TextAlignmentOptions.Left);
        Place(who.rectTransform, new Vector2(0f, 0f), new Vector2(222f + 50f, 48f), new Vector2(100f, 40f));
        who.text = "적용";
        var chips = Rect("Chips", detailContent);
        chips.anchorMin = new Vector2(0f, 0f);
        chips.anchorMax = new Vector2(1f, 0f);
        chips.pivot = new Vector2(0f, 0.5f);
        chips.offsetMin = new Vector2(222f + 110f, 18f);
        chips.offsetMax = new Vector2(-24f, 78f);
        var chipLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 12f;
        chipLayout.childAlignment = TextAnchor.MiddleLeft;
        chipLayout.childControlWidth = true;
        chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = false;
        chipLayout.childForceExpandHeight = false;
        var chipTemplate = BuildChip(chips);
        var common = Txt("Common", detailContent, _body, 22f, Color.white, TextAlignmentOptions.Left);
        Place(common.rectTransform, new Vector2(0f, 0f), new Vector2(222f + 110f + 150f, 48f), new Vector2(300f, 40f));
        common.text = "모든 유닛";

        // 족장 간이 카드 (족장은 유닛 정보창이 없다)
        var chiefCard = Rect("ChiefCard", window);
        Stretch(chiefCard, 0f);
        var chiefDim = Img("Dim", chiefCard, null, new Color(0f, 0f, 0f, 0.35f));
        Stretch(chiefDim.rectTransform, 0f);
        var chiefDismiss = chiefDim.gameObject.AddComponent<Button>();
        chiefDismiss.transition = Selectable.Transition.None;
        var chiefBox = Framed("Box", chiefCard, PanelFill, Ink, out _, out _);
        Place(chiefBox, new Vector2(0.5f, 0.5f), new Vector2(0f, PanelY), new Vector2(760f, 300f));
        var chiefFace = Face(chiefBox, 180f);
        Place((RectTransform)chiefFace.transform, new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(180f, 180f));
        var chiefText = Txt("Text", chiefBox, _body, 26f, Color.white, TextAlignmentOptions.Left);
        chiefText.rectTransform.anchorMin = Vector2.zero;
        chiefText.rectTransform.anchorMax = Vector2.one;
        chiefText.rectTransform.offsetMin = new Vector2(260f, 30f);
        chiefText.rectTransform.offsetMax = new Vector2(-30f, -30f);
        chiefText.textWrappingMode = TextWrappingModes.Normal;

        // 용어 시트 층: 별도 캔버스로 정보창(30) 위
        var termLayer = Rect("TermLayer", root.transform);
        Stretch(termLayer, 0f);
        var termCanvas = termLayer.gameObject.AddComponent<Canvas>();
        termCanvas.overrideSorting = true;
        termCanvas.sortingOrder = TermSortingOrder;
        termLayer.gameObject.AddComponent<GraphicRaycaster>();
        var fontSource = Txt("FontSource", termLayer, _body, 26f, Color.white, TextAlignmentOptions.Left);
        fontSource.gameObject.SetActive(false);

        var ui = root.AddComponent<SelectionInventoryUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("_glossary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TermGlossary>(GlossaryPath);
        so.FindProperty("_blurShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
        so.FindProperty("_fallbackChief").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ChieftainData>(ChiefPath);
        SetTargets(so.FindProperty("_targets"));
        Ref(so, "_window", window.gameObject);
        Ref(so, "_windowGroup", windowGroup);
        Ref(so, "_dimButton", dimButton);
        Ref(so, "_panel", panel);
        Ref(so, "_title", title);
        Ref(so, "_tally", tally);
        Ref(so, "_closeButton", closeButton);
        Ref(so, "_scroll", scroll);
        Ref(so, "_gridContent", content);
        Ref(so, "_slotPrefab", slotPrefab);
        Ref(so, "_detailHint", hint);
        Ref(so, "_detailContent", detailContent);
        Ref(so, "_detailGroup", detailGroup);
        Ref(so, "_detailBorder", detailBorder);
        Ref(so, "_detailFill", detailFill);
        Ref(so, "_detailIcon", detailIcon);
        Ref(so, "_detailName", name);
        Ref(so, "_detailDescription", desc);
        Ref(so, "_chipRow", chips);
        Ref(so, "_chipTemplate", chipTemplate);
        Ref(so, "_commonLabel", common);
        Ref(so, "_chiefCard", chiefCard.gameObject);
        Ref(so, "_chiefDismiss", chiefDismiss);
        Ref(so, "_chiefFace", chiefFace);
        Ref(so, "_chiefText", chiefText);
        Ref(so, "_termLayer", termLayer);
        Ref(so, "_termFontSource", fontSource);
        so.ApplyModifiedPropertiesWithoutUndo();

        chiefCard.gameObject.SetActive(false);
        window.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static Button BuildChip(RectTransform parent)
    {
        var chip = Img("ChipTemplate", parent, _rounded, new Color(1f, 1f, 1f, 0.1f));
        var button = chip.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 20, 2, 2);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var face = Face(chip.transform, 56f);
        var faceLayout = face.gameObject.AddComponent<LayoutElement>();
        faceLayout.minWidth = faceLayout.preferredWidth = 56f;
        faceLayout.minHeight = faceLayout.preferredHeight = 56f;
        var label = Txt("Label", chip.transform, _body, 22f, Color.white, TextAlignmentOptions.Left);
        label.text = "유닛";
        return button;
    }

    // 실제 유닛 데이터와 드론 아이콘으로 대상 표를 채운다. 족장은 비워 두면 선택된 족장을 쓴다.
    private static void SetTargets(SerializedProperty prop)
    {
        var betan = Unit("Betan");
        var gamman = Unit("Gamman");
        var deltan = Unit("Deltan");
        var zeltan = Unit("Zeltan");
        var entries = new (SelectionFeedbackTarget Target, string Label, Sprite Face, UnitData[] Units)[]
        {
            (SelectionFeedbackTarget.ChiefSkill, "족장", null, new UnitData[0]),
            (SelectionFeedbackTarget.Betan, "베탕", null, new[] { betan }),
            (SelectionFeedbackTarget.Gamman, "감망", null, new[] { gamman }),
            (SelectionFeedbackTarget.Deltan, "델탕", null, new[] { deltan }),
            (SelectionFeedbackTarget.Zeltan, "젤탕", null, new[] { zeltan }),
            (SelectionFeedbackTarget.CombatDroneOwner, "드론 보유 유닛", AssetDatabase.LoadAssetAtPath<Sprite>(DroneIconPath), new[] { betan, gamman, deltan }),
        };
        prop.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var element = prop.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Target").enumValueIndex = (int)entries[i].Target;
            element.FindPropertyRelative("Label").stringValue = entries[i].Label;
            element.FindPropertyRelative("Face").objectReferenceValue = entries[i].Face;
            var units = element.FindPropertyRelative("Units");
            units.arraySize = entries[i].Units.Length;
            for (int u = 0; u < entries[i].Units.Length; u++) units.GetArrayElementAtIndex(u).objectReferenceValue = entries[i].Units[u];
        }
    }

    // ── 도우미 ───────────────────────────────────────────────────────

    private static UnitData Unit(string name)
    {
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"{UnitFolder}/UnitData_{name}.asset");
        if (data == null) Debug.LogWarning($"[SelectionInventory] 유닛 데이터 없음: {name}");
        return data;
    }

    // 흰 테두리 원 + 마스크 안 상반신
    private static SelectionInventoryFace Face(Transform parent, float size)
    {
        var ring = Img("Face", parent, _circle, Color.white);
        ring.rectTransform.sizeDelta = new Vector2(size, size);
        ring.raycastTarget = false;
        var inner = Img("Inner", ring.transform, _circle, FaceBack);
        Place(inner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 6f, size - 6f));
        inner.raycastTarget = false;
        inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var art = Img("Art", inner.transform, null, Color.white);
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        art.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.12f);
        art.rectTransform.sizeDelta = new Vector2(size * 1.25f, size * 1.25f);
        art.preserveAspect = true;
        art.raycastTarget = false;
        var face = ring.gameObject.AddComponent<SelectionInventoryFace>();
        var so = new SerializedObject(face);
        so.FindProperty("_art").objectReferenceValue = art;
        so.ApplyModifiedPropertiesWithoutUndo();
        return face;
    }

    // 테두리(살짝 크게) + 채움 두 장. 루트는 투명 판으로 누름만 받는다.
    private static RectTransform Framed(string name, Transform parent, Color fill, Color border, out Image borderImage, out Image fillImage, Sprite sprite = null)
    {
        sprite ??= _rounded;
        var root = Img(name, parent, sprite, Color.clear);
        borderImage = Img("Border", root.transform, sprite, border);
        Stretch(borderImage.rectTransform, -Outline);
        borderImage.raycastTarget = false;
        fillImage = Img("Fill", root.transform, sprite, fill);
        Stretch(fillImage.rectTransform, 0f);
        fillImage.raycastTarget = false;
        return root.rectTransform;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    private static TMP_Text Txt(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Ref(SerializedObject so, string field, Object value)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[SelectionInventory] 필드 없음: {field}"); return; }
        prop.objectReferenceValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
