using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;
public static class DeckUpgradeGlossaryChecks
{
    public static Task<string> Hacking() => Check("\uB378\uD0D5", "hack_stack", 3);
    public static Task<string> Bomb() => Check("\uBCA0\uD0D5", "self_destruct_drone", 6);
    public static Task<string> Chief() => Check(null, "chief_skill", 3);
    public static async Task<string> ChiefBuff()
    {
        await Task.Delay(300);
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>(); screen.SelectUnit(-1); await Task.Delay(150);
        var dialog = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/UnitDetailsPopup/UnitInfoDialog");
        dialog.GetComponent<DeckUpgradeStatsTabs>().Select(1, false);
        var view = dialog.GetComponentInChildren<DeckUpgradeBonusView>(true);
        view.GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = 0;
        var text = view.transform.Find("LevelBonus_12/BonusDescription").GetComponent<TMP_Text>();
        var target = text.GetComponent<TermLinkTapTarget>(); Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
        if (!target.TryGetLinkWorldBounds("upgrade:chief_attack_speed", out var min, out var max)) throw new Exception("Buff link missing");
        var point = RectTransformUtility.WorldToScreenPoint(null, (min + max) * .5f);
        var drag = Pointer(point); target.OnPointerDown(drag); drag.dragging = true; target.OnPointerClick(drag);
        if (view.EffectTooltip != null && view.EffectTooltip.IsOpen) throw new Exception("Drag opened effect tooltip");
        Tap(point); await Task.Delay(200);
        var tip = view.EffectTooltip;
        if (tip == null || !tip.IsOpen || (view.Popup != null && view.Popup.IsOpen)) throw new Exception("Buff must open only a local bubble");
        if (!tip.Body.Contains("5") || !tip.Body.Contains("15%")) throw new Exception("Buff duration/amount missing");
        foreach (var label in tip.GetComponentsInChildren<TMP_Text>())
            if (label.name != "EffectTitle" && label.name != "EffectBody") throw new Exception("Unexpected category badge or extra control");
        var bubble = (RectTransform)tip.transform.Find("EffectBubble"); var corners = new Vector3[4]; bubble.GetWorldCorners(corners);
        if (corners[0].y <= max.y) throw new Exception("Bubble must sit above the clicked effect");
        string output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs"); Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-attack-speed-bubble.png")); await Task.Delay(200);
        Tap(new Vector2(5, Screen.height * .5f)); if (tip.IsOpen) throw new Exception("Outside tap failed to dismiss");
        Tap(point); screen.SelectUnit(0); if (tip.IsOpen) throw new Exception("Unit change left effect tooltip visible");
        return "PASS: green effect tap opens badge-free bubble above the linked words; 5 seconds / 15%, drag cancellation, outside dismissal and unit-change cleanup.";
    }
    private static async Task<string> Check(string name, string id, int level)
    {
        await Task.Delay(350);
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        int index = name == null ? -1 : Array.FindIndex(screen.units, x => x.unitName == name);
        if (name != null && index < 0) throw new Exception("Unit missing");
        screen.SelectUnit(index); await Task.Delay(150);
        var dialog = screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/UnitDetailsPopup/UnitInfoDialog");
        dialog.GetComponent<DeckUpgradeStatsTabs>().Select(1, false); await Task.Delay(100);
        var view = dialog.GetComponentInChildren<DeckUpgradeBonusView>(true);
        view.GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = level >= 9 ? 0 : 1;
        var text = view.transform.Find("LevelBonus_" + level + "/BonusDescription").GetComponent<TMP_Text>();
        var target = text.GetComponent<TermLinkTapTarget>();
        Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
        string link = TermLinker.LinkPrefix + id;
        if (!target.TryGetLinkWorldBounds(link, out var min, out var max)) throw new Exception("Keyword link missing: " + id);
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, (min + max) * .5f);
        var pointer = Pointer(point);
        target.OnPointerDown(pointer); pointer.dragging = true; target.OnPointerClick(pointer);
        if (view.Popup != null && view.Popup.IsOpen) throw new Exception("Scroll drag opened glossary");
        Tap(point);
        if (view.Popup == null || !view.Popup.IsOpen) throw new Exception("Keyword tap did not open production popup");
        var player = view.Popup.Film.GetComponent<VideoPlayer>();
        var settings = Resources.Load<DeckUpgradeGlossarySettings>("DeckUpgradeGlossarySettings");
        settings.Glossary.TryFind(id, out var term);
        for (int i = 0; i < 30 && !view.Popup.Film.NoiseCleared; i++) await Task.Delay(200);
        if (player.clip != term.Clip || !player.isPrepared || !player.isPlaying || !view.Popup.Film.NoiseCleared)
            throw new Exception("Original choice video failed to play: " + id);
        string output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/unit-upgrade-designs");
        Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-term-" + id.Replace(':', '-') + ".png")); await Task.Delay(200);
        Tap(new Vector2(5, Screen.height * .5f)); await Task.Delay(300);
        if (view.Popup.BlocksOwnerInput || (player != null && player.isPlaying)) throw new Exception("Outside tap must close and stop video");
        Tap(point); await Task.Delay(100);
        screen.SelectUnit(index == -1 ? 0 : -1); await Task.Delay(100);
        if (view.Popup.BlocksOwnerInput || (player != null && player.isPlaying)) throw new Exception("Unit switch left video open");
        return "PASS " + id + ": keyword raycast/tap, drag cancellation, original clip playback, outside dismissal and unit-switch cleanup. No progress changes.";
    }
    private static PointerEventData Pointer(Vector2 point) => new PointerEventData(EventSystem.current)
    { position = point, pointerId = -1, button = PointerEventData.InputButton.Left };
    private static void Tap(Vector2 point)
    {
        var data = Pointer(point); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0) throw new Exception("No UI hit at " + point);
        data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
        var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        ExecuteEvents.Execute(receiver, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(receiver, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(receiver, data, ExecuteEvents.pointerClickHandler);
    }
}
