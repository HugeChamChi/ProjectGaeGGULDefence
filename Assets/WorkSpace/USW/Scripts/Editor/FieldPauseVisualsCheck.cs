using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GaeGGUL.Animation;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>Fresh IngameScene Play checks for selection ownership, impacts, bursts and boss clocks.</summary>
public static class FieldPauseVisualsCheck
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Report = "Temp/FieldPauseCheck/result.txt";
    private static readonly StringBuilder Log = new();
    private static int _pass, _fail;
    private static bool _running;

    /// <summary>Runs controlled cases through live gameplay and UI; all temporary state ends with Play.</summary>
    [MenuItem("Tools/USW/Checks/Field Pause Visuals (Play)")]
    public static void Run()
    {
        if (_running) return;
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter a fresh IngameScene Play session.");
        _running = true;
        RunAsync().Forget();
    }

    private static void Check(bool condition, string label)
    {
        if (condition) _pass++; else _fail++;
        Note((condition ? "PASS " : "FAIL ") + label);
    }

    private static void Note(string text)
    {
        Log.AppendLine(text);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, Log.ToString());
        Debug.Log("[FieldPauseCheck] " + text);
    }

    private static T Get<T>(object value, string field) => (T)FindField(value.GetType(), field).GetValue(value);
    private static void Set(object value, string field, object next) => FindField(value.GetType(), field).SetValue(value, next);
    private static FieldInfo FindField(Type type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, Private | BindingFlags.Public);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }
    private static object Call(object value, string method, params object[] args) =>
        value.GetType().GetMethod(method, Private).Invoke(value, args);
    private static UniTask Real(float seconds, CancellationToken token) =>
        UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, cancellationToken: token);
    private static void OpenLevel(GameManager game) => Call(game, "HandleLevelUp");
    private static void CloseLevel(LevelUpUI ui) => Call(ui, "Hide");
    private static void CloseReward(TotemRewardUI ui) => Call(ui, "CloseAsync", Get<CancellationTokenSource>(ui, "_selectionCts").Token);
    private static List<UnitBase> Units(GridManager grid) => grid.GetOccupiedCells().Select(c => c.OccupyingUnit).Where(u => u != null).ToList();
    private static void StopAutomaticAttacks(GridManager grid)
    {
        foreach (var unit in Units(grid)) { unit.Combat.StopLoops(); Set(unit.Combat, "_attackTimer", 0f); Set(unit.Combat, "_skillTimer", 0f); }
        foreach (var drone in Object.FindObjectsByType<DroneUnit>(FindObjectsSortMode.None))
        {
            var attack = Get<CancellationTokenSource>(drone, "_attackCts");
            attack?.Cancel(); attack?.Dispose(); Set(drone, "_attackCts", null);
        }
    }

    private static async UniTaskVoid RunAsync()
    {
        Log.Clear(); _pass = _fail = 0;
        var game = Object.FindFirstObjectByType<GameManager>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(game.GetCancellationTokenOnDestroy());
        using var timeout = cts.CancelAfterSlim(TimeSpan.FromMinutes(4), DelayType.Realtime);
        var token = cts.Token;
        var scope = Object.FindFirstObjectByType<InGameLifetimeScope>();
        var resolver = scope.Container;
        var pause = resolver.Resolve<FieldPauseVisuals>();
        var grid = resolver.Resolve<GridManager>();
        var timer = resolver.Resolve<TimerController>();
        var bosses = resolver.Resolve<BossManager>();
        var pool = resolver.Resolve<ProjectilePool>();
        var exp = resolver.Resolve<ExpManager>();
        var patterns = resolver.Resolve<BossPatternController>();
        var level = Object.FindFirstObjectByType<LevelUpUI>(FindObjectsInactive.Include);
        var reward = Object.FindFirstObjectByType<TotemRewardUI>(FindObjectsInactive.Include);
        var allocated = new List<Object>();
        try
        {
            Check(game.CurrentState == GameManager.GameState.Idle, "fresh session starts Idle");
            exp.DeferLevelUps = true;
            game.OnStartButtonPressed();
            await UniTask.WaitUntil(() => bosses.CurrentBoss != null, cancellationToken: token);
            var boss = bosses.CurrentBoss;
            patterns.enabled = false;
            var factory = resolver.Resolve<UnitFactory>();
            for (int i = 0; i < 4; i++)
            {
                var unit = factory.CreateRandomNormalUnit();
                var cell = grid.GetEmptyCells().First();
                cell.TryPlaceUnit(unit);
                unit.transform.position = cell.transform.position;
                unit.gameObject.SetActive(true);
                unit.OnPlaced(resolver.Resolve<CurrencyManager>(), boss, cell);
            }
            await Real(.25f, token);
            StopAutomaticAttacks(grid);
            Check(Units(grid).Count >= 4, "four real authored units placed");
            var caster = Units(grid).First();
            // Already launched impact versus an unrelated direct request during LevelUp.
            var flight = ScriptableObject.CreateInstance<ProjectileData>(); allocated.Add(flight);
            flight.movement = new StraightMovement { duration = .15f };
            int landed = 0;
            decimal hpBefore = boss.CurrentHp;
            float expBefore = exp.CurrentExp;
            pool.Launch(caster.transform.position, boss.transform.position, flight,
                () => { landed++; boss.ApplyProjectileImpact(() => boss.TakeDamage(100)); }, caster);
            OpenLevel(game);
            boss.TakeDamage(1000);
            Check(boss.CurrentHp == hpBefore, "R1 unrelated direct damage rejected during selection grace");
            Check(pause.AttacksHeld && Time.timeScale > 0f, "single level selection grants existing-projectile grace");
            await Real(.3f, token);
            Check(landed == 1 && boss.CurrentHp < hpBefore, "R1 actually launched projectile arrives and reduces HP in LevelUp grace");
            Check(exp.CurrentExp > expBefore, "R1 impact EXP applied immediately");
            await Real(.8f, token);
            Check(Time.timeScale == 0f && pause.IsActive, "single level freezes after unchanged grace");
            await CheckMotionAsync(Units(grid), token);
            var skeleton = boss.GetComponentInChildren<SkeletonAnimation>();
            Check(skeleton != null, "authored boss Spine is available");
            float idleTime = skeleton.AnimationState.GetTrack(0).TrackTime;
            await Real(.25f, token);
            Check(skeleton.AnimationState.GetTrack(0).TrackTime > idleTime, "boss idle advances in real time at global pause");
            // Overlap: new reward inherits freeze, then closing level cannot resume combat.
            reward.Show(null);
            Check(Time.timeScale == 0f, "R2 new reward inherits existing freeze immediately");
            await Real(1.5f, token);
            Check(Get<bool>(reward, "_isOpen"), "actual reward selection opened");
            CloseLevel(level);
            float timerBefore = timer.RemainingTime;
            await Real(.7f, token);
            Check(pause.AttacksHeld && pause.IsActive && Time.timeScale == 0f, "R2 level close retains reward owner and time pause");
            Check(timer.RemainingTime == timerBefore && Units(grid).All(u => u.Combat.AttacksHeld), "R2 no timer or unit-loop leak between selections");
            CloseReward(reward);
            await Real(.8f, token);
            Check(!pause.AttacksHeld && !pause.IsActive && Time.timeScale > 0f, "R2 last normal reward close releases combat");
            Check(Units(grid).All(u => !u.Combat.AttacksHeld), "last owner resumes every unit");
            Check(!skeleton.UnscaledTime, "boss idle restores original scaled clock");
            StopAutomaticAttacks(grid);
            await Real(.5f, token);
            reward.Show(null);
            Check(pause.AttacksHeld && Time.timeScale > 0f, "single reward grants grace while holding combat");
            await Real(.9f, token);
            Check(pause.IsActive && Time.timeScale == 0f, "single reward freezes after unchanged grace");
            // Reward selections keep GameState.Playing, so EXP must explicitly defer through the owner.
            exp.TryFireLevelUp();
            Check(Get<bool>(exp, "_pendingLevelUp") && game.CurrentState == GameManager.GameState.Playing
                && !level.gameObject.activeInHierarchy, "EXP level is deferred during a reward-only selection");
            CloseReward(reward);
            await Real(.6f, token);
            Check(game.CurrentState == GameManager.GameState.LevelUp && pause.AttacksHeld && Time.timeScale == 0f,
                "reward close hands pending level the existing freeze without a grace gap");
            CloseLevel(level); StopAutomaticAttacks(grid);
            await Real(.5f, token);
            // Reverse overlap closes reward first while level owner remains.
            reward.Show(null);
            await Real(.9f, token);
            OpenLevel(game);
            Check(Time.timeScale == 0f, "level opened over a paused reward inherits freeze immediately");
            CloseReward(reward);
            await Real(.6f, token);
            Check(pause.AttacksHeld && pause.IsActive && Time.timeScale == 0f && Units(grid).All(u => u.Combat.AttacksHeld),
                "reverse overlap retains level owner when reward closes first");
            CloseLevel(level); StopAutomaticAttacks(grid);
            await Real(.5f, token);
            var externalOwner = new object();
            resolver.Resolve<TimeScaleService>().Pause(externalOwner);
            OpenLevel(game); CloseLevel(level);
            await Real(.5f, token);
            Check(!pause.AttacksHeld && Time.timeScale == 0f, "selection close respects an external TimeScaleService pause owner");
            resolver.Resolve<TimeScaleService>().Release(externalOwner);
            StopAutomaticAttacks(grid);
            // Force disable: cleanup cancels delayed Enter as well as the owner/time request.
            OpenLevel(game);
            await Real(.1f, token);
            level.gameObject.SetActive(false);
            await Real(1f, token);
            Check(!pause.AttacksHeld && !pause.IsActive && Time.timeScale > 0f, "forced LevelUp disable releases owner and cancels delayed freeze");
            Check(game.CurrentState == GameManager.GameState.Playing, "forced LevelUp disable leaves Playing state");
            StopAutomaticAttacks(grid);
            // Three-shot action: hold starts after the first launch, two remaining shots survive.
            var hitProbe = new FieldPauseHitProbe();
            var burst = new MultiShotSkillAction { shotCount = new ConstantInt { value = 3 }, shotInterval = .2f };
            var effects = new List<IEffect> { hitProbe };
            burst.Execute(caster, caster.Combat, effects, null);
            await Real(.04f, token);
            OpenLevel(game);
            await Real(1.1f, token);
            Check(hitProbe.Hits == 1, "D2 burst only first in-flight shot lands while selection is open: " + hitProbe.Hits);
            Check(hitProbe.DamageUnits > 0, "D2 real unit burst impact applies damage in LevelUp grace");
            CloseLevel(level);
            StopAutomaticAttacks(grid);
            await Real(1.1f, token);
            Check(hitProbe.Hits == 3, "D2 two remaining burst shots resume exactly once: " + hitProbe.Hits);
            // Boss pattern uses the real controller and authored Spine mapping.
            var pattern = Object.Instantiate(boss.Patterns.First(p => p.patternType == BossPatternType.Earthquake));
            allocated.Add(pattern); pattern.interval = .01f; pattern.ImpactTimeSec = 3f; pattern.SkillDuration = 4f;
            patterns.RegisterBoss(boss, new[] { pattern }); patterns.enabled = true;
            await UniTask.WaitUntil(() => skeleton.AnimationState.GetTrack(0).Animation.Name != "idle_0", cancellationToken: token);
            var entries = Get<IDictionary>(patterns, "_entries");
            var entry = entries[boss];
            float patternElapsed = Get<float>(entry, "Elapsed");
            float trackTime = skeleton.AnimationState.GetTrack(0).TrackTime;
            OpenLevel(game);
            await Real(.25f, token);
            Check(Get<float>(entry, "Elapsed") == patternElapsed, "R4 boss pattern logic freezes from hold, including grace");
            Check(skeleton.AnimationState.GetTrack(0).TrackTime == trackTime && skeleton.timeScale == 0f,
                "R4 boss pattern motion freezes at same hold frame");
            await Real(.9f, token);
            Check(skeleton.AnimationState.GetTrack(0).TrackTime == trackTime, "R4 queued idle cannot advance during held pattern");
            CloseLevel(level);
            StopAutomaticAttacks(grid);
            await Real(.5f, token);
            Check(skeleton.timeScale == 1f && skeleton.AnimationState.GetTrack(0).TrackTime > trackTime,
                "R4 pattern restores original speed and continues");
            patterns.enabled = false;
            patterns.RegisterBoss(boss, boss.Patterns);
            await CheckDroneAsync(resolver, pause, game, level, grid, boss, token);
            await CheckEmitterAsync(resolver, pause, game, level, grid, boss, token);
            StopAutomaticAttacks(grid);
            // D3: lethal in-flight hit, deferred EXP level, then one scaled reward delay.
            exp.DeferLevelUps = false;
            Set(exp, "<CurrentExp>k__BackingField", 0f);
            Set(exp, "_pendingLevelUp", false);
            boss.ExpMultiplier = (exp.ExpToLevelUp + 1f) / (float)boss.CurrentHp;
            int levelBefore = exp.CurrentLevel;
            int deathCount = 0, rewardCount = 0;
            float rewardTime = -1;
            var wave = resolver.Resolve<WaveManager>();
            boss.OnDeath += () => deathCount++;
            wave.OnTotemSelectionRequested += _ => { rewardCount++; rewardTime = Time.time; };
            pool.Launch(caster.transform.position, boss.transform.position, flight,
                () => boss.ApplyProjectileImpact(() => boss.TakeDamage(boss.MaxHp * 10)), caster);
            OpenLevel(game);
            await Real(.3f, token);
            Check(deathCount == 1 && bosses.CurrentBoss == null, "D3 lethal in-flight impact consumes boss death immediately once");
            Check(exp.CurrentLevel == levelBefore + 1 && Get<bool>(exp, "_pendingLevelUp"), "D3 kill EXP queues next level immediately");
            await Real(1.2f, token);
            Check(rewardCount == 0 && Time.timeScale == 0f, "D3 reward scaled delay stays pending while first choice open");
            CloseLevel(level);
            Check(game.CurrentState == GameManager.GameState.LevelUp && pause.AttacksHeld && Time.timeScale == 0f,
                "D3 pending EXP opens chained level without releasing pause");
            await Real(.5f, token);
            Check(rewardCount == 0, "D3 chained level still precedes reward");
            float closeTime = Time.time;
            CloseLevel(level);
            await UniTask.WaitUntil(() => rewardCount > 0, cancellationToken: token);
            Check(rewardCount == 1 && rewardTime > closeTime, "D3 exactly one reward opens after final level closes");
            Check(game.CurrentState == GameManager.GameState.Playing && !level.gameObject.activeInHierarchy,
                "D3 reward and level selection do not overlap");
            await Real(1.4f, token);
            Check(rewardCount == 1 && Get<bool>(reward, "_isOpen"), "D3 reward is neither duplicated nor lost");
            reward.CancelPendingRewards();
            exp.DeferLevelUps = true;
            OpenLevel(game);
            await Real(.2f, token);
            Object.Destroy(level.gameObject);
            await Real(.9f, token);
            Check(!pause.AttacksHeld && !pause.IsActive && Time.timeScale > 0f, "destroyed LevelUp releases owner and time request");
        }
        catch (Exception error) { Check(false, "unexpected exception " + error); }
        finally
        {
            foreach (var asset in allocated) if (asset != null) Object.Destroy(asset);
            _running = false;
            Note($"RESULT PASS {_pass} / FAIL {_fail}");
        }
    }

    private static async UniTask CheckMotionAsync(List<UnitBase> units, CancellationToken token)
    {
        Check(units.Any(u => !u.IsStunned), "at least one non-stunned unit participates in idle-motion checks");
        foreach (var unit in units)
        {
            if (unit.IsStunned) { Note("EXCLUDED stunned unit " + unit.name); continue; }
            var transforms = unit.GetComponentsInChildren<Transform>(true);
            var scales = transforms.Select(t => t.localScale).ToArray();
            var positions = transforms.Select(t => t.localPosition).ToArray();
            float delta = 0;
            for (int sample = 0; sample < 6; sample++)
            {
                await Real(.07f, token);
                for (int i = 0; i < transforms.Length; i++)
                    delta = Mathf.Max(delta, (transforms[i].localScale - scales[i]).magnitude, (transforms[i].localPosition - positions[i]).magnitude);
            }
            Check(delta > .0001f && Time.timeScale == 0f, "per-unit per-axis idle moves: " + unit.name + " delta=" + delta);
            foreach (var visual in unit.GetComponentsInChildren<Anim_Base>(true))
            {
                var tween = Get<Sequence>(visual, "_currentSeq");
                Check(tween != null && tween.IsActive() && tween.IsPlaying(), "idle tween playing: " + unit.name);
            }
        }
    }

    private static async UniTask CheckDroneAsync(IObjectResolver resolver, FieldPauseVisuals pause, GameManager game,
        LevelUpUI level, GridManager grid, BossBase boss, CancellationToken token)
    {
        var manager = resolver.Resolve<DroneManager>();
        StopAutomaticAttacks(grid);
        var owner = Units(grid).First();
        OpenLevel(game);
        var go = new GameObject("PauseCheckDrone");
        var drone = go.AddComponent<DroneUnit>(); resolver.Inject(drone);
        drone.Initialize(owner, Vector2.zero);
        Check(Get<bool>(drone, "_attackHeld"), "R3 drone created during grace inherits hold");
        var authoredDrone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/USW/Prefab/Unit/DronUnit/Drone_Normal_Prefab.prefab").GetComponent<DroneUnit>();
        Set(drone, "_projectilePrefab", Get<Projectile>(authoredDrone, "_projectilePrefab"));
        int deferredImpacts = 0;
        Call(drone, "ShootProjectile", boss.transform.position, (Action)(() => deferredImpacts++), null);
        await Real(.9f, token);
        Check(deferredImpacts == 0, "R3 direct prefab projectile route defers launch during selection");
        go.SetActive(false); go.SetActive(true); drone.Initialize(owner, Vector2.zero);
        Check(Get<bool>(drone, "_attackHeld"), "R3 pooled drone reuse re-inherits active hold");
        Check(Get<bool>(drone, "_inPauseIdle"), "pooled drone inherits idle visual clock");
        CloseLevel(level); StopAutomaticAttacks(grid);
        await Real(.6f, token);
        Check(deferredImpacts == 0, "pooled reuse discards a deferred shot from the prior drone lifetime");
        OpenLevel(game);
        Call(drone, "ShootProjectile", boss.transform.position, (Action)(() => deferredImpacts++), null);
        await Real(.9f, token);
        Check(deferredImpacts == 0, "R3 prefab route keeps current-lifetime shot pending while held");
        CloseLevel(level); StopAutomaticAttacks(grid);
        await Real(.8f, token);
        Check(deferredImpacts == 1, "R3 prefab route resumes current-lifetime shot exactly once");
        int hits = 0;
        Action<decimal, Vector3?> counter = (_, __) => { hits++; if (hits == 1) OpenLevel(game); };
        boss.OnDamaged += counter;
        var rally = manager.ExecuteRallyAsync(10f, token,
            new ChiefVolleySettings(2, .15f, .5f));
        await UniTask.WaitUntil(() => hits > 0, cancellationToken: token);
        await Real(1.1f, token);
        Check(hits == 1, "D2 rally double-shot holds second damage and shot: " + hits);
        boss.OnDamaged -= counter;
        Action<decimal, Vector3?> remaining = (_, __) => hits++;
        boss.OnDamaged += remaining;
        CloseLevel(level); StopAutomaticAttacks(grid);
        await rally;
        Check(hits == 2, "D2 rally remaining shot resumes exactly once: " + hits);
        boss.OnDamaged -= remaining;
        go.SetActive(false);
        Check(!Get<bool>(drone, "_attackHeld") && !Get<bool>(drone, "_inPauseIdle"), "pool return clears transient drone pause state");
        Object.Destroy(go);
    }

    private static async UniTask CheckEmitterAsync(IObjectResolver resolver, FieldPauseVisuals pause, GameManager game,
        LevelUpUI level, GridManager grid, BossBase boss, CancellationToken token)
    {
        StopAutomaticAttacks(grid);
        await Real(.5f, token);
        var data = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1003Data.asset");
        var cell = grid.GetEmptyCells().First();
        bool placed = await resolver.Resolve<TotemSpawner>().PlaceTotemAtCellAsync(data, cell, token);
        Check(placed, "authored TD1003 placed through real spawner");
        var source = cell.OccupyingTotem;
        var emitter = source.GetComponent<TotemDebuffEmitter>();
        Set(emitter, "_elapsed", source.Data.DebuffFireInterval);
        decimal hpBefore = boss.CurrentHp;
        Call(emitter, "Update");
        Set(emitter, "_elapsed", 1.25);
        OpenLevel(game);
        Call(emitter, "Update");
        Check(Get<double>(emitter, "_elapsed") == 1.25, "R3 separate emitter retains periodic timer while attacks held");
        await Real(.35f, token);
        Check(boss.CurrentHp < hpBefore && boss.Debuffs.Active.Count > 0, "R1 real TD1003 projectile applies damage and debuff in grace");
        await Real(.8f, token);
        Check(Get<double>(emitter, "_elapsed") == 1.25 && Time.timeScale == 0f, "R3 real TD1003 emitter stays held throughout choice");
        var manager = resolver.Resolve<DroneManager>();
        var card = ScriptableObject.CreateInstance<LevelUpData>();
        card.chooseId = 990022;
        card.droneEffect = new DroneSelectionEffect { Kind = DroneSelectionKind.BetanPeriodicBomb, Interval = .1f, Count = 1 };
        var selections = resolver.Resolve<LevelUpManager>();
        selections.ApplyEffect(card);
        int bombsBefore = Object.FindObjectsByType<SelfDestructDrone>(FindObjectsSortMode.None).Length;
        Set(manager, "_betanNormalTimer", .05f);
        manager.TickSelections(10f);
        Check(Get<float>(manager, "_betanNormalTimer") == .05f
            && Object.FindObjectsByType<SelfDestructDrone>(FindObjectsSortMode.None).Length == bombsBefore,
            "R3 configured periodic Betan summons retain timer and spawn nothing while held");
        CloseLevel(level); StopAutomaticAttacks(grid);
        manager.TickSelections(.2f);
        Check(Object.FindObjectsByType<SelfDestructDrone>(FindObjectsSortMode.None).Length > bombsBefore,
            "R3 configured periodic Betan summons restart after release");
        selections.RemoveEffect(card); Object.Destroy(card);
        foreach (var bomb in Object.FindObjectsByType<SelfDestructDrone>(FindObjectsSortMode.None)) Object.Destroy(bomb.gameObject);
        source.OnRemoved(); cell.RemoveTotem(); Object.Destroy(source.gameObject);
        boss.Debuffs.Clear();
        await Real(.4f, token);
    }
}
