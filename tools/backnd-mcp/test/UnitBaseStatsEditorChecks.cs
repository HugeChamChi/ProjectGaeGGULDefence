using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GaeGGUL.DeckUpgrade;

public static class UnitBaseStatsEditorChecks
{
    private sealed class Store : IDeckUpgradeStore
    {
        public DeckUpgradeProgress Load() => new DeckUpgradeProgress();
        public void Save(DeckUpgradeProgress progress) { throw new Exception("No save expected"); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static UnitBalanceSnapshot.Config Read() => UnitBalanceSnapshot.ParseConfig(
        GGD.BackendCharts.ChartSnapshotPayload.Decode(File.ReadAllText("outputs/unit-runtime/rows.json")));
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection"); }

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play for isolated checks");
        var source = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/HeroUnits/UD_Frog_Mage.asset");
        var chief = AssetDatabase.LoadAssetAtPath<ChieftainData>("Assets/WorkSpace/USW/Data/Chieftain/AlphanChieftain.asset");
        Check(source != null && chief != null, "Missing authored test data");
        var config = Read();
        var stat = config.Units.Single(u => u.AssetKey == source.name).Stats.Single(s => s.Tier == "normal");
        stat.Attack = 777; stat.AttackFrequency = 2; stat.FoodProduction = 0; stat.SkillCooldown = 9; stat.MaxDroneCount = 6;
        float oldAttack = source.atk.normal, oldSpeed = source.attackSpeed.normal;
        using var snapshot = new UnitBalanceSnapshot(config);
        var copy = snapshot.Resolve(source);
        Check(copy != source && source.atk.normal == oldAttack && source.attackSpeed.normal == oldSpeed, "Source asset changed");
        Check(copy.skillData == source.skillData && copy.characterId.normal == source.characterId.normal, "Gameplay references changed");
        Check(copy.skillCooldown.normal == 9 && copy.maxDroneCount.normal == 6, "Incomplete stat application");
        Check(ReferenceEquals(copy, snapshot.Resolve(copy)), "Runtime copy was copied again");
        var field = typeof(UnitBalanceRuntime).GetField("snapshot", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = field.GetValue(null);
        var remoteFlag = typeof(UnitBalanceRuntime).GetField("remoteRequired", BindingFlags.NonPublic | BindingFlags.Static);
        var previousFlag = remoteFlag.GetValue(null);
        var go = new GameObject("UnitBaseStats_IsolatedCheck");go.SetActive(false);
        try
        {
            field.SetValue(null, snapshot);
            using var presenter = new DeckUpgradePresenter(new DeckUpgradeService(new Store()), new[] {source}, chief);
            var view = presenter.GetItem(0);
            Check(view.CurrentStat.Contains("777") && view.CurrentSpeed.Contains("2.0"), "UI disagrees with remote base stats");
            copy.passive = null; // Isolate authored passive effects from the base-stat test.
            var unit = go.AddComponent<FrogWarrior>();unit.unitData = copy;unit.currentTier = Tier.Normal;
            var modifier = go.AddComponent<UnitStatsModifier>();modifier.Init(unit, new UnitDependencies());
            Check(modifier.GetNonCriticalAttackDamage() == 777, "Combat attack disagrees");
            Check(Mathf.Abs(modifier.GetCurrentAttackInterval() - .5f) < .0001f, "Attack frequency was inverted");
            copy.foodProduction.normal = 12;
            var resource = go.AddComponent<UnitResourceComponent>();resource.Init(unit, new UnitDependencies());
            Check(Mathf.Abs(resource.CurrentFoodProductionPerSecond - 12) < .0001f, "Food production disagrees");
        }
        finally { field.SetValue(null, previous); UnityEngine.Object.DestroyImmediate(go); }
        try {
            field.SetValue(null, null);remoteFlag.SetValue(null, (bool?)true);
            bool refused = false;
            try { UnitBalanceRuntime.Resolve(source); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "Remote mode silently fell back before loading");
        } finally { field.SetValue(null, previous);remoteFlag.SetValue(null, previousFlag); }
        var bad = Read();bad.Units[0].Stats[0].Attack = float.NaN;Reject(() => new UnitBalanceSnapshot(bad));
        bad = Read();bad.Units[0].Stats[0].Tier = "unknown";Reject(() => new UnitBalanceSnapshot(bad));
        bad = Read();bad.Units[0].Stats[0].Tier = bad.Units[0].Stats[1].Tier;Reject(() => new UnitBalanceSnapshot(bad));
        bad = Read();bad.Units[1].AssetKey = bad.Units[0].AssetKey;Reject(() => new UnitBalanceSnapshot(bad));
        Reject(() => UnitBalanceSnapshot.ParseConfig("{\"Units\":[]}"));
        var raw = LitJson.JsonMapper.ToObject(GGD.BackendCharts.ChartSnapshotPayload.Decode(File.ReadAllText("outputs/unit-runtime/rows.json")));
        raw["Units"][0]["Stats"][0]["Attack"] = null;
        Reject(() => UnitBalanceSnapshot.ParseConfig(raw.ToJson()));
        return "PASS: source unchanged; UI/combat attack 777, 2 attacks/sec = 0.5sec, food 12/sec; cooldown/drone count; invalid mappings/tiers/NaN rejected";
    }
}
