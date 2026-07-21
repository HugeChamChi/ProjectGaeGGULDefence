using UnityEngine;

/// <summary>이름/설명을 갖는 스킬 데이터. 실제 발동 방식은 action에 위임한다.</summary>
[CreateAssetMenu(fileName = "SkillData", menuName = "Game/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public int skillId;
    public string skillName;
    [TextArea] public string description;

    [Header("발동 방식 (SelectableReference)")]
    [SerializeReference, SelectableReference]
    public ISkillAction action;
}
