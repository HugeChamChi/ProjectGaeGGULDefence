using UnityEngine;

/// <summary>보스 피격 반응 비교 씬의 시각 전용 설정.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Boss Hit Reaction Settings")]
public sealed class BossHitReactionSettings : ScriptableObject
{
    /// <summary>충격 자세에 도달하는 시간.</summary>
    public float AttackSeconds = .075f;
    /// <summary>충격을 버티는 시간.</summary>
    public float HoldSeconds = .09f;
    /// <summary>원래 자세로 회복하는 시간.</summary>
    public float RecoverSeconds = .42f;
    /// <summary>몸통 세로 축 압축.</summary>
    public float BodyCompression = .15f;
    /// <summary>몸통 가로 축 확장.</summary>
    public float BodyExpansion = .035f;
    /// <summary>몸통의 충격 방향 기울기.</summary>
    public float BodyTilt = 3f;
    /// <summary>머리의 반동 각도.</summary>
    public float HeadTilt = 9f;
    /// <summary>양팔이 몸을 지탱하는 각도.</summary>
    public float ArmBrace = 15f;
    /// <summary>수동 큰 공격 버튼의 최소 간격.</summary>
    public float ManualShotInterval = 1.1f;

    [Header("족장 레이저 적중 후 버팀")]
    /// <summary>족장 적중 순간 최대 압박 자세까지 걸리는 시간.</summary>
    public float ChiefAttackSeconds = .1f;
    /// <summary>최대 압박에서 지속 버팀 자세로 안정되는 시간.</summary>
    public float ChiefSettleSeconds = .22f;
    /// <summary>빔을 맞고 있는 동안 유지하는 압박 강도.</summary>
    public float ChiefSustainIntensity = .85f;
    /// <summary>빔 연출이 없는 경우의 적중 후 버팀 시간.</summary>
    public float ChiefFallbackHoldSeconds = .75f;
    /// <summary>빔이 끝난 뒤 원래 자세로 돌아오는 시간.</summary>
    public float ChiefRecoverSeconds = .75f;
    /// <summary>족장 빔의 몸통 압축.</summary>
    public float ChiefBodyCompression = .2f;
    /// <summary>족장 빔의 몸통 가로 확장.</summary>
    public float ChiefBodyExpansion = .045f;
    /// <summary>족장 빔을 받아내는 몸통 기울기.</summary>
    public float ChiefBodyTilt = 4f;
    /// <summary>족장 빔을 받아내는 머리 기울기.</summary>
    public float ChiefHeadTilt = 13f;
    /// <summary>족장 빔을 버티는 양팔 각도.</summary>
    public float ChiefArmBrace = 20f;
    /// <summary>빔을 버티는 동안 더하는 작은 몸통 떨림 각도.</summary>
    public float ChiefStrainDegrees = .65f;
    /// <summary>버팀 떨림의 초당 주기.</summary>
    public float ChiefStrainFrequency = 7f;
}
