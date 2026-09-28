using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 무한모드 튜닝용 플레이 기록. 라운드별 처치 시간·레벨·패널티와 런 종료 요약을 콘솔과 CSV에 남긴다.
/// 에디터와 개발 빌드에서만 기록하며, 게임 진행에는 관여하지 않는다.
/// CSV: Application.persistentDataPath/endless-runs.csv
/// </summary>
public sealed class EndlessRunLog
{
    private const string LogTag = "[EndlessLog]";
    private const string CsvFileName = "endless-runs.csv";
    private const string CsvHeader = "run_id,event,round,boss,max_hp,defense,exp_reward,fight_sec,level,atk_mult,freq_mult,game_sec,real_sec,result";

    private string _runLabel;
    private string _modeKey;
    private float _runGameStart;
    private float _runRealStart;
    private float _fightStart;
    private int _lastRound;
    private int _clearedRounds;
    private RuntimeBossStats _currentBoss;
    private bool _active;

    /// <summary>마지막으로 시작한 런의 고유 이름 (시작 시각 + 서비스 런 번호). CSV의 run_id 열.</summary>
    public string RunLabel => _runLabel;

    /// <summary>에디터·개발 빌드에서만 true.</summary>
    public static bool Enabled => Application.isEditor || Debug.isDebugBuild;

    /// <summary>새 무한 런의 기록을 시작한다. 유한 진행(모드 없음)은 기록하지 않는다.</summary>
    public void BeginRun(long runId, string modeKey)
    {
        if (!Enabled || string.IsNullOrEmpty(modeKey)) { _active = false; return; }
        _runLabel = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + runId.ToString(CultureInfo.InvariantCulture);
        _modeKey = modeKey;
        _runGameStart = Time.time;
        _runRealStart = Time.realtimeSinceStartup;
        _lastRound = 0;
        _clearedRounds = 0;
        _active = true;
        Debug.Log($"{LogTag} run {_runLabel} start mode={_modeKey}");
    }

    /// <summary>보스가 실제로 등장해 전투가 시작된 시점.</summary>
    public void BossSpawned(int round, RuntimeBossStats stats)
    {
        if (!_active) return;
        _lastRound = round;
        _currentBoss = stats;
        _fightStart = Time.time;
    }

    /// <summary>보스 처치 시점. 전투 시간은 게임 시간(일시정지 제외)으로 잰다.</summary>
    public void BossDefeated(int round, int level, double attackMultiplier, double frequencyMultiplier)
    {
        if (!_active || round != _lastRound) return;
        _clearedRounds = round;
        float fight = Time.time - _fightStart;
        Write("clear", round, fight, level, attackMultiplier, frequencyMultiplier, "");
        Debug.Log($"{LogTag} R{round} clear {BossName()} hp={_currentBoss.MaxHp:N0} def={_currentBoss.Defense:0.#} " +
                  $"fight={fight:0.0}s lv={level} atk×{attackMultiplier:0.###} spd×{frequencyMultiplier:0.###}");
    }

    /// <summary>런이 끝난 시점(패배·중단·오류). 한 런에 한 번만 기록한다.</summary>
    public void EndRun(string result, int level, double attackMultiplier, double frequencyMultiplier)
    {
        if (!_active) return;
        _active = false;
        float fight = _lastRound > _clearedRounds ? Time.time - _fightStart : 0f;
        Write("end", _lastRound, fight, level, attackMultiplier, frequencyMultiplier, result);
        float gameMin = (Time.time - _runGameStart) / 60f;
        float realMin = (Time.realtimeSinceStartup - _runRealStart) / 60f;
        Debug.Log($"{LogTag} run {_runLabel} end result={result} reachedRound={_lastRound} cleared={_clearedRounds} lv={level} " +
                  $"game={gameMin:0.0}min real={realMin:0.0}min");
    }

    private string BossName() => _currentBoss.Source != null ? _currentBoss.Source.name : "-";

    private void Write(string evt, int round, float fight, int level, double atk, double freq, string result)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, CsvFileName);
            bool header = !File.Exists(path);
            var line = new StringBuilder();
            if (header) line.AppendLine(CsvHeader);
            line.Append(string.Join(",",
                _runLabel, evt, round.ToString(CultureInfo.InvariantCulture), BossName(),
                _currentBoss.MaxHp.ToString(CultureInfo.InvariantCulture), _currentBoss.Defense.ToString(CultureInfo.InvariantCulture),
                _currentBoss.ExpReward.ToString(CultureInfo.InvariantCulture), fight.ToString("0.00", CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture), atk.ToString("0.####", CultureInfo.InvariantCulture),
                freq.ToString("0.####", CultureInfo.InvariantCulture),
                (Time.time - _runGameStart).ToString("0.0", CultureInfo.InvariantCulture),
                (Time.realtimeSinceStartup - _runRealStart).ToString("0.0", CultureInfo.InvariantCulture), result));
            File.AppendAllText(path, line.AppendLine().ToString());
        }
        catch (Exception error)
        {
            Debug.LogWarning($"{LogTag} CSV 기록 실패: {error.Message}");
        }
    }
}
