using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>
/// 용어 팝업 영상(필름) 녹화 도구 — IngameScene Play 중에만 쓴다. 씬·SO는 저장하지 않고 Play가 끝나면 모두 사라진다.
///   1. Setup Battle: 게임 시작 → 보스를 악어로 교체(지진 예고 패턴은 악어만 있음) → 베탕(레전더리) 2기 배치 → 드론 대기.
///      유닛·드론의 자동 공격은 멈춘다 (사용자 요청: 자폭 드론만 보이게, 보스가 녹화 전에 죽지 않게).
///   2. Record Self-Destruct: 베탕 자폭 드론 발사 장면 3.5초.
///   3. Record Chief Skill: 악어 지진 예고 중 족장 스킬 버튼 → 드론 집결 일제 사격 → 패턴 끊기 5초.
/// Game 뷰(오버레이 UI 포함)를 실시간으로 절반 해상도 원본 RGBA로 저장한다 → Temp/TermClipCapture/{이름}/f_###.rgba (+info.txt).
/// PNG 인코딩은 프레임을 밀리게 해서 원본 바이트로 쓴다. 영상 변환은 ffmpeg rawvideo로 한다 (위아래 뒤집힘 → vflip).
/// 녹화 중 판이 시간 초과로 끝나지 않게 카운트다운을 멈춘다.
/// </summary>
public static class TermClipRecorder
{
    private const string CrocDataPath = "Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodilePrototypeData.asset";
    private const string EarthquakePath = "Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodileEarthquake.asset";
    private const string BetanDataPath = "Assets/WorkSpace/USW/Data/DroneUnits/UnitData_Betan.asset";
    private const string OutRoot = "Temp/TermClipCapture";
    private const int Fps = 30;
    private const float CaptureScale = 0.5f;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool _busy;

    [MenuItem("Tools/USW/Fx/Term Clips/1. Setup Battle (IngameScene Play)")]
    public static void Setup() => Run(SetupAsync);

    [MenuItem("Tools/USW/Fx/Term Clips/2. Record Self-Destruct")]
    public static void RecordSelfDestruct() => Run(RecordSelfDestructAsync);

    [MenuItem("Tools/USW/Fx/Term Clips/3. Record Chief Skill")]
    public static void RecordChiefSkill() => Run(RecordChiefSkillAsync);

    private static void Run(Func<CancellationToken, UniTask> job)
    {
        var game = Object.FindFirstObjectByType<GameManager>();
        if (!EditorApplication.isPlaying || game == null) { Debug.LogError("[TermClip] IngameScene Play 중에만 실행한다"); return; }
        if (_busy) { Debug.LogWarning("[TermClip] 이전 작업 진행 중"); return; }
        _busy = true;
        Wrap(job, game.GetCancellationTokenOnDestroy()).Forget();
    }

    private static async UniTaskVoid Wrap(Func<CancellationToken, UniTask> job, CancellationToken token)
    {
        try { await job(token); }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e); }
        finally { _busy = false; }
    }

    private static IObjectResolver Resolver => Object.FindFirstObjectByType<InGameLifetimeScope>().Container;

    private static UniTask Real(float seconds, CancellationToken token) =>
        UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, cancellationToken: token);

    private static async UniTask SetupAsync(CancellationToken token)
    {
        var r = Resolver;
        var game = Object.FindFirstObjectByType<GameManager>();
        var bosses = r.Resolve<BossManager>();
        var grid = r.Resolve<GridManager>();
        var patterns = r.Resolve<BossPatternController>();
        var currency = r.Resolve<CurrencyManager>();
        r.Resolve<ExpManager>().DeferLevelUps = true; // 녹화 중 레벨업 창이 뜨지 않게
        var timer = r.Resolve<TimerController>();

        if (game.CurrentState == GameManager.GameState.Idle) game.OnStartButtonPressed();
        await UniTask.WaitUntil(() => bosses.CurrentBoss != null, cancellationToken: token);
        timer.StopTimer(); // 보스를 안 잡으니 시간 초과로 판이 끝나지 않게

        // 족장 스킬이 끊는 지진 예고 패턴은 악어에만 있다 → 보스를 악어로 바꾼다 (패배 콜백 없음, 녹화 전용).
        var croc = AssetDatabase.LoadAssetAtPath<BossData>(CrocDataPath);
        bosses.SpawnSingleBoss(new BossEntry { Data = croc }, null);
        await UniTask.Yield(token);
        var boss = bosses.CurrentBoss;
        patterns.RegisterBoss(boss, Array.Empty<BossPatternData>()); // 녹화 전까지 자연 패턴 끔

        var betan = AssetDatabase.LoadAssetAtPath<UnitData>(BetanDataPath);
        var factory = r.Resolve<UnitFactory>();
        var placed = new HashSet<UnitBase>();
        // 가운데 줄에서 넓게 벌려 배치 (사용자 요청 2026-10-11: 처음 한 칸 띄움 → 다시 좌우로 한 칸씩 더). 6열이면 1·5열.
        int cx = grid.Columns / 2, cz = grid.Rows / 2;
        foreach (var cell in new[] { grid.GetCell(cx - 2, cz), grid.GetCell(cx + 2, cz) })
        {
            if (cell == null || cell.OccupyingUnit != null) continue;
            var unit = factory.CreateUnitFromData(betan, Tier.Legend);
            cell.TryPlaceUnit(unit);
            unit.transform.position = cell.transform.position;
            unit.gameObject.SetActive(true);
            unit.OnPlaced(currency, boss, cell);
            placed.Add(unit);
        }
        await UniTask.Yield(token);
        StopAutomaticAttacks(grid);
        boss.Init(boss.MaxHp); // 멈추기 전에 들어간 피해를 되돌린다
        // 원래 있던 유닛도 새 보스를 겨누게 (WaveManager 보스 교체와 같은 방식)
        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null || placed.Contains(unit)) continue;
            unit.OnRemoved();
            unit.OnPlaced(currency, boss, cell);
        }
        await Real(2.5f, token);
        StopAutomaticAttacks(grid);
        timer.StopTimer();
        Debug.Log($"[TermClip] 준비 완료: 보스 {boss.name}, 베탕 {placed.Count}기, 드론 {Object.FindObjectsByType<DroneUnit>(FindObjectsSortMode.None).Length}기");
    }

    private static async UniTask RecordSelfDestructAsync(CancellationToken token)
    {
        var betan = Object.FindObjectsByType<Drone_Betan>(FindObjectsSortMode.None).FirstOrDefault();
        if (betan == null) { Debug.LogError("[TermClip] 베탕 없음 — 1. Setup Battle 먼저"); return; }
        StopAutomaticAttacks(Resolver.Resolve<GridManager>());
        await RecordAsync("SelfDestruct", 3.5f, async t =>
        {
            await Real(0.3f, t);
            betan.SpawnSelfDestructDrones(3);
        }, token);
    }

    private static async UniTask RecordChiefSkillAsync(CancellationToken token)
    {
        var r = Resolver;
        var bosses = r.Resolve<BossManager>();
        var patterns = r.Resolve<BossPatternController>();
        var boss = bosses.CurrentBoss;
        var skill = Object.FindFirstObjectByType<ChieftainSelection>()?.ActiveSkill as AlphanActiveSkill;
        var view = Object.FindFirstObjectByType<UI_ChiefSkillButtonView>();
        var button = view != null ? view.GetType().GetField("btn_Skill", AnyInstance)?.GetValue(view) as Button : null;
        if (boss == null || skill == null || button == null)
        {
            Debug.LogError($"[TermClip] 준비 안 됨 — 보스 {boss != null}, 족장 스킬 {skill != null}, 버튼 {button != null}");
            return;
        }

        StopAutomaticAttacks(r.Resolve<GridManager>());
        skill.Advance(skill.CooldownSeconds); // 충전 완료 상태로
        var pattern = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BossPatternData>(EarthquakePath));
        pattern.interval = 0.01f;
        pattern.ImpactTimeSec = 3.5f; // 예고가 충분히 보이게
        pattern.SkillDuration = 4.5f;
        bool countered = false;
        Action<BossBase, BossPatternData> onCounter = (_, __) => countered = true;
        patterns.OnPatternCountered += onCounter;
        try
        {
            patterns.RegisterBoss(boss, new[] { pattern });
            await Real(0.6f, token); // 예고 시작
            await RecordAsync("ChiefSkill", 5f, async t =>
            {
                await Real(0.9f, t);
                if (!skill.CanActivate) Debug.LogWarning("[TermClip] 족장 스킬 발동 불가 (드론 0기/집결 중/쿨타임)");
                button.onClick.Invoke(); // 실제 버튼 경로 (Presenter → TryActivate)
                // 녹화용 패턴은 간격이 0.01초라 끊긴 직후 다시 예고한다 → 끊기면 바로 패턴을 비워 "끊긴 뒤"가 보이게.
                await UniTask.WaitUntil(() => countered, cancellationToken: t).TimeoutWithoutException(TimeSpan.FromSeconds(2));
                patterns.RegisterBoss(boss, Array.Empty<BossPatternData>());
            }, token);
        }
        finally
        {
            patterns.OnPatternCountered -= onCounter;
            if (boss != null) patterns.RegisterBoss(boss, Array.Empty<BossPatternData>());
            Object.Destroy(pattern);
        }
        Debug.Log($"[TermClip] 족장 스킬 패턴 끊기: {(countered ? "성공" : "실패 (예고 중이 아니었음)")}");
    }

    // FieldPauseVisualsCheck와 같은 방식: 유닛 공격·스킬 루프와 드론 자동 공격을 멈춘다 (Play 세션 한정).
    private static void StopAutomaticAttacks(GridManager grid)
    {
        foreach (var cell in grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null) continue;
            unit.Combat.StopLoops();
            SetField(unit.Combat, "_attackTimer", 0f);
            SetField(unit.Combat, "_skillTimer", 0f);
        }
        foreach (var drone in Object.FindObjectsByType<DroneUnit>(FindObjectsSortMode.None))
        {
            var attack = GetField(drone, "_attackCts") as CancellationTokenSource;
            attack?.Cancel();
            attack?.Dispose();
            SetField(drone, "_attackCts", null);
        }
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, AnyInstance);
            if (field != null) return field;
        }
        return null;
    }
    private static object GetField(object target, string name) => FindField(target.GetType(), name)?.GetValue(target);
    private static void SetField(object target, string name, object value) => FindField(target.GetType(), name)?.SetValue(target, value);

    // 실시간으로 Game 뷰(오버레이 UI 포함)를 저장한다. 밀린 칸은 직전 프레임으로 채운다.
    private static async UniTask RecordAsync(string name, float seconds, Func<CancellationToken, UniTask> action, CancellationToken token)
    {
        var runner = Object.FindFirstObjectByType<GameManager>();
        string dir = Path.Combine(OutRoot, name);
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        Directory.CreateDirectory(dir);

        int w = Screen.width, h = Screen.height;
        int sw = Mathf.RoundToInt(w * CaptureScale) & ~1, sh = Mathf.RoundToInt(h * CaptureScale) & ~1;
        var full = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        var small = new RenderTexture(sw, sh, 0, RenderTextureFormat.ARGB32);
        float step = 1f / Fps;
        int last = Mathf.CeilToInt(seconds * Fps);
        var written = new bool[last + 1];
        int pending = 0;
        try
        {
            float start = Time.realtimeSinceStartup;
            var act = action(token);
            int next = 0;
            while (next <= last)
            {
                await UniTask.WaitForEndOfFrame(runner, token);
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed + step * 0.5f >= next * step)
                {
                    int index = next;
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                    Graphics.Blit(full, small);
                    pending++;
                    _ = AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, req =>
                    {
                        pending--;
                        if (req.hasError) return;
                        File.WriteAllBytes(Path.Combine(dir, $"f_{index:D3}.rgba"), req.GetData<byte>().ToArray());
                        written[index] = true;
                    });
                    next = Mathf.Max(next + 1, Mathf.FloorToInt(elapsed / step + 0.5f) + 1);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            while (pending > 0) await UniTask.Yield(token);
            await act;
        }
        finally
        {
            full.Release(); Object.Destroy(full);
            small.Release(); Object.Destroy(small);
        }

        int filled = 0;
        string previous = null;
        for (int i = 0; i <= last; i++)
        {
            string path = Path.Combine(dir, $"f_{i:D3}.rgba");
            if (written[i]) previous = path;
            else if (previous != null) { File.Copy(previous, path, true); filled++; }
        }
        File.WriteAllText(Path.Combine(dir, "info.txt"), $"{sw}x{sh} rgba {Fps}fps frames={last + 1} filled={filled}");
        Debug.Log($"[TermClip] 녹화 완료 {name}: {last + 1}프레임 {sw}x{sh} (채운 칸 {filled}) → {Path.GetFullPath(dir)}");
    }
}
