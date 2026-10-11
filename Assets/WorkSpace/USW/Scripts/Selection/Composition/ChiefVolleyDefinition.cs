using System;

/// <summary>족장 추가 발사 설정 설정. 동작은 해당 게임 시스템이 소유한다.</summary>
[Serializable]
public sealed class ChiefVolleyDefinition : SelectionEffectDefinition
{
    /// <summary>발사 사이 지연(초).</summary>
    public float ShotDelaySeconds;
    /// <summary>발사 피해 비율.</summary>
    public float DamageRatio;
    /// <summary>발사 횟수.</summary>
    public int ShotCount;
}
