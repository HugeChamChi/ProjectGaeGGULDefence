using System;

/// <summary>등급과 무관하게 항상 같은 값을 반환한다.</summary>
[Serializable]
[KoreanName("고정값")]
public class ConstantInt : IScaledInt
{
    public int value;

    public int Get(Tier tier) => value;
}
