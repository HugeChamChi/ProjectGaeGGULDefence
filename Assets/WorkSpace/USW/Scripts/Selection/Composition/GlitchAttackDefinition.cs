using System;

/// <summary>오류 누적. 획득 후 실제 글리치 횟수에 비례하는 런 공격력 가산.</summary>
[Serializable]
public sealed class GlitchAttackDefinition : SelectionEffectDefinition
{
    /// <summary>1회 공격력 증가 비율. 0.025%.</summary>
    public float BonusPerGlitch = 0.00025f;
    /// <summary>적립 가능한 글리치 횟수.</summary>
    public int MaximumTriggers = 1000;
}
