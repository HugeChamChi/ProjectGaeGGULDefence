using System;

/// <summary>C10 복구 패킷. 실제 디시그망 회복 한 번당 원래 보스에 스택을 추가한다.</summary>
[Serializable]
public sealed class RecoveryHackingDefinition : SelectionEffectDefinition
{
    /// <summary>회복 횟수당 스택. 2026-11-03 사용자 확정값은 1.</summary>
    public int StacksPerRecovery = 1;
    /// <summary>델탕 유무와 무관하게 사용할 공유 해킹 설정.</summary>
    public DroneHackingData HackingData;
}
