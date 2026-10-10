using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds five body-centred hacking detonations while retaining the original blink comparison.</summary>
public static class GammanHackSignalLabBuilder
{
    /// <summary>Scene for the five revised directions.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_GammanHackSignal.unity";
    private const string DataPath = "Assets/WorkSpace/USW/Data/GammanHackSignalLab";
    private const string MaterialPath = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser/";

    /// <summary>Create a saved scene and five authored visual presets without touching combat data.</summary>
    [MenuItem("Tools/USW/Fx/Build Gamman Hack Signal Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Scene exists; open the existing experiment.");
        if (!AssetDatabase.CopyAsset(GammanBlinkFxLabBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!AssetDatabase.IsValidFolder(DataPath)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "GammanHackSignalLab");
        var lab = Object.FindFirstObjectByType<GammanBlinkFxLab>();
        var target = GameObject.Find("Target").transform;
        foreach (var old in Object.FindObjectsByType<SkyLaserFx>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
        var fx = new GameObject("Boss_ImplantedHacking_VisualOnly").AddComponent<GammanHackDetonationFx>();
        var lineMaterial = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(lineMaterial, DataPath + "/HackingLines.mat");
        var fxSo = new SerializedObject(fx);
        Ref(fxSo, "_target", target);
        Ref(fxSo, "_lineMaterial", lineMaterial);
        Ref(fxSo, "_ringMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "M_SkyLaser_Ring.mat"));
        Ref(fxSo, "_glowMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "M_SkyLaser_Glow.mat"));
        fxSo.ApplyModifiedPropertiesWithoutUndo();
        var labSo = new SerializedObject(lab);
        Ref(labSo, "_detonation", fx);
        var styles = labSo.FindProperty("_styles"); styles.arraySize = 5;
        var shots = labSo.FindProperty("_shots"); shots.arraySize = 5;
        string[] names = { "A / RUPTURE", "B / IMPLODE", "C / CIRCUIT", "D / FRACTURE", "E / BLOOM" };
        string[] descriptions = { "Six implants flash and rupture together", "Implants contract, then burst from within", "Circuit overload breaks into digital shards", "Hacking cracks split across the body", "Six seals open into a luminous blossom" };
        Color[] tints = { new Color(.2f, 1f, .85f), new Color(.45f, .65f, 1f), new Color(.35f, 1f, .45f), new Color(1f, .35f, .16f), new Color(1f, .4f, .85f) };
        float[] holds = { .28f, .38f, .32f, .30f, .36f };
        for (int i = 0; i < 5; i++)
        {
            var style = ScriptableObject.CreateInstance<GammanBlinkStyle>();
            style.Label = names[i]; style.Description = descriptions[i]; style.DetonationPattern = i;
            style.CommandTime = .48f; style.BlinkTime = .16f; style.TransitTime = .08f;
            style.RingSize = .7f; style.AirOffset = new Vector3(0, 2.7f, -.4f);
            style.HackTint = tints[i]; style.BurstDelay = .10f + holds[i] * .68f;
            style.BurstDuration = i == 1 || i == 4 ? .85f : .72f;
            AssetDatabase.CreateAsset(style, DataPath + "/HackSignal_" + (char)('A' + i) + ".asset");
            styles.GetArrayElementAtIndex(i).objectReferenceValue = style;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/Prefab/Effect/SkyLaser/FxSkyLaser_Pulse_Gammang.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "SignalBeam_" + (char)('A' + i);
            var shot = go.GetComponent<SkyLaserFx>();
            var so = new SerializedObject(shot);
            Ref(so, "_target", target);
            so.FindProperty("_pulseCount").intValue = 1;
            Float(so, "_pulseHold", holds[i]); Float(so, "_pulseWidth", .16f);
            Float(so, "_pulseCharge", .05f); Float(so, "_pulseExtend", .04f);
            Float(so, "_pulseFade", .09f); Float(so, "_pulseFinalWidthScale", 1f); Float(so, "_pulseFinalHoldScale", 1f);
            Float(so, "_orbitSpeed", 0); Float(so, "_padDrop", 0); Float(so, "_droneBob", 0); Float(so, "_padAppear", .01f);
            // Suppress the original oversized impact. The implanted marks now own the explosion.
            foreach (string field in new[] { "_impactFlash", "_impactRing", "_impactFlare", "_impactGlow", "_sparks" }) Ref(so, field, null);
            var emitters = so.FindProperty("_emitters"); emitters.arraySize = 1;
            var e = emitters.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("Offset").vector2Value = style.AirOffset;
            e.FindPropertyRelative("Depth").floatValue = style.AirOffset.z;
            var charge = e.FindPropertyRelative("Charge").objectReferenceValue as Renderer;
            if (charge != null) charge.transform.localScale *= .35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            shots.GetArrayElementAtIndex(i).objectReferenceValue = shot;
        }
        labSo.FindProperty("_rest").floatValue = .85f;
        labSo.ApplyModifiedPropertiesWithoutUndo();
        var capture = Object.FindFirstObjectByType<FxLabCapture>();
        var capSo = new SerializedObject(capture);
        capSo.FindProperty("_frameRate").intValue = 30;
        capSo.FindProperty("_duration").floatValue = 24f;
        capSo.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/GammanHackSignal";
        capSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = lab.gameObject;
        Debug.Log("[GammanHackSignal] Five detonation motifs saved: " + ScenePath);
    }
    private static void Ref(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
    private static void Float(SerializedObject so, string name, float value) => so.FindProperty(name).floatValue = value;
}
