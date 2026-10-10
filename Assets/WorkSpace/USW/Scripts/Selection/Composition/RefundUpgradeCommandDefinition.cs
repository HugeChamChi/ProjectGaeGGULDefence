using System;

/// <summary>기존 강화 지출 환급 명령 설정.</summary>
[Serializable]
public sealed class RefundUpgradeCommandDefinition : SelectionCommandDefinition
{
    /// <summary>환급 비율(0~1).</summary>
    public float Ratio;
}
