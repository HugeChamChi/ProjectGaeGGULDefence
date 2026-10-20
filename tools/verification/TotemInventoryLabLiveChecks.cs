using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Exercises conservation, full-storage exchanges and the lab's actual UGUI event handlers.</summary>
public static class TotemInventoryLabLiveChecks
{
    private const string Output = "outputs/totem-inventory-lab-20261020";
    private static readonly List<string> Results = new List<string>();
    /// <summary>Starts checks inside the existing lab Play session.</summary>
    public static string Run() { RunAsync().Forget(); return "Checks started"; }
    private static void Check(bool success, string label)
    {
        if (!success) throw new Exception(label);
        Results.Add(label);
    }
    private static async UniTask RunAsync()
    {
        Results.Clear();
        Directory.CreateDirectory(Output);
        var lab = UnityEngine.Object.FindFirstObjectByType<TotemInventoryLab>();
        try
        {
            Check(EditorApplication.isPlaying && lab != null, "Standalone lab running");
            using var timeout = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(lab.GetCancellationTokenOnDestroy());
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30));
            var token = timeout.Token;
            var settings = AssetDatabase.LoadAssetAtPath<TotemInventoryLabSettings>("Assets/WorkSpace/USW/Scene/FxLab_TotemInventorySettings.asset");
            Check(settings.Samples.All(data => data != null && data.DisplaySprite != null), "All five authored samples have real sprites");
            var model = new TotemInventoryLabState(settings);
            model.Reset(false);
            Check(model.Stored.Count == 3 && model.Field.Count(entry => entry != null) == 2, "Normal preset has two field and three stored items");
            var rooted = model.Field[12];
            rooted.Rotation = 3;
            Check(model.Recall(12) && ReferenceEquals(rooted, model.Stored[3]), "Recall preserves the same entry");
            Check(model.Stored[3].Rotation == 3 && model.Field[12] == null, "Recall retains orientation and clears field");
            Check(!model.Recall(12) && model.Stored.Count == 4, "Double recall cannot duplicate an item");
            Check(model.Place(3, 12) && ReferenceEquals(rooted, model.Field[12]), "Re-placement returns the exact item");
            Check(model.Field[12].Rotation == 3, "Re-placement retains orientation");
            model.Reset(true);
            var oldField = model.Field[12];
            var oldStored = model.Stored[1];
            Check(!model.Recall(12) && ReferenceEquals(oldField, model.Field[12]) && model.Stored.Count == 5, "Full-storage recall fails without losing either side");
            Check(model.Place(1, 12) && ReferenceEquals(oldStored, model.Field[12]) && ReferenceEquals(oldField, model.Stored[1]), "Full-storage swap is atomic");
            Check(model.Stored.Count == 5 && model.Field.Count(entry => entry != null) == 2, "Full-storage swap conserves item count");
            Check(!model.Place(-1, 2) && !model.Place(0, 99) && !model.Move(12, 12), "Invalid indices and self-move leave state intact");
            Check(model.Move(12, 0) && ReferenceEquals(oldStored, model.Field[0]), "Occupied field cells exchange items");
            Check(model.Move(0, 1) && model.Field[0] == null && ReferenceEquals(oldStored, model.Field[1]), "Field movement clears its source");

            lab.ResetSamples(false);
            var field = Target(lab, false, 12);
            var e = Pointer(field, 11);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(e, hits);
            Check(hits.Count > 0 && hits[0].gameObject == field.gameObject, "Actual board raycast reaches its pointer target");
            ExecuteEvents.Execute(field.gameObject, e, ExecuteEvents.pointerClickHandler);
            var recall = lab.transform.Find("Totem Inventory Lab UI/Portrait Content/Recall").gameObject;
            ExecuteEvents.Execute(recall, e, ExecuteEvents.pointerClickHandler);
            await UniTask.WaitUntil(() => !lab.IsBusy, cancellationToken: token);
            Check(lab.State.Field[12] == null && lab.State.Stored.Count == 4, "Field selection plus recall button transfers to storage");

            lab.ResetSamples(false);
            var slot = Target(lab, true, 0);
            var incoming = lab.State.Stored[0];
            var outgoing = lab.State.Field[0];
            e = Pointer(slot, 17);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.beginDragHandler);
            e.position = new Vector2(-500, -500);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.endDragHandler);
            Check(ReferenceEquals(incoming, lab.State.Stored[0]) && lab.State.Stored.Count == 3, "Off-board drag cancellation preserves storage");
            e = Pointer(slot, 18);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.beginDragHandler);
            var wrongPointer = Pointer(field, 99);
            lab.Drop(false, 12, wrongPointer);
            Check(ReferenceEquals(incoming, lab.State.Stored[0]), "Second pointer cannot commit the active drag");
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.endDragHandler);

            e = Pointer(slot, 19);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.beginDragHandler);
            var destination = Target(lab, false, 0);
            e.position = RectTransformUtility.WorldToScreenPoint(null, destination.transform.position);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(destination.gameObject, e, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(slot.gameObject, e, ExecuteEvents.endDragHandler);
            Check(lab.IsBusy, "Successful drop starts transfer presentation");
            lab.Transfer(false, 0, true, -1);
            Check(ReferenceEquals(lab.State.Field[0], incoming), "Busy state rejects overlapping transfers");
            await UniTask.WaitUntil(() => !lab.IsBusy, cancellationToken: token);
            Check(ReferenceEquals(lab.State.Field[0], incoming) && ReferenceEquals(lab.State.Stored[0], outgoing), "UGUI drag swaps both item identities");
            Check(lab.State.Stored.Count == 3, "UGUI swap conserves storage count");

            e = Pointer(destination, 20);
            ExecuteEvents.Execute(destination.gameObject, e, ExecuteEvents.beginDragHandler);
            var storageDrop = Target(lab, true, -1);
            e.position = RectTransformUtility.WorldToScreenPoint(null, storageDrop.transform.position);
            ExecuteEvents.Execute(storageDrop.gameObject, e, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(destination.gameObject, e, ExecuteEvents.endDragHandler);
            await UniTask.WaitUntil(() => !lab.IsBusy, cancellationToken: token);
            Check(lab.State.Field[0] == null && lab.State.Stored.Count == 4, "Dragging field item into drawer recalls it");

            lab.ResetSamples(true);
            lab.Transfer(false, 12, true, -1);
            Check(!lab.IsBusy && lab.State.Field[12] != null && lab.State.Stored.Count == 5, "Full preset rejects recall with no animation");
            incoming = lab.State.Stored[0]; outgoing = lab.State.Field[12];
            ExecuteEvents.Execute(Target(lab, true, 0).gameObject, Pointer(slot, 21), ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(field.gameObject, Pointer(field, 21), ExecuteEvents.pointerClickHandler);
            await UniTask.WaitUntil(() => !lab.IsBusy, cancellationToken: token);
            Check(ReferenceEquals(lab.State.Field[12], incoming) && ReferenceEquals(lab.State.Stored[0], outgoing), "Tap-to-swap works with full storage");

            lab.ResetSamples(false);
            lab.Transfer(true, 0, false, 2);
            lab.gameObject.SetActive(false);
            await UniTask.Yield();
            lab.gameObject.SetActive(true);
            await UniTask.Yield();
            Check(!lab.IsBusy && lab.State.Field[2] != null && lab.State.Stored.Count == 2, "Disable during transfer cancels FX and preserves committed contents");
            Check(!lab.transform.Find("Totem Inventory Lab UI/Portrait Content/Transfer incoming").gameObject.activeSelf, "Interrupted transfer leaves no ghost");
            lab.ResetSamples(false);
            ExecuteEvents.Execute(field.gameObject, Pointer(field, 22), ExecuteEvents.pointerClickHandler);
            Check(lab.State.Field[12].Rotation == 0, "Final preview restored to original preset");
            File.WriteAllText(Output + "/checks.txt", "PASS " + Results.Count + "\n" + string.Join("\n", Results));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Output + "/checks.txt", "FAIL after " + Results.Count + "\n" + string.Join("\n", Results) + "\n" + ex);
            Debug.LogException(ex);
        }
    }
    private static TotemInventoryLabPointer Target(TotemInventoryLab lab, bool storage, int index)
        => lab.GetComponentsInChildren<TotemInventoryLabPointer>().Single(p => p.Storage == storage && p.Index == index);
    private static PointerEventData Pointer(TotemInventoryLabPointer target, int id)
        => new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position) };

    /// <summary>Captures the actual composed Game view outside Assets, without creating imported evidence assets.</summary>
    public static string Capture()
    {
        Directory.CreateDirectory(Output);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/preview.png"));
        return $"Screen {Screen.width}x{Screen.height}; screenshot requested";
    }
}
