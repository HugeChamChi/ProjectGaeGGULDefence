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
/// 보스 HP바 보호막 시안 실험실 씬(FxLab_BossShieldBar)을 만든다 (사용자 요청 2026-10-22).
/// 길이는 B안(바 오른쪽 끝부터 최대HP 비율, 사용자 확정)으로 고정하고, 실제 BossHpBar 프리팹 네 개에
/// "보호막 느낌" 디자인 시안을 하나씩 붙여 나란히 본다: 1 육각 에너지막 / 2 유리 실드 / 3 사선 배리어 / 4 오라+방패.
/// 글꼴·그림·재질은 없을 때만 만들고(튜닝 보존), 씬은 매번 덮어쓴다.
/// </summary>
public static class BossShieldBarLabBuilder
{
    /// <summary>실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_BossShieldBar.unity";
    private const string DataFolderParent = "Assets/WorkSpace/USW/Data/UI";
    private const string DataFolderName = "BossShieldBarLab";
    private const string DataFolder = DataFolderParent + "/" + DataFolderName;
    private const string FontPath = DataFolder + "/BossShieldBarLabFont.asset";
    // 확정안(2번 유리 실드)은 게임이 쓰는 위치에 있다. 나머지 시안 재질·그림은 실험실 폴더.
    private const string SeamPath = "Assets/WorkSpace/HSD/Sprites/BossShieldSeam.png";
    private const string GameMaterialPath = "Assets/WorkSpace/HSD/Materials/BossShieldBand_Glass.mat";
    private const string HaloPath = DataFolder + "/ShieldHalo.png";
    private const string IconPath = DataFolder + "/ShieldIcon.png";
    private const string ShaderPath = "Assets/WorkSpace/HSD/Shaders/UI/BossShieldBand.shader";
    private const string HpBarPrefabPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/BossHpBar.prefab";
    private const string FontSourcePath = "Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound.ttf";
    private const float BarScale = 1.2f;
    private const float BarTop = -215f;
    private const float BarStep = 175f;
    private static readonly Color Sky = new Color(0.31f, 0.31f, 0.54f);
    private const string Glyphs =
        "보스 HP바 보호막 시안 네온 파랑 A안 B안 현재 줄 위 최대 한 줄 오른쪽 끝부터 비율 슬라이더 부여 깨기 직전 제거 육각 에너지막 유리 실드 사선 배리어 오라 방패 아이콘 디자인 광택이 지나감 " +
        "작은 큰 피격 자동 끄기 리셋 모두 로 길이 기준 · = % ( ) / , . + - 0123456789 " +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>실험실 씬을 만들고 연다.</summary>
    [MenuItem("Tools/USW/Fx/Build Boss Shield Bar Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[BossShieldBarLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        Debug.Log($"[BossShieldBarLab] 완료: {ScenePath}");
    }

    /// <summary>씬을 새로 만들어 저장한다 (자동화 진입점).</summary>
    public static string BuildScene()
    {
        EnsureFolder();
        var seam = EnsureSeamSprite();
        var font = EnsureFont();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var root = new GameObject("ShieldLab Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)root.transform;

        var bg = Rect(canvasRt, "Background", 0, 0, 0, 0);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.sizeDelta = Vector2.zero;
        var bgImage = bg.gameObject.AddComponent<Image>(); bgImage.color = Sky; bgImage.raycastTarget = false;

        Text(canvasRt, font, "보스 HP바 보호막 디자인 시안 · 길이 B안", 0, -60, 1000, 60, 38);
        var looks = new[]
        {
            ("1 · 육각 에너지막", EnsureMaterial("Hex", 1f, 0.72f, 3.2f, 0.06f, 0.9f, new Vector4(0.3f, 0.12f), 1.4f, 0.45f, 0f, 0.45f), new Color(0.1f, 0.5f, 1f, 0.92f), true, false, false),
            ("2 · 유리 실드 (광택이 지나감)", EnsureMaterial("Glass", 0f, 0.6f, 2.2f, 0.05f, 0f, Vector4.zero, 1f, 0.55f, 0.9f, 0f), new Color(0.2f, 0.62f, 1f, 0.85f), true, false, false),
            ("3 · 사선 배리어", EnsureMaterial("Stripe", 2f, 0.72f, 2.4f, 0.08f, 0.55f, new Vector4(0.7f, 0f), 1.4f, 0.35f, 0f, 0.2f), new Color(0.12f, 0.55f, 1f, 0.92f), true, false, false),
            ("4 · 오라 + 방패 아이콘", EnsureMaterial("Aura", 1f, 0.72f, 3.2f, 0.06f, 0.55f, new Vector4(0.3f, 0.12f), 1.2f, 0.45f, 0f, 0.35f), new Color(0.1f, 0.5f, 1f, 0.92f), false, true, true),
        };
        var halo = EnsureGeneratedSprite(HaloPath, 64, 64, HaloAlpha, new Vector4(30, 30, 30, 30));
        var icon = EnsureGeneratedSprite(IconPath, 96, 96, null, Vector4.zero, IconColor);
        var shieldBars = new UI_BossHpShield[looks.Length];
        for (int i = 0; i < looks.Length; i++)
        {
            float y = BarTop - i * BarStep;
            Text(canvasRt, font, looks[i].Item1, 0, y + 58, 1000, 40, 28).color = new Color(0.75f, 0.95f, 1f);
            var (_, material, bandColor, useSeam, useHalo, useIcon) = looks[i];
            shieldBars[i] = CreateBar(canvasRt, "BossHpBar " + (i + 1), y, useSeam ? seam : null, UI_BossHpShield.ShieldSpan.WholeBar,
                material, bandColor, useHalo ? halo : null, useIcon ? icon : null);
        }

        float controlsTop = BarTop - looks.Length * BarStep - 10f;
        var status = Text(canvasRt, font, "HP", 0, controlsTop, 1040, 50, 30);
        Text(canvasRt, font, "보호막 슬라이더 (0 ~ 30%)", 0, controlsTop - 70, 1000, 40, 26);
        var slider = CreateSlider(canvasRt, controlsTop - 120);

        var lab = new GameObject("Boss Shield Bar Lab").AddComponent<BossShieldBarLab>();
        var row1 = new[] { ("10% 부여", (UnityAction)lab.Grant10), ("30% 부여", lab.Grant30), ("깨기 직전", lab.NearBreak), ("제거", lab.ClearShield) };
        var row2 = new[] { ("작은 피격", (UnityAction)lab.SmallHit), ("큰 피격", lab.BigHit), ("자동 피격", lab.ToggleAuto), ("HP 리셋", lab.ResetBoss) };
        TMP_Text autoLabel = null;
        for (int i = 0; i < 4; i++)
        {
            var b1 = Button(canvasRt, font, row1[i].Item1, -390 + i * 260, controlsTop - 200, 240);
            UnityEventTools.AddPersistentListener(b1.onClick, row1[i].Item2);
            var b2 = Button(canvasRt, font, row2[i].Item1, -390 + i * 260, controlsTop - 290, 240);
            UnityEventTools.AddPersistentListener(b2.onClick, row2[i].Item2);
            if (i == 2) autoLabel = b2.GetComponentInChildren<TMP_Text>();
        }

        var so = new SerializedObject(lab);
        var bars = so.FindProperty("_bars"); bars.arraySize = shieldBars.Length;
        var shields = so.FindProperty("_shields"); shields.arraySize = shieldBars.Length;
        for (int i = 0; i < shieldBars.Length; i++)
        {
            bars.GetArrayElementAtIndex(i).objectReferenceValue = shieldBars[i].GetComponent<UI_BossHpBar>();
            shields.GetArrayElementAtIndex(i).objectReferenceValue = shieldBars[i];
        }
        so.FindProperty("_status").objectReferenceValue = status;
        so.FindProperty("_shieldSlider").objectReferenceValue = slider;
        so.FindProperty("_autoLabel").objectReferenceValue = autoLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = lab.gameObject;
        return ScenePath;
    }

    // 실제 HP바 프리팹을 놓고 보호막 띠를 붙인다. 참조·보간 값은 HP바에서 그대로 복사한다.
    private static UI_BossHpShield CreateBar(RectTransform parent, string name, float y, Sprite seam, UI_BossHpShield.ShieldSpan span,
        Material material, Color bandColor, Sprite halo, Sprite icon)
    {
        var holder = Rect(parent, name + " Holder", 0, y, 682.5f, 100f);
        holder.localScale = Vector3.one * BarScale;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        instance.name = name;
        var bar = instance.GetComponent<UI_BossHpBar>();
        // 게임 프리팹에 이미 확정안이 붙어 있으면 그 컴포넌트의 시안 값만 이 씬에서 덮어쓴다.
        var shield = instance.GetComponent<UI_BossHpShield>();
        if (shield == null) shield = instance.AddComponent<UI_BossHpShield>();
        var from = new SerializedObject(bar);
        var to = new SerializedObject(shield);
        foreach (var field in new[] { "_hpFillMask", "_hpColor", "_hpBackground" })
            to.FindProperty(field).objectReferenceValue = from.FindProperty(field).objectReferenceValue;
        to.FindProperty("_fillSpriteHorizontalPadding").vector2Value = from.FindProperty("_fillSpriteHorizontalPadding").vector2Value;
        to.FindProperty("_tweenDuration").floatValue = from.FindProperty("_tweenDuration").floatValue;
        to.FindProperty("_damageEase").enumValueIndex = from.FindProperty("_damageEase").enumValueIndex;
        to.FindProperty("_span").enumValueIndex = (int)span;
        to.FindProperty("_seamSprite").objectReferenceValue = seam;
        to.FindProperty("_bandMaterial").objectReferenceValue = material;
        to.FindProperty("_bandColor").colorValue = bandColor;
        to.FindProperty("_haloSprite").objectReferenceValue = halo;
        to.FindProperty("_iconSprite").objectReferenceValue = icon;
        to.ApplyModifiedPropertiesWithoutUndo();
        return shield;
    }

    private static Slider CreateSlider(RectTransform parent, float y)
    {
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var r = Rect(parent, "Shield Slider", 0, y, 900, 50);
        var slider = r.gameObject.AddComponent<Slider>();
        var back = Rect(r, "Background", 0, 0, 0, 0); Stretch(back, 0.3f, 0.7f);
        var backImage = back.gameObject.AddComponent<Image>(); backImage.sprite = uiSprite; backImage.type = Image.Type.Sliced;
        backImage.color = new Color(0.1f, 0.12f, 0.2f, 1f);
        var area = Rect(r, "Fill Area", 0, 0, 0, 0); Stretch(area, 0.3f, 0.7f); area.offsetMin = new Vector2(10, 0); area.offsetMax = new Vector2(-10, 0);
        var fill = Rect(area, "Fill", 0, 0, 0, 0); Stretch(fill, 0f, 1f);
        var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.sprite = uiSprite; fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.25f, 0.85f, 1f, 1f);
        var handleArea = Rect(r, "Handle Slide Area", 0, 0, 0, 0); Stretch(handleArea, 0f, 1f); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
        var handle = Rect(handleArea, "Handle", 0, 0, 50, 0); Stretch(handle, 0f, 1f); handle.sizeDelta = new Vector2(50, 0);
        var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.sprite = knob;
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
        slider.minValue = 0f; slider.maxValue = 0.3f; slider.value = 0f;
        return slider;
    }

    private static void Stretch(RectTransform r, float minY, float maxY)
    {
        r.anchorMin = new Vector2(0f, minY); r.anchorMax = new Vector2(1f, maxY);
        r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder(DataFolderParent, DataFolderName);
    }

    // 세로 빛줄기: 가운데 밝은 심 + 넓게 퍼지는 은은한 빛, 위아래 끝은 흐려진다.
    private static Sprite EnsureSeamSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SeamPath);
        if (existing != null) return existing;
        const int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f);
            float dy = Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
            float core = Mathf.Exp(-Mathf.Pow(dx * 7f, 2f)) + 0.4f * Mathf.Exp(-Mathf.Pow(dx * 2.4f, 2f));
            float fade = 1f - Mathf.SmoothStep(0.6f, 1f, dy);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(core) * fade));
        }
        tex.Apply();
        File.WriteAllBytes(SeamPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SeamPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SeamPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SeamPath);
    }

    // 시안별 띠 재질. 이미 있으면 그대로 둔다 (인스펙터 튜닝 보존).
    private static Material EnsureMaterial(string name, float style, float fillAlpha, float scale, float line, float pattern,
        Vector4 scroll, float rim, float gloss, float sweep, float shimmer)
    {
        string path = name == "Glass" ? GameMaterialPath : DataFolder + "/ShieldBand_" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
        material.SetFloat("_Style", style);
        material.SetFloat("_FillAlpha", fillAlpha);
        material.SetFloat("_PatternScale", scale);
        material.SetFloat("_PatternLine", line);
        material.SetFloat("_PatternStrength", pattern);
        material.SetVector("_Scroll", scroll);
        material.SetFloat("_RimStrength", rim);
        material.SetFloat("_Gloss", gloss);
        material.SetFloat("_Sweep", sweep);
        material.SetFloat("_Shimmer", shimmer);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // 바 뒤 오라: 가운데는 꽉 차고 바깥으로 부드럽게 사라지는 둥근 사각형.
    private static float HaloAlpha(float x, float y)
    {
        const float inset = 24f, radius = 10f;
        float dx = Mathf.Max(0f, Mathf.Abs(x - 32f) - (32f - inset));
        float dy = Mathf.Max(0f, Mathf.Abs(y - 32f) - (32f - inset));
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Exp(-(d / radius) * (d / radius) * 2.2f);
    }

    // 방패 아이콘: 검은 외곽선 + 밝은 몸통(Image 색으로 물듦) + 왼쪽 위 하이라이트.
    private static Color IconColor(float x, float y)
    {
        float u = (x - 48f) / 48f, v = y / 96f;
        float outer = ShieldShape(u, v, 1f), inner = ShieldShape(u, v, 0.8f), shine = ShieldShape(u + 0.22f, v - 0.08f, 0.38f);
        if (inner > 0.5f) return shine > 0.5f && u < 0f ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f);
        return outer > 0.5f ? new Color(0.05f, 0.08f, 0.12f, 1f) : Color.clear;
    }

    private static float ShieldShape(float u, float v, float scale)
    {
        // 중심(0, 0.52) 기준으로 줄여 같은 모양의 안쪽 영역을 만든다.
        u /= scale; v = 0.52f + (v - 0.52f) / scale;
        float top = 0.93f - 0.06f * u * u;
        if (v > top || v < 0.04f) return 0f;
        float half = v >= 0.5f ? 0.8f : 0.8f * Mathf.Pow(Mathf.Clamp01((v - 0.04f) / 0.46f), 0.6f);
        return Mathf.Abs(u) <= half ? 1f : 0f;
    }

    // 4x4 슈퍼샘플로 가장자리를 부드럽게 만든 생성 그림. 이미 있으면 그대로 둔다.
    private static Sprite EnsureGeneratedSprite(string path, int w, int h, System.Func<float, float, float> alpha, Vector4 border,
        System.Func<float, float, Color> color = null)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color sum = Color.clear;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                float px = x + (sx + 0.5f) / 4f, py = y + (sy + 0.5f) / 4f;
                Color c = color != null ? color(px, py) : new Color(1f, 1f, 1f, alpha(px, py));
                sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
            sum /= 16f;
            tex.SetPixel(x, y, sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear);
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
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TMP_FontAsset EnsureFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        AssetDatabase.CreateAsset(font, FontPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        font.TryAddCharacters(Glyphs, out _);
        foreach (var texture in font.atlasTextures)
            if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
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
        var r = Rect(parent, value, x, y, w, 70);
        var image = r.gameObject.AddComponent<Image>(); image.color = new Color(0.12f, 0.16f, 0.26f, 0.96f);
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text(r, font, value, 0, 0, w, 70, 28);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        return button;
    }
}
