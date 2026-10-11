using UnityEngine;

/// <summary>디시그망 사운드 비교 시안. 승인 원본에서 가공한 실험실 전용 클립.</summary>
[CreateAssetMenu(menuName = "USW/Fx Lab/Disigman Sound Variant")]
public sealed class DisigmanRailgunSoundVariant : ScriptableObject
{
    /// <summary>버튼 이름.</summary>
    public string DisplayName;
    /// <summary>시안의 청감 목표.</summary>
    [TextArea] public string Description;
    /// <summary>0.4초 충전음.</summary>
    public AudioClip Charge;
    /// <summary>스킬 전용 충전음. 비어 있으면 기존 공통 충전음을 사용한다.</summary>
    public AudioClip SkillCharge;
    /// <summary>일반 발사의 충격음과 짧은 잔향.</summary>
    public AudioClip Basic;
    /// <summary>스킬 발사의 충격음과 잔향.</summary>
    public AudioClip Skill;
}
