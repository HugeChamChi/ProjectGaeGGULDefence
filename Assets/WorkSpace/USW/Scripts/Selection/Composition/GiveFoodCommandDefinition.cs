using System;

/// <summary>즉시 식량 지급 명령 설정.</summary>
[Serializable]
public sealed class GiveFoodCommandDefinition : SelectionCommandDefinition
{
    /// <summary>지급량.</summary>
    public float Amount;
}
