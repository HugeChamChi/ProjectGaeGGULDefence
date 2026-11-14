using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Runtime checks for tap, placement range preview and shared stripe materials.</summary>
public static class TotemRangePreviewChecks
{
    /// <summary>Runs in a fresh Play Mode session and writes results to Temp.</summary>
    public static async UniTaskVoid Run()
    {
        var passed = new List<string>();
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); passed.Add(name); };
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode");
            var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
            var token = scope.GetCancellationTokenOnDestroy();
            var resolver = scope.Container;
            var grid = (GridManager)resolver.Resolve(typeof(GridManager));
            var spawner = (TotemSpawner)resolver.Resolve(typeof(TotemSpawner));
            var inventory = (TotemInventory)resolver.Resolve(typeof(TotemInventory));
            var ui = (TotemInventoryUI)resolver.Resolve(typeof(TotemInventoryUI));
            ((UIManager)resolver.Resolve(typeof(UIManager))).HideStartButton();
            var data = UnityEngine.Object.Instantiate(LevelTotemCatalog.All.First(t => t.IsLevelTotem && GGD.BackendCharts.TotemBalance.IsEnabled(t.totemId)));
            // The level totem SO is user content; define the test range only on this runtime clone (TotemSpawner only places level totems).
            data.EffectGroups.Clear();
            data.effectRanges.Clear();
            data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.left, Vector2Int.right } });
            await data.LoadAssetsAsync().AttachExternalCancellation(token);
            var origin = grid.GetCell(2, 1);
            // Wait for the scene-entry fade, which intentionally blocks UI gestures.
            var probe = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(origin.transform.position) };
            var probeHits = new List<RaycastResult>();
            await UniTask.WaitUntil(() => { probeHits.Clear(); EventSystem.current.RaycastAll(probe, probeHits); return probeHits.Count == 0; }, cancellationToken: token).Timeout(TimeSpan.FromSeconds(15));
            check(await spawner.PlaceTotemAtCellAsync(data, origin, token), "Spawn actual playable totem");
            var totem = origin.OccupyingTotem;
            var drag = totem.GetComponent<DragHandler>();
            var input = (InputManager)resolver.Resolve(typeof(InputManager));
            Vector2 screen = Camera.main.WorldToScreenPoint(totem.transform.position);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            input.GetType().GetMethod("ProcessPointerDown", flags).Invoke(input, new object[] { screen });
            check(grid.IsPreviewingTotem(totem), "Touch down previews placed totem");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            var left = grid.GetCell(1, 1);
            var right = grid.GetCell(3, 1);
            check(left.Model.IsTotemRangePreviewed && right.Model.IsTotemRangePreviewed, "Held touch paints configured cells");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorkSpace/USW/Materials/TotemEffectRange.mat");
            check(left.GetComponent<SpriteRenderer>().sharedMaterial == mat && right.GetComponent<SpriteRenderer>().sharedMaterial == mat, "Range cells share stripe material");
            check(left.GetComponent<SpriteRenderer>().color.a > 0, "Preview overrides normally transparent cell tint");
            check(mat.shader.isSupported && !ShaderUtil.ShaderHasError(mat.shader), "URP stripe shader supported and compiles");
            ScreenCapture.CaptureScreenshot("Temp/totem-range-tap.png");
            await UniTask.Delay(120, DelayType.Realtime, cancellationToken: token);
            input.GetType().GetMethod("ProcessPointerUp", flags).Invoke(input, new object[] { screen });
            check(!grid.IsPreviewingTotem(totem) && !left.Model.IsTotemRangePreviewed, "Touch release hides range even when info panel opens");
            check(left.GetComponent<SpriteRenderer>().sharedMaterial != mat && !left.Model.IsTotemRangePreviewed, "Clear restores original cell material");
            // Multi-group (attack/speed) range colors were a legacy TD totem feature; level totems have no effect groups, so that block was removed (VC8-LT).
            inventory.TryAdd(data);
            var candidate = grid.GetCell(3, 2);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = Camera.main.WorldToScreenPoint(candidate.transform.position) };
            // Close info popups opened by the tap before starting the independent inventory gesture.
            UnityEngine.Object.FindFirstObjectByType<TotemActionPopupUI>(FindObjectsInactive.Include)?.Hide();
            var infoPanel = UnityEngine.Object.FindFirstObjectByType<UI_TotemInfoPanel>(FindObjectsInactive.Include);
            if (infoPanel != null) await infoPanel.CloseAsync();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            ui.BeginDrag(0, data.DisplaySprite, pointer);
            var panel = (GameObject)typeof(TotemInventoryUI).GetField("_panel", flags).GetValue(ui);
            var panelGroup = panel.GetComponent<CanvasGroup>();
            check(panelGroup.alpha == 0f && !panelGroup.blocksRaycasts, "Inventory immediately hides and stops blocking drag");
            check(grid.GetCell(2, 2).Model.IsTotemRangePreviewed && grid.GetCell(4, 2).Model.IsTotemRangePreviewed, "Inventory hover previews candidate effect cells");
            check(!candidate.IsOccupied && inventory.Items.Count == 1, "Hover does not place or consume item");
            check(!grid.GetCell(2, 2).HasAttackBuff, "Hover does not apply actual buffs");
            pointer.position = new Vector2(-1000, -1000);
            ui.Drag(pointer);
            check(!grid.GetCell(2, 2).Model.IsTotemRangePreviewed, "Leaving grid clears hover range");
            ui.EndDrag(pointer);
            check(panel.activeSelf, "Invalid drop reopens inventory");
            check(inventory.Items.Count == 1 && !candidate.IsOccupied, "Invalid drop keeps stored item");
            drag.BeginPress();
            check(left.Model.IsTotemRangePreviewed, "Tap range works again after canceled inventory drag");
            Time.timeScale = 0f;
            float before = Shader.GetGlobalFloat("_TotemRangeUnscaledTime");
            await UniTask.Delay(100, DelayType.Realtime, cancellationToken: token);
            check(Shader.GetGlobalFloat("_TotemRangeUnscaledTime") > before, "Stripe clock advances while paused");
            Time.timeScale = 1f;
            drag.EndPress();
            check(!left.Model.IsTotemRangePreviewed, "Ending held preview clears range");
            System.IO.File.WriteAllText("Temp/totem-range-checks.txt", "PASS " + passed.Count + "\n" + string.Join("\n", passed));
        }
        catch (Exception exception)
        {
            Time.timeScale = 1f;
            System.IO.File.WriteAllText("Temp/totem-range-checks.txt", "FAIL after " + passed.Count + "\n" + exception);
            Debug.LogException(exception);
        }
    }
}
