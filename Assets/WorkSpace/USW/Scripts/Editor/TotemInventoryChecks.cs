using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Play-mode checks for reward storage, overflow, placement and rotation gestures.</summary>
public static class TotemInventoryChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    /// <summary>Runs on a fresh test play session. Results are written under Temp.</summary>
    public static async UniTaskVoid Run()
    {
        var checks = new List<string>();
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); checks.Add(label); };
        TotemSelectUI selection = null;
        TotemData[] originalPool = null;
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires fresh Play Mode");
            var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
            var resolver = scope.Container;
            var token = scope.GetCancellationTokenOnDestroy();
            var inventory = (TotemInventory)resolver.Resolve(typeof(TotemInventory));
            var grid = (GridManager)resolver.Resolve(typeof(GridManager));
            var currency = (CurrencyManager)resolver.Resolve(typeof(CurrencyManager));
            var ui = (TotemInventoryUI)resolver.Resolve(typeof(TotemInventoryUI));
            var rotation = (TotemRotationUI)resolver.Resolve(typeof(TotemRotationUI));
            check(inventory.Items.Count == 0, "Fresh run inventory empty");
            var data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/SampleAttackTotemData.asset"));
            await data.LoadAssetsAsync().AttachExternalCancellation(token);
            selection = UnityEngine.Object.FindFirstObjectByType<TotemSelectUI>(FindObjectsInactive.Include);
            var poolField = typeof(TotemSelectUI).GetField("totemPool", Private);
            originalPool = (TotemData[])poolField.GetValue(selection);
            poolField.SetValue(selection, new[] { data });
            int callbacks = 0;
            int emptyBefore = grid.GetEmptyCells().Count;
            selection.Show(() => callbacks++);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            var cards = (List<TotemSelectCardUI>)typeof(TotemSelectUI).GetField("_spawnedCards", Private).GetValue(selection);
            check(cards.Count == 1, "Reward card loaded");
            typeof(TotemSelectUI).GetMethod("OnCardClicked", Private).Invoke(selection, new object[] { cards[0] });
            selection.OnConfirmClicked();
            check(inventory.Items.Count == 1 && grid.GetEmptyCells().Count == emptyBefore, "Reward enters inventory instead of grid");
            check(callbacks == 1, "Reward callback exactly once");
            for (int i = 0; i < 4; i++) check(inventory.TryAdd(data), "Accept stored item " + (i + 2));
            check(!inventory.TryAdd(data) && inventory.Items.Count == 5, "Capacity five enforced");
            var chosen = (HashSet<int>)typeof(TotemSelectUI).GetField("_chosenTotems", Private).GetValue(selection);
            chosen.Clear();
            float beforeFood = currency.Currency;
            float fallback = (float)typeof(TotemSelectUI).GetField("fallbackFood", Private).GetValue(selection);
            selection.Show(() => callbacks++);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            typeof(TotemSelectUI).GetMethod("OnCardClicked", Private).Invoke(selection, new object[] { cards[0] });
            selection.OnConfirmClicked();
            check(inventory.Items.Count == 5 && Mathf.Approximately(currency.Currency - beforeFood, fallback), "Overflow converts once to configured food");
            check(callbacks == 2, "Overflow resumes reward flow once");
            var button = (Button)typeof(TotemInventoryUI).GetField("_toggleButton", Private).GetValue(ui);
            button.onClick.Invoke();
            var panel = (GameObject)typeof(TotemInventoryUI).GetField("_panel", Private).GetValue(ui);
            check(panel.activeSelf, "Totem button opens inventory");
            Canvas.ForceUpdateCanvases();
            var slots = (TotemInventorySlotUI[])typeof(TotemInventoryUI).GetField("_slots", Private).GetValue(ui);
            check(slots[0].transform.position.x > slots[4].transform.position.x, "Oldest item is rightmost");
            check(!await inventory.TryPlaceAsync(0, null, token) && inventory.Items.Count == 5, "Invalid drop preserves storage");
            var cell = grid.GetEmptyCells()[0];
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                try { await inventory.TryPlaceAsync(0, cell, canceled.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { check(inventory.Items.Count == 5 && !cell.IsOccupied, "Canceled load preserves storage and cell"); }
            }
            check(await inventory.TryPlaceAsync(2, cell, token), "Explicit cell placement succeeds");
            check(inventory.Items.Count == 4 && cell.OccupyingTotem != null, "Successful placement consumes exactly one item");
            check(!await inventory.TryPlaceAsync(0, cell, token) && inventory.Items.Count == 4, "Occupied drop preserves storage");
            Canvas.ForceUpdateCanvases();
            check(!slots[4].gameObject.activeSelf && slots[0].transform.position.x > slots[3].transform.position.x, "Remaining icons pack to right");
            var totem = cell.OccupyingTotem;
            rotation.Begin(totem);
            var camera = Camera.main;
            Vector2 center = camera.WorldToScreenPoint(totem.transform.position);
            Func<Vector2, Vector2> world = screen => { var ray = camera.ScreenPointToRay(screen); new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance); return ray.GetPoint(distance); };
            rotation.Drag(world(center + Vector2.right * 100));
            check(totem.RotationStep == 0, "Preview does not commit rotation");
            rotation.End(world(center));
            check(totem.RotationStep == 0, "Center release cancels rotation");
            check(!grid.IsPreviewingTotem(totem), "Center release hides range");
            rotation.Begin(totem);
            rotation.End(world(center + Vector2.right * 100));
            check(totem.RotationStep == 1, "Right release commits clockwise rotation");
            check(!grid.IsPreviewingTotem(totem), "Committed rotation release hides range");
            check(TotemRotationUI.ResolveDirection(Vector2.up * 100, 24) == 0 && TotemRotationUI.ResolveDirection(Vector2.down * 100, 24) == 2 && TotemRotationUI.ResolveDirection(Vector2.left * 100, 24) == 3, "All four cardinal directions quantized");
            check(totem.RotateOffset(Vector2Int.right) == Vector2Int.down, "Effect offsets follow committed direction");
            var drag = totem.GetComponent<DragHandler>();
            var oldPosition = totem.transform.position;
            drag.BeginPress(); drag.OnBeginDrag(); drag.OnDrag(world(center + Vector2.left * 100));
            check(totem.transform.position == oldPosition, "Short drag rotates without movement");
            drag.CancelPointerDrag();
            typeof(DragHandler).GetField("_pressStartedAt", Private).SetValue(drag, Time.unscaledTime - 1f);
            drag.OnBeginDrag();
            var target = grid.GetEmptyCells()[0];
            drag.OnDrag(target.transform.position); drag.OnEndDrag(target.transform.position);
            check(target.OccupyingTotem == totem && inventory.Items.Count == 4, "Long hold moves on grid without returning to storage");
            System.IO.File.WriteAllText("Temp/totem-inventory-checks.txt", "PASS " + checks.Count + "\n" + string.Join("\n", checks));
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText("Temp/totem-inventory-checks.txt", "FAIL after " + checks.Count + "\n" + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (selection != null && originalPool != null) typeof(TotemSelectUI).GetField("totemPool", Private).SetValue(selection, originalPool);
        }
    }
}
