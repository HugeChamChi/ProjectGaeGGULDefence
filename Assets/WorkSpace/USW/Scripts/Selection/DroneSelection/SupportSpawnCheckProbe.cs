#if UNITY_EDITOR
/// <summary>대기 보상의 셀 예약을 검사하는 에디터 전용 생성기.</summary>
public sealed class SupportSpawnCheckProbe : UnitSpawner
{
    /// <summary>검사에서 배치할 유닛.</summary>
    public UnitBase Reward;
    /// <summary>생성 실패를 모사한다.</summary>
    public bool Fail;
    /// <summary>성공적으로 예약한 셀 수.</summary>
    public int Placed;
    protected override bool TryPlaceSupportUnit(GridCell cell)
    {
        if (Fail || !cell.TryPlaceUnit(Reward)) return false;
        Placed++;
        return true;
    }
}
#endif
