/// <summary>Preview-scene checks for active boss debuff presentation and expiry.</summary>
public static class BossDebuffUiChecks
{
    /// <summary>Runs checks without modifying the loaded scene or assets.</summary>
    public static object RunChecks()
    {
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var checks = new System.Collections.Generic.List<string>();
System.Action<bool,string> check = (ok, name) => { if (!ok) throw new System.Exception(name); checks.Add(name); };
try
{
    var root = new UnityEngine.GameObject("Debuff UI check", typeof(UnityEngine.RectTransform), typeof(BossDebuffBar));
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
    var bar = root.GetComponent<BossDebuffBar>();
    var settings = UnityEngine.Resources.Load<DebuffSettings>("DebuffSettings");
    bar.Configure(null, settings, new UnityEngine.Vector2(58,58), null);
    var controller = new DebuffController(d => {}, () => true);
    var damage = settings.FindPresentation(1003).CreateDefinition();
    controller.Apply(damage, new DebuffApplyContext(1,1,100000));
    bar.Refresh(controller);
    check(root.transform.childCount == 1 && root.transform.GetChild(0).gameObject.activeSelf, "Applied effect is visible");
    check(controller.DamageTakenMultiplier == 1.2m, "Deltan definition gives 20% damage taken");
    var detail = root.transform.GetChild(0).Find("DurationStacks").GetComponent<TMPro.TMP_Text>();
    check(detail.text == "5s", "Initial duration");
    controller.Advance(2);
    bar.Refresh(controller);
    check(detail.text == "3s", "Combat clock remaining time");
    bar.Refresh(controller);
    check(detail.text == "3s" && controller.CurrentTime == 2, "UI does not advance paused clock");
    controller.Apply(damage, new DebuffApplyContext(1,1,100000));
    bar.Refresh(controller);
    check(detail.text == "5s" && root.transform.childCount == 1, "Reapply refreshes same slot");
    controller.Advance(7);
    bar.Refresh(controller);
    check(!root.transform.GetChild(0).gameObject.activeSelf && controller.DamageTakenMultiplier == 1m, "Expiry removes icon and gameplay effect");
    controller.Apply(settings.FindPresentation(1002).CreateDefinition(), new DebuffApplyContext(1,3,100000));
    bar.Refresh(controller);
    check(detail.text == "x3", "Permanent armor break shows stacks without timer");
    bar.Refresh(null);
    check(!root.transform.GetChild(0).gameObject.activeSelf, "Missing boss clears row");
    controller.Clear();
    bar.Refresh(controller);
    check(!root.transform.GetChild(0).gameObject.activeSelf, "Death clear hides row");
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/WorkSpace/HSD/Prefab/UI/InGame/BossHpBar.prefab");
    var hpRoot = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
    var hp = hpRoot.GetComponentInChildren<UI_BossHpBar>(true);
    hp.ConfigureDebuffs(null, settings);
    hp.ConfigureDebuffs(null, settings);
    check(hp.GetComponentsInChildren<BossDebuffBar>(true).Length == 1, "Actual HP prefab supports one status row");
    return new { passed = checks.Count, checks };
}
finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }

    }
}
