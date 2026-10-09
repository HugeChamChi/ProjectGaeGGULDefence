using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>ZIP E 아트 임포트·숫자 폰트 생성과 기존 HackBloom 씬 연결. 씬을 재생성하지 않는다.</summary>
public static class HackSlantedGaugeSetup
{
    private const string ArtPath = "Assets/WorkSpace/USW/UI/HackGaugeE";
    /// <summary>씬에 직접 주입하는 스타일 에셋.</summary>
    public const string StylePath = ArtPath + "/HackSlantedGaugeStyle.asset";

    /// <summary>PNG를 여백 포함 FullRect 스프라이트로 임포트하고 고정 숫자 SDF를 만든다.</summary>
    public static HackSlantedGaugeStyle EnsureStyle()
    {
        string[] pieces = { "strip_bg", "tag_blank", "tag_hack", "tag_ready", "tick_on", "tick_half", "tick_off" };
        foreach (string piece in pieces)
        {
            string path = ArtPath + "/pieces/" + piece + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing ZIP art: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 300f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        string fontPath = ArtPath + "/ChakraPetch-Bold SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(ArtPath + "/ChakraPetch-Bold.ttf");
            if (source == null) throw new InvalidOperationException("Missing Chakra Petch Bold font.");
            font = TMP_FontAsset.CreateFontAsset(source, 96, 24, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
            font.name = "ChakraPetch-Bold SDF";
            if (!font.TryAddCharacters("0123456789/", out string missing))
                throw new InvalidOperationException("Missing gauge glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, fontPath);
            foreach (var texture in font.atlasTextures)
            {
                texture.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, font);
            }
            font.material.name = font.name + " Material";
            // 96px SDF, 24px padding: 0.5 * (24+1) * 12/96 ≈ 1.56px outline at @1x.
            font.material.SetFloat(ShaderUtilities.ID_OutlineWidth, .5f);
            font.material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(13, 16, 36, 255));
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
        }
        font.material.EnableKeyword("OUTLINE_ON");
        // TMP 외곽선은 글자 안쪽도 차지하므로 같은 양의 dilate로 원래 흰 획 두께를 보존한다.
        font.material.SetFloat(ShaderUtilities.ID_FaceDilate, .5f);
        // TMP 외곽선은 글자 안쪽도 차지하므로 같은 양의 dilate로 원래 흰 획 두께를 보존한다.
        font.material.SetFloat(ShaderUtilities.ID_FaceDilate, .5f);
        EditorUtility.SetDirty(font.material);

        var style = AssetDatabase.LoadAssetAtPath<HackSlantedGaugeStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<HackSlantedGaugeStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        style.Background = Sprite("strip_bg");
        style.HackTag = Sprite("tag_hack");
        style.ReadyTag = Sprite("tag_ready");
        style.TickOn = Sprite("tick_on");
        style.TickHalf = Sprite("tick_half");
        style.TickOff = Sprite("tick_off");
        style.Font = font;
        style.FontMaterial = font.material;
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        return style;
    }

    /// <summary>현재 HackBloom 씬에 E 스타일을 연결하고 기본 시안으로 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Apply ZIP E Hack Gauge")]
    public static void ApplyToCurrentScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != HackBloomLabBuilder.ScenePath) throw new InvalidOperationException("Open FxLab_HackBloom first.");
        if (scene.isDirty) throw new InvalidOperationException("Save existing scene edits first.");
        var lab = UnityEngine.Object.FindFirstObjectByType<HackBloomFxLab>();
        if (lab == null) throw new InvalidOperationException("Missing HackBloom driver.");
        var style = EnsureStyle();
        var so = new SerializedObject(lab);
        so.FindProperty("_slantedGaugeStyle").objectReferenceValue = style;
        so.FindProperty("_gaugeDesign").intValue = 7;
        so.FindProperty("_useHud").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        Debug.Log("[HackGaugeE] ZIP E connected to " + scene.path);
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/pieces/" + name + ".png");
}
