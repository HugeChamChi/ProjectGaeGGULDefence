using UnityEngine;

/// <summary>등급/공격력/그리드가 없는 알팡 액티브 스킬 설정.</summary>
[CreateAssetMenu(fileName = "AlphanSkillData", menuName = "Game/Chieftain/Alphan Active Skill")]
public sealed class AlphanSkillData : ScriptableObject
{
    [Min(0.05f)] public float CooldownSeconds = 14f;
    [Min(0)] public float DamagePerDrone = 80f;
    public Sprite Icon;
    public string SoundAddress = "05.Leader_Skill_Effect";
}
