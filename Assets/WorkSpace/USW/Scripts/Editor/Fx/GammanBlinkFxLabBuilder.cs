using System;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds the isolated Gamman blink comparison using existing SkyLaser art.</summary>
public static class GammanBlinkFxLabBuilder
{
    /// <summary>Saved experiment scene.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_GammanBlink.unity";
    private const string Art = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/";
    private const string Styles = "Assets/WorkSpace/USW/Data/GammanBlinkLab";

    /// <summary>Create once without overwriting a reviewed experiment or dirty scene.</summary>
    [MenuItem("Tools/USW/Fx/Build Gamman Blink Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the dirty scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Lab already exists; open it instead.");
        if (!AssetDatabase.CopyAsset("Assets/WorkSpace/USW/Scene/FxLab_SkyLaser.unity", ScenePath)) throw new InvalidOperationException("Cannot copy SkyLaser lab.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        Object.DestroyImmediate(Object.FindFirstObjectByType<SkyLaserFxLab>().gameObject);
        foreach (var shot in Object.FindObjectsByType<SkyLaserFx>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
        var camera = Camera.main;
        var target = GameObject.Find("Target").transform;
        // Keep the battlefield's existing authored grid.
        if (!AssetDatabase.IsValidFolder(Styles)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "GammanBlinkLab");
        var skeletonData = Load<SkeletonDataAsset>(Art + "Spine_Artwork/SPN_Char_Gamman_SkeletonData.asset");
        var gamman = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonData).skeletonAnimation;
        gamman.name = "Gamman_VisualOnly";
        gamman.transform.position = new Vector3(0f, -2.2f, -.1f);
        gamman.transform.localScale = Vector3.one * .17f;
        gamman.Initialize(false);
        gamman.AnimationName = "idle";
        gamman.loop = true;
        gamman.GetComponent<MeshRenderer>().sortingOrder = 10;
        var sprite = Load<Sprite>(Art + "Character_Designs/Drone_Yellow_Gammang_1.png");
        var drone = MakeSprite("Drone_Home", sprite);
        drone.transform.position = new Vector3(-.913f, -1.894f, -.4f);
        var ghosts = new SpriteRenderer[3];
        for (int i = 0; i < ghosts.Length; i++) { ghosts[i] = MakeSprite("BlinkAfterimage_" + i, sprite); ghosts[i].enabled = false; }
        var rings = new Renderer[2];
        for (int i = 0; i < rings.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = i == 0 ? "HomeTeleportRing" : "AirTeleportRing";
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Load<Material>("Assets/WorkSpace/USW/Materials/Fx/SkyLaser/M_SkyLaser_Ring.mat");
            r.sortingLayerName = "FX"; r.sortingOrder = 45; r.enabled = false;
            rings[i] = r;
        }
        var profiles = new GammanBlinkStyle[3];
        var shots = new SkyLaserFx[3];
        string[] labels = { "A / SNAP", "B / PORTAL", "C / ECHO" };
        string[] descriptions = { "Fast blink / direct overhead pulse", "Twin gates / deliberate diagonal strike", "Golden afterimages / sharp side strike" };
        for (int i = 0; i < 3; i++)
        {
            var style = ScriptableObject.CreateInstance<GammanBlinkStyle>();
            style.Label = labels[i]; style.Description = descriptions[i];
            style.AirOffset = new Vector3(i == 0 ? 0f : i == 1 ? 1.1f : -1.1f, 2.7f, -.4f);
            style.CommandTime = i == 1 ? .5f : .3f;
            style.BlinkTime = i == 1 ? .35f : i == 2 ? .24f : .14f;
            style.TransitTime = i == 1 ? .16f : .08f;
            style.RingSize = i == 1 ? 1.5f : .95f;
            style.Portal = i == 1; style.Afterimages = i == 2;
            AssetDatabase.CreateAsset(style, Styles + "/BlinkStyle_" + (char)('A' + i) + ".asset");
            profiles[i] = style;
            var prefab = Load<GameObject>("Assets/WorkSpace/USW/Prefab/Effect/SkyLaser/FxSkyLaser_Pulse_Gammang.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "SinglePulse_" + (char)('A' + i);
            var shot = instance.GetComponent<SkyLaserFx>(); shots[i] = shot;
            var so = new SerializedObject(shot);
            so.FindProperty("_target").objectReferenceValue = target;
            so.FindProperty("_pulseCount").intValue = 1;
            so.FindProperty("_pulseFinalWidthScale").floatValue = 1f;
            so.FindProperty("_pulseFinalHoldScale").floatValue = 1f;
            so.FindProperty("_orbitSpeed").floatValue = 0f;
            so.FindProperty("_padDrop").floatValue = 0f;
            so.FindProperty("_droneBob").floatValue = 0f;
            so.FindProperty("_padAppear").floatValue = .01f;
            var emitters = so.FindProperty("_emitters");
            emitters.arraySize = 1;
            var rig = emitters.GetArrayElementAtIndex(0);
            rig.FindPropertyRelative("Offset").vector2Value = style.AirOffset;
            rig.FindPropertyRelative("Depth").floatValue = style.AirOffset.z;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var lab = new GameObject("GammanBlinkFxLab").AddComponent<GammanBlinkFxLab>();
        var labSo = new SerializedObject(lab);
        References(labSo, "_styles", profiles); References(labSo, "_shots", shots);
        References(labSo, "_ghosts", ghosts); References(labSo, "_rings", rings);
        labSo.FindProperty("_drone").objectReferenceValue = drone;
        labSo.FindProperty("_gamman").objectReferenceValue = gamman;
        labSo.FindProperty("_target").objectReferenceValue = target;
        labSo.FindProperty("_home").vector3Value = drone.transform.position;
        labSo.ApplyModifiedPropertiesWithoutUndo();
        var capture = camera.GetComponent<FxLabCapture>();
        var cap = new SerializedObject(capture);
        cap.FindProperty("_target").objectReferenceValue = lab;
        cap.FindProperty("_duration").floatValue = 12f;
        cap.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/GammanBlink";
        cap.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = lab.gameObject;
        Debug.Log("[GammanBlinkLab] Saved 3 styles: " + ScenePath);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing: " + path);
    private static SpriteRenderer MakeSprite(string name, Sprite sprite)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.sortingLayerName = "FX"; r.sortingOrder = 33;
        r.transform.localScale = Vector3.one * 1.6f;
        return r;
    }
    private static void References(SerializedObject so, string name, Object[] values)
    {
        var p = so.FindProperty(name); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
