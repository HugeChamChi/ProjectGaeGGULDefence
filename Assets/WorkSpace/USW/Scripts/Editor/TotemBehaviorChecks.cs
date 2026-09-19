using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>TD 토템의 실제 컴포넌트/데이터를 임시 Preview Scene에서 검증한다. 사용자 자산을 저장하지 않는다.</summary>
public static class TotemBehaviorChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
    private static readonly List<TotemBase> _totems = new List<TotemBase>();
    private static int _passed;

    /// <summary>Tools 메뉴 또는 배치 executeMethod에서 실행한다.</summary>
    [MenuItem("Tools/Totems/Run Behavior Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these checks outside Play Mode.");
        _passed = 0;
        _scene = EditorSceneManager.NewPreviewScene();
        try
        {
            CheckSheetIsolation();
            CheckCharge();
            CheckTierAndMerge();
            var grid = CreateGrid();
            var manager = Component<TotemBuffManager>();
            Set(manager, "_gridManager", grid);
            CheckGrowth(grid, manager);
            CheckBonusTargets(grid, manager);
            CheckBonusFiring(grid, manager);
            CheckArmorRouting(grid, manager);
            CheckTierRange(grid, manager);
            CheckSpawnSpace(grid);
            Debug.Log($"[TotemBehaviorChecks] PASS {_passed} assertions.");
        }
        finally
        {
            foreach (var totem in _totems) if (totem != null && totem.IsActive) totem.OnRemoved();
            _totems.Clear();
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }
    }

    private static void CheckSheetIsolation()
    {
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.totemName = "SO";
        var conditional = new ConditionalBuffFunction();
        data.functions.Add(conditional);
        data.ApplySheetData(new GameDataManager.TotemSheetRow { TotemName = "Sheet", AtkIncreaseRate = 0.8f });
        Check(data.totemName == "SO" && data.functions.Count == 1 && data.functions[0] == conditional,
            "SO mode preserves authored functions and identity");
        data.UseSheetData = true;
        data.ApplySheetData(new GameDataManager.TotemSheetRow { TotemName = "Sheet", AtkIncreaseRate = 0.8f });
        Check(data.totemName == "Sheet" && Mathf.Approximately(data.GetSimpleAmount(StatKind.AttackPercent), 0.8f),
            "legacy sheet mode still applies data");
    }

    private static void CheckCharge()
    {
        var charge = new TotemSpawnCharge();
        charge.Advance(39, 40);
        Check(!charge.IsReady(40), "no early spawn");
        charge.Advance(1, 40);
        Check(charge.IsReady(40) && charge.Progress(40) == 1f, "ready at exact interval");
        charge.Advance(400, 40);
        Check(charge.Progress(40) == 1f, "full grid holds one charge without backlog");
        charge.Consume();
        Check(!charge.IsReady(40) && charge.Progress(40) == 0f, "successful spawn consumes charge");
        charge.Advance(0, 40);
        Check(charge.Progress(40) == 0f, "paused time does not charge");
    }

    private static void CheckTierAndMerge()
    {
        var original = Asset<UnitData>();
        original.atk.normal = 10;
        var upper = Asset<UnitData>();
        upper.atk.rare = 80;
        upper.skillData = Asset<SkillData>();
        var first = Unit<FrogArcher>(original);
        var second = Unit<FrogArcher>(original);
        var different = Unit<FrogArcher>(Asset<UnitData>());
        var joker = Unit<WildcardUnit>(Asset<UnitData>());
        var source = Component<TotemTemporaryTierBoost>();
        var otherSource = Component<TotemTemporaryTierBoost>();
        Check(first.TryApplyTemporaryTier(source, upper), "normal unit can gain one tier");
        Check(first.currentTier == Tier.Rare && first.unitData.skillData == upper.skillData &&
            first.unitData.atk.Get(first.currentTier) == 80, "full upper combat data including skill is active");
        Check(first.OriginalData == original && first.OriginalTier == Tier.Normal, "ownership remains original");
        Check(UnitMergeRules.CanPair(first, second), "boosted unit merges using original data and tier");
        Check(!UnitMergeRules.CanPair(first, different), "ordinary different types do not merge");
        Check(UnitMergeRules.CanPair(first, joker) && UnitMergeRules.CanPair(joker, first), "joker pairing is symmetric");
        var otherJoker = Unit<WildcardUnit>(joker.unitData);
        Check(!UnitMergeRules.CanPair(joker, otherJoker), "two wildcards cannot merge even with matching data");
        Check(!UnitMergeRules.CanPair(first, first), "cannot merge with self");
        Check(!first.TryApplyTemporaryTier(otherSource), "temporary upgrades never stack");
        first.RemoveTemporaryTier(otherSource);
        Check(first.HasTemporaryTier, "unrelated source cannot remove upgrade");
        first.RestoreOriginalTier();
        Check(first.unitData == original && first.currentTier == Tier.Normal, "sale preparation restores original stats");
        first.currentTier = Tier.Legend;
        Check(!first.TryApplyTemporaryTier(source), "legend is capped");
        first.currentTier = Tier.Chieftain;
        Check(!first.TryApplyTemporaryTier(source), "chieftain excluded");
        Check(joker.TryApplyTemporaryTier(source) && joker.OriginalTier == Tier.Normal, "joker keeps normal ownership while boosted");
        joker.RestoreOriginalTier();
        second.currentTier = Tier.Rare;
        Check(!UnitMergeRules.CanPair(joker, second), "joker cannot merge with rare");
        Check(!joker.CanBasicAttack && !joker.CanAutoSkill && joker.GetBaseFoodPerSecond() == 0f,
            "joker has no combat or production behavior");
    }

    private static void CheckGrowth(GridManager grid, TotemBuffManager manager)
    {
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.functions.Add(new SimpleBuffFunction { kind = StatKind.AttackPercent, amount = 0.05f });
        for (int stage = 0; stage < 3; stage++)
        {
            var preset = new TotemGrowthStage { RequiredKills = stage };
            for (int y = -1; y < stage + 2; y++)
            for (int x = -1; x < stage + 2; x++) preset.Offsets.Add(new Vector2Int(x, y));
            data.GrowthStages.Add(preset);
        }
        var totem = Place<TotemKillRangeGrowth>(grid, manager, data, new Vector2Int(2, 2));
        Check(totem.GetAffectedCells().Count == 9 && totem.KillCount == 0, "new placement starts at 3x3");
        Call(totem, "OnBossKilled");
        Check(totem.GetAffectedCells().Count == 16, "first kill expands to 4x4");
        Call(totem, "OnBossKilled");
        Call(totem, "OnBossKilled");
        Check(totem.GetAffectedCells().Count == 25 && totem.KillCount == 2, "second kill reaches capped 5x5");
        Check(Mathf.Approximately(grid.GetCell(3, 3).Model.GetTotemCellBonus(StatKind.AttackPercent), 0.05f),
            "growth does not accumulate attack strength");
        var origin = totem.CurrentCell;
        totem.OnRemoved();
        origin.RemoveTotem();
        totem.OnPlaced(origin);
        origin.TryPlaceTotem(totem);
        Check(totem.KillCount == 2, "moving same totem preserves growth");
        totem.Rotate();
        Check(totem.KillCount == 2, "rotation preserves growth");
        totem.OnRemoved();
        origin.RemoveTotem();
        Check(grid.GetCell(3, 3).Model.GetTotemCellBonus(StatKind.AttackPercent) == 0f, "removal clears cell buff");
    }

    private static void CheckBonusTargets(GridManager grid, TotemBuffManager manager)
    {
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.right } });
        var unit = Unit<FrogArcher>(Asset<UnitData>());
        var cell = grid.GetCell(3, 2);
        cell.TryPlaceUnit(unit);
        Set(unit, "<currentCell>k__BackingField", cell);
        var totem = Place<TotemBonusProjectile>(grid, manager, data, new Vector2Int(2, 2));
        var listeners = (IDictionary)Get(totem, "_listeners");
        Check(listeners.Count == 1, "bonus attack attaches to affected unit");
        totem.PaintAffectedCells();
        totem.PaintAffectedCells();
        Check(listeners.Count == 1, "cell repaint never duplicates attack subscription");
        totem.Rotate();
        Check(listeners.Count == 0, "rotation detaches previous target");
        totem.Rotate(); totem.Rotate(); totem.Rotate();
        Check(listeners.Count == 1, "rotating back attaches once");
        totem.OnRemoved();
        grid.GetCell(2, 2).RemoveTotem();
        Check(listeners.Count == 0, "totem removal detaches all attack subscriptions");
        cell.RemoveUnit();
    }

    private static void CheckBonusFiring(GridManager grid, TotemBuffManager manager)
    {
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.BonusProjectile.Chance = 1;
        data.BonusProjectile.AttackCoefficient = 0.5f;
        data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.right } });
        var unitData = Asset<UnitData>();
        unitData.atk.normal = 100;
        var unit = Unit<FrogArcher>(unitData);
        unit.Init(new UnitDependencies());
        var cell = grid.GetCell(3, 2);
        cell.TryPlaceUnit(unit);
        Set(unit, "<currentCell>k__BackingField", cell);
        var bosses = Component<BossManager>();
        var firstBoss = Component<BossNormal>();
        firstBoss.Init(1000);
        Set(firstBoss, "_defenseScale", 500d);
        var bossList = (List<BossBase>)Get(bosses, "_currentBosses");
        bossList.Add(firstBoss);
        var projectile = Component<TotemCheckProjectile>();
        var pool = Component<ProjectilePool>();
        Set(pool, "_pool", new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile));
        var totem = Place<TotemBonusProjectile>(grid, manager, data, new Vector2Int(2, 2));
        Set(totem, "_bosses", bosses);
        Set(totem, "_projectiles", pool);
        unit.onAttack.Invoke();
        Check(projectile.LaunchCount == 1 && firstBoss.CurrentHp == 1000, "bonus damage waits for projectile impact");
        var nextBoss = Component<BossNormal>();
        nextBoss.Init(1000);
        bossList[0] = nextBoss;
        projectile.Complete();
        Check(firstBoss.CurrentHp == 950 && nextBoss.CurrentHp == 1000, "50 percent shot retains its original target");
        data.BonusProjectile.Chance = 0;
        unit.onAttack.Invoke();
        Check(projectile.LaunchCount == 1, "zero chance produces no shot");
        totem.OnRemoved();
        grid.GetCell(2, 2).RemoveTotem();
        cell.RemoveUnit();
    }

    private static void CheckArmorRouting(GridManager grid, TotemBuffManager manager)
    {
        var settings = Resources.Load<DebuffSettings>("DebuffSettings");
        var catalog = new DebuffCatalog(settings);
        int armorId = 0;
        foreach (var definition in settings.CreateDefinitions())
            if (definition.Kind == DebuffKind.ArmorBreak) armorId = definition.Id;
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        Set(data, "_debuffBinding", new DebuffBinding(armorId, DebuffTrigger.AffectedUnitBasicAttackAttempt, 1));
        data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.right } });
        var other = Asset<TotemData>();
        other.UseSheetData = false;
        Set(other, "_debuffBinding", new DebuffBinding(armorId, DebuffTrigger.AffectedUnitBasicAttackAttempt, 1));
        other.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.left } });
        var first = Place<GenericBuffTotem>(grid, manager, data, new Vector2Int(2, 2));
        var second = Place<GenericBuffTotem>(grid, manager, other, new Vector2Int(4, 2));
        var boss = Component<BossNormal>();
        boss.ConfigureDebuffs(catalog, settings, null, 0, null);
        boss.Init(1000);
        manager.ApplyAttackDebuff(grid.GetCell(3, 2), boss);
        Check(boss.Debuffs.Active.Count == 1 && boss.Debuffs.Active[0].Stacks == 1, "overlapping armor totems add only one stack");
        manager.RebuildCellBuffFlags();
        Check(boss.Debuffs.Active[0].Stacks == 1, "repainting never applies a debuff");
        first.OnRemoved(); second.OnRemoved();
        grid.GetCell(2, 2).RemoveTotem(); grid.GetCell(4, 2).RemoveTotem();
        manager.ApplyAttackDebuff(grid.GetCell(3, 2), boss);
        Check(boss.Debuffs.Active[0].Stacks == 1, "removal keeps existing stacks but stops new applications");
    }

    private static void CheckTierRange(GridManager grid, TotemBuffManager manager)
    {
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.right } });
        var unit = Unit<FrogArcher>(Asset<UnitData>());
        var cell = grid.GetCell(3, 2);
        cell.TryPlaceUnit(unit);
        Set(unit, "<currentCell>k__BackingField", cell);
        var totem = Place<TotemTemporaryTierBoost>(grid, manager, data, new Vector2Int(2, 2));
        Check(unit.currentTier == Tier.Rare, "range entry promotes unit");
        totem.PaintAffectedCells();
        Check(unit.currentTier == Tier.Rare, "repaint does not promote twice");
        cell.RemoveUnit();
        Set(unit, "<currentCell>k__BackingField", grid.GetCell(4, 2));
        totem.PaintAffectedCells();
        Check(unit.currentTier == Tier.Normal, "range exit restores original tier");
        cell.TryPlaceUnit(unit);
        Set(unit, "<currentCell>k__BackingField", cell);
        totem.PaintAffectedCells();
        Check(unit.currentTier == Tier.Rare, "re-entry promotes again");
        totem.OnRemoved();
        grid.GetCell(2, 2).RemoveTotem();
        Check(unit.currentTier == Tier.Normal, "source removal restores tier");
        cell.RemoveUnit();
    }

    private static void CheckSpawnSpace(GridManager grid)
    {
        var origin = new Vector2Int(3, 3);
        var occupied = Unit<WildcardUnit>(Asset<UnitData>());
        foreach (var cell in grid.AllCells()) cell.TryPlaceUnit(occupied);
        Check(TotemWildcardSpawner.FindNearestAvailableCell(grid, origin) == null, "full grid keeps pending spawn");
        grid.GetCell(0, 0).RemoveUnit();
        grid.GetCell(4, 3).RemoveUnit();
        Check(TotemWildcardSpawner.FindNearestAvailableCell(grid, origin) == grid.GetCell(4, 3), "nearest adjacent cell wins");
        grid.GetCell(4, 3).TryPlaceUnit(occupied);
        Check(TotemWildcardSpawner.FindNearestAvailableCell(grid, origin) == grid.GetCell(0, 0), "search expands outward");
    }

    private static GridManager CreateGrid()
    {
        var grid = Component<GridManager>();
        var config = Asset<GameConfig>();
        config.gridColumns = config.gridRows = 7;
        Set(grid, "config", config);
        var cells = new GridCell[7, 7];
        for (int y = 0; y < 7; y++)
        for (int x = 0; x < 7; x++)
        {
            var cell = Component<GridCell>();
            Set(cell, "<Model>k__BackingField", new GridCellModel());
            cell.Init(new Vector2Int(x, y));
            cells[x, y] = cell;
        }
        Set(grid, "_grid", cells);
        return grid;
    }

    private static T Place<T>(GridManager grid, TotemBuffManager manager, TotemData data, Vector2Int position) where T : TotemBase
    {
        var totem = Component<T>();
        Set(totem, "totemData", data);
        Set(totem, "_gridManager", grid);
        Set(totem, "_totemBuffManager", manager);
        grid.GetCell(position).TryPlaceTotem(totem);
        _totems.Add(totem);
        totem.OnPlaced(grid.GetCell(position));
        return totem;
    }

    private static T Unit<T>(UnitData data) where T : UnitBase
    {
        var unit = Component<T>();
        unit.unitData = data;
        unit.onAttack = new UnityEvent();
        return unit;
    }

    private static T Component<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name);
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, _scene);
        return go.AddComponent<T>();
    }

    private static T Asset<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _assets.Add(asset);
        return asset;
    }

    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[TotemBehaviorChecks] FAIL: " + message);
        _passed++;
    }
}
