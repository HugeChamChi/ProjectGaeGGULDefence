using UnityEngine;
using VContainer;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject, ILoadableAsset
{
    [Header("Info")]
    public int characterId;
    public int unitType;
    public string unitName;
    public Tier unitTier;
    public UnitTribe unitTribe;
    
    [Header("Addressables")]
    public string iconAddress;
    public string prefabAddress;

    [HideInInspector] public Sprite icon;
    [HideInInspector] public GameObject prefab;
    [TextArea] public string description;

    public bool IsLoaded => icon != null || prefab != null;

    [Header("Sound")]
    public string attackSoundAddress;

    [Header("Attack")]
    public float atk;           // 일반 공격력
    public float attackSpeed;   // 공격 속도 (초)
    public float skillCooldown; // 스킬 쿨다운 (초)

    [Tooltip("기본 공격 발동 방식을 데이터로 정의(SkillData 재사용). 비워두면 기존 LaunchProjectile(GetAttackDamage()) 1발 고정 방식을 사용한다.")]
    public SkillData basicAttackData;

    [Header("Skill")]
    public string skillName;    // 스킬 이름 (skillData 없을 때 UI 표시용 폴백)
    public SkillData skillData;

    [Header("Economy")]
    public float foodProduction; // 초당 식량 생산량


    [Header("Population")]
    public int populationCost = 1;

    [Header("Drone")]
    public int maxDroneCount;

    [Header("패시브")]
    [Tooltip("이 유닛 자신에게 항상 적용되는 패시브. 파티 리더(족장)로 배치되면 파티의 모든 유닛에게도 적용된다.")]
    public PassiveData passive;



    /// <summary>
    /// 구글 시트에서 가져온 캐릭터 성장 데이터를 SO 인스턴스에 적용합니다.
    /// (주의: 런타임에 에셋 자체를 수정하지 않도록 인스턴스화된 객체에 사용하는 것이 좋습니다)
    /// </summary>
    public void ApplySheetData(GameDataManager.CharacterSheetRow row)
    {
        if (row == null) return;

        unitName = row.Name;
        atk = row.Atk;
        attackSpeed = row.AttackSpeed;
        foodProduction = row.FoodProduction;
    }

    public async Cysharp.Threading.Tasks.UniTask LoadAssetsAsync()
    {
        if (!string.IsNullOrEmpty(iconAddress) && icon == null)
            icon = await RM.LoadAsync<Sprite>(iconAddress);
            
        if (!string.IsNullOrEmpty(prefabAddress) && prefab == null)
            prefab = await RM.LoadAsync<GameObject>(prefabAddress);
    }

    public void UnloadAssets()
    {
        if (icon != null)
        {
            RM.Unload(icon);
            icon = null;
        }
        if (prefab != null)
        {
            RM.Unload(prefab);
            prefab = null;
        }
    }
}
