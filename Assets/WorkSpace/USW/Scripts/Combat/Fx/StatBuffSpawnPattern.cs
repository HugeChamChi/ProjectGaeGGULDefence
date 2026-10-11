/// <summary>
/// StatBuffFx 조각이 생겨나는 자리.
/// </summary>
public enum StatBuffSpawnPattern
{
    /// <summary>유닛 주변 여기저기 (좌우 번갈아).</summary>
    Scatter = 0,
    /// <summary>발밑 가운데에서 좁게 솟는다.</summary>
    Column = 1,
    /// <summary>발밑 타원 둘레에서 생긴다.</summary>
    Ring = 2,
}
