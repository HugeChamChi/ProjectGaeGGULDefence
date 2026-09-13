using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>족장 풀 격리와 실제 매니저 추첨/효과를 임시 씬에서 검증한다.</summary>
public static class LevelUpPoolChecks
{
    /// <summary>사용자 씬과 SO를 저장하지 않고 동작 검증을 실행한다.</summary>
    [MenuItem("Tools/LevelUp/Run Pool Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var assets = new List<UnityEngine.Object>();
        var previousParty = GlobalData.SelectedParty;
        var scene = EditorSceneManager.NewPreviewScene();
        int passed = 0;
        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed++;
        }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, message);
        }
        try
        {
            var shared = Asset<LevelUpData>();
            shared.chooseId = 10;
            shared.spawnRate = 1f;
            shared.primaryEffect = LevelUpEffectType.ExpGainPercent;
            shared.primaryValue = 20f;
            var exclusive = Asset<LevelUpData>();
            exclusive.chooseId = 11;
            exclusive.spawnRate = 1f;
            var other = Asset<LevelUpData>();
            other.chooseId = 12;
            other.spawnRate = 1f;
            var a = Asset<LevelUpPoolData>();
            a.PoolId = 1;
            a.Cards = new[] { shared, exclusive, shared };
            var b = Asset<LevelUpPoolData>();
            b.PoolId = 2;
            b.Cards = new[] { shared, other };
            ILevelUpCatalog catalog = new LevelUpCatalog(new[] { a, b });
            Check(catalog.GetCardIds(1).Count == 2, "Duplicate references are not weights");
            Check(catalog.GetCardIds(2).Count == 2, "Shared definitions work in independent pools");
            Check(catalog.TryGetCard(10, out var found) && found == shared, "Shared card identity");
            Check(catalog.GetCardIds(999).Count == 0, "Unknown pool stays empty");
            Check(!catalog.TryGetCard(999, out _), "Unknown card lookup");
            Reject(() => new LevelUpCatalog(new[] { a, a }), "Duplicate pool ID rejected");
            Reject(() => new LevelUpCatalog(new LevelUpPoolData[] { null }), "Missing pool rejected");
            var invalid = Asset<LevelUpPoolData>();
            invalid.Cards = new LevelUpData[] { null };
            Reject(() => new LevelUpCatalog(new[] { invalid }), "Broken reference rejected");
            var collision = Asset<LevelUpData>();
            collision.chooseId = 10;
            invalid.Cards = new[] { collision, shared };
            Reject(() => new LevelUpCatalog(new[] { invalid }), "Conflicting card ID rejected");

            var chief = Asset<UnitData>();
            chief.LevelUpPool = a;
            var party = Asset<PartyDataSO>();
            party.chieftainData = chief;
            party.exclusiveLevelUpChoices = new List<LevelUpData> { other };
            GlobalData.SelectedParty = party;
            var root = new GameObject("LevelUpPoolChecks");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var spawner = root.AddComponent<ChieftainSpawner>();
            var manager = root.AddComponent<LevelUpManager>();
            Set(manager, "_chieftainSpawner", spawner);
            Set(manager, "levelUpPool", new[] { other });
            Check(spawner.GetSelectedLevelUpPool() == a, "Lobby chief pool routing");
            manager.Init();
            var choices = manager.GetRandomChoices();
            Check(choices.Count == 2 && choices.Contains(shared) && choices.Contains(exclusive), "Short pool displays remaining cards");
            Check(!choices.Contains(other), "Neither common nor party list leaks into chief pool");
            var copy = manager.LevelUpPool;
            copy[0] = other;
            Check(!manager.GetRandomChoices().Contains(other), "Debug array cannot change active pool");
            chief.LevelUpPool = b;
            manager.Init();
            Check(!manager.GetRandomChoices().Contains(other), "Pool fixed for the run and Init idempotent");
            manager.ApplyEffect(shared);
            Check(Mathf.Approximately(manager.ExpGainMultiplier, 1.2f), "Existing effect execution");
            manager.ApplyEffect(shared);
            Check(Mathf.Approximately(manager.ExpGainMultiplier, 1.2f), "Duplicate selection does not reapply");
            Check(!manager.GetRandomChoices().Contains(shared), "Acquired card excluded");
            manager.ApplyEffect(exclusive);
            Check(manager.GetRandomChoices().Count == 0, "Exhausted pool empty");
            manager.RemoveEffect(shared);
            Check(manager.GetRandomChoices().Contains(shared), "Removed effect becomes selectable again");
            Check(Mathf.Approximately(manager.ExpGainMultiplier, 1f), "Existing removal execution");
            Check(shared.primaryValue == 20f && a.Cards.Length == 3, "Shared SO remains unchanged");

            var next = root.AddComponent<LevelUpManager>();
            Set(next, "_chieftainSpawner", spawner);
            next.Init();
            Check(next.GetRandomChoices().Contains(other) && next.GetRandomChoices().Contains(shared), "New scene manager has fresh choices and next chief pool");
            shared.spawnRate = 0f;
            Check(!next.GetRandomChoices().Contains(shared), "Zero weight excluded");
            shared.spawnRate = float.NaN;
            Check(!next.GetRandomChoices().Contains(shared), "Invalid weight excluded");
            shared.spawnRate = 1f;
            shared.applicableTribes = new[] { default(UnitTribe) };
            Check(!next.GetRandomChoices().Contains(shared), "Existing tribe condition retained");
            GlobalData.SelectedParty = null;
            Set(spawner, "_testUnitData", chief);
            Check(spawner.GetSelectedLevelUpPool() == b, "Test chief uses same pool selection path");
            Debug.Log($"[LevelUpPoolChecks] PASS {passed} assertions.");
        }
        finally
        {
            GlobalData.SelectedParty = previousParty;
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
