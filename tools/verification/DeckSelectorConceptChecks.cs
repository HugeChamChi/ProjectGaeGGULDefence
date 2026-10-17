using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using UnityEngine;
using UnityEngine.UI;
public static class DeckSelectorConceptChecks
{
    public static async Task<string> CheckAndExport()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play mode.");
        await Task.Delay(250);
        var selector = UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>(); var screen = selector.screen;
        var page = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page");
        var grid = page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
        if (grid.childCount != 13 || grid.GetChild(0).name != "ChiefUnit") throw new Exception("Chief-first unit order failed.");
        for (int i = 0; i < 5; i++) if (grid.GetChild(i + 1).name != "Unit_" + i) throw new Exception("Drone units must stay consecutive.");
        for (int i = 0; i < 3; i++)
            if (Mathf.Abs(((RectTransform)grid.GetChild(i)).anchoredPosition.y + 40) > .1f) throw new Exception("First row must contain three units.");
        for (int i = 0; i < 6; i++)
        {
            var card = grid.GetChild(i);
            if (card.Find("DroneFrameTrim/CombatDroneBadge") == null) throw new Exception("Drone frame decoration missing.");
            var gauge = (RectTransform)(i == 0 ? card.Find("ChiefGoldGauge") : card.Find("UnitFrame/CostStrip"));
            var text = (RectTransform)gauge.Find(i == 0 ? "ChiefCost" : "Cost");
            if (Mathf.Abs(gauge.anchoredPosition.x + gauge.rect.width * .5f - 116) > .1f || text.anchoredPosition != Vector2.zero)
                throw new Exception("Gauge or cost text is off center.");
        }
        if (page.Find("DeckUpgradePopup") != null || page.Find("DeckScroll").gameObject.activeSelf) throw new Exception("Old deck view must be removed.");
        var dialog = page.Find("UnitDetailsPopup/UnitInfoDialog");
        var upgradeButton = (RectTransform)dialog.Find("UpgradeButton");
        if (upgradeButton.rect.width != 680 || upgradeButton.rect.height != 128 || upgradeButton.Find("RequiredGoldCost") == null || dialog.Find("StatsPanel/PopupGoldGauge") == null)
            throw new Exception("Button must show cost only, gauge must be in the upper information panel.");
        if (dialog.Find("StatsPanel/SelectedUnitSlot") == null || page.GetComponentsInChildren<Transform>(true).Any(x => x.name == "ChiefBadge"))
            throw new Exception("Selected slot or chief badge removal failed.");
        var statRows = dialog.Find("StatsPanel/UpgradeStatRows");
        if (statRows.Find("AttackRow/IconSlot/AttackIcon") == null || statRows.Find("AttackSpeedRow/IconSlot/AttackSpeedIcon") == null)
            throw new Exception("Separate stat rows and icon slots missing.");
        if (dialog.Find("StatsPanel/SelectedUnitSlot/ChiefSlotFrame/DroneFrameTrim/ChiefCrestBase") == null)
            throw new Exception("Chief detail frame must match collection crest.");
        var divider = (RectTransform)grid.Find("UnownedHeader");
        if (divider == null || ((RectTransform)grid.GetChild(6)).anchoredPosition.y >= divider.anchoredPosition.y - 64)
            throw new Exception("Unowned units must begin below the divider on a fresh row.");
        string output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/deck-selector");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "unit-grid.png")); await Task.Delay(180);
        grid.GetChild(0).GetComponent<Button>().onClick.Invoke(); await Task.Delay(300);
        if (screen.PopupPreview.Graphic == null) throw new Exception("Chief detail missing.");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "chief-detail.png")); await Task.Delay(180); screen.CloseDetails();
        grid.GetChild(1).GetComponent<Button>().onClick.Invoke(); await Task.Delay(300);
        if (screen.PopupPreview.Graphic == null) throw new Exception("Unit detail missing.");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "unit-detail.png")); await Task.Delay(180); screen.CloseDetails();
        var toggle = page.Find("DeckCollection/DeckSummaryStrip/OwnedOnlyToggle").GetComponent<Toggle>(); toggle.isOn = true;
        if (grid.Cast<Transform>().Count(x => x.gameObject.activeSelf) != 6) throw new Exception("Owned filter failed.");
        await Task.Delay(100); ScreenCapture.CaptureScreenshot(Path.Combine(output, "owned-only.png")); await Task.Delay(180); toggle.isOn = false;
        using (var sha = SHA256.Create())
        {
            string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/WorkSpace/USW/DeckUpgrade/Legacy/DroneUpgradeConcept2.unity"))).Replace("-", "");
            if (hash != File.ReadAllText(Path.Combine(output, "original-scene.sha256"))) throw new Exception("Original scene changed.");
        }
        string result = "PASS: three-column unit collection; chief first; same-deck units consecutive; owned-only filtering; direct chief/unit detail; original scene unchanged.";
        File.WriteAllText(Path.Combine(output, "RESULT.md"), result); return result;
    }
}
