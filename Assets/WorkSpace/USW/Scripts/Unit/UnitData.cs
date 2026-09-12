using UnityEngine;
using VContainer;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject, ILoadableAsset
{
    /// <summary>등급별 디버프 FK/발동 설정.</summary>
    public PerTierDebuffBinding DebuffBindings = new PerTierDebuffBinding();
    [Header("Info")]
    [TierTabGroup("등급")] public PerTierInt characterId = new PerTierInt();
    [TierTabGroup("등급")] public PerTierInt unitType = new PerTierInt();
    public string unitName;
    public UnitTribe unitTribe;

    [Header("Addressables")]
    public string iconAddress;
    public string prefabAddress;

    [HideInInspector] public Sprite icon;
    [HideInInspector] public GameObject prefab;
    [TextArea] public string description;

    public bool IsLoaded => icon != null || prefab != null;

    [TierTabGroup("등급")] public PerTierFloat atk = new PerTierFloat();           // 일반 공격력
    [TierTabGroup("등급")] public PerTierFloat attackSpeed = new PerTierFloat();   // 공격 속도 (초)
    [TierTabGroup("등급")] public PerTierFloat skillCooldown = new PerTierFloat(); // 스킬 쿨다운 (초)

    [Header("Attack")]
    [Tooltip("기본 공격 발동 방식을 데이터로 정의(SkillData 재사용). 비워두면 기존 LaunchProjectile(GetAttackDamage()) 1발 고정 방식을 사용한다.")]
    public SkillData basicAttackData;

    [Header("Skill")]
    public SkillData skillData;

    [TierTabGroup("등급")] public PerTierFloat foodProduction = new PerTierFloat(); // 초당 식량 생산량

    [TierTabGroup("등급")] public PerTierInt populationCost = new PerTierInt { normal = 1, rare = 1, epic = 1, legend = 1 };

    [TierTabGroup("등급")] public PerTierInt maxDroneCount = new PerTierInt();

    [Header("패시브")]
    [Tooltip("이 유닛 자신에게 항상 적용되는 패시브. 파티 리더(족장)로 배치되면 파티의 모든 유닛에게도 적용된다.")]
    public PassiveData passive;



    /// <summary>
    /// 구글 시트에서 가져온 캐릭터 성장 데이터를 SO 인스턴스에 적용합니다.
    /// (주의: 런타임에 에셋 자체를 수정하지 않도록 인스턴스화된 객체에 사용하는 것이 좋습니다)
    /// </summary>
    public void ApplySheetData(Tier tier, GameDataManager.CharacterSheetRow row)
    {
        if (row == null) return;

        unitName = row.Name;
        atk.Set(tier, row.Atk);
        attackSpeed.Set(tier, row.AttackSpeed);
        foodProduction.Set(tier, row.FoodProduction);
        if (row.DebuffBinding.HasValue) DebuffBindings.Set(tier, row.DebuffBinding.Value);
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
