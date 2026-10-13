using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 시각 실험실을 보존하고 소리 비교용 별도 씬과 SO를 Unity API로 만든다.</summary>
public static class DisigmanRailgunSoundLabBuilder
{
    /// <summary>독립 사운드 실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_DisigmanRailgunSound.unity";
    private const string Original = "Assets/WorkSpace/USW/Scene/FxLab_DisigmanRailgun.unity";
    private const string Data = "Assets/WorkSpace/USW/Data/Fx/DisigmanRailgunSound/";
    private const string Audio = "Assets/WorkSpace/USW/Audio/Lab/DisigmanRailgun/";
    private static readonly string[] Keys = { "A_Electromagnetic", "B_Heavy", "C_Piercing", "D_Metallic", "E_BtoA" };
    private static readonly string[] Names = { "A · 전자기형", "B · 중량형", "C · 관통형", "D · 금속 공명형", "E · 스킬 B → A" };
    private static readonly string[] Descriptions = {
        "전자기 충전 뒤 짧고 단단한 충격. 기본 권장안.",
        "낮고 묵직한 발사 충격. 스킬의 무게감을 비교.",
        "빠르고 날카로운 발사음. 짧은 잔향과 관통감.",
        "금속성 공명과 전자기 잔향. 기계적인 느낌.",
        "스킬: B 충전·첫 충격 → A 끝음 / 일반 A" };

    /// <summary>별도 사운드 씬을 생성하거나 참조를 복구한다.</summary>
    [MenuItem("Tools/USW/Fx/Build Disigman Railgun Sound Lab")]
    public static void Build() => Debug.Log(BuildScene());

    /// <summary>클린 Edit 모드에서 사운드 시안과 씬을 저작한다.</summary>
    public static string BuildScene()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires clean Edit Mode.");
        AssetDatabase.Refresh(); EnsureFolder(Data.TrimEnd('/'));
        var settings = AssetDatabase.LoadAssetAtPath<DisigmanRailgunSoundSettings>(Data + "Settings.asset");
        if (settings == null) { settings = ScriptableObject.CreateInstance<DisigmanRailgunSoundSettings>(); AssetDatabase.CreateAsset(settings, Data + "Settings.asset"); }
        var variants = new DisigmanRailgunSoundVariant[Keys.Length];
        for (int i = 0; i < Keys.Length; i++)
        {
            var path = Data + Keys[i] + ".asset";
            var variant = AssetDatabase.LoadAssetAtPath<DisigmanRailgunSoundVariant>(path);
            if (variant == null) { variant = ScriptableObject.CreateInstance<DisigmanRailgunSoundVariant>(); AssetDatabase.CreateAsset(variant, path); }
            variant.DisplayName = Names[i]; variant.Description = Descriptions[i];
            variant.Charge = Clip(Keys[i==4?0:i], "Charge"); variant.Basic = Clip(Keys[i==4?0:i], "Basic"); variant.Skill = Clip(Keys[i], "Skill");
            variant.SkillCharge = i==4 ? Clip(Keys[1], "Charge") : null;
            if (i == 0)
            {
                variant.Charge = RefinedA("Charge"); variant.SkillCharge = RefinedA("SkillCharge");
                variant.Basic = RefinedA("Basic"); variant.Skill = RefinedA("Skill");
                variant.Description = "전자기 A · 짧은 디지털 발사 / 패킷 충전과 절제된 글리치 착탄";
            }
            if (Mathf.Abs(variant.Charge.length - settings.ChargeSeconds) > .001f) throw new Exception("Charge length mismatch.");
            EditorUtility.SetDirty(variant); variants[i] = variant;
        }
        AssetDatabase.SaveAssets();
        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(Original, ScenePath)) throw new Exception("Visual lab copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        // A Single scene transition can unload freshly created asset objects; reload after opening.
        settings = AssetDatabase.LoadAssetAtPath<DisigmanRailgunSoundSettings>(Data + "Settings.asset");
        for (int i = 0; i < Keys.Length; i++) variants[i] = AssetDatabase.LoadAssetAtPath<DisigmanRailgunSoundVariant>(Data + Keys[i] + ".asset");
        var old = UnityEngine.Object.FindFirstObjectByType<DisigmanRailgunFxLab>();
        var lab = UnityEngine.Object.FindFirstObjectByType<DisigmanRailgunSoundLab>();
        if (old != null)
        {
            var source = new SerializedObject(old);
            lab = old.gameObject.AddComponent<DisigmanRailgunSoundLab>();
            var target = new SerializedObject(lab);
            foreach (string field in new[] { "_unit", "_idleSprite", "_fireSprite", "_target", "_basicFx", "_skillFx" })
                target.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
            target.FindProperty("_muzzleOffset").vector2Value = source.FindProperty("_muzzleOffset").vector2Value;
            target.ApplyModifiedPropertiesWithoutUndo();
            lab.gameObject.name = "DisigmanRailgunSoundLab";
            UnityEngine.Object.DestroyImmediate(old);
        }
        if (lab == null) throw new Exception("Copied lab missing.");
        foreach (var capture in UnityEngine.Object.FindObjectsByType<FxLabCapture>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(capture);
        var so = new SerializedObject(lab);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Imports/Font/KCC-Ganpan SDF.asset");
        var entries = so.FindProperty("_variants"); entries.arraySize = Keys.Length;
        for (int i = 0; i < Keys.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
        so.FindProperty("_initialVariant").intValue = 4;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Disigmang_1.png");
        if (sprite != null)
        {
            so.FindProperty("_idleSprite").objectReferenceValue = sprite;
            // Only the authored idle illustration is available; the lab does not invent an attack pose.
            so.FindProperty("_fireSprite").objectReferenceValue = sprite;
            var unit = (SpriteRenderer)so.FindProperty("_unit").objectReferenceValue; unit.sprite = sprite;
            unit.gameObject.name = "Disigman_Preview";
            unit.transform.position = new Vector3(-.9f, -.35f, 0f);
            unit.transform.localScale = Vector3.one * (1.25f / Mathf.Max(.01f, sprite.bounds.size.y));
        }
        var boss = GameObject.Find("Boss_Dummy");
        if (boss != null) boss.transform.localScale = Vector3.one * .34f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EnableAudio(); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        return "Saved railgun sound lab: A uses the approved refined combat clips; B-E preserve their audition clips.";
    }

    /// <summary>저장된 사운드 실험실을 연다.</summary>
    [MenuItem("Tools/USW/Fx/Open Disigman Railgun Sound Lab")]
    public static void Open()
    {
        if (!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    private static AudioClip RefinedA(string kind)
    {
        string path = "Assets/WorkSpace/USW/Audio/StudioDartEdited/Disigman_A_Hack_" + kind + ".wav";
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new Exception("Missing refined A WAV " + path);
    }

    private static AudioClip Clip(string key, string kind)
    {
        var path = Audio + key + "_" + kind + ".wav";
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new Exception("Missing authored WAV " + path);
        var sample = importer.defaultSampleSettings; sample.loadType = AudioClipLoadType.DecompressOnLoad;
        sample.compressionFormat = AudioCompressionFormat.PCM; sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        sample.preloadAudioData = true;
        importer.defaultSampleSettings = sample; importer.forceToMono = true; importer.loadInBackground = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void EnableAudio()
    {
        EditorUtility.audioMasterMute = false;
        const string path = "ProjectSettings/AudioManager.asset";
        var backup = "outputs/disigman-audio-lab-20261012/before/AudioManager.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(backup)); if (!File.Exists(backup)) File.Copy(path, backup);
        var asset = AssetDatabase.LoadAllAssetsAtPath(path).First(); var so = new SerializedObject(asset);
        if (so.FindProperty("m_DisableAudio").boolValue)
        {
            so.FindProperty("m_DisableAudio").boolValue = false; so.ApplyModifiedProperties(); EditorUtility.SetDirty(asset);
        }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
