/// <summary>출격 실험실의 움직임 방식. 판정은 모두 칸 기준이고 보이는 몸만 움직인다.</summary>
public enum SortieLabMotionKind
{
    /// <summary>현재 게임 — 칸에 선 채로 쏜다.</summary>
    Static,
    /// <summary>공격마다 보스 쪽으로 짧게 튀어나갔다 돌아온다.</summary>
    Lunge,
    /// <summary>유닛은 진지에 남고 드론만 보스 앞까지 출격했다 돌아온다.</summary>
    DroneSortie,
    /// <summary>평소엔 제자리, 가끔 한 기가 보스 앞까지 도약해 내려찍고 돌아온다.</summary>
    SkillLeap,
    /// <summary>칸을 떠나지 않고 판 전체가 박자에 맞춰 아래에서 위로 출렁인다.</summary>
    Pulse,
    /// <summary>앞발 사격 + 베탕 드론 출격 + 가끔 도약.</summary>
    Mixed,
}
