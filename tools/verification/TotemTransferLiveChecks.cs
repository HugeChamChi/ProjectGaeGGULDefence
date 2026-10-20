using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

/// <summary>Real IngameScene checks for inventory recall/exchange using its existing UI and injected services.</summary>
public static class TotemTransferLiveChecks
{
    private const string Output = "outputs/totem-transfer-20261020";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Checks = new List<string>();
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Checks.Add(label); }
    /// <summary>Starts a fresh-session integration check; changes are runtime only.</summary>
    public static string Run() { VerifyAsync().Forget(); return "Real-scene checks started"; }
    private static async UniTask VerifyAsync()
    {
        Checks.Clear(); Directory.CreateDirectory(Output);
        TimeScaleService time = null; object owner = new object();
        try
        {
            var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
            Check(scope != null && scope.Container != null, "IngameScene DI scope ready");
            var resolver = scope.Container;
            var inventory = resolver.Resolve<TotemInventory>();
            var spawner = resolver.Resolve<TotemSpawner>();
            var grid = resolver.Resolve<GridManager>();
            var buffs = resolver.Resolve<TotemBuffManager>();
            var ui = resolver.Resolve<TotemInventoryUI>();
            var settings = resolver.Resolve<TotemInteractionSettings>();
            var currency = resolver.Resolve<CurrencyManager>();
            time = resolver.Resolve<TimeScaleService>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(scope.GetCancellationTokenOnDestroy());
            using var timer = cts.CancelAfterSlim(TimeSpan.FromSeconds(45), DelayType.UnscaledDeltaTime);
            var token = cts.Token;
            await UniTask.WaitUntil(() => resolver.Resolve<GameInitializer>().IsReady, cancellationToken: token);
            Check(resolver.Resolve<GameInitializer>().IsReady, "Scene preload and blocking entry fade finished");
            resolver.Resolve<UIManager>().HideStartButton();
            time.Pause(owner);
            Check(inventory.Items.Count == 0, "Fresh real inventory empty");
            Check(grid.GetEmptyCells().Count >= 4, "Real board has test cells");
            var data = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1001Data.asset");
            var other = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1009Data.asset");
            var growthData = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1002Data.asset");
            string authored = EditorJsonUtility.ToJson(data) + EditorJsonUtility.ToJson(other) + EditorJsonUtility.ToJson(growthData);
            int active = buffs.GetActiveTotemCount(); float food = currency.Currency;
            var a = grid.GetEmptyCells()[0]; var b = grid.GetEmptyCells()[1];
            Check(inventory.TryAdd(data) && inventory.TryAdd(other), "Rewards enter original storage");
            Check(await inventory.TryPlaceAsync(0, a, token), "Empty-cell placement through production inventory");
            var first = a.OccupyingTotem; first.SetRotationStep(1);
            Check(first.RotationStep == 1 && buffs.GetActiveTotemCount() == active + 1, "Placed totem registered with real buffs");
            Check(inventory.TryRecall(first), "Production recall succeeds");
            Check(a.OccupyingTotem == null && buffs.GetActiveTotemCount() == active, "Recall clears occupancy and unregisters all effects");
            Check(inventory.Items[1] == data && inventory.GetRotationStep(1) == 1, "Recall stores authored SO and orientation");
            Check(!inventory.TryRecall(first), "Repeat recall cannot duplicate");
            Check(Mathf.Approximately(food, currency.Currency), "Recall pays no currency");
            Check(await inventory.TryPlaceAsync(1, a, token) && a.OccupyingTotem.RotationStep == 1, "Re-placement restores orientation");
            for (int i = inventory.Items.Count; i < inventory.Capacity; i++) Check(inventory.TryAdd(other), "Fill inventory slot " + i);
            var outgoing = a.OccupyingTotem;
            Check(!inventory.TryRecall(outgoing) && outgoing.IsPlaced && a.OccupyingTotem == outgoing, "Full recall preserves field object");
            Check(await inventory.TryPlaceAsync(0, a, token), "Full-storage exchange succeeds");
            Check(inventory.Items.Count == inventory.Capacity && inventory.Items[0] == data && inventory.GetRotationStep(0) == 1,
                "Exchange retains outgoing source and orientation in same slot");
            Check(a.OccupyingTotem.Data.totemId == other.totemId && buffs.GetActiveTotemCount() == active + 1, "Exchange replaces effect registration exactly once");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel(); var before = a.OccupyingTotem;
                try { await inventory.TryPlaceAsync(0, a, cancelled.Token); throw new Exception("Canceled placement accepted"); }
                catch (OperationCanceledException) { Check(a.OccupyingTotem == before && inventory.Items.Count == inventory.Capacity, "Canceled exchange preserves both sides"); }
            }
            b.Model.SetSealed(true);
            Check(!await inventory.TryPlaceAsync(0, b, token), "Sealed target rejected");
            b.Model.SetSealed(false);
            int gateCalls = 0; var beforeGate = a.OccupyingTotem;
            inventory.CanPlace = (index, cell) => ++gateCalls <= 1;
            Check(!await inventory.TryPlaceAsync(0, a, token) && a.OccupyingTotem == beforeGate, "Placement gate rechecked before mutating field");
            Check(!inventory.TryRecall(a.OccupyingTotem), "Guided placement cannot be bypassed by recall");
            inventory.CanPlace = null;

            var panel = Get<GameObject>(ui, "_panel");
            var toggle = Get<Button>(ui, "_toggleButton");
            toggle.onClick.Invoke();
            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: token);
            Check(ui.IsOpen && panel.activeSelf, "Existing bag button opens existing drawer");
            var slots = Get<TotemInventorySlotUI[]>(ui, "_slots");
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 71, button = PointerEventData.InputButton.Left };
            pointer.pressPosition = RectTransformUtility.WorldToScreenPoint(null, slots[0].transform.position);
            pointer.position = pointer.pressPosition + Vector2.up * 35;
            ExecuteEvents.Execute(slots[0].gameObject, pointer, ExecuteEvents.beginDragHandler);
            Check(ui.IsDraggingOrPlacing && !Get<CanvasGroup>(ui, "_panelGroup").blocksRaycasts, "Existing slot drag releases board raycasts");
            Physics2D.SyncTransforms();
            pointer.position = Camera.main.WorldToScreenPoint(a.transform.position);
            ExecuteEvents.Execute(slots[0].gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slots[0].gameObject, pointer, ExecuteEvents.endDragHandler);
            await UniTask.WaitUntil(() => !ui.IsDraggingOrPlacing, cancellationToken: token);
            Check(a.OccupyingTotem.Data.totemId == data.totemId && a.OccupyingTotem.RotationStep == 1, "Existing UGUI drag exchanges occupied target with stored orientation");
            Check(inventory.Items.Count == inventory.Capacity && !ui.IsOpen, "Existing drawer closes after successful exchange");

            var drag = a.OccupyingTotem.GetComponent<DragHandler>();
            var origin = a.OccupyingTotem;
            drag.BeginPress(); drag.OnBeginDrag();
            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: token);
            Check(ui.IsOpen && drag.IsDragging && !drag.IsRotating, "Field totem movement opens existing drawer for recall");
            var panelRect = (RectTransform)panel.transform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, panelRect.TransformPoint(panelRect.rect.center));
            Vector2 world = ScreenWorld(screen);
            drag.OnDrag(world); drag.OnEndDrag(world);
            Check(a.OccupyingTotem == origin && ui.IsOpen && inventory.Items.Count == inventory.Capacity, "Full drawer drop restores field totem and leaves drawer open for exchange");
            drag.BeginPress(); drag.OnBeginDrag();
            var outside = ScreenWorld(new Vector2(-100, -100));
            drag.OnDrag(outside); drag.OnEndDrag(outside);
            Check(a.OccupyingTotem == origin && origin.IsPlaced, "Off-screen recall gesture cannot sell through an invisible sale strip");
            Check(await inventory.TryPlaceAsync(1, b, token), "Free one slot for field recall");
            drag = origin.GetComponent<DragHandler>();
            drag.BeginPress(); drag.OnBeginDrag();
            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: token);
            screen = RectTransformUtility.WorldToScreenPoint(null, panelRect.TransformPoint(panelRect.rect.center));
            world = ScreenWorld(screen);
            drag.OnDrag(world); drag.OnEndDrag(world);
            Check(a.OccupyingTotem == null && inventory.Items.Count == inventory.Capacity, "Actual field drag into original drawer recalls to storage");
            Check(ui.IsOpen && !ui.IsDraggingOrPlacing, "Successful field recall leaves drawer usable");

            // Failure to instantiate a usable prefab must not remove the existing field totem.
            var brokenData = UnityEngine.Object.Instantiate(data);
            var brokenPrefab = new GameObject("TotemTransfer invalid prefab");
            brokenData.prefab = brokenPrefab; brokenData.prefabAddress = string.Empty;
            var isolated = new TotemInventory(spawner, settings); isolated.TryAdd(brokenData);
            var unchanged = b.OccupyingTotem;
            Check(!await isolated.TryPlaceAsync(0, b, token) && b.OccupyingTotem == unchanged && unchanged.IsPlaced,
                "Invalid incoming prefab preserves existing occupant and effects");
            UnityEngine.Object.Destroy(brokenPrefab); UnityEngine.Object.Destroy(brokenData);
            Check(await inventory.TryPlaceAsync(inventory.Items.Count - 1, a, token), "Recall item can be placed again through original service");
            Check(a.OccupyingTotem.RotationStep == 1, "Field-drag recall also retains direction");

            var growthCell = grid.GetEmptyCells()[0];
            Check(await spawner.PlaceTotemAtCellAsync(growthData, growthCell, token), "Real TD1002 placed");
            var growth = growthCell.OccupyingTotem as TotemKillRangeGrowth;
            typeof(TotemKillRangeGrowth).GetField("_seconds", Private).SetValue(growth, 100d);
            // Use an independent inventory, leaving the real full-storage example intact.
            var growthInventory = new TotemInventory(spawner, settings);
            Check(growthInventory.TryRecall(growth) && growth.CombatSeconds == 0, "Growth reset follows existing removal lifecycle");
            Check(await growthInventory.TryPlaceAsync(0, growthCell, token) && ((TotemKillRangeGrowth)growthCell.OccupyingTotem).CombatSeconds == 0,
                "Re-placement starts TD1002 growth from placement time");
            Check(authored == EditorJsonUtility.ToJson(data) + EditorJsonUtility.ToJson(other) + EditorJsonUtility.ToJson(growthData), "Authored SO configuration unchanged");
            Check(Mathf.Approximately(food, currency.Currency), "All recall and exchange operations leave currency unchanged");
            var stale = growthCell.OccupyingTotem; int commitChecks = 0;
            bool stalePlaced = await spawner.PlaceOrExchangeTotemAsync(other, 0, growthCell, stale,
                () => { if (++commitChecks == 2) spawner.RemoveTotemFromField(stale); return true; }, token);
            Check(!stalePlaced && growthCell.OccupyingTotem == null, "Removed target cannot be exchanged or duplicated during placement validation");
            File.WriteAllText(Output + "/checks.txt", "PASS " + Checks.Count + "\n" + string.Join("\n", Checks));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Output + "/checks.txt", "FAIL after " + Checks.Count + "\n" + string.Join("\n", Checks) + "\n" + ex);
            Debug.LogException(ex);
        }
        finally { time?.Release(owner); }
    }
    private static Vector2 ScreenWorld(Vector2 screen)
    {
        var ray = Camera.main.ScreenPointToRay(screen);
        new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance);
        return ray.GetPoint(distance);
    }
    /// <summary>Reports loaded scope and scene state before verification.</summary>
    public static string Probe()
    {
        var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
        return $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}; scope={scope != null}; container={scope?.Container != null}; screen={Screen.width}x{Screen.height}";
    }
}
