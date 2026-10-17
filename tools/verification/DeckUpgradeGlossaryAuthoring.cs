using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEngine;
public static class DeckUpgradeGlossaryAuthoring
{
    public static string Build()
    {
        const string folder = "Assets/WorkSpace/USW/DeckUpgrade/Resources";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/DeckUpgrade", "Resources");
        string path = folder + "/DeckUpgradeGlossarySettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<DeckUpgradeGlossarySettings>(path);
        if (settings == null) { settings = ScriptableObject.CreateInstance<DeckUpgradeGlossarySettings>(); AssetDatabase.CreateAsset(settings, path); }
        settings.Glossary = AssetDatabase.LoadAssetAtPath<TermGlossary>("Assets/WorkSpace/USW/Data/UI/TermGlossary.asset");
        settings.BlurShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/WorkSpace/USW/Shaders/UI/UI_SnapshotBlur.shader");
        foreach (string id in new[] { "hack_stack", "self_destruct_drone", "chief_skill" })
            if (settings.Glossary == null || !settings.Glossary.TryFind(id, out var term) || term.Clip == null)
                throw new System.Exception("Missing production term/video: " + id);
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
        return "Runtime settings reference the original choice glossary, all three original video clips and blur shader. Scenes and source glossary unchanged.";
    }
}
