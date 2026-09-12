using System;

/// <summary>TD1005의 선택적 상위 등급 데이터 교체. 서로 다른 스킬도 기존 실행 경로를 사용한다.</summary>
[Serializable]
public sealed class TotemTierUpgrade
{
    /// <summary>강화 전 유닛 정의.</summary>
    public UnitData OriginalData;
    /// <summary>강화 전 등급.</summary>
    public Tier OriginalTier;
    /// <summary>상위 등급에서 사용할 전체 정의. 같은 유닛 행동 클래스용 데이터를 지정한다.</summary>
    public UnitData UpgradedData;
}
