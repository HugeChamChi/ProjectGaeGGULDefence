using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Plays the authored 3WAVE tutorial (TUT26) end to end through the real UI/world handlers
/// and checks the lesson gates: level merge, earthquake telegraph, field view, totem +2,
/// practice conversion, deer curse release and the Wave 3 result.
/// </summary>
public static class IngameTutorialThreeWaveChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Report = "outputs/tutorial-qa/three-wave.txt";

    /// <summary>Runs in a fresh TutorialScene Play session; restores the completion PlayerPrefs key.</summary>
    [MenuItem("Tools/USW/Tutorial/Run 3WAVE Play Checks")]
    public static void Run() => RunAsync().Forget();

    private static async UniTaskVoid RunAsync()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter TutorialScene Play Mode first.");
        Directory.CreateDirectory("outputs/tutorial-qa");
        var log = new StringBuilder();
        float startedAt = Time.realtimeSinceStartup;
        var director = Object.FindFirstObjectByType<IngameTutorialDirector>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(director.GetCancellationTokenOnDestroy());
        cts.CancelAfterSlim(TimeSpan.FromMinutes(8), DelayType.Realtime);
        var token = cts.Token;
        const string completedKey = "IngameTutorial.Completed.v1";
        bool hadProgress = PlayerPrefs.HasKey(completedKey);
        int previousProgress = PlayerPrefs.GetInt(completedKey, 0);
        void Note(string text) { log.AppendLine($"[{Time.realtimeSinceStartup - startedAt,6:F1}s] {text}"); File.WriteAllText(Report, log.ToString()); }
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            Note("PASS " + description);
        }
        async UniTask Stage(IngameTutorialStage stage)
        {
            await UniTask.WaitUntil(() => director.CurrentStage == stage, cancellationToken: token);
            Note("STAGE " + stage);
            await UniTask.Delay(100, DelayType.Realtime, cancellationToken: token);
        }
        async UniTask Frames() { await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token); }
        async UniTask Snapshot(string name) { ScreenCapture.CaptureScreenshot("outputs/tutorial-qa/3w-" + name + ".png"); await Frames(); }
        try
        {
            var overlay = Get<IngameTutorialOverlay>(director, "_overlay");
            var dialogue = Get<IngameTutorialDialogue>(director, "_dialogue");
            var spawn = Get<UnitSpawner>(director, "_spawner");
            var input = Get<InputManager>(director, "_input");
            var grid = Get<GridManager>(director, "_grid");
            var game = Get<GameManager>(director, "_game");
            var bosses = Get<BossManager>(director, "_boss");
            var wave = Get<WaveManager>(director, "_wave");
            var timer = Get<TimerController>(director, "_timer");
            var wallet = Get<CurrencyManager>(director, "_currency");
            var settings = Get<IngameTutorialSettings>(director, "_settings");
            var summon = Get<Button>(director, "_summonButton");
            var camera = Camera.main;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            async UniTask FocusReady() => await UniTask.WaitUntil(() => !overlay.IsTransitioning, cancellationToken: token);
            void ClickAt(GameObject expected, Vector2 screen)
            {
                pointer.position = screen;
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                var target = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                Check(target == expected, "Actual UI raycast reaches " + expected.name + (target != null ? "" : " (nothing hit)"));
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                pointer.eligibleForClick = true;
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
                if (pointer.eligibleForClick) ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
            }
            bool Reaches(GameObject expected, Vector2 screen)
            {
                pointer.position = screen;
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                return hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == expected;
            }
            async UniTask WaitReach(GameObject expected, Func<Vector2> screen) =>
                await UniTask.WaitUntil(() => Reaches(expected, screen()), cancellationToken: token).TimeoutWithoutException(TimeSpan.FromSeconds(5), DelayType.Realtime);
            void ClickButton(Button button) => ClickAt(button.gameObject, overlay.ScreenRect((RectTransform)button.transform).center);
            async UniTask WaitOpen(Button button) => await UniTask.WaitUntil(() =>
                !overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)button.transform).center, null), cancellationToken: token);
            async UniTask Acknowledge()
            {
                await UniTask.WaitUntil(() => dialogue.gameObject.activeSelf, cancellationToken: token);
                await FocusReady();
                if (dialogue.IsTyping) { overlay.OnPointerClick(pointer); await Frames(); }
                overlay.OnPointerClick(pointer); await Frames();
            }
            async UniTask WaitText(string prefix)
            {
                var label = Get<TMPro.TMP_Text>(dialogue, "_instruction");
                await UniTask.WaitUntil(() => dialogue.gameObject.activeInHierarchy && label.text.StartsWith(prefix), cancellationToken: token);
            }

            // ---- WAVE 1: entrance, time, summons, merge, upgrade ----
            await Stage(IngameTutorialStage.TimeLimit);
            Check(bosses.CurrentBoss != null && spawn.SuccessfulSpawnCount == 0, "Wave 1 boss enters before any summons");
            Check(Mathf.Abs(timer.RemainingTime - 60f) < 1f, "Tutorial countdown starts at 60 seconds (" + timer.RemainingTime.ToString("F1") + ")");
            await Acknowledge();
            await Stage(IngameTutorialStage.FirstSummon);
            for (int i = 0; i < settings.SpawnUnits.Length; i++)
            {
                await WaitOpen(summon);
                int before = spawn.SuccessfulSpawnCount; ClickButton(summon);
                await UniTask.WaitUntil(() => spawn.SuccessfulSpawnCount == before + 1 && spawn.LastSpawnedUnit.gameObject.activeInHierarchy, cancellationToken: token);
            }
            Check(spawn.SuccessfulSpawnCount == 4, "Four authored summons placed");
            var units = settings.SpawnCells.Select(c => grid.GetCell(c).OccupyingUnit).ToArray();
            Check(units.All(u => u != null && u.UsesTotemLevels && u.BaseLevel == 1), "Summoned units are level units at base level 1");
            await Stage(IngameTutorialStage.Merge);
            await FocusReady(); await Frames();
            var first = units[0]; var second = units[1];
            var data = first.OriginalData;
            Check(UnitMergeRules.CanPair(first, second), "Same kind and base level pair can merge");
            Drag(input, first.GetComponent<Collider2D>().bounds.center, second.currentCell.transform.position);
            await Stage(IngameTutorialStage.OpenUpgrade);
            var merged = Get<UnitBase>(director, "_mergedUnit");
            Check(first == null && second == null, "Merge consumes both ingredients");
            Check(merged != null && merged.UsesTotemLevels && merged.BaseLevel == 2 && merged.OriginalData == data, "Level merge makes the same kind at base level 2");
            var upgrade = Get<Button>(director, "_upgradeButton");
            await WaitOpen(upgrade); ClickButton(upgrade);
            await Stage(IngameTutorialStage.UpgradeSlots);
            await UniTask.Delay(800, DelayType.Realtime, cancellationToken: token); await FocusReady();
            var item = Get<RectTransform>(director, "_upgradeSlots").GetComponentsInChildren<HSD.UI.Upgrade.UI_UpgradeItem>().First(i => i.UpgradeTarget == settings.UpgradeTarget && i.CanUpgrade);
            int upgradeLevel = item.CurrentLevel;
            ClickButton((Button)new SerializedObject(item).FindProperty("btn_Upgrade").objectReferenceValue);
            await Acknowledge();
            Check(item.CurrentLevel > upgradeLevel, "Paid kind upgrade increases its level");

            // ---- WAVE 2: kill reset, earthquake telegraph, choices + field view, totem +2 ----
            await Stage(IngameTutorialStage.SecondWave);
            var firstBoss = bosses.CurrentBoss;
            await UniTask.WaitUntil(() => wave.CurrentWave == 1 && bosses.CurrentBoss != null && bosses.CurrentBoss != firstBoss, cancellationToken: token);
            Check(timer.RemainingTime > 55f && timer.RemainingTime <= 60.5f, "Wave 1 kill resets the countdown to 60 seconds (" + timer.RemainingTime.ToString("F1") + ")");
            await Stage(IngameTutorialStage.EarthquakeTelegraph);
            var patterns = Get<BossPatternController>(director, "_patterns");
            await UniTask.WaitUntil(() => patterns.IsCounterablePattern(bosses.CurrentBoss), cancellationToken: token);
            await Acknowledge();
            Check(grid.AllCells().Any(c => c.Model.IsBossTelegraphPreviewed), "Earthquake warning is shown on the field");
            await Snapshot("01-earthquake");
            await Stage(IngameTutorialStage.ExperienceGauge);
            await Acknowledge();
            await Stage(IngameTutorialStage.LevelUp);
            var level = Get<LevelUpUI>(director, "_levelUpUI");
            await UniTask.WaitUntil(() => level.IsReadyForSelection, cancellationToken: token); await Frames();
            await Acknowledge();
            var cards = level.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>();
            Check(cards.Select(c => c.GetData()).SequenceEqual(settings.LevelUpChoices), "Choice cards use the authored order");
            var card = cards.First(c => c.GetData() == settings.LevelUpChoices[0]);
            await UniTask.WaitUntil(() => cards.All(c => !c.AllowSelection), cancellationToken: token);
            await FocusReady(); dialogue.CompleteTyping();
            async UniTask TapTerm()
            {
                var text = card.DescriptionText; text.ForceMeshUpdate();
                Check(text.textInfo.linkCount > 0, "Required choice has a linked term");
                var ch = text.textInfo.characterInfo[text.textInfo.linkInfo[0].linkTextfirstCharacterIndex];
                var canvas = text.GetComponentInParent<Canvas>();
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                ClickAt(card.gameObject, RectTransformUtility.WorldToScreenPoint(cam, text.transform.TransformPoint((ch.bottomLeft + ch.topRight) * 0.5f)));
            }
            await TapTerm();
            await UniTask.WaitUntil(() => level.DescriptionBlocksInput, cancellationToken: token);
            await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
            var termPopup = Object.FindFirstObjectByType<TermPopup>();
            ClickAt(Get<GameObject>(termPopup, "_shield"), overlay.ScreenRect(level.DescriptionVideoArea).center);
            await UniTask.WaitUntil(() => !level.DescriptionBlocksInput, cancellationToken: token);
            var field = level.FieldView;
            await WaitText("필드 보기");
            await FocusReady();
            Check(!level.HideFieldViewFooter && field.ToggleButton.gameObject.activeInHierarchy, "Field view button is shown for the field-view lesson");
            Check(cards.All(c => !c.AllowSelection), "Choices stay locked while the field-view lesson waits");
            ClickButton(field.ToggleButton);
            await UniTask.WaitUntil(() => field.ViewingField && !field.BlocksSelection || field.ViewingField, cancellationToken: token);
            await UniTask.Delay(600, DelayType.Realtime, cancellationToken: token);
            await Snapshot("02-field-view");
            Check(field.ViewingField, "Field view opens the battlefield behind the choices");
            await WaitText("확인이 끝나면");
            await FocusReady();
            ClickButton(field.ToggleButton);
            await UniTask.WaitUntil(() => !field.ViewingField && !field.BlocksSelection, cancellationToken: token);
            await UniTask.WaitUntil(() => card.AllowSelection, cancellationToken: token);
            await FocusReady();
            Check(cards.Count(c => c.AllowSelection) == 1, "Only the required card can be selected");
            await TapTerm();
            await UniTask.WaitUntil(() => game.CurrentState == GameManager.GameState.Playing && !level.IsReadyForSelection, cancellationToken: token);
            Check(true, "Required card selected and battle resumed");
            await Stage(IngameTutorialStage.TotemChoice);
            var reward = Get<TotemRewardUI>(director, "_rewardUI");
            await UniTask.WaitUntil(() => reward.IsReadyForSelection, cancellationToken: token);
            Check(wave.CurrentWave == 1, "Totem reward opens after the Wave 2 boss kill");
            await FocusReady(); await Frames();
            var overview = reward.ChoiceArea.GetComponentsInChildren<Button>().First();
            await WaitReach(overview.gameObject, () => overlay.ScreenRect(reward.RequiredChoiceArea).center);
            ClickAt(overview.gameObject, overlay.ScreenRect(reward.RequiredChoiceArea).center);
            await Stage(IngameTutorialStage.OpenInventory);
            var storage = Get<TotemInventory>(director, "_inventory");
            Check(storage.Items.Count == 1 && storage.Items[0] == settings.RequiredTotem, "Required level totem is stored");
            var invButton = Get<Button>(director, "_inventoryButton");
            await WaitOpen(invButton); ClickButton(invButton);
            await Stage(IngameTutorialStage.PlaceTotem);
            await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token); await FocusReady();
            var invUi = Get<TotemInventoryUI>(director, "_inventoryUI");
            var slot = invUi.GetSlotRect(0).GetComponent<TotemInventorySlotUI>();
            var drop = Get<GridCell>(director, "_dropCell");
            pointer.pressPosition = overlay.ScreenRect(invUi.GetSlotRect(0)).center;
            pointer.position = camera.WorldToScreenPoint(drop.transform.position);
            slot.OnBeginDrag(pointer); slot.OnDrag(pointer); slot.OnEndDrag(pointer);
            await Stage(IngameTutorialStage.MoveTotem);
            await FocusReady();
            var totem = Get<TotemBase>(director, "_placedTotem");
            Drag(input, totem.transform.position, Get<GridCell>(director, "_dropCell").transform.position);
            await Stage(IngameTutorialStage.RotateTotem);
            await FocusReady(); await Frames();
            var rotationTarget = Get<GridCell>(director, "_rotationTargetCell");
            Check(rotationTarget != null && rotationTarget.OccupyingUnit != null, "Rotation lesson targets a unit outside the current range");
            var rotatedUnit = rotationTarget.OccupyingUnit;
            int levelBefore = rotatedUnit.CurrentLevel;
            Physics2D.SyncTransforms();
            // Press the totem's own cell: its collider centre can sit over the unit cell above.
            var start = (Vector2)camera.WorldToScreenPoint(totem.CurrentCell.transform.position);
            Call(input, "ProcessPointerDown", start);
            await UniTask.Delay(800, DelayType.Realtime, cancellationToken: token);
            var totemScreen = (Vector2)camera.WorldToScreenPoint(totem.transform.position);
            var end = totemScreen + ((Vector2)camera.WorldToScreenPoint(rotationTarget.transform.position) - totemScreen).normalized * 150;
            Call(input, "ProcessPointerMove", end); Call(input, "ProcessPointerUp", end);
            await WaitText(settings.TotemRotateSuccessInstruction.Substring(0, 6));
            await UniTask.Delay(300, DelayType.Realtime, cancellationToken: token);
            Note($"Rotated unit {rotatedUnit.name} base={rotatedUnit.BaseLevel} level {levelBefore}->{rotatedUnit.CurrentLevel}");
            Check(rotatedUnit.CurrentLevel == rotatedUnit.BaseLevel + 2 && rotatedUnit.CurrentLevel > levelBefore, "Rotating the +2 totem onto the unit raises its level by 2");
            await Snapshot("03-totem-plus2");

            // ---- WAVE 3: practice conversion, curse release, final battle ----
            await Stage(IngameTutorialStage.Conversion);
            var conversion = Get<ConversionService>(director, "_conversion");
            var stones = Get<ConversionStoneWallet>(director, "_stones");
            await UniTask.WaitUntil(() => conversion.TutorialTarget != null, cancellationToken: token);
            var target = conversion.TutorialTarget;
            var targetCell = target.currentCell;
            var targetData = target.OriginalData;
            int stonesBefore = stones.Stones;
            Check(wave.CurrentWave == 2 && target.BaseLevel == 1 && stonesBefore >= 1, "Wave 3 practice conversion targets a base level 1 unit with a stone");
            await FocusReady(); await Frames();
            Physics2D.SyncTransforms();
            var unitScreen = (Vector2)camera.WorldToScreenPoint(target.GetComponent<Collider2D>().bounds.center);
            Call(input, "ProcessPointerDown", unitScreen); Call(input, "ProcessPointerUp", unitScreen);
            var button = Object.FindFirstObjectByType<ConversionButtonView>(FindObjectsInactive.Include);
            await UniTask.WaitUntil(() => button.Unit == target && button.gameObject.activeInHierarchy, cancellationToken: token)
                .TimeoutWithoutException(TimeSpan.FromSeconds(5), DelayType.Realtime);
            Check(button.Unit == target && button.gameObject.activeInHierarchy, "Tapping the practice unit shows its conversion button");
            await FocusReady(); await Frames();
            ClickAt(button.gameObject, overlay.ScreenRect((RectTransform)button.transform).center);
            await UniTask.WaitUntil(() => targetCell.OccupyingUnit != null && targetCell.OccupyingUnit != target, cancellationToken: token);
            var converted = targetCell.OccupyingUnit;
            Check(converted.OriginalData != targetData && converted.BaseLevel == 1, "Conversion makes a different kind at the same base level");
            Check(stones.Stones == stonesBefore - 1, "Conversion spends exactly one stone");
            await Snapshot("04-conversion");
            await Stage(IngameTutorialStage.DeerCurse);
            var deer = Get<BossDeertree>(director, "_deer");
            await UniTask.WaitUntil(() => deer.Encounter != null && deer.Encounter.CurseActive, cancellationToken: token);
            Check(deer.CurrentHp <= deer.MaxHp * 0.5m + 1, "Curse starts at the 50% HP threshold");
            await Acknowledge();
            int index = Array.FindIndex(deer.Encounter.Cells, c => c.CurseTapsLeft > 0);
            Check(index >= 0 && deer.Encounter.Cells.Count(c => c.CurseTapsLeft > 0) == 1, "Practice curse binds exactly one unit");
            await WaitText("묶인 유닛을");
            await FocusReady(); await Frames();
            await Snapshot("05-curse");
            var presentation = deer.GetComponentInChildren<DeertreeBossPresentation>(true);
            var curseScreen = (Vector2)camera.WorldToScreenPoint(deer.Cells[index].transform.position);
            for (int tap = 0; tap < 10 && deer.Encounter.Cells[index].CurseTapsLeft > 0; tap++)
            {
                Check(!(bool)typeof(DeertreeBossPresentation).GetMethod("IsOverUI", Private).Invoke(presentation, new object[] { curseScreen, -1 }),
                    "Curse tap " + (tap + 1) + " is not blocked by tutorial UI");
                typeof(DeertreeBossPresentation).GetMethod("Tap", Private).Invoke(presentation, new object[] { curseScreen });
                await UniTask.Delay(120, DelayType.Realtime, cancellationToken: token);
            }
            Check(deer.Encounter.Cells[index].CurseTapsLeft == 0, "Five taps release the curse");
            await Stage(IngameTutorialStage.FinalBattle);
            await WaitText(settings.CombatPracticeInstruction.Substring(0, 5));
            await FocusReady();
            wallet.AddCurrency(Mathf.Max(0, spawn.CurrentCost - wallet.Currency));
            int beforePractice = spawn.SuccessfulSpawnCount;
            ClickButton(summon);
            await UniTask.WaitUntil(() => spawn.SuccessfulSpawnCount == beforePractice + 1 && !dialogue.gameObject.activeInHierarchy, cancellationToken: token);
            Check(true, "Final explanation: sortie summons and dismisses the explanation");
            await UniTask.WaitUntil(() => director.IsComplete, cancellationToken: token);
            await UniTask.Delay(1500, DelayType.Realtime, cancellationToken: token);
            Check(game.CurrentState == GameManager.GameState.Win && game.ResultSnapshot != null && game.ResultSnapshot.Round == 3 && game.ResultSnapshot.BossKills == 3,
                "Tutorial result commits Wave 3 victory and three boss kills");
            await Snapshot("06-result");
            Note("COMPLETE 3WAVE - scripted handlers; Android touch and visual timing still need device review.");
        }
        catch (Exception e) { Note("FAIL stage=" + director.CurrentStage + " " + e); Debug.LogException(e); }
        finally
        {
            if (hadProgress) PlayerPrefs.SetInt(completedKey, previousProgress); else PlayerPrefs.DeleteKey(completedKey);
            PlayerPrefs.Save();
            File.WriteAllText(Report, log.ToString());
            Debug.Log("[3WaveChecks] " + log);
        }
    }

    private static T Get<T>(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Private | BindingFlags.Public);
            if (field != null) return (T)field.GetValue(obj);
        }
        throw new MissingFieldException(obj.GetType().Name, name);
    }
    private static void Call(object obj, string method, Vector2 p) => obj.GetType().GetMethod(method, Private).Invoke(obj, new object[] { p });
    private static void Drag(InputManager input, Vector2 from, Vector2 to)
    {
        Physics2D.SyncTransforms();
        Call(input, "ProcessPointerDown", Camera.main.WorldToScreenPoint(from));
        Call(input, "ProcessPointerMove", Camera.main.WorldToScreenPoint(to));
        Call(input, "ProcessPointerUp", Camera.main.WorldToScreenPoint(to));
    }
}
