using System;

/// <summary>등급과 무관하게 항상 같은 값을 반환한다.</summary>
[Serializable]
[DisplayName("고정값")]
public class ConstantFloat : IScaledFloat
{
    public float value;

    public float Get(Tier tier) => value;
}
