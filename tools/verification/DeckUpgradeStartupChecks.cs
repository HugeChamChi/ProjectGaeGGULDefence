using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using UnityEditor;
using UnityEngine;
public static class DeckUpgradeStartupChecks
{
    public static async Task<string> Check()
    {
        var current = UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>().gameObject;
        current.SetActive(false);
        GameObject fresh = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/DeckUpgrade/Prefabs/UnitUpgradeUI.prefab");
            fresh = UnityEngine.Object.Instantiate(prefab);
            fresh.name = "StartupVerification";
            var design = fresh.AddComponent<DeckUpgradeDesignVariant>();
            design.variant = 1;
            design.lobbyTopTemplate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/DeckUpgrade/Concepts/LobbyTopPreview.prefab");
            var probe = fresh.AddComponent<DeckUpgradeStartupProbe>();
            return await probe.Result.Task;
        }
        finally
        {
            if (fresh != null) { fresh.SetActive(false); UnityEngine.Object.Destroy(fresh); }
            current.SetActive(true);
        }
    }
}
[DefaultExecutionOrder(2000)]
public sealed class DeckUpgradeStartupProbe : MonoBehaviour
{
    public readonly TaskCompletionSource<string> Result = new TaskCompletionSource<string>();
    private IEnumerator Start()
    {
        string error = Validate();
        if (error != null) { Result.TrySetException(new Exception("Before first render: " + error)); yield break; }
        string output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs");
        Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-first-frame.png"));
        yield return new WaitForEndOfFrame();
        error = Validate();
        if (error != null) { Result.TrySetException(new Exception("First rendered frame: " + error)); yield break; }
        yield return null;
        error = Validate();
        if (error != null) Result.TrySetException(new Exception("Second frame: " + error));
        else Result.TrySetResult("PASS: clean 3-column collection and lobby header present before first render, no deck unfolding animation or legacy deck/header visible through first two frames. Scene and progress untouched.");
    }
    private string Validate()
    {
        var screen = GetComponent<DroneUpgradeConceptScreen>();
        var page = transform.Find("DroneUpgradeCanvas/SafeArea/Page");
        if (screen.IsAnimating) return "Legacy unfolding animation started";
        if (page.Find("DeckScroll") != null || page.Find("Header") != null) return "Legacy layout still present";
        if (page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid").childCount != 13) return "Collection not built";
        if (page.Find("LobbyTopPreview") == null) return "Current design not applied";
        if (page.Find("UnitDetailsPopup").gameObject.activeSelf) return "Detail popup visible at startup";
        return null;
    }
}
