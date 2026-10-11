/// <summary>기존 Unity 전역 난수 소비 순서를 유지하는 선택지 어댑터.</summary>
public sealed class UnitySelectionRandom : ISelectionRandom
{
    /// <inheritdoc />
    public int Range(int minimum, int maximumExclusive) => UnityEngine.Random.Range(minimum, maximumExclusive);
    /// <inheritdoc />
    public float Range(float minimum, float maximum) => UnityEngine.Random.Range(minimum, maximum);
}
