using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>격리된 PreviewScene에서 실제 보스 피해와 체력바 연동을 검증한다.</summary>
public static class BossHpIntegrationChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary>메뉴 또는 자동화에서 실행. 현재 게임 씬과 원본 SO는 수정하지 않는다.</summary>
    [MenuItem("Tools/Checks/Boss HP Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        var temporaryAssets = new List<UnityEngine.Object>();
        BossHpBarShake shake = null;
        BossHpBreakFlipbook flip = null;
        int passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            passed++;
        }
        try
        {
            var root = new GameObject("Boss HP Checks");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/HSD/Prefab/UI/InGame/BossHpBar.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab, root.transform);
            var bar = instance.GetComponent<UI_BossHpBar>();
            shake = instance.GetComponent<BossHpBarShake>();
            flip = instance.GetComponentInChildren<BossHpBreakFlipbook>(true);
            Invoke(shake, "Awake"); Invoke(shake, "OnEnable");
            Invoke(flip, "Awake"); Invoke(flip, "OnEnable");
            Check(instance.GetComponentInChildren<BossHpDamageTester>(true) == null, "Runtime prefab excludes tester");
            Check(((Sprite[])Get(flip, "_frames")).Length == 13, "All sliced frames connected");
            var ui = root.AddComponent<UIManager>();
            Set(ui, "_bossHpBar", bar);
            Invoke(ui, "Awake");
            Check(bar.IsRuntimeControlled, "UI reserves runtime ownership before tester Start");
            ui.BeginBossHp(1000, 1000, 100);
            Check(bar.CurrentLine == 100 && !Playing(flip), "New boss starts full without damage effect");
            ui.UpdateBossHp(990, 1000);
            Check(bar.CurrentLine == 99 && !Playing(flip) && Get(shake, "_shakeTween") == null, "One lost line does not trigger ten-line effect");
            ui.UpdateBossHp(900, 1000);
            Check(bar.CurrentLine == 90 && Playing(flip) && Get(shake, "_shakeTween") != null, "Ten lost lines trigger both effects");
            var mask = (RectTransform)Get(bar, "_hpFillMask");
            Check(Mathf.Abs(mask.anchorMax.x - .9f) < .00001f, "Fill remains total HP ratio");
            ui.BeginBossHp(5000, 5000, 7);
            Check(bar.CurrentLine == 7 && !Playing(flip) && Get(shake, "_shakeTween") == null, "Boss swap clears effects and resets line count");
            var tester = root.AddComponent<BossHpDamageTester>();
            Set(tester, "_hpBar", bar);
            tester.SetHpValues(1, 10000);
            Check(bar.CurrentLine == 7 && mask.anchorMax.x == 1f, "Tester cannot overwrite runtime bar");
            bar.BeginBoss(100000000000000m, 100000000000000m, 100);
            bar.SetHpExact(90000000000000.0001m, 100000000000000m);
            Check(bar.CurrentLine == 91, "Decimal boundary above ninety percent remains line 91");
            Check(((TMPro.TMP_Text)Get(bar, "_hpLineText")).text == "91줄", "Displayed line preserves decimal boundary");
            bar.SetHpExact(.0001m, 100000000000000m);
            Check(bar.CurrentLine == 1, "Small positive HP keeps last line");
            bar.SetHpExact(0, 100000000000000m);
            Check(bar.CurrentLine == 0 && mask.anchorMax.x == 0, "Zero HP empties bar");

            var data = ScriptableObject.CreateInstance<BossData>(); temporaryAssets.Add(data);
            var settings = ScriptableObject.CreateInstance<DebuffSettings>(); temporaryAssets.Add(settings);
            Set(settings, "_defenseScale", 100d);
            var bossTemplate = new GameObject("Check Boss"); bossTemplate.transform.SetParent(root.transform);
            bossTemplate.AddComponent<BossNormal>();
            Set(data, "_prefab", bossTemplate); Set(data, "_maxHp", 1000L);
            Set(data, "_defense", 100d); Set(data, "_hpLineCount", 50);
            var manager = root.AddComponent<BossManager>();
            Set(manager, "_debuffSettings", settings); Set(manager, "_uiManager", ui);
            var sheets = new GameDataManager(null, settings);
            typeof(GameDataManager).GetMethod("ParseBossData", Flags).Invoke(sheets, new object[] { "id,round,name,hp,expPerHp,exp,defense\n1,100,SheetBoss,999999,0.01,100,9999" });
            typeof(GameDataManager).GetProperty("IsLoaded", Flags).SetValue(sheets, true);
            Set(manager, "_gameDataManager", sheets);
            Set(manager, "_waveManager", root.AddComponent<WaveManager>());
            Check(sheets.IsLoaded && sheets.GetBossMaxHp(100) == 999999, "Conflicting loaded sheet supplied for priority check");
            manager.SpawnSingleBoss(new BossEntry { Data = data, hp = 7, Defense = 999 }, null);
            var boss = manager.CurrentBoss;
            Check(boss.MaxHp == 1000 && bar.LineCount == 50 && bar.CurrentLine == 50, "Real spawn uses BossData instead of fallback fields");
            boss.TakeDamage(200);
            // 로그 방어: 200 / (1 + ln(2)) = 118.1232 (고정소수 4자리 반올림).
            Check(boss.CurrentHp == 881.8768m && bar.CurrentLine == 45, $"Actual defense-adjusted damage updates bar through BossManager (HP={boss.CurrentHp}, line={bar.CurrentLine})");
            Check(Mathf.Abs(mask.anchorMax.x - .8818768f) < .00001f, "Real damage updates fill");
            boss.ClearListeners();

            var stage = AssetDatabase.LoadAssetAtPath<StageData>("Assets/WorkSpace/USW/Data/WaveData/StageData.asset");
            Check(stage.waves.Length == 9, "Exactly nine rounds configured");
            Check(stage.waves.All(w => w != null && w.bosses.Length == 1 && w.bosses[0].Data != null && w.bosses[0].Prefab.GetComponent<BossBase>() != null), "Every round has one valid BossData boss");
            var legacy = new BossEntry { hp = 700, Defense = 20, prefab = bossTemplate };
            Check(legacy.MaxHp == 700 && legacy.BaseDefense == 20 && legacy.Prefab == bossTemplate, "Unmigrated entries retain fallback fields");
            Debug.Log($"[BossHpIntegrationChecks] {passed} PASS");
        }
        finally
        {
            if (shake != null) Invoke(shake, "OnDisable");
            if (flip != null) Invoke(flip, "OnDisable");
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var asset in temporaryAssets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Flags).Invoke(target, null);
    private static bool Playing(BossHpBreakFlipbook flip) => (bool)Get(flip, "_playing");
}
