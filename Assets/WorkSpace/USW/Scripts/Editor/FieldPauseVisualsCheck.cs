using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택 화면 정지 확인 (IngameScene Play 중 메뉴 실행): 시작 → 보스 등장 대기 → 유닛 소환 → 토템 보상 열기/취소 → 레벨업 열기.
/// 검사: 열리자마자 새 공격 보류 + 시간은 흐름(투사체 도착 유예) → 유예 뒤 timeScale 0 + 화면 살리기 + 날아가는 투사체 0개,
///       정지 중 유닛 대기 모션이 움직임, 보스 Spine 대기 모션 실제 시간, 닫으면 전부 복구.
/// 결과는 콘솔과 Temp/FieldPauseCheck/result.txt.
/// </summary>
public static class FieldPauseVisualsCheck
{
    private const string OutDir = "Temp/FieldPauseCheck";
    private const float BossWaitSeconds = 45f;
    private const int SpawnCount = 6;
    private const double FreezeWait = 1.2; // 토템 유예 0.4 / 레벨업 유예 0.4 + 슬로모션 0.35 보다 넉넉히

    private static readonly StringBuilder Log = new();
    private static int _pass, _fail, _step;
    private static double _t0;
    private static float _poseA;

    [MenuItem("Tools/USW/Checks/Field Pause Visuals (Play)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[FieldPauseCheck] IngameScene Play 중에 실행"); return; }
        System.IO.Directory.CreateDirectory(OutDir);
        Log.Clear(); _pass = _fail = 0; _step = 0; _t0 = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Object.FindFirstObjectByType<GameManager>()?.OnStartButtonPressed();
        Note("시작 버튼 호출, 보스 대기");
    }

    [MenuItem("Tools/USW/Checks/Field Pause Visuals - Dump Units")]
    public static void DumpUnits()
    {
        foreach (var unit in Units())
        {
            var visuals = unit.GetComponentsInChildren<IPauseIdleVisual>(true);
            Debug.Log($"[FieldPauseCheck] {unit.name}: 기절={unit.IsStunned}, 외형 {visuals.Length}개 ({string.Join(", ", visuals.Select(v => v.GetType().Name))})");
        }
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - _t0;

    private static void Next() { _step++; _t0 = EditorApplication.timeSinceStartup; }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Play 종료로 중단"); return; }
        try { Step(); }
        catch (System.Exception error) { Finish("예외로 중단: " + error.Message); Debug.LogException(error); }
    }

    private static void Step()
    {
        var service = InGameLifetimeScope.GlobalResolver?.Resolve(typeof(FieldPauseVisuals)) as FieldPauseVisuals;
        var reward = Object.FindFirstObjectByType<TotemRewardUI>(FindObjectsInactive.Include);
        switch (_step)
        {
            case 0: // 보스 등장 → 유닛 소환
                if (Object.FindFirstObjectByType<BossBase>() == null && Elapsed < BossWaitSeconds) return;
                var spawner = Object.FindFirstObjectByType<UnitSpawner>();
                for (int i = 0; i < SpawnCount && spawner != null; i++) spawner.OnSpawnButtonPressed();
                Next();
                break;
            case 1: // 유닛이 공격을 시작할 시간 → 토템 보상 열기
                if (Elapsed < 3.0) return;
                Note($"보스 {(Object.FindFirstObjectByType<BossBase>() != null ? "있음" : "없음")}, 유닛 {Units().Count}, 날아가는 투사체 {InFlight()}개");
                if (reward == null) { Finish("TotemRewardUI 없음"); return; }
                reward.Show(() => Note("토템 보상 콜백"));
                Next();
                break;
            case 2: // 유예 중
                if (Elapsed < 0.05) return;
                Check(Time.timeScale > 0f, $"토템 보상 유예: 시간은 흐름 (timeScale {Time.timeScale:0.##}, 투사체 {InFlight()}개 비행 중)");
                Check(service != null && service.AttacksHeld, "토템 보상 유예: 새 공격·보스 패턴 보류");
                Next();
                break;
            case 3: // 정지 후
                if (Elapsed < FreezeWait) return;
                CheckFrozen("토템 보상", service);
                _poseA = UnitPose();
                Next();
                break;
            case 4:
                if (Elapsed < 0.6) return;
                CheckPoseMoved("토템 보상");
                reward.CancelPendingRewards();
                Next();
                break;
            case 5: // 닫힘 → 복구
                if (Elapsed < 0.3) return;
                Check(service != null && !service.IsActive && !service.AttacksHeld, "토템 보상 닫힘: 보류·화면 살리기 해제");
                CheckBoss(false);
                Check(Time.timeScale > 0f, "토템 보상 닫힘: 시간 재개");
                if (Object.FindFirstObjectByType<LevelUpRevealSequence>(FindObjectsInactive.Include) == null) { Finish("LevelUpRevealSequence 없음"); return; }
                foreach (var u in Units()) u.ResumeLoops(); // CancelPendingRewards는 전투를 재개하지 않으므로 레벨업 전 공격 재개
                Next();
                break;
            case 6: // 다시 싸우게 둔 뒤 레벨업
                if (Elapsed < 2.0) return;
                Note($"레벨업 직전 날아가는 투사체 {InFlight()}개");
                Object.FindFirstObjectByType<LevelUpRevealSequence>(FindObjectsInactive.Include).DebugTriggerLevelUp();
                Next();
                break;
            case 7:
                if (Elapsed < 0.05) return;
                Check(service != null && service.AttacksHeld, $"레벨업 유예: 새 공격 보류 (timeScale {Time.timeScale:0.##})");
                Next();
                break;
            case 8:
                if (Elapsed < FreezeWait) return;
                CheckFrozen("레벨업", service);
                _poseA = UnitPose();
                Next();
                break;
            case 9:
                if (Elapsed < 0.6) return;
                CheckPoseMoved("레벨업");
                Finish("완료 (레벨업 화면은 열어 둠)");
                break;
        }
    }

    private static void CheckFrozen(string label, FieldPauseVisuals service)
    {
        Check(Time.timeScale == 0f, $"{label}: 유예 뒤 timeScale 0");
        Check(service != null && service.IsActive, $"{label}: 화면 살리기 켜짐");
        int inFlight = InFlight();
        Check(inFlight == 0, $"{label}: 멈췄을 때 날아가는 투사체 {inFlight}개 (0이어야 함)");
        CheckBoss(true);
        foreach (var unit in Units())
        {
            var visuals = unit.GetComponentsInChildren<IPauseIdleVisual>(true);
            Note($"{label}: {unit.name} 기절={unit.IsStunned}, 외형 {string.Join(", ", visuals.Select(v => v.GetType().Name))}");
        }
        Note($"{label}: 실제 시간으로 돈 파티클 {Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.main.useUnscaledTime)}개");
    }

    private static void CheckPoseMoved(string label)
    {
        float delta = Mathf.Abs(UnitPose() - _poseA);
        Check(Time.timeScale == 0f && delta > 1e-4f, $"{label}: 정지 중 유닛 대기 모션이 움직임 (자세 변화 {delta:0.#####})");
    }

    private static int InFlight() => Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

    private static List<UnitBase> Units()
    {
        var grid = Object.FindFirstObjectByType<GridManager>();
        return grid == null ? new List<UnitBase>() : grid.GetOccupiedCells().Select(c => c.OccupyingUnit).Where(u => u != null).ToList();
    }

    // 유닛들 하위 트랜스폼 크기·위치 합 — 숨쉬기/대기 모션이 돌면 시간에 따라 바뀐다.
    private static float UnitPose()
    {
        float sum = 0f;
        foreach (var unit in Units())
            foreach (var t in unit.GetComponentsInChildren<Transform>())
                sum += t.localScale.x + t.localScale.y + t.localPosition.x + t.localPosition.y;
        return sum;
    }

    private static void CheckBoss(bool paused)
    {
        var boss = Object.FindFirstObjectByType<BossBase>();
        var spine = boss != null ? boss.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() : null;
        if (spine == null) { Note("보스 Spine 없음 — 보스 검사 생략"); return; }
        string anim = spine.AnimationState?.GetTrack(0)?.Animation?.Name ?? "-";
        Note($"보스 Spine 현재 '{anim}', UnscaledTime={spine.UnscaledTime}");
        if (!paused) Check(!spine.UnscaledTime, "보스 Spine 원래 시간 복구");
    }

    private static void Check(bool ok, string label)
    {
        if (ok) _pass++; else _fail++;
        Note((ok ? "PASS " : "FAIL ") + label);
    }

    private static void Note(string line)
    {
        Log.AppendLine(line);
        Debug.Log("[FieldPauseCheck] " + line);
    }

    private static void Finish(string reason)
    {
        EditorApplication.update -= Tick;
        Note($"{reason} — PASS {_pass} / FAIL {_fail}");
        System.IO.File.WriteAllText($"{OutDir}/result.txt", Log.ToString());
    }
}
