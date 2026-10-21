using System;
using UnityEngine;

/// <summary>결과 화면에 표시할 한 판의 기록.</summary>
[Serializable]
public class ResultScreenData
{
    /// <summary>이번 판 도달 라운드.</summary>
    public int Round;

    /// <summary>이번 판 이전의 최고 라운드.</summary>
    public int BestRound;

    public int BossKills;
    public float SurvivalSeconds;

    /// <summary>마지막 미처치 보스의 HP 감소율(0~1). 음수는 기록 없음.</summary>
    public float LastBossDamageRatio = -1f;

    /// <summary>마지막 보스 HP 진행도 연출 후보.</summary>
    public ResultBossProgressStyle BossProgressStyle;

    /// <summary>이번 판에 고른 선택지를 선택 순서대로 전달한다.</summary>
    public ResultBuildChoice[] BuildChoices;

    public ResultReward[] Rewards;

    /// <summary>Actual account EXP retained, including the maximum-level cap.</summary>
    public int AccountExperience;
    /// <summary>True only when the account settlement produced a durable receipt.</summary>
    public bool HasAccountExperience;

    /// <summary>패배 순간 필드 보스 그림. 있으면 결과 화면이 고정 초상화 대신 이 보스를 이어 보여 준다.</summary>
    [NonSerialized] public ResultBossSnapshot BossSnapshot;

    public bool IsNewRecord => Round > BestRound;
}
