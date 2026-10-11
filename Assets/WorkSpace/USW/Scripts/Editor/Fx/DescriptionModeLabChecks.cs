using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using Object = UnityEngine.Object;

/// <summary>Play-mode raycast, state, rendering and video continuity checks for the isolated preview.</summary>
public static class DescriptionModeLabChecks
{
    private const string Folder = "outputs/description-mode-20261011";
    private static readonly StringBuilder Log = new StringBuilder();
    private static int _pass;
    private static int _fail;
    private static bool _running;

    /// <summary>Runs on the current game-view aspect; writes evidence without changing source assets.</summary>
    [MenuItem("Tools/USW/Fx/Check Description Mode Lab (Play)")]
    public static void Run()
    {
        if (_running) throw new InvalidOperationException("Checks already running");
        var lab = Object.FindAnyObjectByType<DescriptionModeLab>();
        if (!Application.isPlaying || lab == null) throw new InvalidOperationException("Play DescriptionMode lab first");
        RunAsync(lab).Forget();
    }

    private static async UniTaskVoid RunAsync(DescriptionModeLab lab)
    {
        _running = true;
        _pass = _fail = 0;
        Log.Clear();
        Directory.CreateDirectory(Folder);
        var token = lab.GetCancellationTokenOnDestroy();
        try
        {
            var independent = new DescriptionModeState();
            int changed = 0;
            independent.OnChanged += _ => changed++;
            independent.Set(DescriptionMode.Brief);
            independent.Set(DescriptionMode.Detailed);
            independent.Set(DescriptionMode.Detailed);
            Expect(changed == 1 && new DescriptionModeState().Current == DescriptionMode.Brief, "New session defaults; duplicate mode does not notify");
            lab.ShowSelections();
            lab.State.Set(DescriptionMode.Brief);
            await Wait(0.4f, token);
            int count = lab.SelectionCount;
            var firstText = lab.GetDescription(0);
            var originalObject = firstText.gameObject;
            string original = firstText.text;
            TapControl(lab, false, true);
            await Wait(0.2f, token);
            Expect(lab.State.Current == DescriptionMode.Detailed && lab.GetDescription(0).text != original, "Screen toggle tap changes description");
            Expect(lab.GetDescription(0).gameObject == originalObject && lab.SelectionCount == count, "Toggle keeps cards and never chooses/rerolls");
            CheckDescriptions(lab, "level detailed");
            Shot("A-level-detailed");
            await Wait(0.2f, token);
            TapControl(lab, false, false);
            await Wait(0.2f, token);
            CheckDescriptions(lab, "level brief");
            Shot("A-level-brief");
            await Wait(0.2f, token);
            TapTerm(lab.GetDescription(1), "chief_skill");
            await Wait(1.8f, token);
            Expect(lab.Popup.IsOpen && lab.SelectionCount == count && lab.Popup.Title == "족장 스킬", "Term tap opens popup without selecting card");
            string brief = lab.Popup.Body;
            var player = lab.Popup.Film.GetComponent<VideoPlayer>();
            Expect(player != null && player.isPlaying, "Term video playing before toggle");
            bool preparedBefore = player != null && player.isPrepared;
            double timeBefore = player != null ? player.time : 0;
            TapControl(lab, true, true);
            Expect(player != null && player.isPrepared == preparedBefore && Math.Abs(player.time - timeBefore) < 0.1,
                "Popup toggle does not stop, prepare or seek the video");
            await Wait(0.25f, token);
            Expect(lab.State.Current == DescriptionMode.Detailed && lab.Popup.Body != brief && lab.Popup.IsOpen && lab.SelectionCount == count,
                "Popup toggle shares state and does not close/select");
            Expect(lab.GetDescription(0).text != original, "Card behind popup receives shared preference");
            CheckPopup(lab);
            Shot("popup-detailed");
            await Wait(0.2f, token);
            Tap(new Vector2(Screen.width * 0.04f, Screen.height * 0.6f));
            await Wait(0.4f, token);
            Expect(!lab.Popup.IsOpen && lab.SelectionCount == count && !lab.Popup.Film.IsPlaying, "Outside tap only closes popup and stops video");
            lab.ShowTotems();
            await Wait(0.3f, token);
            Expect(lab.State.Current == DescriptionMode.Detailed && lab.ShowingTotems, "Totem screen inherits detailed mode");
            CheckDescriptions(lab, "totem detailed");
            Shot("totem-detailed");
            await Wait(0.2f, token);
            TapControl(lab, false, false);
            await Wait(0.2f, token);
            CheckDescriptions(lab, "totem brief");
            Expect(lab.GetDescription(2).text.Contains("-20%") && lab.GetDescription(1).text.Contains("초기화"), "Brief totems retain penalty and reset rule");
            Shot("totem-brief");
            await Wait(0.2f, token);
            TapTerm(lab.GetDescription(1), "food");
            await Wait(0.5f, token);
            Expect(lab.Popup.IsOpen && lab.Popup.Title == "식량" && lab.SelectionCount == count, "Totem link opens same popup without choosing");
            TapControl(lab, true, true);
            await Wait(0.2f, token);
            CheckPopup(lab);
            Shot("totem-popup");
            await Wait(0.2f, token);
            lab.Popup.HideImmediately();
            lab.ShowSelections();
            await Wait(0.2f, token);
            Expect(lab.State.Current == DescriptionMode.Detailed, "Returning to level-up keeps preference");
            for (int i = 0; i < 40; i++) lab.State.Set((DescriptionMode)(i % 2));
            Expect(lab.SelectionCount == count && Object.FindObjectsByType<DescriptionModeToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 2,
                "Repeated changes do not select or duplicate controls");
            // Re-enable must read the current preference and must not duplicate event subscriptions.
            var top = TopControl(lab);
            top.gameObject.SetActive(false);
            lab.State.Set(DescriptionMode.Brief);
            top.gameObject.SetActive(true);
            TapControl(lab, false, true);
            Expect(lab.State.Current == DescriptionMode.Detailed, "Re-enabled toggle reconnects");
            lab.TogglePlacement();
            Shot("B-below-heading");
            await Wait(0.25f, token);
            lab.TogglePlacement();
            // A non-link location on the first card still selects normally.
            var card = lab.GetDescription(0).GetComponentInParent<LevelUpCardUI>();
            foreach (var label in card.GetComponentsInChildren<TMP_Text>())
                if (label.text == card.GetData().chooseName) { Tap(ScreenOf(label.rectTransform)); break; }
            await Wait(0.2f, token);
            Expect(lab.SelectionCount == count + 1, "Ordinary card tap remains selectable");
            lab.State.Set(DescriptionMode.Brief);
        }
        catch (Exception exception) { Expect(false, exception.ToString()); }
        finally
        {
            Log.AppendLine($"PASS {_pass} / FAIL {_fail}; GameView {Screen.width}x{Screen.height}");
            File.WriteAllText(Folder + "/checks.txt", Log.ToString());
            _running = false;
            Debug.Log(Log.ToString());
        }
    }

    private static void CheckDescriptions(DescriptionModeLab lab, string label)
    {
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < 3; i++)
        {
            var text = lab.GetDescription(i);
            text.ForceMeshUpdate();
            Expect(!text.isTextOverflowing && !text.text.Contains("{"), $"{label} card {i}: no overflow or unresolved tokens ({text.preferredHeight:0}/{text.rectTransform.rect.height:0})");
        }
    }

    private static void CheckPopup(DescriptionModeLab lab)
    {
        Canvas.ForceUpdateCanvases();
        var body = lab.Popup.Sheet.Find("Body").GetComponent<TMP_Text>();
        body.ForceMeshUpdate();
        Expect(!body.isTextOverflowing && !body.text.Contains("{"), "Popup body fits and resolves values");
        var bodyCorners = new Vector3[4];
        body.rectTransform.GetWorldCorners(bodyCorners);
        Expect(ScreenOfWorld(bodyCorners[0]).y >= 0, "Popup body stays on screen");
        if (lab.Popup.Film.Rect.gameObject.activeInHierarchy)
        {
            var film = new Vector3[4]; var sheet = new Vector3[4];
            lab.Popup.Film.Rect.GetWorldCorners(film); lab.Popup.Sheet.GetWorldCorners(sheet);
            Expect(film[0].y > sheet[1].y, "Video stays above resized sheet");
        }
    }

    private static DescriptionModeToggle TopControl(DescriptionModeLab lab)
    {
        foreach (var control in Object.FindObjectsByType<DescriptionModeToggle>(FindObjectsSortMode.None))
            if (!control.transform.IsChildOf(lab.Popup.transform)) return control;
        throw new InvalidOperationException("Top control missing");
    }
    private static void TapControl(DescriptionModeLab lab, bool popup, bool detailed)
    {
        var control = popup ? lab.Popup.GetComponentInChildren<DescriptionModeToggle>(true) : TopControl(lab);
        Tap(ScreenOf((RectTransform)control.transform.Find(detailed ? "Detailed" : "Brief")));
    }
    private static void TapTerm(TMP_Text text, string id)
    {
        text.ForceMeshUpdate();
        var link = text.GetComponent<TermLinkTapTarget>();
        if (!link.TryGetLinkWorldBounds(TermLinker.LinkPrefix + id, out var min, out var max))
            throw new InvalidOperationException("Missing link " + id);
        Tap(ScreenOfWorld((min + max) * 0.5f));
    }
    private static void Tap(Vector2 screen)
    {
        var es = EventSystem.current;
        var data = new PointerEventData(es) { position = screen, pressPosition = screen, pointerId = -1,
            button = PointerEventData.InputButton.Left, clickCount = 1, eligibleForClick = true };
        var hits = new List<RaycastResult>();
        es.RaycastAll(data, hits);
        if (hits.Count == 0) throw new InvalidOperationException("No raycast hit at " + screen);
        var go = hits[0].gameObject;
        data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
        var press = ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.pointerDownHandler);
        var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
        if (press == null) press = click;
        data.pointerPress = press; data.rawPointerPress = go; data.pointerClick = click;
        if (press != null) ExecuteEvents.Execute(press, data, ExecuteEvents.pointerUpHandler);
        if (click != null && click == press) ExecuteEvents.Execute(click, data, ExecuteEvents.pointerClickHandler);
    }
    private static Vector2 ScreenOf(RectTransform rt) => ScreenOfWorld(rt.TransformPoint(rt.rect.center));
    private static Vector2 ScreenOfWorld(Vector3 world) => RectTransformUtility.WorldToScreenPoint(Camera.main, world);
    private static void Expect(bool value, string label)
    { if (value) _pass++; else _fail++; Log.AppendLine((value ? "PASS " : "FAIL ") + label); }
    private static void Shot(string name) => ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
    private static UniTask Wait(float seconds, CancellationToken token) => UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, cancellationToken: token);
}
