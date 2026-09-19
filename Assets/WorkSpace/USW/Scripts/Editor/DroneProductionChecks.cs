using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>합성 지원 큐 및 추가 전투 드론의 수명/대형/군단 등록을 검사한다.</summary>
public static class DroneProductionChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static int _passed;

    /// <summary>사용자 씬을 저장하지 않고 임시 씬에서 검사한다.</summary>
    [MenuItem("Tools/Selections/Run Drone Production Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var random = UnityEngine.Random.state;
        _scene = EditorSceneManager.NewPreviewScene(); _passed = 0;
        CombatDroneCheckProbe beta = null;
        try
        {
            var manager = Component<LevelUpManager>();
            var support = Asset<LevelUpData>(); support.chooseId = 9119;
            DroneSelectionPresets.Configure(support, DroneSelectionKind.MergeSupport);
            var production = Asset<LevelUpData>(); production.chooseId = 9120;
            DroneSelectionPresets.Configure(production, DroneSelectionKind.ExtraCombatDrone);
            Check(support.tier == Tier.Rare && support.droneEffect.Value == 0.1f, "Support preset");
            Check(production.tier == Tier.Epic && production.droneEffect.Count == 1, "Production preset");

            var spawn = Component<SupportSpawnCheckProbe>(); Set(spawn, "_levelUpManager", manager);
            spawn.RequestMergeSupport(); Check(spawn.PendingSupportCount == 0, "No card no reward");
            manager.ApplyEffect(support); UnityEngine.Random.InitState(1919);
            for (int i = 0; i < 10000; i++) spawn.RequestMergeSupport();
            Check(spawn.PendingSupportCount > 850 && spawn.PendingSupportCount < 1150, "Ten percent probability over 10000 merges");
            Set(spawn, "<PendingSupportCount>k__BackingField", 2);
            var grid = Component<GridManager>(); var cell = Component<GridCell>();
            Set(cell, "<Model>k__BackingField", new GridCellModel());
            Set(grid, "_grid", new[,] { { cell } }); Set(spawn, "_gridManager", grid);
            spawn.Reward = Component<FrogArcher>(); cell.TryPlaceUnit(spawn.Reward);
            spawn.ProcessPendingSupport(); Check(spawn.PendingSupportCount == 2 && spawn.Placed == 0, "Full grid retains queue");
            manager.RemoveEffect(support);
            cell.RemoveUnit(); spawn.Fail = true; spawn.ProcessPendingSupport();
            Check(spawn.PendingSupportCount == 2, "Factory failure retains reward");
            spawn.Fail = false; spawn.ProcessPendingSupport();
            Check(spawn.PendingSupportCount == 1 && spawn.Placed == 1 && cell.IsOccupied, "Vacancy pays one and reserves cell");
            spawn.ProcessPendingSupport(); Check(spawn.PendingSupportCount == 1, "Occupied cell cannot receive second reward");
            cell.RemoveUnit(); spawn.ProcessPendingSupport();
            Check(spawn.PendingSupportCount == 0 && spawn.Placed == 2, "Second vacancy drains queue even after card removed");
            Check(Component<UnitSpawner>().PendingSupportCount == 0, "New scene owner has no pending rewards");

            var drones = Component<DroneManager>(); Set(drones, "_levelUpManager", manager);
            beta = Component<CombatDroneCheckProbe>(); beta.unitData = Asset<UnitData>();
            Set(beta, "_formation", AssetDatabase.LoadAssetAtPath<DroneFormationSettings>("Assets/WorkSpace/USW/Data/DroneUnits/BetanFormationSettings.asset"));
            beta.unitData.maxDroneCount.normal = 1; beta.unitData.maxDroneCount.rare = 2;
            beta.unitData.maxDroneCount.epic = 3; beta.unitData.maxDroneCount.legend = 4;
            beta.unitData.attackSpeed.legend = 1; beta.unitData.atk.legend = 10;
            beta.currentTier = Tier.Legend; beta.Init(new UnitDependencies { LevelUpManager = manager });
            Set(beta, "_droneManager", drones); Set(beta, "<currentCell>k__BackingField", cell);
            beta.gameObject.SetActive(true); beta.PlaceForCheck();
            Check(beta.OwnedDroneCount == 4 && drones.DroneCount == 4, "Legend baseline four registered drones");
            int skills = 0; beta.onSkillFull = new UnityEngine.Events.UnityEvent(); beta.onSkillFull.AddListener(() => skills++);
            manager.ApplyEffect(production);
            Check(beta.OwnedDroneCount == 5 && drones.DroneCount == 5 && skills == 0, "Card immediately adds attack drone without skill");
            manager.ApplyEffect(production); Check(beta.OwnedDroneCount == 5, "Duplicate apply does not grow fleet");
            Check(drones.GetRallyDamage(drones.DroneCount, 80) == 400m, "Additional drone contributes to Alphan damage");
            var positions = new HashSet<Vector3>();
            foreach (var drone in drones.Drones)
            {
                positions.Add(drone.HomePosition);
                Check(drone.Owner == beta && drone.Atk == beta.GetAttackDamage(), "Additional drones inherit attack owner");
            }
            Check(positions.Count == 5, "Five distinct home positions");
            var offsets = beta.Formation;
            Check(offsets.Length == 5 && offsets[0].x < offsets[2].x && offsets[2].x < offsets[4].x
                && offsets[4].x < offsets[3].x && offsets[3].x < offsets[1].x
                && Mathf.Approximately(offsets[0].y, offsets[1].y)
                && Mathf.Approximately(offsets[2].y, offsets[3].y)
                && offsets[0].y > offsets[2].y && offsets[2].y > offsets[4].y,
                "Reference order: upper left/right, lower left/right, bottom center");
            manager.RemoveEffect(production); Check(beta.OwnedDroneCount == 4 && drones.DroneCount == 4, "Removal retracts extra drone");
            manager.ApplyEffect(production);
            beta.RemoveForCheck(); Check(drones.DroneCount == 0 && beta.OwnedDroneCount == 0, "Removal unregisters all owned drones");
            manager.RemoveEffect(production); manager.ApplyEffect(production);
            Check(beta.OwnedDroneCount == 0, "Removed owner unsubscribed");
            foreach (Tier tier in new[] { Tier.Normal, Tier.Rare, Tier.Epic, Tier.Legend })
            {
                beta.currentTier = tier;
                Check(beta.CombatDroneCapacity == beta.unitData.maxDroneCount.Get(tier) + (tier >= Tier.Epic ? 1 : 0), "Capacity tier " + tier);
            }
            var gamma = Component<Drone_Gamman>(); gamma.unitData = Asset<UnitData>(); gamma.unitData.maxDroneCount.epic = 1;
            gamma.currentTier = Tier.Epic; gamma.Init(new UnitDependencies { LevelUpManager = manager });
            var delta = Component<Drone_Deltan>(); delta.unitData = gamma.unitData; delta.currentTier = Tier.Epic;
            delta.Init(new UnitDependencies { LevelUpManager = manager });
            Check(gamma.CombatDroneCapacity == 2 && delta.CombatDroneCapacity == 2, "Gamman and Deltan epic two drones");
            Check(!typeof(DroneSpawnerBase).IsAssignableFrom(typeof(Drone_Zeltan)), "Zeltan excluded from spawn extension");
            Debug.Log($"[DroneProductionChecks] PASS {_passed} assertions.");
        }
        finally
        {
            if (beta != null) beta.RemoveForCheck();
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear(); UnityEngine.Random.state = random;
        }
    }
    private static T Component<T>() where T : Component
    { var go = new GameObject(typeof(T).Name); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, _scene); return go.AddComponent<T>(); }
    private static T Asset<T>() where T : ScriptableObject
    { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _passed++; }
}
