using System;

/// <summary>자폭 선택지 BasicBombCount의 저작 설정. 런 상태를 포함하지 않는다.</summary>
[Serializable]
public sealed class BasicBombCountDefinition : SelectionEffectDefinition
{
    /// <summary>BasicSkillExtraBombs 설정.</summary>
    public int ExtraBombs = 1;
}
