using UnityEngine;

/// <summary>등급/공격력/그리드가 없는 알팡 액티브 스킬 설정.</summary>
[CreateAssetMenu(fileName = "AlphanSkillData", menuName = "Game/Chieftain/Alphan Active Skill")]
public sealed class AlphanSkillData : ScriptableObject
{
    [Min(0.05f)] public float CooldownSeconds = 14f;
    [Tooltip("참가 드론의 현재 비치명 일반 사격 공격력 합에 곱할 집결 계수.")]
    [Min(0)] public float RallyAttackCoefficient = 1f;
    [Tooltip("긴급 교신이 소비할 공유 해킹 장부의 저작 설정. 감망/델탕과 같은 설정을 사용한다.")]
    public DroneHackingData Hacking;
    public Sprite Icon;
    public string SoundAddress = "05.Leader_Skill_Effect";
    [Tooltip("집결 사격 수렴 빔 연출 프리팹 (Prefab/Effect/ChiefConvergeBeam). 비어 있으면 연출 없이 피해만 준다")]
    public ChiefConvergeBeamFx BeamFx;
}
