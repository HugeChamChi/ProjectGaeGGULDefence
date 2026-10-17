using System;
using System.Linq;
using GaeGGUL.DeckUpgrade;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DeckUpgradeListGaugeAuthoring
{
    public static string AdjustHeight()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/WorkSpace/USW/DeckUpgrade/Concepts/UnitUpgrade_01_Clean.unity") throw new Exception("Open current clean scene.");
        var selector = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DeckSelectorConcept>(true)).First();
        var grid = selector.transform.Find("DroneUpgradeCanvas/SafeArea/Page/DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
        var gauges = grid.GetComponentsInChildren<UpgradeCostGauge>(true);
        if (gauges.Length != 6) throw new Exception("Expected six list gauges");
        foreach (var gauge in gauges)
        {
            var rect = (RectTransform)gauge.transform; Undo.RecordObject(rect, "Raise unit upgrade gauge");
            rect.anchoredPosition = new Vector2(0, -241); EditorUtility.SetDirty(rect);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Saved six list gauges 8 canvas units higher, centered at x=0, y=-241. Size and child layout unchanged.";
    }
    public static async System.Threading.Tasks.Task<string> Capture()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        var progress = (DeckUpgradeProgress)typeof(DroneUpgradeConceptScreen).GetField("progress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(screen);
        var refresh = typeof(DroneUpgradeConceptScreen).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int money = progress.money;
        try
        {
            progress.money = 1500; refresh.Invoke(screen, null);
            var grid = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
            foreach (var gauge in grid.GetComponentsInChildren<UpgradeCostGauge>(true))
            {
                if (gauge.transform.Find("GoldIcon") != null || !gauge.GetComponentInChildren<TMP_Text>().text.StartsWith("1500/")) throw new Exception("Wrong list gauge");
                if (gauge.Ready != gauge.Arrow.gameObject.activeSelf || gauge.Arrow.anchorMin.x != 1) throw new Exception("Wrong ready arrow");
                var slot = (RectTransform)gauge.transform;
                var frame = (RectTransform)slot.parent.Find(slot.name == "ChiefGoldGauge" ? "ChiefFrame" : "ColoredEdge");
                var text = gauge.GetComponentInChildren<TMP_Text>();
                Vector3 center = slot.TransformPoint(slot.rect.center), frameCenter = frame.TransformPoint(frame.rect.center);
                Vector3 textCenter = text.rectTransform.TransformPoint(text.rectTransform.rect.center);
                if (Mathf.Abs(center.x - frameCenter.x) > .05f || Vector2.Distance(center, textCenter) > .05f) throw new Exception("Gauge or amount is off center");
                var mask = slot.Find("GaugeClip").GetComponent<UnityEngine.UI.Image>();
                if (gauge.Fill.sprite != mask.sprite || gauge.Fill.sprite == null || gauge.Fill.sprite.border.x <= 0 || gauge.Fill.pixelsPerUnitMultiplier != mask.pixelsPerUnitMultiplier)
                    throw new Exception("Rounded track/fill slicing must match");
            }
            await System.Threading.Tasks.Task.Delay(200);
            string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs/01-list-gauge-location.png");
            ScreenCapture.CaptureScreenshot(path); await System.Threading.Tasks.Task.Delay(200);
            return "PASS: six gauge centers match unit frames, number centers match gauge centers; track and green fill share rounded slicing. 1500/required, no gold icons, right ready arrows. Preview balance restored.";
        }
        finally { progress.money = money; refresh.Invoke(screen, null); }
    }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/WorkSpace/USW/DeckUpgrade/Concepts/UnitUpgrade_01_Clean.unity") throw new Exception("Open the current clean design scene.");
        var selector = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DeckSelectorConcept>(true)).First();
        var grid = selector.transform.Find("DroneUpgradeCanvas/SafeArea/Page/DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
        var progress = DeckUpgradeProgress.Load();
        Undo.RegisterFullObjectHierarchyUndo(selector.gameObject, "Replace list gold costs with upgrade gauges");
        int count = 0;
        foreach (var oldGauge in grid.GetComponentsInChildren<UpgradeCostGauge>(true))
        {
            var slot = (RectTransform)oldGauge.transform;
            var price = slot.GetComponentsInChildren<TMP_Text>(true).First();
            slot.anchorMin = slot.anchorMax = new Vector2(.5f, 1); slot.pivot = new Vector2(.5f, 1);
            slot.anchoredPosition = new Vector2(0, -241); slot.sizeDelta = new Vector2(180, 34);
            price.rectTransform.anchorMin = price.rectTransform.anchorMax = price.rectTransform.pivot = new Vector2(.5f, .5f);
            price.rectTransform.anchoredPosition = Vector2.zero; price.rectTransform.sizeDelta = new Vector2(132, 34);
            price.alignment = TextAlignmentOptions.Center; price.margin = Vector4.zero;
            string id = slot.name == "ChiefGoldGauge" ? DeckUpgradeProgress.ChiefId
                : selector.screen.units[int.Parse(slot.parent.parent.name.Substring("Unit_".Length))].name;
            foreach (var child in slot.Cast<Transform>().ToArray())
                if (child.gameObject != price.gameObject) UnityEngine.Object.DestroyImmediate(child.gameObject);
            UnityEngine.Object.DestroyImmediate(oldGauge);
            var gauge = UpgradeCostGauge.Create(slot, selector.screen.style.RoundedFill, selector.screen.style.UpgradeArrow);
            // Edit-mode illustration only; runtime uses the isolated preview wallet's real balance.
            int available = Mathf.Max(1500, progress.money), required = progress.Cost(id);
            bool upgradeable = progress.Level(id) < DeckUpgradeProgress.MaxLevel;
            gauge.SetAmount(available, required, upgradeable);
            price.text = upgradeable ? available + "/" + required : "MAX";
            price.enableAutoSizing = true; price.fontSizeMin = 15; price.fontSizeMax = 23;
            price.transform.SetAsLastSibling();
            EditorUtility.SetDirty(slot); EditorUtility.SetDirty(price); count++;
        }
        if (count != 6) throw new Exception("Expected exactly six owned unit list gauges, got " + count);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Replaced six list-frame cost areas in the saved clean scene: no gold icons, owned/required gauge and right green ready arrow. Edit preview balance 1500; PlayerPrefs untouched.\n" + Inspect();
    }
    public static string Inspect()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var selector = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DeckSelectorConcept>(true)).First();
        var grid = selector.transform.Find("DroneUpgradeCanvas/SafeArea/Page/DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
        return scene.path + " dirty=" + scene.isDirty + " playing=" + EditorApplication.isPlaying + "\n" +
            string.Join("\n", grid.GetComponentsInChildren<UpgradeCostGauge>(true).Select(x => x.name + " icons=" + x.GetComponentsInChildren<Transform>(true).Count(t => t.name == "GoldIcon") + " text=" + x.GetComponentInChildren<TMP_Text>().text));
    }
}
