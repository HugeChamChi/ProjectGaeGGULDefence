using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 버프·디버프 상태 팝업 실험실 씬(FxLab_StatusPopup)을 만든다 (사용자 요청 2026-10-22).
/// 확정안: 유닛 몸통 위에 "공격속도 ▲"처럼 줄 세워 띄우고, 0.5초 뒤 위로 슥 올라가며 위쪽부터 그라데이션으로 지움(A안), 글꼴 주아.
/// 글꼴·재질·화살표 그림·스타일 SO는 없을 때만 만들고(튜닝 보존), 씬은 매번 덮어쓴다.
/// </summary>
public static class StatusPopupLabBuilder
{
    /// <summary>실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_StatusPopup.unity";
    private const string DataFolderParent = "Assets/WorkSpace/USW/Data/UI";
    private const string DataFolderName = "StatusPopup";
    private const string DataFolder = DataFolderParent + "/" + DataFolderName;
    private const string FontSourcePath = "Assets/WorkSpace/HSD/Fonts/Jua-Regular.ttf";
    private const string FontPath = DataFolder + "/StatusPopupJuaFont.asset";
    private const string BuffMaterialPath = DataFolder + "/StatusPopupJua_Buff.mat";
    private const string DebuffMaterialPath = DataFolder + "/StatusPopupJua_Debuff.mat";
    private const string ArrowFillPath = DataFolder + "/StatusArrowFill.png";
    private const string ArrowStrokePath = DataFolder + "/StatusArrowStroke.png";
    private const string StylePath = DataFolder + "/StatusPopupStyle.asset";
    // 퇴장 타이밍 비교용 변형 (사용자 요청 2026-10-23): A 이전 0.44초 / B 현재 0.3초 / C 상승 0.3초 + 흐려짐 0.44초.
    private const string BeforeStylePath = DataFolder + "/StatusPopupStyle_A_Before.asset";
    private const string MixStylePath = DataFolder + "/StatusPopupStyle_C_Mix.asset";
    private const string UnitSpritePath = "Assets/WorkSpace/USW/Sprites/Unit/Betang_SpinePortrait.png";

    private const int ArrowTextureSize = 64;
    private const float ArrowHalfSide = 17f;
    private const float ArrowRounding = 4f;
    private const float ArrowStrokeWidth = 6f;

    private static readonly Color Grass = new Color(0.30f, 0.46f, 0.23f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.16f, 0.12f, 0.92f);

    private static readonly string[] Titles =
    {
        "상태 팝업 - A안 (주아)",
        "몸통 위 - 0.5초 유지 - 위로 슥 + 위쪽부터 그라데이션",
    };
    private static readonly string[] Row1 = { "버프 1개", "버프 2개", "버프+디버프", "3줄" };
    private static readonly string[] Row2 = { "연달아 걸기", "같은 버프 반복", "전체 유닛", "자동 켜기" };
    // 게임 연결 때 흔히 쓸 능력치 이름도 미리 넣어 둔다(정적 아틀라스).
    private const string ExtraGlyphs =
        "공격속도 공격력 사거리 치명타 확률 피해 스킬 쿨타임 재사용 방어력 이동속도 받는 증가 감소 식량 생산 게이지 충전 자동 끄기 켜기 공격 불가 화상 " +
        "+-%.()0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>실험실 씬을 만들고 연다.</summary>
    [MenuItem("Tools/USW/Fx/Build Status Popup Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[StatusPopupLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        Debug.Log($"[StatusPopupLab] 완료: {ScenePath}");
    }

    /// <summary>씬을 새로 만들어 저장한다 (자동화 진입점).</summary>
    public static string BuildScene()
    {
        // 새 씬을 연 뒤에 에셋을 불러온다. 먼저 불러 두면 씬 전환 때 내려가 스포너 참조가 비는 일이 있었다.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureFolder();
        var font = EnsureFont();
        var style = EnsureStyle(font);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Grass;
        cam.orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var root = new GameObject("StatusPopupLab Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)root.transform;

        var bg = Rect(canvasRt, "Background", 0, 0, 0, 0);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.sizeDelta = Vector2.zero;
        var bgImage = bg.gameObject.AddComponent<Image>(); bgImage.color = Grass; bgImage.raycastTarget = false;

        Text(canvasRt, font, Titles[0], 0, -80, 1000, 70, 46);
        Text(canvasRt, font, Titles[1], 0, -160, 1040, 50, 30).color = new Color(0.9f, 1f, 0.85f);

        // 가짜 유닛 세 마리: 그림 중심 = 몸통 중심.
        var unitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UnitSpritePath);
        // 유닛마다 다른 퇴장 타이밍 스타일을 붙여 나란히 비교한다 (이름표의 숫자 = 상승 초 + 흐려짐 초).
        var looks = new[]
        {
            ("A  0.44", EnsureTimingVariant(BeforeStylePath, 0.44f, 0.44f)),
            ("B  0.3", style),
            ("C  0.3 + 0.44", EnsureTimingVariant(MixStylePath, 0.44f, 0.3f)),
        };
        var units = new RectTransform[looks.Length];
        for (int i = 0; i < units.Length; i++)
        {
            var unit = Rect(canvasRt, "Unit " + (i + 1), -320 + i * 320, -640, 260, 260);
            unit.pivot = new Vector2(0.5f, 0.5f);
            var image = unit.gameObject.AddComponent<Image>();
            image.sprite = unitSprite; image.preserveAspect = true; image.raycastTarget = false;
            if (unitSprite == null) image.color = new Color(0.55f, 0.8f, 0.4f);
            units[i] = unit;
            Text(canvasRt, font, looks[i].Item1, -320 + i * 320, -920, 300, 50, 34);
        }

        var popups = Rect(canvasRt, "Status Popups", 0, 0, 0, 0);
        popups.anchorMin = Vector2.zero; popups.anchorMax = Vector2.one; popups.pivot = new Vector2(0.5f, 0.5f); popups.sizeDelta = Vector2.zero;

        var spawners = new StatusPopupSpawner[looks.Length];
        for (int i = 0; i < looks.Length; i++)
        {
            spawners[i] = new GameObject("Status Popup Spawner " + looks[i].Item1).AddComponent<StatusPopupSpawner>();
            var spawnerSo = new SerializedObject(spawners[i]);
            spawnerSo.FindProperty("_container").objectReferenceValue = popups;
            spawnerSo.FindProperty("_style").objectReferenceValue = looks[i].Item2;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var lab = new GameObject("Status Popup Lab").AddComponent<StatusPopupLab>();
        var actions1 = new UnityAction[] { lab.OneBuff, lab.TwoBuffs, lab.BuffAndDebuff, lab.ThreeLines };
        var actions2 = new UnityAction[] { lab.Chain, lab.RepeatSame, lab.AllUnits, lab.ToggleAuto };
        TMP_Text autoLabel = null;
        for (int i = 0; i < 4; i++)
        {
            var b1 = Button(canvasRt, font, Row1[i], -390 + i * 260, -1420, 240);
            UnityEventTools.AddPersistentListener(b1.onClick, actions1[i]);
            var b2 = Button(canvasRt, font, Row2[i], -390 + i * 260, -1510, 240);
            UnityEventTools.AddPersistentListener(b2.onClick, actions2[i]);
            if (i == 3) autoLabel = b2.GetComponentInChildren<TMP_Text>();
        }

        var labSo = new SerializedObject(lab);
        var spawnersProp = labSo.FindProperty("_spawners"); spawnersProp.arraySize = spawners.Length;
        for (int i = 0; i < spawners.Length; i++) spawnersProp.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];
        var unitsProp = labSo.FindProperty("_units"); unitsProp.arraySize = units.Length;
        for (int i = 0; i < units.Length; i++) unitsProp.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
        labSo.FindProperty("_autoLabel").objectReferenceValue = autoLabel;
        labSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = lab.gameObject;
        return ScenePath;
    }

    private static StatusPopupStyle EnsureStyle(TMP_FontAsset font)
    {
        var style = AssetDatabase.LoadAssetAtPath<StatusPopupStyle>(StylePath);
        bool created = style == null;
        if (created) style = ScriptableObject.CreateInstance<StatusPopupStyle>();

        // 참조만 비어 있으면 채운다. 수치 튜닝은 건드리지 않는다.
        if (style.font == null) style.font = font;
        if (style.buffMaterial == null) style.buffMaterial = EnsureMaterial(BuffMaterialPath, font, style.arrowStrokeColor);
        if (style.debuffMaterial == null) style.debuffMaterial = EnsureMaterial(DebuffMaterialPath, font, style.arrowStrokeColor);
        if (style.arrowFill == null) style.arrowFill = EnsureArrowSprite(ArrowFillPath, 0f);
        if (style.arrowStroke == null) style.arrowStroke = EnsureArrowSprite(ArrowStrokePath, ArrowStrokeWidth);

        if (created) AssetDatabase.CreateAsset(style, StylePath);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        return style;
    }

    // 기본 스타일을 복사해 퇴장 시간만 바꾼 비교용 변형. 이미 있으면 그대로 둔다(튜닝 보존).
    private static StatusPopupStyle EnsureTimingVariant(string path, float fadeDuration, float riseDuration)
    {
        var existing = AssetDatabase.LoadAssetAtPath<StatusPopupStyle>(path);
        if (existing != null) return existing;
        AssetDatabase.CopyAsset(StylePath, path);
        var variant = AssetDatabase.LoadAssetAtPath<StatusPopupStyle>(path);
        variant.exitDuration = fadeDuration;
        variant.exitRiseDuration = riseDuration;
        EditorUtility.SetDirty(variant);
        AssetDatabase.SaveAssetIfDirty(variant);
        return variant;
    }

    // 글자 외곽선 + 아래로 살짝 떨어진 같은 색 그림자 = 통통한 스티커 느낌.
    private static Material EnsureMaterial(string path, TMP_FontAsset font, Color outline)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(font.material);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.34f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, outline);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.28f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static TMP_FontAsset EnsureFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        AssetDatabase.CreateAsset(font, FontPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        font.TryAddCharacters(string.Concat(Titles) + string.Concat(Row1) + string.Concat(Row2) + ExtraGlyphs, out var missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[StatusPopupLab] 주아에 없는 글자: {missing}");
        foreach (var texture in font.atlasTextures)
            if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }

    // 위를 향한 둥근 정삼각형. dilate > 0이면 그만큼 두꺼운 외곽선용 그림.
    private static Sprite EnsureArrowSprite(string path, float dilate)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        int size = ArrowTextureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float covered = 0f;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                var p = new Vector2(x + (sx + 0.5f) / 4f - size * 0.5f, y + (sy + 0.5f) / 4f - size * 0.5f);
                if (EquilateralTriangleDistance(p, ArrowHalfSide) - ArrowRounding - dilate < 0f) covered += 1f;
            }
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, covered / 16f));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 무게중심이 원점인 위쪽 꼭짓점 정삼각형의 부호 거리 (r = 변 길이의 절반, 안쪽 음수).
    private static float EquilateralTriangleDistance(Vector2 p, float r)
    {
        const float k = 1.7320508f;
        p.x = Mathf.Abs(p.x) - r;
        p.y = p.y + r / k;
        if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) * 0.5f;
        p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
        return -p.magnitude * Mathf.Sign(p.y);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder(DataFolderParent, DataFolderName);
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    private static TMP_Text Text(Transform parent, TMP_FontAsset font, string value, float x, float y, float w, float h, float size)
    {
        var text = Rect(parent, "Label", x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = Color.white;
        text.raycastTarget = false; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Button Button(Transform parent, TMP_FontAsset font, string value, float x, float y, float w)
    {
        var r = Rect(parent, value, x, y, w, 76);
        var image = r.gameObject.AddComponent<Image>(); image.color = ButtonColor;
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text(r, font, value, 0, 0, w, 76, 30);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        return button;
    }
}
