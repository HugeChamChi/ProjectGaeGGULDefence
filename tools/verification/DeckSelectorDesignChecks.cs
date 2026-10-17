using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using UnityEngine;
using UnityEngine.UI;
public static class DeckSelectorDesignChecks
{
    public static string Audit()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return string.Join("\n", scene.GetRootGameObjects().Select(x=>x.name)) + "\nLoaded scenes: " + UnityEngine.SceneManagement.SceneManager.sceneCount;
    }
    public static async Task<string> Capture()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play mode.");
        await Task.Delay(350);
        var selector = UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>();
        var design = selector.GetComponent<DeckUpgradeDesignVariant>();
        var page = selector.transform.Find("DroneUpgradeCanvas/SafeArea/Page");
        var grid = page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
        if (grid.GetChild(0).Find("DroneFrameTrim") != null || grid.GetChild(0).Find("SlotNamePlate") != null)
            throw new Exception("Frame decorations and nameplate must be hidden.");
        if (page.Find("Header") != null || page.Find("DeckScroll") != null || ((RectTransform)page.Find("CollectionBanner")).anchoredPosition.y != -112)
            throw new Exception("Hide header while reserving its original space.");
        if (page.GetComponent<Image>().color != page.Find("CollectionBanner").GetComponent<Image>().color)
            throw new Exception("Screen background must match banner color without a separate top block.");
        var crown = (RectTransform)grid.GetChild(0).Find("FloatingGoldCrown");
        if (crown == null || grid.GetChild(0).Find("ChiefGoldMotes").childCount != 9) throw new Exception("Chief crown and motes missing.");
        float crownY = crown.anchoredPosition.y; await Task.Delay(120);
        if (Mathf.Abs(crownY - crown.anchoredPosition.y) < .01f) throw new Exception("Crown must float slowly.");
        if (page.Find("LobbyTopPreview") == null || !page.Find("LobbyTopPreview").gameObject.activeInHierarchy)
            throw new Exception("Copied lobby top UI must be visible.");
        if (grid.childCount != 13) throw new Exception("Unit collection missing.");
        var output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs"); Directory.CreateDirectory(output);
        string prefix = design.variant.ToString("00");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, prefix + "-list.png")); await Task.Delay(180);
        grid.GetChild(1).GetComponent<Button>().onClick.Invoke(); await Task.Delay(350);
        if (selector.screen.PopupPreview.Graphic == null) throw new Exception("Spine preview missing.");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, prefix + "-detail.png")); await Task.Delay(180); selector.screen.CloseDetails();
        var toggle = page.Find("DeckCollection/DeckSummaryStrip/OwnedOnlyToggle").GetComponent<Toggle>(); toggle.isOn = true;
        if (grid.Cast<Transform>().Count(x=>x.gameObject.activeSelf) != 6) throw new Exception("Owned filter failed."); toggle.isOn = false;
        File.WriteAllText(Path.Combine(output, prefix + "-RESULT.txt"), "PASS: independent variant, clean frames, name text only, unit information, Spine, owned-only filtering.");
        return "PASS design " + design.variant;
    }
}
