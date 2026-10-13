/// <summary>드론 배치 실험실(FxLab_DroneLayout)의 배치 방식. 실험실 전용.</summary>
public enum DroneLayoutKind
{
    /// <summary>현재 게임 슬롯(BetanFormationSettings) 그대로.</summary>
    Current = 0,
    /// <summary>현재 슬롯을 안쪽으로 당기고 드론을 작게.</summary>
    Compact = 1,
    /// <summary>머리 위 가로 일렬.</summary>
    HeadRow = 2,
    /// <summary>머리 위 아치(후광).</summary>
    HeadArc = 3,
    /// <summary>칸 아래쪽 발밑 줄.</summary>
    FootRow = 4,
    /// <summary>유닛 등 뒤에 부채꼴로 숨김.</summary>
    BehindFan = 5,
    /// <summary>자기 칸 네 모서리.</summary>
    CellCorners = 6,
    /// <summary>등 쪽(바깥쪽) 세로 열.</summary>
    BackColumn = 7,
    /// <summary>유닛 주위 납작한 궤도 회전, 위쪽 반은 뒤로.</summary>
    Orbit = 8,
    /// <summary>유닛 본체를 줄이고 현재 슬롯 축소.</summary>
    ShrinkUnit = 9,
    /// <summary>대표 드론 1기 + 숫자 배지.</summary>
    SingleWithCount = 10,
    /// <summary>드론이 그리드를 떠나 그리드 위 하늘 격납 띠에 모임.</summary>
    SkyHangar = 11,
    /// <summary>유닛 어깨 위에 작게 탑승 — 자기 몸 범위 안.</summary>
    ShoulderRide = 12,
}
