using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates a separate lab focused on teleport timing, attack position and motion.</summary>
public static class GammanTeleportLabBuilder
{
    /// <summary>Choreography comparison scene.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_GammanTeleport.unity";
    private const string DataPath = "Assets/WorkSpace/USW/Data/GammanTeleportLab";
    private static readonly Vector3 Above = new Vector3(0, 2.7f, -.4f);
    private static readonly Vector3 Left = new Vector3(-1.6f, 2.2f, -.4f);
    private static readonly Vector3 Right = new Vector3(1.6f, 2.2f, -.4f);

    /// <summary>Build and save five takes; preserves the separate hacking explosion experiment.</summary>
    [MenuItem("Tools/USW/Fx/Build Gamman Teleport Choreography")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Scene already exists.");
        if (!AssetDatabase.CopyAsset(GammanBlinkFxLabBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        Object.DestroyImmediate(Object.FindFirstObjectByType<GammanBlinkFxLab>().gameObject);
        foreach (var shot in Object.FindObjectsByType<SkyLaserFx>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
        foreach (var name in new[] { "HomeTeleportRing", "AirTeleportRing", "BlinkAfterimage_0", "BlinkAfterimage_1", "BlinkAfterimage_2" })
        { var go = GameObject.Find(name); if (go != null) Object.DestroyImmediate(go); }
        if (!AssetDatabase.IsValidFolder(DataPath)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "GammanTeleportLab");
        var material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, DataPath + "/TeleportLines.mat");
        var lab = new GameObject("GammanTeleportFxLab").AddComponent<GammanTeleportFxLab>();
        var so = new SerializedObject(lab);
        Ref(so, "_drone", GameObject.Find("Drone_Home").GetComponent<SpriteRenderer>());
        Ref(so, "_gamman", Object.FindFirstObjectByType<SkeletonAnimation>());
        Ref(so, "_target", GameObject.Find("Target").transform);
        Ref(so, "_fireSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Drone_Yellow_Gammang_2.png"));
        Ref(so, "_lineMaterial", material);
        Ref(so, "_beamMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/WorkSpace/USW/Materials/Fx/SkyLaser/M_SkyLaser_Beam.mat"));
        Ref(so, "_glowMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/WorkSpace/USW/Materials/Fx/SkyLaser/M_SkyLaser_Glow.mat"));
        string[] labels = { "A / SNAP FIRE", "B / AFTERIMAGE", "C / DOUBLE BLINK", "D / CHARGE WARP", "E / AIR STRAFE" };
        string[] descriptions = { "Disappear. A beat of silence. Appear firing.", "A decoy stays below while the real drone fires.", "Left feint, second blink, right-side shot.", "Charge below, warp with energy, release at once.", "Blink overhead, slide sideways while firing." };
        var takes = so.FindProperty("_takes"); takes.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            var take = ScriptableObject.CreateInstance<GammanTeleportTake>();
            take.Label = labels[i]; take.Description = descriptions[i];
            var beats = new List<GammanTeleportTake.Beat> { Home("READY", .75f), Home("RADIO", .28f) };
            if (i == 0)
            {
                beats.Add(Home("TENSE", .08f, squeeze: .8f));
                beats.Add(Home("VANISH", .06f, body: false, snap: true));
                beats.Add(Home("SILENCE", .14f, body: false));
                beats.Add(Air("APPEAR + FIRE", .30f, Above, fire: true, snap: true));
                beats.Add(Air("RECOIL", .12f, Above, to: Above + Vector3.up * .22f));
            }
            else if (i == 1)
            {
                beats.Add(Home("LEAVE DECOY", .08f, body: false, snap: true, ghost: true));
                beats.Add(Home("DECOY ONLY", .16f, body: false, ghost: true));
                beats.Add(Air("SWAP + FIRE", .32f, Left, fire: true, snap: true, ghost: true));
                beats.Add(Air("RECOVER", .13f, Left));
            }
            else if (i == 2)
            {
                beats.Add(Home("VANISH", .07f, body: false, snap: true));
                beats.Add(Home("GAP ONE", .08f, body: false));
                beats.Add(Air("LEFT FEINT", .15f, Left, snap: true));
                beats.Add(Air("SECOND VANISH", .06f, Left, body: false, snap: true));
                beats.Add(Air("GAP TWO", .10f, Left, body: false));
                beats.Add(Air("RIGHT + FIRE", .30f, Right, fire: true, snap: true));
                beats.Add(Air("RECOIL", .12f, Right, to: Right + new Vector3(.18f, .14f, 0)));
            }
            else if (i == 3)
            {
                beats.Add(Home("PRECHARGE", .42f, charge: 1f, squeeze: .2f));
                beats.Add(Home("CHARGED VANISH", .06f, body: false, snap: true));
                beats.Add(Home("GAP", .08f, body: false));
                beats.Add(Air("POINT BLANK FIRE", .34f, new Vector3(.7f, 1.65f, -.4f), fire: true, snap: true));
                beats.Add(Air("KICKBACK", .18f, new Vector3(.7f, 1.65f, -.4f), to: new Vector3(1.1f, 2.15f, -.4f)));
            }
            else
            {
                beats.Add(Home("VANISH", .06f, body: false, snap: true));
                beats.Add(Home("GAP", .12f, body: false));
                beats.Add(Air("AIR LOCK", .08f, Left, snap: true));
                beats.Add(Air("SLIDE + FIRE", .38f, Left, fire: true, to: Right));
                beats.Add(Air("BRAKE", .12f, Right, to: Right + Vector3.right * .15f));
            }
            var last = beats[beats.Count - 1];
            beats.Add(Air("RETURN BLINK", .07f, last.To, body: false, snap: true));
            beats.Add(Home("HOME SNAP", .07f, snap: true));
            beats.Add(Home("HOME", .8f));
            take.Beats = beats.ToArray(); AssetDatabase.CreateAsset(take, DataPath + "/TeleportTake_" + (char)('A' + i) + ".asset");
            takes.GetArrayElementAtIndex(i).objectReferenceValue = take;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var capture = Object.FindFirstObjectByType<FxLabCapture>(); var cap = new SerializedObject(capture);
        Ref(cap, "_target", lab); cap.FindProperty("_duration").floatValue = 20;
        cap.FindProperty("_frameRate").intValue = 30; cap.FindProperty("_outputFolder").stringValue = "Temp/FxCapture/GammanTeleport";
        cap.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene); Selection.activeGameObject = lab.gameObject;
        AddGroup();
    }
    /// <summary>Add actor-count and timing comparison controls to the saved teleport lab.</summary>
    [MenuItem("Tools/USW/Fx/Add Gamman Teleport Group Comparison")]
    public static void AddGroup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open the saved teleport lab first.");
        if (Object.FindFirstObjectByType<GammanTeleportGroupLab>() != null) throw new InvalidOperationException("Group already configured.");
        var original = Object.FindFirstObjectByType<GammanTeleportFxLab>();
        var originalSo = new SerializedObject(original);
        var gamman = (SkeletonAnimation)originalSo.FindProperty("_gamman").objectReferenceValue;
        var drone = (SpriteRenderer)originalSo.FindProperty("_drone").objectReferenceValue;
        var actor = new GameObject("Actor_1");
        original.transform.SetParent(actor.transform, true);
        gamman.transform.SetParent(actor.transform, true);
        drone.transform.SetParent(actor.transform, true);
        var group = new GameObject("GammanTeleportGroupLab").AddComponent<GammanTeleportGroupLab>();
        var members = new GammanTeleportFxLab[5]; members[0] = original;
        Vector3[] homes = { Vector3.zero, new Vector3(-1.25f, -1.05f), new Vector3(1.25f, -1.05f), new Vector3(-1.25f, 1.05f), new Vector3(1.25f, 1.05f) };
        Vector3[] air = { Vector3.zero, new Vector3(-.75f, .12f), new Vector3(.75f, .12f), new Vector3(-1.5f, .28f), new Vector3(1.5f, .28f) };
        for (int i = 1; i < 5; i++)
        {
            var clone = Object.Instantiate(actor); clone.name = "Actor_" + (i + 1);
            var member = clone.GetComponentInChildren<GammanTeleportFxLab>(); members[i] = member;
            var memberSo = new SerializedObject(member);
            memberSo.FindProperty("_home").vector3Value += homes[i];
            var sk = (SkeletonAnimation)memberSo.FindProperty("_gamman").objectReferenceValue;
            var dr = (SpriteRenderer)memberSo.FindProperty("_drone").objectReferenceValue;
            sk.transform.position += homes[i]; dr.transform.position += homes[i];
            memberSo.ApplyModifiedPropertiesWithoutUndo();
        }
        for (int i = 0; i < members.Length; i++)
        {
            var memberSo = new SerializedObject(members[i]);
            Ref(memberSo, "_group", group); memberSo.FindProperty("_airOffset").vector3Value = air[i];
            memberSo.ApplyModifiedPropertiesWithoutUndo();
        }
        var so = new SerializedObject(group);
        var list = so.FindProperty("_members"); list.arraySize = 5;
        var takes = so.FindProperty("_takes"); takes.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
            takes.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GammanTeleportTake>(DataPath + "/TeleportTake_" + (char)('A' + i) + ".asset");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var capture = new SerializedObject(Object.FindFirstObjectByType<FxLabCapture>());
        Ref(capture, "_target", group); capture.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Selection.activeGameObject = group.gameObject;
    }
    private static GammanTeleportTake.Beat Home(string label, float duration, bool body = true, bool snap = false, bool ghost = false, float charge = 0, float squeeze = 0)
        => new GammanTeleportTake.Beat { Label = label, Duration = duration, AtHome = true, Body = body, Snap = snap, HomeGhost = ghost, Charge = charge, Squeeze = squeeze };
    private static GammanTeleportTake.Beat Air(string label, float duration, Vector3 pos, bool body = true, bool fire = false, bool snap = false, bool ghost = false, Vector3? to = null)
        => new GammanTeleportTake.Beat { Label = label, Duration = duration, From = pos, To = to ?? pos, Body = body, Fire = fire, Snap = snap, HomeGhost = ghost };
    private static void Ref(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
}
