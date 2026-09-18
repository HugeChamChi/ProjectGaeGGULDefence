using System;
using System.Collections.Generic;
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
            var data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/SampleAttackTotemData.asset"));
            // The editable sample is user content; define the test range only on this runtime clone.
            data.EffectGroups.Clear();
            data.effectRanges.Clear();
            data.effectRanges.Add(new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.left, Vector2Int.right } });
            await data.LoadAssetsAsync().AttachExternalCancellation(token);
            var origin = grid.GetCell(2, 1);
            // Wait for the scene-entry fade, which intentionally blocks UI gestures.
            var probe = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(origin.transform.position) };
            var probeHits = new List<RaycastResult>();
            await UniTask.WaitUntil(() => { probeHits.Clear(); EventSystem.current.RaycastAll(probe, probeHits); return probeHits.Count == 0; }, cancellationToken: token).Timeout(TimeSpan.FromSeconds(15));
            check(await spawner.PlaceTotemAtCellAsync(data, origin, token), "Spawn actual sample totem");
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
            var dualData = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/SampleDualEffectTotemData.asset"));
            await dualData.LoadAssetsAsync().AttachExternalCancellation(token);
            var dualOrigin = grid.GetCell(4, 0);
            check(await spawner.PlaceTotemAtCellAsync(dualData, dualOrigin, token), "Place dual-effect sample");
            var dual = dualOrigin.OccupyingTotem;
            var attackCell = grid.GetCell(3, 0);
            var speedCell = grid.GetCell(5, 0);
            check(Mathf.Approximately(attackCell.Model.GetTotemCellBonus(StatKind.AttackPercent), 0.1f) && attackCell.Model.GetTotemCellBonus(StatKind.Speed) == 0f, "Attack effect applies only to its own range");
            check(Mathf.Approximately(speedCell.Model.GetTotemCellBonus(StatKind.Speed), 0.2f) && speedCell.Model.GetTotemCellBonus(StatKind.AttackPercent) == 0f, "Speed effect applies only to its own range");
            dual.GetComponent<DragHandler>().BeginPress();
            check(attackCell.Model.TotemPreviewColor == dualData.EffectGroups[0].Color && speedCell.Model.TotemPreviewColor == dualData.EffectGroups[1].Color, "Separate range colors follow groups");
            var properties = new MaterialPropertyBlock();
            speedCell.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
            check(Mathf.Approximately(properties.GetColor("_StripeColor").b, dualData.EffectGroups[1].Color.b), "Group color reaches shader property block");
            check(dualData.GetDisplayDescription().Contains("#FF4C1A") && dualData.GetDisplayDescription().Contains("#26B2FF"), "Descriptions use matching rich-text colors");
            ScreenCapture.CaptureScreenshot("Temp/totem-range-dual.png");
            await UniTask.Delay(120, DelayType.Realtime, cancellationToken: token);
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
            check(!panel.activeSelf, "Inventory stays closed after release");
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
