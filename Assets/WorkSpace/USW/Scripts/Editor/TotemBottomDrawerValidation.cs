using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;

/// <summary>Validates the split background/content bottom drawer (background reaches the real
/// screen bottom past Safe Area, content stays in Safe Area, drag/close/board-drop still work).
/// Temporary validation script — safe to delete after the user confirms the behavior.</summary>
public static class TotemBottomDrawerValidation
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);

    [MenuItem("Tools/USW/Validate Totem Bottom Drawer")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[TotemBottomDrawerValidation] Enter Play Mode first."); return; }
        var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
        RunAsync(scope, scope.GetCancellationTokenOnDestroy()).Forget();
    }

    static async UniTaskVoid RunAsync(InGameLifetimeScope scope, CancellationToken token)
    {
        var log = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); log.Add("PASS " + name); }
        try
        {
            var startPanel = GameObject.Find("StartPanel");
            if (startPanel != null && startPanel.activeInHierarchy)
            {
                var startButton = startPanel.GetComponentInChildren<Button>(true);
                if (startButton != null) startButton.onClick.Invoke();
                await UniTask.Delay(300, ignoreTimeScale: true, cancellationToken: token);
                log.Add("DEBUG closed StartPanel via its button before testing");
            }
            var ui = UnityEngine.Object.FindFirstObjectByType<TotemInventoryUI>(FindObjectsInactive.Include);
            var inventory = (TotemInventory)scope.Container.Resolve(typeof(TotemInventory));
            var data = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1004Data.asset");
            await data.LoadAssetsAsync().AttachExternalCancellation(token);
            while (inventory.Items.Count < inventory.Capacity) inventory.TryAdd(data);

            var panel = (GameObject)Field(ui, "_panel");
            var background = (RectTransform)Field(ui, "_background");
            Check(background != null, "_background is wired");

            typeof(TotemInventoryUI).GetMethod("OpenInventory", Flags).Invoke(ui, null);
            await UniTask.Delay(600, ignoreTimeScale: true, cancellationToken: token);

            var panelRect = (RectTransform)panel.transform;
            var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            var panelCorners = new Vector3[4]; panelRect.GetWorldCorners(panelCorners);
            var panelScreenBottom = RectTransformUtility.WorldToScreenPoint(camera, panelCorners[0]);
            var panelScreenTop = RectTransformUtility.WorldToScreenPoint(camera, panelCorners[1]);
            Check(panelScreenBottom.y >= Screen.safeArea.yMin && panelScreenBottom.y - Screen.safeArea.yMin < 30f,
                "content (_panel) stays at Safe Area bottom, unaffected by background change");

            var bgCorners = new Vector3[4]; background.GetWorldCorners(bgCorners);
            var bgScreenBottom = RectTransformUtility.WorldToScreenPoint(camera, bgCorners[0]);
            var bgScreenTop = RectTransformUtility.WorldToScreenPoint(camera, bgCorners[1]);
            Check(bgScreenBottom.y < 0f, "background bottom is pushed past the real screen edge (clipped off-screen)");
            Check(Mathf.Abs(bgScreenTop.y - panelScreenTop.y) < 5f, "background top aligns with content top, not shrunk");
            Check(background.gameObject.activeInHierarchy, "background renders (active)");
            var bgImage = background.GetComponent<Image>();
            Check(bgImage != null && bgImage.enabled, "background Image is the visible one (content's own Image should be disabled)");
            var panelImage = panel.GetComponent<Image>();
            Check(panelImage != null && !panelImage.enabled, "content (_panel) own Image stays disabled so it doesn't double-render");

            var bgLeftScreen = RectTransformUtility.WorldToScreenPoint(camera, bgCorners[0]).x;
            var bgRightScreen = RectTransformUtility.WorldToScreenPoint(camera, bgCorners[2]).x;
            Check(bgLeftScreen <= 1f && bgRightScreen >= Screen.width - 1f, "background stretches full screen width, zero side margins");

            var close = (Button)Field(ui, "_closeButton");
            var toggle = (Button)Field(ui, "_toggleButton");
            bool Hits(Transform target)
            {
                var targetRect = (RectTransform)target;
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, targetRect.TransformPoint(targetRect.rect.center)) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                return hits.Count > 0 && (hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target));
            }
            Check(Hits(close.transform), "close button still receives touch");
            var slots = (TotemInventorySlotUI[])Field(ui, "_slots");
            foreach (var slot in slots) Check(Hits(slot.transform), slot.name + " still receives touch ahead of background");

            var grid = (GridManager)scope.Container.Resolve(typeof(GridManager));
            var target2 = grid.GetEmptyCells().Last();
            var firstSlot = slots[0];
            var slotRect = (RectTransform)firstSlot.transform;
            var start = RectTransformUtility.WorldToScreenPoint(camera, slotRect.TransformPoint(slotRect.rect.center));
            var end = (Vector2)Camera.main.WorldToScreenPoint(target2.transform.position);
            var dragPointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(firstSlot.gameObject, dragPointer, ExecuteEvents.pointerDownHandler);
            dragPointer.position = end;
            ExecuteEvents.Execute(firstSlot.gameObject, dragPointer, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(firstSlot.gameObject, dragPointer, ExecuteEvents.dragHandler);
            var panelGroup = panel.GetComponent<CanvasGroup>();
            Check(panelGroup.alpha == 0f && !panelGroup.blocksRaycasts, "dragging a totem hides the drawer (CanvasGroup alpha/raycasts off)");
            int count = inventory.Items.Count;
            log.Add($"DEBUG target2 IsAvailable={target2.IsAvailable} IsOccupied={target2.IsOccupied} modelAvailable={target2.Model.IsAvailable} pos={target2.GridPosition} worldPos={target2.transform.position} end={end}");
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(dragPointer, raycastResults);
            log.Add($"DEBUG raycastResults at end pointer: {raycastResults.Count} -> {string.Join(",", raycastResults.Select(r => r.gameObject.name))}");
            ExecuteEvents.Execute(firstSlot.gameObject, dragPointer, ExecuteEvents.endDragHandler);
            await UniTask.Delay(600, ignoreTimeScale: true, cancellationToken: token);
            log.Add($"DEBUG after drop: occupyingTotem={target2.OccupyingTotem} items={inventory.Items.Count} (was {count})");
            Check(target2.OccupyingTotem != null && inventory.Items.Count == count - 1, "drag places totem on board and consumes exactly once");

            typeof(TotemInventoryUI).GetMethod("OpenInventory", Flags).Invoke(ui, null);
            await UniTask.Delay(600, ignoreTimeScale: true, cancellationToken: token);
            close.onClick.Invoke();
            await UniTask.Delay(500, ignoreTimeScale: true, cancellationToken: token);
            Check(!panel.activeSelf && Hits(toggle.transform), "close button closes drawer and restores toggle access");

            log.Add("COMPLETE " + log.Count);
        }
        catch (Exception e) { log.Add("FAIL " + e); Debug.LogException(e); }
        System.IO.File.WriteAllLines("Temp/totem-bottom-drawer-split-checks.txt", log);
        Debug.Log("[TotemBottomDrawerValidation] " + string.Join(" | ", log));
    }
}
