using UnityEngine;

/// <summary>델탕 충전과 감망 기폭이 공유하는 저작 데이터. 전투와 정보창이 같은 값을 읽는다.</summary>
[CreateAssetMenu(menuName = "Game/Drone Hacking Data")]
public sealed class DroneHackingData : ScriptableObject
{
    /// <summary>보스별 공유 스택 상한.</summary>
    [Min(1)] public int Capacity = 50;
    /// <summary>델탕 완충에 필요한 일반 공격 횟수.</summary>
    public PerTierInt AttacksToCharge = new PerTierInt();
    /// <summary>델탕 해킹 스킬당 생산량.</summary>
    public PerTierInt StacksProduced = new PerTierInt();
    /// <summary>감망 스킬당 최대 소비량. 부족하면 남은 만큼만 소비한다.</summary>
    public PerTierInt MaxStacksConsumed = new PerTierInt();
    /// <summary>스택0에서도 적용하는 감망 기본 공격력 계수.</summary>
    public PerTierFloat BaseCoefficient = new PerTierFloat();
    /// <summary>실제 소비한 스택당 추가 공격력 계수.</summary>
    public PerTierFloat CoefficientPerStack = new PerTierFloat();
    /// <summary>해킹 효과가 대상에 도착하는 전투 시간.</summary>
    [Min(0)] public float HackFlightSeconds = .25f;
    /// <summary>순간이동 후 피해까지의 전투 시간.</summary>
    [Min(0)] public float ContactSeconds = .35f;
    /// <summary>기폭과 복귀 시각 효과 유지 시간.</summary>
    [Min(0)] public float RecoverySeconds = .35f;
    /// <summary>순간이동 드론의 대상 중심 상대 위치.</summary>
    public Vector3 TeleportOffset = new Vector3(-1.4f, .35f, -.2f);
    /// <summary>실제 드론 대신 재생하는 시각 사본의 순간이동 잔상 색.</summary>
    public Color HackColor = new Color(.38f, .86f, 1f);
    /// <summary>보스 표식과 신호선의 공유 머티리얼.</summary>
    public Material LineMaterial;
    /// <summary>Teleport laboratory shader beam, shared by hacking casts.</summary>
    public Material BeamMaterial;
    /// <summary>World-space width of the skill beam.</summary>
    [Min(.01f)] public float CastBeamWidth = .24f;
    /// <summary>FXLab과 같은 부드러운 해킹 후광 소재.</summary>
    public Material MarkGlowMaterial;
    /// <summary>표식 부착과 기폭 파동 소재.</summary>
    public Material MarkRingMaterial;
    /// <summary>표식 하나로 표현하는 스택 수. 50스택에서 10개.</summary>
    [Min(1)] public int StacksPerMark = 5;
    /// <summary>보스 몸에 맺히는 후광 지름(월드 단위).</summary>
    [Min(.01f)] public float MarkGlowSize = .75f;
    /// <summary>후광 중심의 흰 코어 지름(월드 단위).</summary>
    [Min(.01f)] public float MarkCoreSize = .22f;
}
