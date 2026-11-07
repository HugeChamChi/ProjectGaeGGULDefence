using System;

/// <summary>C04 과부하 합성. 통상 레전더리 결과를 제외한 합성의 비용과 추가 승급 확률.</summary>
[Serializable]
public sealed class OverloadMergeDefinition : SelectionEffectDefinition
{
    /// <summary>유효 합성 한 번의 시간 비용. 확률 실패에도 지불한다.</summary>
    public float CostSeconds = 0.25f;
    /// <summary>통상 결과에서 한 등급 더 올라갈 확률.</summary>
    public float UpgradeChance = 0.2f;
}
