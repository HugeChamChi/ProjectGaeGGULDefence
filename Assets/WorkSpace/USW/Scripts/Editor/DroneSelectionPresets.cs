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
        card.tier = Tier.Normal;
        switch (kind)
        {
            case DroneSelectionKind.MergeSupport:
                card.chooseName="지원 요청 바람"; card.description="합성 시 10% 확률로 노말 랜덤 유닛을 생성합니다. 빈칸이 없으면 대기 후 자리가 생기면 생성합니다.";
                card.tier=Tier.Rare; e.Value=0.1f; break;
            case DroneSelectionKind.ExtraCombatDrone:
                card.chooseName="드론 생성 공정"; card.description="에픽 이상 베탕·감망·델탕의 전투 드론이 1기 추가됩니다. 추가 드론은 일반 공격과 알팡 액티브에 참여하며, 본체 스킬은 사용하지 않습니다.";
                card.tier=Tier.Epic; e.Count=1; break;
            case DroneSelectionKind.BetanPeriodicBomb:
                card.chooseName="만나서 반가워!"; card.description="10초마다 배치된 베탕 각각의 자폭 드론 1기를 추가 생성합니다.";
                e.Interval=10; e.Count=1; break;
            case DroneSelectionKind.BetanAttackSpeed:
                card.chooseName="다 쏴 제껴버려!!!!"; card.description="베탕의 전투 드론 공격속도가 20% 상승합니다.";
                card.tier=Tier.Rare; e.Value=0.2f; break;
            case DroneSelectionKind.BetanFleetBomb:
                card.chooseName="슈슝 콰광!!!!!"; card.description="20초마다 필드 전체 베탕의 전투 드론 수만큼 자폭 드론을 추가 생성합니다. 최대 10기.";
                card.tier=Tier.Epic; e.Interval=20; e.Count=10; break;
            case DroneSelectionKind.GammanFrequency:
                card.chooseName="주파수 동조"; card.description="필드 드론 1기당 감망 버프 배율에 0.3%p를 추가합니다. 최대 9%p.";
                card.tier=Tier.Rare; e.Value=0.003f; e.MaxValue=0.09f; break;
            case DroneSelectionKind.GammanEmergency:
                card.chooseName="긴급 교신"; card.description="알팡 액티브 스킬 사용 시 배치된 감망의 버프를 적용합니다."; break;
            case DroneSelectionKind.ZeltanAirFryer:
                card.chooseName="에어프라이기"; card.description="젤탕의 자체 식량 생산량이 20% 증가하며 3초마다 모아서 지급됩니다.";
                e.Interval=3; e.Value=0.2f; break;
            case DroneSelectionKind.DeltanDefenseReduction:
                card.chooseName="넹?"; card.description="델탕의 디버프 스킬에 방어력 감소 10%를 추가합니다. 기존 디버프와 함께 만료됩니다.";
                card.tier=Tier.Rare; e.Value=0.1f; break;
            case DroneSelectionKind.DeltanDamageTaken:
                card.chooseName="넹 !"; card.description="델탕의 받는 피해 증가 스킬에 {value}%p를 추가합니다. 이번 런 동안 고정됩니다.";
                card.tier=Tier.Rare; e.Value=0.01f; e.MaxValue=0.1f; break;
            case DroneSelectionKind.DeltanCooldown:
                card.chooseName="넹..."; card.description="델탕의 디버프 스킬 쿨타임이 10% 감소합니다.";
                e.Value=0.1f; break;
            case DroneSelectionKind.AlphanCooldown:
                card.chooseName="위대한 지도자"; card.description="알팡 액티브 스킬 쿨타임이 20% 감소합니다.";
                e.Value=0.2f; break;
            case DroneSelectionKind.AlphanDoubleShot:
                card.chooseName="전술적 프로토콜"; card.description="알팡 액티브 스킬이 2회 연속 발사됩니다. 각 피해는 원래 최종 피해의 60%입니다.";
                card.tier=Tier.Epic; e.Value=0.6f; e.Count=2; e.Interval=0.2f; break;
            case DroneSelectionKind.ZeltanColdStorage:
                card.chooseName="냉동 보관"; card.description="젤탕의 자체 식량 생산량이 10% 증가합니다.";
                e.Value=0.1f; break;
            case DroneSelectionKind.ZeltanMaintenance:
                card.chooseName="정기 점검"; card.description="젤탕의 드론당 군단 식량 생산량이 10% 증가합니다.";
                e.Value=0.1f; break;
            case DroneSelectionKind.AlphanGoldenMonocle:
                card.chooseName="황금 모노클"; card.description="알팡의 액티브 스킬이 확정 치명타로 1.5배 피해를 줍니다.";
                card.tier=Tier.Epic; e.Value=1.5f; break;
            case DroneSelectionKind.BetanRepairKit:
                card.chooseName="수리 키트"; card.description="자폭 드론이 폭발할 때마다 배치된 베탕의 남은 스킬 쿨타임이 0.5초 감소합니다.";
                card.tier=Tier.Epic; e.Value=0.5f; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        card.spawnRate = 1f;
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
