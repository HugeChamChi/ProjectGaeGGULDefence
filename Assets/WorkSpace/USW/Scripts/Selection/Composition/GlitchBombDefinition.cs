using System;

/// <summary>G10 소각 신관. 글리치를 만든 유닛의 공격력으로 자폭 드론 한 기를 발사한다.</summary>
[Serializable]
public sealed class GlitchBombDefinition : SelectionEffectDefinition
{
    /// <summary>공유 자폭 드론 프리팹. 베탕 배치 여부와 독립적이다.</summary>
    public SelfDestructDrone Prefab;
}
