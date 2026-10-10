/// <summary>선택지 추첨 난수 공급. 기존 Unity 범위 계약을 보존한다.</summary>
public interface ISelectionRandom
{
    /// <summary>최솟값 포함, 최댓값 제외 정수 난수.</summary>
    int Range(int minimum, int maximumExclusive);
    /// <summary>양 끝을 포함하는 실수 난수.</summary>
    float Range(float minimum, float maximum);
}
