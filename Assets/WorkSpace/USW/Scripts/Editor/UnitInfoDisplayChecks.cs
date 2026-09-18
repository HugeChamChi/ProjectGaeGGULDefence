using System;
using System.Reflection;
using System.Collections.Generic;
using GaeGGUL.UI.Unit;
using GaeGGUL.UI.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>유닛 정보창의 실제 TMP 표시, 드론 합계, 전투 보정 및 열린 창 갱신을 검사한다.</summary>
public static class UnitInfoDisplayChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static int _passed;

    /// <summary>사용자 씬을 저장하지 않는 임시 씬 검사.</summary>
    [MenuItem("Tools/UI/Run Unit Info Display Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var random = UnityEngine.Random.state;
        _scene = EditorSceneManager.NewPreviewScene(); _passed = 0;
        CombatDroneCheckProbe beta = null;
        try
        {
            var panel = Component<UI_UnitInfoPanel>();
            var attack = Component<UI_StatSlot>(); var speed = Component<UI_StatSlot>();
            var atkText = Component<TextMeshProUGUI>(); var speedText = Component<TextMeshProUGUI>();
            var bonus = Component<TextMeshProUGUI>(); bonus.gameObject.SetActive(true);
            Set(attack, "txt_Value", atkText); Set(attack, "txt_BonusValue", bonus); Set(speed, "txt_Value", speedText);
            Set(panel, "statSlot_Atk", attack); Set(panel, "statSlot_AtkSpeed", speed);
            var presenter = new UI_UnitInfoPresenter(panel); Set(panel, "_presenter", presenter);
            var manager = Component<LevelUpManager>(); var drones = Component<DroneManager>(); Set(drones, "_levelUpManager", manager);
            beta = Component<CombatDroneCheckProbe>(); beta.unitData = Asset<UnitData>(); beta.currentTier = Tier.Legend;
            beta.unitData.atk.legend = 80; beta.unitData.attackSpeed.legend = 2; beta.unitData.skillCooldown.legend = 14;
            beta.unitData.maxDroneCount.legend = 4;
            beta.Init(new UnitDependencies { LevelUpManager = manager }); Set(beta, "_droneManager", drones);
            var cell = Component<GridCell>(); Set(cell, "<Model>k__BackingField", new GridCellModel());
            Set(beta, "<currentCell>k__BackingField", cell); beta.gameObject.SetActive(true); beta.PlaceForCheck();
            presenter.SetUnitData(beta);
            Check(atkText.text == "320" && speedText.text == $"{0.5f:F2}초", "Four drones sum, interval in seconds");
            Check(!bonus.gameObject.activeSelf, "No separate upgrade bonus label");
            var crit = Asset<LevelUpData>(); crit.chooseId = 15100; crit.primaryEffect = LevelUpEffectType.CritChancePercent; crit.primaryValue = 100;
            manager.ApplyEffect(crit);
            var before = UnityEngine.Random.state;
            for (int i = 0; i < 30; i++) presenter.Refresh();
            Check(before.Equals(UnityEngine.Random.state) && atkText.text == "320", "Display excludes critical and consumes no random numbers");
            Check(beta.GetAttackDamage() == 120, "Combat still applies critical");
            cell.Model.SetTotemAttackModifier(1.5f); cell.Model.SetTotemSpeedModifier(0.8f);
            presenter.Refresh();
            Check(atkText.text == "480" && speedText.text == $"{0.4f:F2}초", "Totem bonuses displayed");
            drones.ApplyDroneBuff(2, 2, 10);
            Set(panel, "_isShowing", true); Set(panel, "_justShown", true);
            Invoke(panel, "LateUpdate");
            Check(atkText.text == "960" && speedText.text == $"{0.2f:F2}초", "Open panel refresh includes legion attack and speed buff");
            Set(drones, "_buffEndTime", -1f); Set(panel, "_justShown", true); Invoke(panel, "LateUpdate");
            Check(atkText.text == "480" && speedText.text == $"{0.4f:F2}초", "Expired buff disappears while panel stays open");
            var production = Asset<LevelUpData>(); production.chooseId = 15101;
            DroneSelectionPresets.Configure(production, DroneSelectionKind.ExtraCombatDrone); manager.ApplyEffect(production);
            presenter.Refresh(); Check(atkText.text == "600", "Additional drone reflected without reopening");
            var haste = Asset<LevelUpData>(); haste.chooseId = 15102;
            DroneSelectionPresets.Configure(haste, DroneSelectionKind.BetanAttackSpeed); manager.ApplyEffect(haste);
            presenter.Refresh(); Check(speedText.text == $"{(0.4f / 1.2f):F2}초", "Betan selection modifies displayed interval");
            manager.RemoveEffect(production); presenter.Refresh(); Check(atkText.text == "480", "Removed drone reduces display");
            beta.RemoveForCheck(); presenter.Refresh(); Check(atkText.text == "0", "No live drones means zero combined attack");
            Set(beta, "<currentCell>k__BackingField", null); Check(!presenter.Refresh(), "Removed unit invalidates current view");
            var zelta = Component<Drone_Zeltan>(); zelta.unitData = Asset<UnitData>();
            zelta.Init(new UnitDependencies()); presenter.SetUnitData(zelta);
            Check(atkText.text == "0", "Zero-attack Zeltan remains a visible numeric field");
            var frog = Component<FrogArcher>(); frog.unitData = Asset<UnitData>(); frog.unitData.atk.normal = 50;
            frog.unitData.attackSpeed.normal = 2; frog.Init(new UnitDependencies()); presenter.SetUnitData(frog);
            Check(atkText.text == "50", "Switching unit resets cached drone display");
            UnityEngine.Object.DestroyImmediate(frog.gameObject); Check(!presenter.Refresh(), "Destroyed unit invalidates view");
            presenter.SetUnitData(zelta.unitData); Check(presenter.Refresh(), "Static SO fallback uses no sheet service");
            presenter.Clear(); Check(!presenter.Refresh(), "Closing clears old target");
            Check(typeof(UI_UnitInfoPanel).GetField("statSlot_Food", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "Deleted food display not restored");
            Debug.Log($"[UnitInfoDisplayChecks] PASS {_passed} assertions.");
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
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _passed++; }
}
