using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Info")]
    public int        characterId;
    public int        unitType;
    public string     unitName;
    public Tier       unitTier;
    public UnitTribe  unitTribe;
    public Sprite     icon;
    public GameObject prefab;
    [TextArea] public string description;

    [Header("Sound")]
    public string attackSoundAddress;

    [Header("Attack")]
    public float atk;           // 일반 공격력
    public float attackSpeed;   // 공격 속도 (초)
    public float skillAtk;      // 스킬 공격력
    public float skillCooldown; // 스킬 쿨다운 (초)

    [Header("Skill")]
    public string skillName;    // 스킬 이름
    public float foodPerTick = 10f; // 스킬 발동 시 식량 생산량

    [Header("Population")]
    public int populationCost = 1;

    /// <summary>
    /// 구글 시트에서 가져온 캐릭터 성장 데이터를 SO 인스턴스에 적용합니다.
    /// (주의: 런타임에 에셋 자체를 수정하지 않도록 인스턴스화된 객체에 사용하는 것이 좋습니다)
    /// </summary>
    public void ApplySheetData(GameDataManager.CharacterSheetRow row)
    {
        if (row == null) return;

        unitName      = row.Name;
        atk           = row.Atk;
        attackSpeed   = row.AttackSpeed;
        skillAtk      = row.SkillAtk;
        skillCooldown = row.SkillCooldown;
        skillName     = row.SkillName;
        description   = row.SkillDescription;
    }
}
