using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>Exercises the real scene entry, all five random slots, counter/pause cleanup and hard-only progression.</summary>
public static class HardIngameChecks
{
    private const string Output = "outputs/hard-develop-20261022";
    private static readonly List<string> Results = new();

    public static string Start()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "HardIngameScene")
            throw new InvalidOperationException("Play HardIngameScene first.");
        RunAsync().Forget();
        return "Testing normal entry and boss patterns; completion is written to " + Output + "/checks.txt";
    }

    private static void Check(bool ok, string message)
    {
        Results.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) throw new InvalidOperationException(message);
    }

    private static async UniTaskVoid RunAsync()
    {
        Directory.CreateDirectory(Output);
        Results.Clear();
        TimeScaleService time = null;
        FieldPauseVisuals pause = null;
        var owner = new object();
        var random = UnityEngine.Random.state;
        var scope = Object.FindFirstObjectByType<InGameLifetimeScope>();
        var token = scope.GetCancellationTokenOnDestroy();
        try
        {
            await UniTask.WaitUntil(() => scope.Container != null, cancellationToken: token);
            var container = scope.Container;
            var initializer = container.Resolve<GameInitializer>();
            for (int i = 0; i < 300 && !initializer.IsReady; i++)
                await UniTask.Delay(100, DelayType.Realtime, cancellationToken: token);
            Check(initializer.IsReady, "real GameInitializer reaches ready without a NORMAL server snapshot");
            var game = container.Resolve<GameManager>();
            var wave = container.Resolve<WaveManager>();
            var grid = container.Resolve<GridManager>();
            var bosses = container.Resolve<BossManager>();
            var patterns = container.Resolve<BossPatternController>();
            time = container.Resolve<TimeScaleService>();
            pause = container.Resolve<FieldPauseVisuals>();
            Check(game.CurrentState == GameManager.GameState.Idle, "ordinary start button controls entry; no preview auto-start");
            Check(!grid.GetOccupiedCells().Any(), "no pre-placed preview units");
            Check(GameObject.Find("Hard Boss Controls") == null && GameObject.Find("Hard Boss Preview") == null,
                "no custom boss, skill, health, counter or cycle controls");
            Check(wave.EndlessMode != null && wave.EndlessMode.Key == "HARD_INGAME" && wave.EndlessMode.Slots.Count == 2,
                "dedicated local hard-only endless profile");
            Check(wave.EndlessMode.Slots.All(s => s.Role == EndlessBossRole.Hard), "all encounter slots are hard bosses");
            Check(wave.EndlessMode.Slots[0].Boss.Prefab.name == "Crocus" && wave.EndlessMode.Slots[1].Boss.Prefab.name == "Lizard",
                "only Crocus and Lizard are installed into the cycle");
            using (var run = new EndlessRunService(new EndlessRandom()))
            {
                Check(run.TryStart(wave.EndlessMode, out var error), "hard-only mode passes actual run validation: " + error);
                bool sequence = true;
                for (int round = 1; round <= 24; round++)
                {
                    sequence &= run.BossStats.Source == wave.EndlessMode.Slots[(round - 1) % 2].Boss;
                    if (!run.TryCompleteRound(run.RunId, round)) throw new Exception("round completion rejected");
                    if (run.AwaitingPenaltyChoice && !run.TryChoosePenalty(run.RunId, round, run.PenaltyChoices[0].Key))
                        throw new Exception("penalty choice rejected");
                    if (!run.TryAdvanceRound(run.RunId, round)) throw new Exception("round advance rejected");
                }
                Check(sequence, "24 rounds keep only Crocus/Lizard, preserving penalty choices and endless growth");
            }
            ScreenCapture.CaptureScreenshot(Output + "/ordinary-start.png");
            game.OnStartButtonPressed();
            for (int i = 0; i < 300 && bosses.CurrentBoss == null; i++)
                await UniTask.Delay(100, DelayType.Realtime, cancellationToken: token);
            Check(game.CurrentState == GameManager.GameState.Playing && bosses.CurrentBoss != null,
                "real start enters Playing and WaveManager spawns the first boss");
            var boss = bosses.CurrentBoss;
            ScreenCapture.CaptureScreenshot(Output + "/ordinary-combat.png");
            Check(boss.name.Contains("Crocus") && !boss.PreventDeath, "first boss is mortal Crocus with normal gameplay health");
            boss.ExpMultiplier = 0f; // isolate checks from ordinary EXP selection while placing test actors
            var spawner = container.Resolve<UnitSpawner>();
            var currency = container.Resolve<CurrencyManager>();
            currency.AddCurrency(10000f); // test setup only; the scene retains normal starting food
            for (int i = 0; i < 6; i++) spawner.OnSpawnButtonPressed();
            await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: token);
            Check(spawner.SuccessfulSpawnCount >= 2 && grid.GetOccupiedCells().Count() >= 2,
                "ordinary summon button creates real draggable units");
            time.Pause(owner);
            var driver = boss.GetComponent<PatternLabDriver>();
            Check(boss.Patterns.Length == 6 && driver != null, "Crocus owns earthquake plus five scheduled placement slots");
            Check(boss.Patterns[0].patternType == BossPatternType.Earthquake, "skill 1 remains the original earthquake");
            for (int slot = 1; slot < boss.Patterns.Length; slot++)
            {
                var data = boss.Patterns[slot] as PlacementBossPatternData;
                Check(data != null && data.Candidates.Length == 8 && data.Candidates.Select(c => c.Mechanic).Distinct().Count() == 8,
                    "skill " + (slot + 1) + " can draw all eight authored mechanics");
                var seen = new HashSet<PatternLabMechanic>();
                for (int seed = 0; seed < 160 && seen.Count < 8; seed++)
                {
                    UnityEngine.Random.InitState(slot * 1000 + seed);
                    patterns.RegisterBoss(boss, boss.Patterns);
                    if (!patterns.TryRequestPattern(boss, slot)) throw new Exception("request rejected");
                    patterns.AdvanceCombat(.01f);
                    if (driver.CurrentPattern == null) throw new Exception("random cast did not start");
                    seen.Add(driver.CurrentPattern.Mechanic);
                    var track = boss.GetComponentInChildren<SkeletonAnimation>().AnimationState.GetTrack(0);
                    if (track.Animation.Name != "skill_" + (slot + 1)) throw new Exception("wrong Spine clip");
                }
                Check(seen.Count == 8, "skill " + (slot + 1) + " actually produces all 8 mechanics across seeded casts, using its own Spine clip");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<PatternLabCatalog>("Assets/WorkSpace/USW/Data/BossPatterns/Library/PatternCatalog.asset");
            foreach (var mechanic in catalog.Patterns)
            {
                patterns.UnregisterBoss(boss);
                driver.Configure(boss, grid, game, pause,
                    ((PlacementBossPatternData)boss.Patterns[1]).TelegraphMaterial, true);
                Check(driver.TryStart(mechanic), "real board accepts " + mechanic.DisplayName);
                float elapsed = driver.Elapsed;
                pause.HoldAttacks(owner);
                driver.Advance(1f);
                Check(driver.Elapsed == elapsed, "selection hold freezes " + mechanic.DisplayName);
                pause.Exit(owner, false);
                Check(driver.TryCounter() == mechanic.AllowChiefCounter, "counter policy preserved for " + mechanic.DisplayName);
                driver.ResetAll();
            }
            patterns.RegisterBoss(boss, boss.Patterns);
            // Request until a counterable random cast is selected, then exercise the actual scheduler counter event.
            bool countered = false;
            for (int i = 0; i < 40 && !countered; i++)
            {
                patterns.RegisterBoss(boss, boss.Patterns);
                patterns.TryRequestPattern(boss, 1);
                patterns.AdvanceCombat(.01f);
                if (!patterns.IsCounterablePattern(boss)) continue;
                Check(patterns.TryCounterTelegraphedPatterns(), "shared chieftain counter cancels the random placement cast");
                Check(!driver.IsBusy && !patterns.IsCastingPattern(boss), "counter releases scheduler and warning driver together");
                Check(boss.GetComponentInChildren<SkeletonAnimation>().AnimationState.GetTrack(0).Animation.Name == "idle_0",
                    "counter returns Crocus Spine to idle");
                countered = true;
            }
            Check(countered, "random counterable mechanic was exercised");
            boss.ExpMultiplier = 0f;
            boss.Init(boss.MaxHp);
            var automaticSlots = new List<int>();
            Action<BossPatternData> onAutomatic = data => automaticSlots.Add(Array.IndexOf(boss.Patterns, data));
            boss.OnPatternStarted += onAutomatic;
            try
            {
                patterns.RegisterBoss(boss, boss.Patterns);
                for (int i = 0; i < 44; i++)
                {
                    patterns.AdvanceCombat(.5f);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                Check(automaticSlots.Count > 0 && automaticSlots.All(i => i == 0),
                    "above HP70, only earthquake is timed; skill2..6 never fire on a timer");
                boss.TakeDamage(boss.MaxHp * .31m);
                for (int i = 0; i < 24; i++)
                {
                    patterns.AdvanceCombat(.5f);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                Check(automaticSlots.Count(i => i > 0) == 1,
                    "crossing HP70 automatically selects exactly one random skill2..6");
                boss.Heal(boss.MaxHp);
                boss.TakeDamage(boss.MaxHp * .31m);
                patterns.AdvanceCombat(.1f);
                Check(automaticSlots.Count(i => i > 0) == 1, "healing and re-crossing HP70 cannot repeat the phase");
                boss.TakeDamage(boss.MaxHp * .4m);
                for (int i = 0; i < 24; i++)
                {
                    patterns.AdvanceCombat(.5f);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                Check(automaticSlots.Count(i => i > 0) == 2,
                    "crossing HP30 automatically selects exactly one more random skill2..6");
                boss.Heal(boss.MaxHp);
                boss.TakeDamage(boss.MaxHp * .76m);
                patterns.AdvanceCombat(.1f);
                Check(automaticSlots.Count(i => i > 0) == 2, "both HP gates stay consumed after healing or further damage");

                boss.Init(boss.MaxHp);
                patterns.RegisterBoss(boss, boss.Patterns);
                automaticSlots.Clear();
                boss.TakeDamage(boss.MaxHp * .75m);
                for (int i = 0; i < 48; i++)
                {
                    patterns.AdvanceCombat(.5f);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                Check(automaticSlots.Count(i => i > 0) == 2,
                    "one hit crossing both HP gates queues two sequential casts without dropping or multiplying either gate");

                var phaseSkills = new HashSet<int>();
                for (int seed = 0; seed < 80 && phaseSkills.Count < 5; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    boss.Init(boss.MaxHp);
                    patterns.RegisterBoss(boss, boss.Patterns);
                    automaticSlots.Clear();
                    boss.TakeDamage(boss.MaxHp * .31m);
                    patterns.AdvanceCombat(.01f);
                    if (automaticSlots.Count != 1 || automaticSlots[0] < 1) throw new Exception("HP gate cast missing");
                    phaseSkills.Add(automaticSlots[0]);
                }
                Check(phaseSkills.Count == 5, "HP gates actually select every one of skill2..6 across seeded boss spawns");
            }
            finally { boss.OnPatternStarted -= onAutomatic; }
            patterns.UnregisterBoss(boss);
            Check(!driver.IsBusy && !grid.AllCells().Any(c => c.OccupyingUnit != null && c.OccupyingUnit.IsStunned),
                "unregister clears placement warnings and unit stun ownership");

            // The following spawn uses the same manager; original Lizard data and presentation stay installed.
            bosses.SpawnSingleBoss(new BossEntry { Data = wave.EndlessMode.Slots[1].Boss }, null);
            var lizard = bosses.CurrentBoss as BossLizard;
            lizard.ExpMultiplier = 0f; // isolate pattern assertions from the ordinary level-up selection flow
            Check(lizard != null && lizard.Patterns.Length == 3 && lizard.GetComponent<PatternLabDriver>() == null,
                "Lizard retains exactly its three original patterns and no random placement driver");
            patterns.TryRequestPattern(lizard, 0);
            patterns.AdvanceCombat(.01f);
            patterns.AdvanceCombat(1.5f);
            Check(lizard.ThreatRemaining > 0f, "original Lizard threat still applies");
            patterns.UnregisterBoss(lizard);
            patterns.RegisterBoss(lizard, lizard.Patterns);
            lizard.TakeDamage(lizard.MaxHp * .31m);
            patterns.AdvanceCombat(.01f);
            patterns.AdvanceCombat(1.5f);
            Check(lizard.BarrierActive, "original HP70 Lizard barrier still activates");
            lizard.TakeDamage(lizard.MaxHp * .5m);
            Check(lizard.CurrentHp <= lizard.MaxHp * .3m, "barrier-reduced real damage crosses HP30");
            patterns.AdvanceCombat(lizard.CastingPattern.CastDuration + .01f);
            Check(lizard.IsBurrowed && !lizard.IsTargetable, "original HP30 burrow still excludes targeting");
            decimal hp = lizard.CurrentHp;
            patterns.AdvanceCombat(1.1f);
            Check(lizard.CurrentHp > hp, "original burrow heals through real boss health events");
            patterns.AdvanceCombat(3f);
            Check(!lizard.IsBurrowed && lizard.IsTargetable, "original burrow returns to targetable state");
            File.WriteAllLines(Output + "/checks.txt", new[] { "PASS " + Results.Count + " checks" }.Concat(Results));
        }
        catch (Exception error)
        {
            File.WriteAllLines(Output + "/checks.txt", new[] { "FAIL " + error }.Concat(Results));
            Debug.LogException(error);
        }
        finally
        {
            pause?.Exit(owner, false);
            time?.Release(owner);
            UnityEngine.Random.state = random;
        }
    }
}
