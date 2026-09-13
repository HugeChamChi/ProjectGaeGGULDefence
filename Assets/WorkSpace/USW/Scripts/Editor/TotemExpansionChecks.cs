using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>TD1007~1010 범위/피해 기록/발사/원복을 임시 씬에서 검증한다.</summary>
public static class TotemExpansionChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static readonly List<TotemBase> _totems = new();
    private static int _passed;

    /// <summary>사용자 씬과 SO를 저장하지 않고 실제 게임 코드를 실행한다.</summary>
    [MenuItem("Tools/Totems/Run Expansion Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        _passed = 0;
        var random = UnityEngine.Random.state;
        _scene = EditorSceneManager.NewPreviewScene();
        try
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
            var buffs = Component<TotemBuffManager>();
            Set(buffs, "_gridManager", grid);
            CheckRings(grid, buffs);
            CheckShadow(grid, buffs);
            Debug.Log($"[TotemExpansionChecks] PASS {_passed} assertions.");
        }
        finally
        {
            foreach (var totem in _totems) if (totem != null) totem.OnRemoved();
            _totems.Clear();
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
            UnityEngine.Random.state = random;
        }
    }

    private static void CheckRings(GridManager grid, TotemBuffManager buffs)
    {
        var inner = new TotemSquareRingRange();
        var outer = new TotemSquareRingRange { MinRadius = 2, MaxRadius = 2 };
        Check(inner.GetPreviewOffsets().Count == 8 && outer.GetPreviewOffsets().Count == 16, "8/16 ring counts");
        Check(!inner.Contains(Vector2Int.zero) && !outer.Contains(Vector2Int.one), "Center and inner excluded from outer");
        var data = Asset<TotemData>();
        TotemExpansionPresets.Configure(data, 1008);
        var totem = Place<GenericBuffTotem>(data, grid, buffs);
        Check(totem.GetAffectedCells().Count == 24, "TD1008 has 24 unique cells");
        bool separated = true;
        foreach (var cell in totem.GetAffectedCells())
        {
            bool isInner = inner.Contains(cell.GridPosition - new Vector2Int(3, 3));
            separated &= Mathf.Approximately(cell.Model.GetTotemCellBonus(StatKind.AttackPercent), isInner ? 0.1f : 0f);
            separated &= Mathf.Approximately(cell.Model.GetTotemCellBonus(StatKind.Speed), isInner ? 0f : 0.1f);
        }
        Check(separated, "Inner attack and outer speed never overlap");
        totem.Rotate();
        Check(totem.GetAffectedCells().Count == 24, "Rotation preserves square rings");
        totem.OnRemoved();
        Check(grid.GetCell(4, 3).Model.GetTotemCellBonus(StatKind.AttackPercent) == 0, "Removal clears attack");
        Check(grid.GetCell(5, 3).Model.GetTotemCellBonus(StatKind.Speed) == 0, "Removal clears outer speed");

        TotemExpansionPresets.Configure(data, 1009);
        totem.OnPlaced(grid.GetCell(3, 3));
        var cellNear = grid.GetCell(4, 3);
        Check(Mathf.Approximately(cellNear.Model.GetTotemCellBonus(StatKind.Speed), -0.2f), "Negative speed applies");
        Check(Mathf.Approximately(cellNear.Model.GetTotemCellBonus(StatKind.AttackPercent), 0.5f), "Tradeoff attack applies");
        var unit = Component<FrogArcher>();
        unit.unitData = Asset<UnitData>();
        unit.unitData.attackSpeed.normal = 1;
        unit.Init(new UnitDependencies());
        Set(unit, "<currentCell>k__BackingField", cellNear);
        Check(Mathf.Approximately(unit.GetCurrentAttackInterval(), 1.25f), "Speed reduction increases real attack interval");
        buffs.RebuildCellBuffFlags();
        Check(Mathf.Approximately(unit.GetCurrentAttackInterval(), 1.25f), "Repaint does not accumulate penalties");
        totem.OnRemoved();
        Check(Mathf.Approximately(unit.GetCurrentAttackInterval(), 1f), "Tradeoff removal restores interval");
        totem.OnPlaced(grid.GetCell(0, 0));
        Check(totem.GetAffectedCells().Count == 3, "Board edges clip the ring");
        totem.OnRemoved();
        TotemExpansionPresets.Configure(data, 1010);
        Check(data.GetFoodGenerator() != null && data.GetFoodGenerator().interval > 0 && data.GetFoodGenerator().amount > 0,
            "Food generator uses existing editable interval and amount");
        Check(data.GetEffectPreviewOffsets().Count == 0 && !data.UseSheetData, "Food generation needs no neighboring unit");
    }

    private static void CheckShadow(GridManager grid, TotemBuffManager buffs)
    {
        var bosses = Component<BossManager>();
        var boss = Component<BossNormal>();
        boss.Init(100000);
        Set(boss, "_defenseScale", 500d);
        ((List<BossBase>)Get(bosses, "_currentBosses")).Add(boss);
        var shots = new List<TotemCheckProjectile>();
        var pool = Component<ProjectilePool>();
        Set(pool, "_pool", new UnityEngine.Pool.ObjectPool<Projectile>(() =>
        {
            var shot = Component<TotemCheckProjectile>();
            shots.Add(shot);
            return shot;
        }));
        int Launches() { int count = 0; foreach (var shot in shots) count += shot.LaunchCount; return count; }
        var unit = Component<FrogArcher>();
        unit.unitData = Asset<UnitData>();
        unit.unitData.atk.normal = 100;
        unit.unitData.attackSpeed.normal = 1;
        unit.onAttack = new UnityEvent();
        int attacks = 0;
        unit.onAttack.AddListener(() => attacks++);
        unit.Init(new UnitDependencies { BossManager = bosses, ProjectileManager = pool });
        var cell = grid.GetCell(4, 3);
        cell.TryPlaceUnit(unit);
        Set(unit, "<currentCell>k__BackingField", cell);
        unit.gameObject.AddComponent<SpriteRenderer>();
        var data = Asset<TotemData>();
        data.UseSheetData = false;
        data.effectRanges.Add(new TotemSquareRingRange());
        var totem = Place<TotemShadowAttack>(data, grid, buffs);
        Invoke(unit.Combat, "ExecuteAttack");
        Check(Launches() == 1 && attacks == 1, "Only original fires immediately");
        Invoke(totem, "Advance", 0.19f);
        Check(Launches() == 1, "Shadow waits for delay");
        Invoke(totem, "Advance", 0.02f);
        Check(Launches() == 2 && attacks == 1, "One shadow at 0.2 seconds without another attack event");
        decimal hp = boss.CurrentHp;
        shots[0].Complete();
        decimal firstDamage = hp - boss.CurrentHp;
        Set(boss, "_defense", 10000d);
        hp = boss.CurrentHp;
        shots[1].Complete();
        Check(hp - boss.CurrentHp == firstDamage, "Final damage unchanged by later defense");

        var probe = new TotemReplayProbeEffect();
        var extra = new TotemReplayProbeEffect();
        var skill = Asset<SkillData>();
        skill.action = new MultiShotSkillAction { shotCount = new ConstantInt { value = 2 } };
        skill.hitEffects = new List<IEffect> { new AtkCoefficientDamage(), probe };
        skill.additionalEffects = new List<IAdditionalEffect> { extra };
        unit.unitData.basicAttackData = skill;
        cell.AddTotemCellBonus(StatKind.CritChance, 1f);
        int before = Launches();
        Invoke(unit.Combat, "ExecuteAttack");
        Invoke(totem, "Advance", 0.21f);
        Check(Launches() == before + 4, "Two original projectiles and two shadow projectiles");
        foreach (var shot in shots) shot.Complete();
        Check(probe.Count == 4 && extra.Count == 4, "Every projectile repeats hit effects and extra effects");
        Check(attacks == 2, "No recursive shadow attack events");

        var recorded = new RecordedHitEffects(unit, new List<IEffect> { new AtkCoefficientDamage() }, null);
        hp = boss.CurrentHp;
        recorded.Apply(boss, Vector3.zero);
        decimal critical = hp - boss.CurrentHp;
        cell.Model.ClearTotemEffects();
        unit.unitData.atk.normal = 900;
        hp = boss.CurrentHp;
        recorded.Apply(boss, Vector3.zero);
        Check(hp - boss.CurrentHp == critical, "Attack and crit changes cannot reroll recorded damage");

        unit.unitData.skillData = skill;
        before = Launches();
        unit.Combat.TriggerSkillManually();
        Invoke(totem, "Advance", 1f);
        Check(Launches() == before + 2, "Skill volley is not copied");
        foreach (var shot in shots) shot.Complete();

        var context = new BasicAttackReplay(unit, boss);
        int scheduled = 0;
        Check(context.Enable((fire, first) => scheduled++) && !context.Enable((fire, first) => scheduled++), "Overlapping shadows enable once");
        Set(unit, "<currentCell>k__BackingField", grid.GetCell(6, 6));
        Check(context.CanReplay, "Moving out does not cancel an already recorded attack");
        var visual = totem.GetComponentInChildren<ShadowAttackVisual>(true);
        Check(visual != null && visual.GetComponentInChildren<UnitBase>(true) == null &&
            visual.GetComponentInChildren<Collider2D>(true) == null, "Visual clone has no unit or collider");
        Set(visual, "_elapsed", 0.25f);
        Invoke(visual, "LateUpdate");
        Check(Mathf.Approximately(visual.GetComponentInChildren<SpriteRenderer>(true).color.a, data.ShadowAttack.Tint.a),
            "Clone uses configured transparency");
        Check(unit.GetComponent<SpriteRenderer>().color == Color.white, "Clone does not tint original renderer");

        Set(unit, "<currentCell>k__BackingField", cell);
        totem.PaintAffectedCells();
        Invoke(unit.Combat, "ExecuteAttack");
        before = Launches();
        Set(unit, "<currentCell>k__BackingField", grid.GetCell(6, 6));
        totem.PaintAffectedCells();
        var nextBoss = Component<BossNormal>();
        nextBoss.Init(100000);
        Set(nextBoss, "_defenseScale", 500d);
        ((List<BossBase>)Get(bosses, "_currentBosses"))[0] = nextBoss;
        Invoke(totem, "Advance", 0.21f);
        Check(Launches() == before + 2, "Pending volley survives moving out of range");
        foreach (var shot in shots) shot.Complete();
        Check(nextBoss.CurrentHp == 100000, "Recorded volley never retargets next boss");

        Set(unit, "<currentCell>k__BackingField", cell);
        totem.PaintAffectedCells();
        Invoke(unit.Combat, "ExecuteAttack");
        totem.OnRemoved();
        before = Launches();
        Invoke(totem, "Advance", 1f);
        Check(Launches() == before, "Totem removal clears pending work");

        totem.OnPlaced(grid.GetCell(3, 3));
        Invoke(unit.Combat, "ExecuteAttack");
        before = Launches();
        UnityEngine.Object.DestroyImmediate(unit.gameObject);
        // 편집 모드에서 프레임 끝 Destroy를 실행할 수 없으므로 임시 분신은 테스트가 즉시 정리한다.
        foreach (var shadowVisual in totem.GetComponentsInChildren<ShadowAttackVisual>(true))
            UnityEngine.Object.DestroyImmediate(shadowVisual.gameObject);
        Invoke(totem, "Advance", 0.21f);
        Check(Launches() == before, "Source sale cancels actual scheduled projectiles");
        Check(!context.CanReplay, "Sold/destroyed source cancels replay");
    }

    private static T Place<T>(TotemData data, GridManager grid, TotemBuffManager buffs) where T : TotemBase
    {
        var totem = Component<T>();
        Set(totem, "totemData", data);
        Set(totem, "_gridManager", grid);
        Set(totem, "_totemBuffManager", buffs);
        _totems.Add(totem);
        totem.OnPlaced(grid.GetCell(3, 3));
        return totem;
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
        var data = ScriptableObject.CreateInstance<T>();
        _assets.Add(data);
        return data;
    }
    private static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Invoke(object obj, string method, params object[] args)
        => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _passed++;
    }
}
