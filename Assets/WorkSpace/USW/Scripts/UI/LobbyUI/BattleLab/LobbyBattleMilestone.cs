using System;

/// <summary>최고 라운드 도달 보상 한 칸(시안 값). 실제 수치는 미정이다.</summary>
[Serializable]
public class LobbyBattleMilestone
{
    /// <summary>도달해야 하는 최고 라운드.</summary>
    public int Round;
    /// <summary>보상 종류.</summary>
    public LobbyBattleRewardKind Kind;
    /// <summary>보상 수량.</summary>
    public int Amount;

    /// <summary>시안 보상 한 칸을 만든다.</summary>
    public LobbyBattleMilestone(int round, LobbyBattleRewardKind kind, int amount)
    {
        Round = round;
        Kind = kind;
        Amount = amount;
    }
}
