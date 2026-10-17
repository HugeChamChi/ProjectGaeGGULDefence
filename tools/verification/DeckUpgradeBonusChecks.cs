using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public static class DeckUpgradeBonusChecks
{
    public static async Task<string> Check()
    {
        await Task.Delay(400);
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        var dialog = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/UnitDetailsPopup/UnitInfoDialog");
        var progress = (DeckUpgradeProgress)typeof(DroneUpgradeConceptScreen).GetField("progress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
        string snapshot = JsonUtility.ToJson(progress);
        string output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs");
        Directory.CreateDirectory(output);
        try
        {
            for (int index = -1; index < screen.units.Length; index++)
            {
                screen.SelectUnit(index); await Task.Delay(80);
                var view = dialog.GetComponentInChildren<DeckUpgradeBonusView>(true);
                for (int i = 0; i < 5; i++)
                {
                    var row = view.transform.Find("LevelBonus_" + ((i + 1) * 3));
                    string label = row.Find("BonusDescription").GetComponent<TMP_Text>().text;
                    if (string.IsNullOrEmpty(label)) throw new Exception("Missing unit bonus");
                    if (System.Text.RegularExpressions.Regex.IsMatch(System.Text.RegularExpressions.Regex.Replace(label, "<[^>]+>", ""), @"\d") && !label.Contains("<color=#FFA64D>"))
                        throw new Exception("Bonus values must be orange");
                    if (row.Find("BonusDetailsTarget") != null || dialog.Find("BonusTooltipDismiss") != null) throw new Exception("Tooltip should be removed");
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(label, @"<color=#FFDD8F>(.*?)</color>"))
                    {
                        string term = match.Groups[1].Value;
                        if (term != "[\uD574\uD0B9]" && term != "[\uC790\uD3ED\uB4DC\uB860]" && term != "[\uC871\uC7A5 \uC2A4\uD0AC]") throw new Exception("Only proper nouns may be highlighted");
                    }
                }
                string id = index < 0 ? DeckUpgradeProgress.ChiefId : screen.units[index].name;
                // Resolve the same identifier as the preview controller.
                id = (string)typeof(DroneUpgradeConceptScreen).GetMethod("Id", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, new object[] { index });
                var entry = progress.units.Find(x => x.id == id);
                if (entry == null) { entry = new DeckUpgradeProgress.Entry { id = id }; progress.units.Add(entry); }
                foreach (int level in new[] { 1, 3, 6, 9, 12, 15 })
                {
                    entry.level = level; screen.SetAbilityTab(1);
                    for (int i = 0; i < 5; i++)
                    {
                        var row = view.transform.Find("LevelBonus_" + ((i + 1) * 3));
                        bool locked = level < (i + 1) * 3;
                        if (row.Find("LockedCover").gameObject.activeSelf != locked || row.Find("BonusLock").gameObject.activeSelf != locked) throw new Exception("Unlock boundary failed");
                    }
                }
            }
            foreach (int money in new[] { 0, 100000 })
            {
                progress.money = money; screen.SelectUnit(0); await Task.Delay(100);
                var grid = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
                foreach (var gauge in grid.GetComponentsInChildren<UpgradeCostGauge>(true))
                {
                    if (gauge.transform.Find("GoldIcon") != null) throw new Exception("List gold icon remains");
                    if (gauge.Ready != (money > 0) || gauge.Arrow.gameObject.activeSelf != gauge.Ready || gauge.Arrow.anchorMin.x != 1) throw new Exception("List readiness arrow failed");
                    var cost = gauge.GetComponentInChildren<TMP_Text>();
                    if (!cost.text.Contains("/")) throw new Exception("List owned/required amount missing");
                    if (gauge.Ready)
                    {
                        Vector3 scale = gauge.Arrow.localScale; await Task.Delay(100);
                        if (gauge.Arrow.localScale == scale) throw new Exception("Ready arrow must animate");
                    }
                }
            }
            dialog.parent.gameObject.SetActive(false); await Task.Delay(100);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-unit-upgrade-gauges.png")); await Task.Delay(200);
        }
        finally { var restored = JsonUtility.FromJson<DeckUpgradeProgress>(snapshot); progress.money = restored.money; progress.units = restored.units; screen.SelectUnit(0); }
        await Task.Delay(150);
        dialog.GetComponent<DeckUpgradeStatsTabs>().Select(1, false);
        await Task.Delay(150);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-level-bonuses.png")); await Task.Delay(200);
        return "PASS: six unit bonus sets, concise labels, proper-noun highlights, no tooltips and lock boundaries 1/3/6/9/12/15. Preview progress restored; no save writes.";
    }
}
