using System;
using System.Linq;
using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Spine.Unity;

public static class DeckUpgradeConceptAuthoring
{
    public static string ScenePath(int index) => "Assets/WorkSpace/USW/DeckUpgrade/DroneUpgradeConcept2.unity";

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before authoring.");
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty && !current.path.StartsWith("Assets/WorkSpace/USW/DeckUpgrade/") &&
            !current.GetRootGameObjects().Any(x => x.name.StartsWith("DroneUpgradeConcept_")))
            throw new Exception("Unsaved scene: " + current.path);
        var party = AssetDatabase.LoadAssetAtPath<PartyDataSO>("Assets/WorkSpace/HSD/Data/Party/Dron_Party.asset");
        // NewScene unloads unused assets; retain asset paths and reload after each replacement.
        var paths = party.unitDataList.Where(x => x != null).Select(AssetDatabase.GetAssetPath).ToArray();
        if (paths.Length != 5) throw new Exception("Drone roster must have 5 units; found " + paths.Length);
        for (int i = 0; i < 1; i++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("DroneUpgradeConcept_" + (i + 1));
            var screen = root.AddComponent<DroneUpgradeConceptScreen>();
            screen.style = AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
            screen.units = paths.Select(AssetDatabase.LoadAssetAtPath<UnitData>).ToArray();
            screen.chief = AssetDatabase.LoadAssetAtPath<ChieftainData>("Assets/WorkSpace/USW/Data/Chieftain/AlphanChieftain.asset");
            screen.combatDroneSprites = new[] { "Drone_Normal_Prefab", "Drone_Buffer_Prefab", "Drone_Debuffer_Prefab", "Drone_Normal_Prefab" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/Prefab/Unit/DronUnit/" + name + ".prefab").GetComponent<SpriteRenderer>().sprite).ToArray();
            screen.spineData = new SkeletonDataAsset[screen.units.Length];
            screen.idleAnimations = new string[screen.units.Length];
            screen.attackAnimations = new string[screen.units.Length];
            screen.spineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/Materials/UI-StraightAlphaTex/SkeletonGraphicDefault-Straight.mat");
            screen.spineBlendMaterials = new[] { "Additive", "Multiply", "Screen" }.Select(blend =>
                AssetDatabase.LoadAssetAtPath<Material>("Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/Materials/UI-StraightAlphaTex/SkeletonGraphic" + blend + "-Straight.mat")).ToArray();
            if (screen.spineMaterial == null) throw new Exception("Spine UI material missing");
            for (int unit = 0; unit < screen.units.Length; unit++)
            {
                var prefab = screen.units[unit].prefab;
                var source = prefab != null ? prefab.GetComponentInChildren<SkeletonAnimation>(true) : null;
                if (source == null) continue; // Disigman's source is a SpriteRenderer, not a Spine rig.
                var renderer = source.GetComponent<SkeletonRenderer>();
                screen.spineData[unit] = renderer.SkeletonDataAsset;
                var data = screen.spineData[unit].GetSkeletonData(false);
                screen.idleAnimations[unit] = data.FindAnimation("idle") != null ? "idle"
                    : data.FindAnimation("Idle") != null ? "Idle" : data.Animations.Items[0].Name;
                screen.attackAnimations[unit] = data.FindAnimation("attack") != null ? "attack"
                    : data.FindAnimation("attack_01") != null ? "attack_01"
                    : data.FindAnimation("skill") != null ? "skill" : null;
            }
            const string chiefSource = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Spine_Artwork/SPN_Char_Alphan";
            const string own = "Assets/WorkSpace/USW/DeckUpgrade/AlphanPreview";
            screen.chiefSpine = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(own + "Skeleton.asset");
            if (screen.chiefSpine == null)
            {
                var material = new Material(screen.spineMaterial);
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(chiefSource + ".png");
                AssetDatabase.CreateAsset(material, own + ".mat");
                var atlas = SpineAtlasAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>(chiefSource + ".atlas.txt"), new[] { material }, true);
                AssetDatabase.CreateAsset(atlas, own + "Atlas.asset");
                screen.chiefSpine = SkeletonDataAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>(chiefSource + ".skel.bytes"), atlas, true);
                AssetDatabase.CreateAsset(screen.chiefSpine, own + "Skeleton.asset");
            }
            var chiefData = screen.chiefSpine.GetSkeletonData(false);
            screen.chiefIdle = chiefData.FindAnimation("idle") != null ? "idle" : chiefData.Animations.Items[0].Name;
            screen.BuildView();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10); camera.GetComponent<Camera>().orthographic = true;
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color32(6, 30, 34, 255);
            EditorSceneManager.SaveScene(scene, ScenePath(i));
        }
        AssetDatabase.DeleteAsset("Assets/WorkSpace/USW/DeckUpgrade/Examples");
        AssetDatabase.DeleteAsset("Assets/WorkSpace/USW/DeckUpgrade/RoundedPanel.png");
        AssetDatabase.DeleteAsset("Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeScreen.cs");
        AssetDatabase.DeleteAsset("Assets/WorkSpace/USW/DeckUpgrade/DeckUnitUpgrade.unity");
        AssetDatabase.DeleteAsset("Assets/WorkSpace/USW/DeckUpgrade/DroneUpgradeConcept3.unity");
        EditorSceneManager.OpenScene(ScenePath(0));
        Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>().gameObject;
        return "Kept navy scene only. Circular Alphan upgrade, roomy 3-column list, rising motes, right-edge arrows. Chief animations: " + string.Join(", ", UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>().chiefSpine.GetSkeletonData(false).Animations.Select(x => x.Name));
    }
}
