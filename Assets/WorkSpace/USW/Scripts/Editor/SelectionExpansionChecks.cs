using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>추가 선택지의 추첨, 실제 지출 환급, 드론 효과, UI 재선택 경계를 검사한다.</summary>
public static class SelectionExpansionChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static int _passed;

    /// <summary>임시 씬에서 검증하며 사용자 씬과 SO는 저장하지 않는다.</summary>
    [MenuItem("Tools/Selections/Run Seven Selection Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var random = UnityEngine.Random.state;
        float timeScale = Time.timeScale;
        _scene = EditorSceneManager.NewPreviewScene();
        _passed = 0;
        try
        {
            var cards = new LevelUpData[7];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = Asset<LevelUpData>();
                cards[i].chooseId = 9201 + i;
                SelectionExpansionPresets.Configure(cards[i], i);
            }
            var manager = Component<LevelUpManager>();
            Check(cards[0].tier == Tier.Normal && cards[1].tier == Tier.Normal && cards[2].tier == Tier.Epic
                && cards[3].tier == Tier.Epic && cards[4].tier == Tier.Rare && cards[5].tier == Tier.Normal
                && cards[6].tier == Tier.Rare, "Seven preset tiers");
            CheckRarity(manager, cards[4]);
            CheckRefund(manager, cards[5]);
            CheckDrones(manager, cards);
            CheckReroll(cards[6]);
            Debug.Log($"[SelectionExpansionChecks] PASS {_passed} assertions.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
            UnityEngine.Random.state = random;
            Time.timeScale = timeScale;
        }
    }

    private static void CheckRarity(LevelUpManager manager, LevelUpData guarantee)
    {
        var pool = Asset<LevelUpPoolData>();
        var candidates = new List<LevelUpData>();
        foreach (var tier in new[] { Tier.Normal, Tier.Rare, Tier.Epic })
            for (int i = 0; i < (tier == Tier.Epic ? 12 : 3); i++)
            {
                var card = Asset<LevelUpData>(); card.chooseId = 10000 + candidates.Count;
                card.tier = tier; card.spawnRate = tier == Tier.Epic ? 100f : 1f;
                candidates.Add(card);
            }
        Set(manager, "_poolInitialized", true); Set(manager, "_effectivePool", candidates.ToArray());
        Set(manager, "_selectedPool", pool);
        manager.ApplyEffect(guarantee);
        var choices = manager.GetRandomChoices();
        Check(choices.Count == 3 && choices.TrueForAll(c => c.tier == Tier.Epic), "Next draw is all epic");
        pool.NormalWeight = 1; pool.RareWeight = pool.EpicWeight = 0;
        Check(manager.GetRandomChoices().TrueForAll(c => c.tier == Tier.Normal), "Guarantee consumed after one draw");
        pool.NormalWeight = 60; pool.RareWeight = 30; pool.EpicWeight = 10;
        UnityEngine.Random.InitState(1739);
        var counts = new int[3];
        bool unique = true;
        for (int i = 0; i < 10000; i++)
        {
            choices = manager.GetRandomChoices(); counts[(int)choices[0].tier]++;
            unique &= new HashSet<LevelUpData>(choices).Count == 3;
        }
        Check(unique, "No duplicate cards per draw");
        Check(Math.Abs(counts[0] - 6000) < 200 && Math.Abs(counts[1] - 3000) < 200
            && Math.Abs(counts[2] - 1000) < 150, "60/30/10 independent of card count and spawn weight");
        manager.RemoveEffect(guarantee); manager.ApplyEffect(guarantee);
        Set(manager, "_effectivePool", new[] { candidates[6] });
        Check(manager.GetRandomChoices().Count == 1, "Epic shortage never inserts lower tiers");
        manager.RemoveEffect(guarantee); manager.ApplyEffect(guarantee);
        Set(manager, "_effectivePool", new[] { candidates[0] });
        Check(manager.GetRandomChoices().Count == 0, "No eligible epic yields empty draw");
        Check(manager.GetRandomChoices().Count == 1, "Empty forced draw does not retain guarantee");
        manager.RemoveEffect(guarantee);
    }

    private static void CheckRefund(LevelUpManager manager, LevelUpData discount)
    {
        var currency = Component<CurrencyManager>(); currency.AddCurrency(1000);
        var upgrade = Component<UpgradeManager>(); var data = new GameDataManager(null, null);
        Set(data, "<IsLoaded>k__BackingField", true);
        var rows = (Dictionary<int, GameDataManager.UpgradeSheetRow>)Get(data, "_upgradeRows");
        rows[1] = new GameDataManager.UpgradeSheetRow { UpgradeCost = 100 };
        rows[2] = new GameDataManager.UpgradeSheetRow { UpgradeCost = 200 };
        rows[3] = new GameDataManager.UpgradeSheetRow { UpgradeCost = 300 };
        ((Dictionary<string, int>)Get(upgrade, "_jobLevel"))["test"] = 1;
        Set(upgrade, "_currencyManager", currency); Set(upgrade, "_gameDataManager", data);
        Set(manager, "_upgradeManager", upgrade);
        Check(!upgrade.TryUpgrade("unknown") && currency.Currency == 1000, "Unknown upgrade does not spend");
        Check(upgrade.TryUpgrade("test") && upgrade.TryUpgrade("test") && upgrade.TotalSpent == 300,
            "Successful payments accumulated");
        currency.Spend(50); currency.AddCurrency(5);
        manager.ApplyEffect(discount);
        Check(currency.Currency == 685 && upgrade.GetUpgradeCost("test") == 270,
            "Refund only actual upgrade spend and discount future cost");
        manager.ApplyEffect(discount);
        Check(currency.Currency == 685, "Duplicate card cannot refund twice");
        Check(upgrade.TryUpgrade("test") && upgrade.TotalSpent == 570, "Discounted actual charge recorded");
        manager.RemoveEffect(discount); manager.ApplyEffect(discount);
        Check(currency.Currency == 415, "Removal and reapply cannot repeat refund");
        ((Dictionary<string, int>)Get(upgrade, "_jobLevel"))["test"] = 1;
        currency.Spend(currency.Currency);
        Check(!upgrade.TryUpgrade("test") && upgrade.TotalSpent == 570, "Failed payment excluded");
        Check(Component<UpgradeManager>().TotalSpent == 0, "New run starts with zero spend");
    }

    private static void CheckDrones(LevelUpManager manager, LevelUpData[] cards)
    {
        var drones = Component<DroneManager>(); Set(drones, "_levelUpManager", manager);
        var zelta = Component<Drone_Zeltan>(); zelta.unitData = Asset<UnitData>();
        zelta.unitData.foodProduction.normal = 4;
        zelta.Init(new UnitDependencies { LevelUpManager = manager });
        var cell = Component<GridCell>(); Set(cell, "<Model>k__BackingField", new GridCellModel());
        Set(zelta, "<currentCell>k__BackingField", cell); zelta.gameObject.SetActive(true);
        drones.SetPerDroneFood(0.15f); drones.RegisterFoodProducer(zelta);
        manager.ApplyEffect(cards[0]);
        Check(Mathf.Approximately(zelta.GetBaseFoodPerSecond(), 4.4f) && drones.BaseFoodPerDrone == 1f,
            "Cold storage affects own production only");
        manager.ApplyEffect(cards[1]);
        Check(Mathf.Approximately(drones.BaseFoodPerDrone, 1.1f) && Mathf.Approximately(zelta.GetBaseFoodPerSecond(), 4.4f),
            "Maintenance affects legion production only");
        manager.RemoveEffect(cards[1]); Check(drones.BaseFoodPerDrone == 1f, "Maintenance removal updates immediately");
        manager.ApplyEffect(cards[1]); drones.UnregisterFoodProducer(zelta);
        Check(Mathf.Approximately(drones.BaseFoodPerDrone, 0.15f), "No Zeltan means no maintenance bonus");
        Check(drones.GetRallyDamage(10, 80) == 800m, "Unmodified rally damage");
        manager.ApplyEffect(cards[2]);
        Check(drones.GetRallyDamage(10, 80) == 1200m && drones.GetRallyDamage(0, 80) == 0,
            "Monocle guaranteed 1.5 with zero-drone boundary");
        manager.RemoveEffect(cards[2]); Check(drones.GetRallyDamage(10, 80) == 800m, "Monocle removed");
        var beta = Component<DroneSelectionCheckBetan>(); beta.unitData = Asset<UnitData>();
        beta.unitData.skillCooldown.normal = 14; beta.Init(new UnitDependencies { LevelUpManager = manager });
        beta.gameObject.SetActive(true); Set(beta, "<currentCell>k__BackingField", cell);
        Set(cell, "<OccupyingUnit>k__BackingField", beta);
        var grid = Component<GridManager>(); Set(grid, "_grid", new[,] { { cell } });
        Set(drones, "_gridManager", grid);
        beta.Combat.SkillTimer = 3; drones.NotifySelfDestructExplosion();
        Check(beta.Combat.SkillTimer == 3, "No repair kit no recharge");
        manager.ApplyEffect(cards[3]); drones.NotifySelfDestructExplosion(); drones.NotifySelfDestructExplosion();
        Check(beta.Combat.SkillTimer == 4, "Each explosion advances by half second");
        beta.Combat.SkillTimer = 13.8f; drones.NotifySelfDestructExplosion();
        Check(beta.Combat.SkillTimer == 14, "Recharge caps at ready");
        manager.RemoveEffect(cards[3]); beta.Combat.SkillTimer = 3; drones.NotifySelfDestructExplosion();
        Check(beta.Combat.SkillTimer == 3, "Repair kit removal stops recharge");
    }

    private static void CheckReroll(LevelUpData contract)
    {
        var manager = Component<LevelUpManager>(); var pool = Asset<LevelUpPoolData>();
        pool.NormalWeight = pool.EpicWeight = 0; pool.RareWeight = 1;
        var candidates = new LevelUpData[4]; candidates[0] = contract;
        for (int i = 1; i < 4; i++)
        {
            candidates[i] = Asset<LevelUpData>(); candidates[i].chooseId = 12000 + i;
            candidates[i].spawnRate = 1; candidates[i].tier = Tier.Rare;
        }
        Set(manager, "_poolInitialized", true); Set(manager, "_effectivePool", candidates); Set(manager, "_selectedPool", pool);
        var ui = Component<LevelUpUI>(); var prefab = Component<LevelUpCardUI>();
        var timer = Component<TimerController>(); var grid = Component<GridManager>();
        Set(grid, "_grid", new GridCell[0, 0]);
        var game = Component<GameManager>(); Set(game, "<CurrentState>k__BackingField", GameManager.GameState.LevelUp);
        Set(ui, "_levelUpManager", manager); Set(ui, "_timerManager", timer); Set(ui, "_gridManager", grid); Set(ui, "_gameManager", game);
        Set(ui, "obj", ui.gameObject); Set(ui, "cardContainer", Component<Transform>().transform); Set(ui, "cardPrefab", prefab);
        var label = Component<TMPro.TextMeshProUGUI>(); Set(ui, "selectionTimerText", label); Set(ui, "selectionSeconds", 30f);
        var selected = Component<LevelUpCardUI>(); selected.Setup(contract, null); Set(ui, "_selectedCard", selected);
        var oldCts = new CancellationTokenSource(); var oldToken = oldCts.Token; Set(ui, "_selectionCts", oldCts);
        ui.OnConfirmClicked();
        var spawned = (List<LevelUpCardUI>)Get(ui, "_spawnedCards");
        Check(spawned.Count == 3 && spawned.TrueForAll(c => c.GetData() != contract), "Contract draws three fresh eligible cards");
        Check(oldToken.IsCancellationRequested && Get(ui, "_selectionCts") != oldCts && label.text == "30", "Old timer cancelled and full timer restarted");
        Check(Time.timeScale == 0 && game.CurrentState == GameManager.GameState.LevelUp && ui.gameObject.activeSelf,
            "Reroll keeps selection open and battle paused");
    }

    private static T Component<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, _scene);
        return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
    }
    private static T Asset<T>() where T : ScriptableObject
    { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _passed++; }
}
