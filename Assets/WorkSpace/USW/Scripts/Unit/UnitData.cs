using UnityEngine;
using VContainer;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject, ILoadableAsset
{
    /// <summary>이 유닛을 족장으로 선택했을 때 사용하는 독립 레벨업 풀.</summary>
    [Header("족장 레벨업 풀")]
    public LevelUpPoolData LevelUpPool;
    /// <summary>지정하면 족장 선택 시 유닛 대신 그리드 밖 액티브 스킬을 사용한다.</summary>
    public AlphanSkillData AlphanSkill;

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
    /// <summary>발밑 게이지 표시 정책. 패시브만 있는 유닛은 PassiveOnly로 지정할 수 있습니다.</summary>
    [Tooltip("Automatic: 사용 가능한 스킬과 양수 쿨다운이 있을 때 충전 표시. PassiveOnly: 회색 고정 바. 전투 동작은 변경하지 않음.")]
    public SkillGaugeMode SkillGaugeMode;

    [TierTabGroup("등급")] public PerTierFloat foodProduction = new PerTierFloat(); // 초당 식량 생산량

    [TierTabGroup("등급")] public PerTierInt maxDroneCount = new PerTierInt();

    [Header("패시브")]
    [Tooltip("이 유닛 자신에게 항상 적용되는 패시브. 파티 리더(족장)로 배치되면 파티의 모든 유닛에게도 적용된다.")]
    public PassiveData passive;

    [Header("배치 시각 보정")]
    [Tooltip("이 유닛만 스프라이트 여백 등으로 셀 기준 높이가 어긋날 때 보정하는 값. UnitFactory의 공통 spawnOffsetY에 더해진다.")]
    public float spawnOffsetY = 0f;



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

    /// <summary>description의 {0}~{4} 자리표시자에 현재 등급 수치를 채워 반환한다.
    /// {0}=공격력 {1}=공격속도 {2}=스킬쿨타임 {3}=식량생산량 {4}=최대드론수.
    /// 자리표시자가 없으면(또는 잘못 쓰였으면) description을 그대로 반환한다.</summary>
    public string GetFormattedDescription(Tier tier)
    {
        if (string.IsNullOrEmpty(description)) return description;
        try
        {
            return string.Format(description, atk.Get(tier), attackSpeed.Get(tier),
                skillCooldown.Get(tier), foodProduction.Get(tier), maxDroneCount.Get(tier));
        }
        catch (System.FormatException)
        {
            return description;
        }
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
