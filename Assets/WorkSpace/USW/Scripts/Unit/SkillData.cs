using System.Collections.Generic;
using UnityEngine;

/// <summary>이름/설명을 갖는 스킬 데이터. 실제 발동 방식은 action에, 적중 결과는 hitEffects에 위임한다.</summary>
[CreateAssetMenu(fileName = "SkillData", menuName = "Game/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public int skillId;
    public string skillName;
    [TextArea] public string description;
    [Tooltip("발동 시 재생할 SFX 주소 (AudioManager.PlaySFX 인자). 비워두면 재생 안 함.")]
    public string attackSoundAddress;

    [Header("발동 방식 (SelectableReference)")]
    [SerializeReference, SelectableReference]
    public ISkillAction action;

    [Header("적중 결과 (SelectableReference, 여러 개 조합 가능)")]
    [SerializeReference, SelectableReference]
    public List<IEffect> hitEffects;

    [Header("추가 연출 (SelectableReference, 여러 개 조합 가능, 예: 카메라 흔들림)")]
    [SerializeReference, SelectableReference]
    public List<IAdditionalEffect> additionalEffects;

    [Header("시전 이펙트 (비워두면 사용 안 함)")]
    [SerializeReference, SelectableReference]
    public IEffectSpawner castEffect;
}
