using System;
using UnityEditor;
using UnityEngine;

/// <summary>드론 선택지 11종의 SO 초안을 만든다. 기존 카드/풀/파티를 덮어쓰지 않는다.</summary>
public static class DroneSelectionPresets
{
    /// <summary>카드의 드론 설정과 표시 정보를 명세 기본값으로 채운다. ID는 작성자가 지정한다.</summary>
    public static void Configure(LevelUpData card, DroneSelectionKind kind)
    {
        card.primaryEffect = card.secondaryEffect = LevelUpEffectType.None;
        card.specialEffect = LevelUpSpecialEffect.None;
        card.droneEffect = new DroneSelectionEffect { Kind = kind };
        var e = card.droneEffect;
        card.tier = Tier.Rare;
        switch (kind)
        {
            case DroneSelectionKind.DeltanBackdoor:
                card.chooseName="백도어";card.description="델탕 해킹의 상한 초과 스택 1개당 해당 델탕 마나 +{count} (완충까지).";
                card.tier=Tier.Epic;e.Count=1;break;
            case DroneSelectionKind.BetanHackingBomb:
                card.chooseName="해킹 자폭";card.description="베탕의 모든 자폭 드론을 해킹 드론으로 전환합니다. 폭발 피해 대신 보스 적중 시 해킹 스택 +{count}. 수리 키트도 발동합니다.";
                card.tier=Tier.Legend;e.Count=1;break;
            case DroneSelectionKind.HackingCarryover:
                card.chooseName="포맷 실패";card.description="보스 처치 시 남은 해킹 스택의 {value}%를 내림하여 다음 보스로 이월합니다.";
                e.Value=.33f;break;
            case DroneSelectionKind.DeltanZombiePc:
                card.chooseName="좀비 PC";card.description="델탕 소유 드론 일반 공격 시 {value}% 확률로 마나 +{count}.";
                e.Value=.15f;e.Count=2;break;
            case DroneSelectionKind.MergeSupport:
                card.chooseName="지원 요청 바람"; card.description="합성 시 10% 확률로 노말 랜덤 유닛을 생성합니다. 빈칸이 없으면 대기 후 자리가 생기면 생성합니다.";
                card.tier=Tier.Epic; e.Value=0.1f; break;
            case DroneSelectionKind.ExtraCombatDrone:
                card.chooseName="드론 생성 공정"; card.description="에픽 이상 베탕·감망·델탕의 전투 드론이 1기 추가됩니다. 추가 드론은 일반 공격과 알팡 액티브에 참여하며, 본체 스킬은 사용하지 않습니다.";
                card.tier=Tier.Legend; e.Count=1; break;
            case DroneSelectionKind.BetanPeriodicBomb:
                card.chooseName="만나서 반가워!"; card.description="{seconds}초마다 배치된 베탕 각각의 자폭 드론 {count}기를 추가 생성합니다.";
                e.Interval=10; e.Count=1; break;
            case DroneSelectionKind.BetanAttackSpeed:
                card.chooseName="다 쏴 제껴버려!!!!"; card.description="소유 유닛과 관계없이 모든 전투 드론의 공격 속도가 {value}% 증가합니다.";
                card.tier=Tier.Epic; e.Value=0.2f; break;
            case DroneSelectionKind.BetanFleetBomb:
                card.chooseName="슈슝 콰광!!!!!"; card.description="{seconds}초마다 필드에 있는 베탕의 전투 드론 수만큼 자폭 드론이 추가 생성됩니다 (최대 {count}기)";
                card.tier=Tier.Legend; e.Interval=20; e.Count=10; break;
            case DroneSelectionKind.GammanFrequency:
                card.chooseName="주파수 동조"; card.description="전장에 있는 전투 드론 1기당 감망의 해킹 추가 피해가 {value}% 증가합니다.";
                card.tier=Tier.Epic; e.Value=0.005f; e.MaxValue=0f; break;
            case DroneSelectionKind.GammanEmergency:
                card.chooseName="긴급 교신"; card.description="액티브 스킬 사용 시, 보스에게 쌓인 해킹 스택을 모두 소모해 기폭합니다."; break;
            case DroneSelectionKind.ZeltanAirFryer:
                card.chooseName="에어프라이기"; card.description="젤탕의 식량 생산량이 {value}% 증가하고, 생산한 식량을 {seconds}초마다 한 번에 지급합니다.";
                e.Interval=3; e.Value=0.2f; break;
            case DroneSelectionKind.DeltanDefenseReduction:
                card.chooseName="넹?"; card.description="델탕 해킹 스킬의 생산량이 1스택 증가합니다.";
                card.tier=Tier.Epic; e.Count=1; break;
            case DroneSelectionKind.DeltanDamageTaken:
                card.chooseName="넹 !"; card.description="델탕 해킹 시 {value}% 확률로 그 해킹의 스택 생산량이 {count}배가 됩니다.";
                card.tier=Tier.Epic; e.Value=0.05f; e.Count=2; break;
            case DroneSelectionKind.DeltanCooldown:
                card.chooseName="넹..."; card.description="델탕의 기본 해킹 충전량이 6에서 {count}로 증가하고, 해킹 스택 생산량이 {value}배가 됩니다.";
                e.Value=2f; e.Count=9; break;
            case DroneSelectionKind.AlphanCooldown:
                card.chooseName="위대한 지도자"; card.description="알팡 액티브 스킬 쿨타임이 20% 감소합니다.";
                e.Value=0.2f; break;
            case DroneSelectionKind.AlphanDoubleShot:
                card.chooseName="전술적 프로토콜"; card.description="액티브 스킬을 최대 {count}회까지 충전해 보관할 수 있습니다.";
                card.tier=Tier.Legend; e.Count=2; break;
            case DroneSelectionKind.ZeltanColdStorage:
                card.chooseName="냉동 보관"; card.description="젤탕의 자체 식량 생산량이 10% 증가합니다.";
                e.Value=0.1f; break;
            case DroneSelectionKind.ZeltanMaintenance:
                card.chooseName="정기 점검"; card.description="젤탕이 배치되어 있으면 웨이브 클리어 시 현재 보유 식량의 {value}%를 추가로 획득합니다.";
                e.Value=0.03f; break;
            case DroneSelectionKind.AlphanGoldenMonocle:
                card.chooseName="황금 모노클"; card.description="알팡의 액티브 스킬이 확정 치명타로 1.5배 피해를 줍니다.";
                card.tier=Tier.Legend; e.Value=1.5f; break;
            case DroneSelectionKind.BetanRepairKit:
                card.chooseName="수리 키트"; card.description="각 베탕은 자신의 자폭 드론이 폭발할 때마다 남은 스킬 쿨타임이 {seconds}초 감소합니다.";
                card.tier=Tier.Legend; e.Value=0.5f; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        card.spawnRate = 1f;
        card.Composition = SelectionDefinitionReader.CopyLegacyForMigration(card);
        if (kind == DroneSelectionKind.AlphanDoubleShot)
            card.Composition.Effects[0] = new ChiefChargeCapacityDefinition { MaximumCharges = e.Count };
        if (kind == DroneSelectionKind.GammanEmergency)
            card.Composition.Effects[0] = new ChiefHackingDetonationDefinition { DamageRatioPerStack = 0.03f };
        card.EffectSchemaVersion = 1;
    }

    /// <summary>사용자가 저장 위치를 정한 새 폴더에 11개 카드와 명시적 풀을 생성한다.</summary>
    [MenuItem("Tools/Selections/Create Drone Selection Drafts")]
    public static void CreateDrafts()
    {
        var path = EditorUtility.SaveFilePanelInProject("새 드론 풀 저장", "DroneLevelUpPool", "asset", "기존 풀은 덮어쓰지 않습니다.");
        if (string.IsNullOrEmpty(path)) return;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            throw new InvalidOperationException("Choose a new path.");
        var folder = System.IO.Path.GetDirectoryName(path).Replace('\\','/');
        var pool = ScriptableObject.CreateInstance<LevelUpPoolData>();
        pool.PoolId = 9100;
        pool.Cards = new LevelUpData[11];
        for (int i=0;i<pool.Cards.Length;i++)
        {
            var card=ScriptableObject.CreateInstance<LevelUpData>();
            Configure(card,(DroneSelectionKind)(i+1));
            card.chooseId=9101+i;
            AssetDatabase.CreateAsset(card,AssetDatabase.GenerateUniqueAssetPath(folder+"/DroneSelection"+(i+1)+".asset"));
            pool.Cards[i]=card;
        }
        AssetDatabase.CreateAsset(pool,path);
        AssetDatabase.SaveAssets();
        Selection.activeObject=pool;
    }
}
