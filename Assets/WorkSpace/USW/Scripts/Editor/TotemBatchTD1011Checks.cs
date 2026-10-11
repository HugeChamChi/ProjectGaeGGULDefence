using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TD1011~1014 저작 SO를 임시 씬에서 실제 토템 코드로 배치해 범위·수치·보스 등장 시간 조건을 검증한다.
/// 사용자 씬과 SO는 저장하지 않는다.
/// </summary>
public static class TotemBatchTD1011Checks
{
    private const string Folder = "Assets/WorkSpace/USW/Data/TotemData/Playable";
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static readonly List<TotemBase> _totems = new();
    private static int _passed;

    [MenuItem("Tools/USW/Totems/Run TD1011-1014 Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        _passed = 0;
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

            CheckTD1011(grid, buffs);
            CheckTD1012(grid, buffs);
            CheckTD1013(grid, buffs);
            CheckTD1014(grid, buffs);
            Debug.Log($"[TotemBatchTD1011Checks] PASS {_passed} assertions.");
        }
        finally
        {
            foreach (var totem in _totems) if (totem != null && totem.IsPlaced) totem.OnRemoved();
            _totems.Clear();
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }
    }

    private static void CheckTD1011(GridManager grid, TotemBuffManager buffs)
    {
        var data = Load(1011);
        Check(!data.isRotatable, "TD1011 not rotatable");
        var totem = Place<GenericBuffTotem>(data, grid, buffs, 3, 3);
        Check(Mathf.Approximately(Bonus(grid, 3, 2, StatKind.Speed), 0.4f), "TD1011 below cell speed +40%");
        Check(Disabled(grid, 2, 4) && Disabled(grid, 3, 4) && Disabled(grid, 4, 4), "TD1011 upper row of 3 disabled");
        Check(!Disabled(grid, 3, 2) && !Disabled(grid, 3, 5) && Bonus(grid, 3, 4, StatKind.Speed) == 0f, "TD1011 touches nothing else");
        totem.OnRemoved();
        Check(!Disabled(grid, 3, 4) && Bonus(grid, 3, 2, StatKind.Speed) == 0f, "TD1011 removal clears");
        totem.OnPlaced(grid.GetCell(3, 6));
        Check(Mathf.Approximately(Bonus(grid, 3, 5, StatKind.Speed), 0.4f), "TD1011 top row keeps speed (no-risk edge allowed)");
        totem.OnRemoved();
    }

    private static void CheckTD1012(GridManager grid, TotemBuffManager buffs)
    {
        var count = Place<TotemTotemCount>(Load(1012), grid, buffs, 0, 0);
        Check(Mathf.Approximately(buffs.CritChanceBonus, 0.02f) && Mathf.Approximately(buffs.CritDamageBonus, 0.03f), "TD1012 counts itself");
        var other = Place<GenericBuffTotem>(Load(1013), grid, buffs, 6, 6);
        Check(Mathf.Approximately(buffs.CritChanceBonus, 0.04f) && Mathf.Approximately(buffs.CritDamageBonus, 0.06f), "TD1012 scales with totem count");
        other.OnRemoved();
        Check(Mathf.Approximately(buffs.CritChanceBonus, 0.02f), "TD1012 drops when a totem leaves");
        count.OnRemoved();
        Check(Mathf.Approximately(buffs.CritChanceBonus, 0f) && Mathf.Approximately(buffs.CritDamageBonus, 0f), "TD1012 removal clears global crit");
    }

    private static void CheckTD1013(GridManager grid, TotemBuffManager buffs)
    {
        var totem = Place<GenericBuffTotem>(Load(1013), grid, buffs, 3, 3);
        bool row = true;
        for (int x = 0; x < 7; x++)
            if (x != 3) row &= Mathf.Approximately(Bonus(grid, x, 3, StatKind.AttackPercent), 0.15f);
        Check(row, "TD1013 whole row attack +15%");
        Check(Bonus(grid, 3, 4, StatKind.AttackPercent) == 0f && Bonus(grid, 3, 2, StatKind.AttackPercent) == 0f, "TD1013 other rows untouched");
        totem.OnRemoved();
    }

    private static void CheckTD1014(GridManager grid, TotemBuffManager buffs)
    {
        var game = Component<GameManager>();
        Set(game, "<CurrentState>k__BackingField", GameManager.GameState.Playing);
        var bosses = Component<BossManager>();
        var boss = Component<BossNormal>();
        boss.Init(100000);
        ((List<BossBase>)Get(bosses, "_currentBosses")).Add(boss);
        Set(buffs, "_gameManager", game);
        Set(buffs, "_bossManager", bosses);

        var totem = Place<GenericBuffTotem>(Load(1014), grid, buffs, 3, 3);
        Check(Bonus(grid, 4, 3, StatKind.Speed) == 0f, "TD1014 inactive before first boss");
        Invoke(buffs, "OnBossEntered", null, null);
        Check(Mathf.Approximately(Bonus(grid, 4, 4, StatKind.Speed), 0.4f) && Mathf.Approximately(Bonus(grid, 4, 3, StatKind.Speed), 0.4f)
              && Mathf.Approximately(Bonus(grid, 4, 2, StatKind.Speed), 0.4f), "TD1014 right column of 3 speed +40% on boss entry");
        Check(Bonus(grid, 2, 3, StatKind.Speed) == 0f && Bonus(grid, 3, 4, StatKind.Speed) == 0f, "TD1014 other cells untouched");

        Set(buffs, "_bossEncounterSeconds", 6.5f);
        Invoke(buffs, "Update");
        Check(Mathf.Approximately(Bonus(grid, 4, 3, StatKind.Speed), 0.4f), "TD1014 still active before 7s");
        Set(buffs, "_bossEncounterSeconds", 7f);
        Invoke(buffs, "Update");
        Check(Bonus(grid, 4, 3, StatKind.Speed) == 0f, "TD1014 ends at 7s");

        Invoke(buffs, "OnBossEntered", null, null);
        Check(Mathf.Approximately(Bonus(grid, 4, 3, StatKind.Speed), 0.4f), "TD1014 restarts on next boss");
        Set(game, "<CurrentState>k__BackingField", GameManager.GameState.Idle);
        Set(buffs, "_bossEncounterSeconds", 6.99f);
        Invoke(buffs, "Update");
        Check(Mathf.Approximately((float)Get(buffs, "_bossEncounterSeconds"), 6.99f), "TD1014 clock stops outside Playing");
        totem.OnRemoved();
        Set(buffs, "_gameManager", null);
        Set(buffs, "_bossManager", null);
    }

    private static TotemData Load(int id)
    {
        var data = AssetDatabase.LoadAssetAtPath<TotemData>($"{Folder}/TD{id}Data.asset");
        if (data == null) throw new InvalidOperationException($"TD{id}Data missing");
        return data;
    }

    private static float Bonus(GridManager grid, int x, int y, StatKind kind) => grid.GetCell(x, y).Model.GetTotemCellBonus(kind);
    private static bool Disabled(GridManager grid, int x, int y) => grid.GetCell(x, y).Model.TotemAttackDisabled;

    private static T Place<T>(TotemData data, GridManager grid, TotemBuffManager buffs, int x, int y) where T : TotemBase
    {
        var totem = Component<T>();
        Set(totem, "totemData", data);
        Set(totem, "_gridManager", grid);
        Set(totem, "_totemBuffManager", buffs);
        _totems.Add(totem);
        totem.OnPlaced(grid.GetCell(x, y));
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
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(obj.GetType().Name, name);
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
