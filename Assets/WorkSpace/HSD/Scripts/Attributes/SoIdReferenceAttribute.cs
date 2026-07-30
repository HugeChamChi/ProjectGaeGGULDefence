using System;

/// <summary>
/// int id 필드가 어떤 ScriptableObject 타입을 id로 참조하는지 표시한다.
/// SkillEditorWindow가 이 속성을 보고 해당 타입의 에셋 목록/생성 UI를 그려준다.
/// (예: MultiShotSkillAction.projectileDataId → ProjectileData)
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class SoIdReferenceAttribute : Attribute
{
    public readonly Type SoType;
    public SoIdReferenceAttribute(Type soType) => SoType = soType;
}
