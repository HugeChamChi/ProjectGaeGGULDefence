using UnityEngine;

[CreateAssetMenu(fileName = "SkillData_", menuName = "GaeGGUL/SkillData", order = 0)]
public class SkillData : ScriptableObject
{
    public int skillId;
    public string skillName;
    public string description;
    public float damage;
    public float cooldown;
}
