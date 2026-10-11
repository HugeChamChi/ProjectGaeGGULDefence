/// <summary>Authored choice wording shared by asset migration and draft builders. No effect values are assigned here.</summary>
public static class ChoiceDescriptionContent
{
    public static void Configure(LevelUpData card)
    {
        switch (card.droneEffect?.Kind ?? DroneSelectionKind.None)
        {
            case DroneSelectionKind.BetanPeriodicBomb:
                card.description = "{Interval}초마다 배치된 베탕 각각의 {term:bomb-drone} {Count}기를 추가 생성합니다.";
                card.simpleDescription = "주기적으로 각 베탕의 {term:bomb-drone}을 추가 생성합니다."; break;
            case DroneSelectionKind.BetanAttackSpeed:
                card.description = "베탕의 {term:combat-drone} 공격속도가 {ValuePercent}% 상승합니다.";
                card.simpleDescription = "베탕의 {term:combat-drone} 공격속도가 상승합니다."; break;
            case DroneSelectionKind.BetanFleetBomb:
                card.description = "{Interval}초마다 필드 전체 베탕의 {term:combat-drone} 수만큼 {term:bomb-drone}을 추가 생성합니다. 최대 {Count}기.";
                card.simpleDescription = "주기적으로 베탕의 {term:combat-drone} 수에 따라 {term:bomb-drone}을 추가 생성합니다. 생성 수에는 상한이 있습니다."; break;
            case DroneSelectionKind.GammanFrequency:
                card.description = "필드 드론 1기당 {term:gamman-buff} 배율에 {ValuePercent}%p를 추가합니다. 최대 {MaxValuePercent}%p.";
                card.simpleDescription = "필드 드론 수에 따라 {term:gamman-buff}가 강해집니다. 증가량에는 상한이 있습니다."; break;
            case DroneSelectionKind.GammanEmergency:
                card.description = "{term:alphan-active} 사용 시 배치된 감망의 {term:gamman-buff}를 적용합니다.";
                card.simpleDescription = "{term:alphan-active}로 배치된 감망의 {term:gamman-buff}도 발동합니다."; break;
            case DroneSelectionKind.ZeltanAirFryer:
                card.description = "{term:zeltan-production}이 {ValuePercent}% 증가하며 {Interval}초마다 모아서 지급됩니다.";
                card.simpleDescription = "{term:zeltan-production}이 증가하며 주기적으로 모아서 지급됩니다."; break;
            case DroneSelectionKind.DeltanDefenseReduction:
                card.description = "델탕의 {term:hacking-stack} 생산량이 {Count}스택 증가합니다.";
                card.simpleDescription = "델탕의 {term:hacking-stack} 생산량이 증가합니다."; break;
            case DroneSelectionKind.DeltanDamageTaken:
                card.description = "델탕의 {term:hacking-stack} 생산량이 {value}% 증가합니다. 소수 생산분은 다음 시전으로 이월됩니다.";
                card.simpleDescription = "델탕의 {term:hacking-stack} 생산량이 증가합니다."; break;
            case DroneSelectionKind.DeltanCooldown:
                card.description = "{term:deltan-charge}에 필요한 공격 횟수가 {ValuePercent}% 감소합니다 (최소 1회 감소).";
                card.simpleDescription = "{term:deltan-charge}에 필요한 공격 횟수가 감소합니다."; break;
            case DroneSelectionKind.AlphanCooldown:
                card.description = "{term:alphan-active} 쿨타임이 {ValuePercent}% 감소합니다.";
                card.simpleDescription = "{term:alphan-active} 쿨타임이 감소합니다."; break;
            case DroneSelectionKind.AlphanDoubleShot:
                card.description = "{term:alphan-active}이 {Count}회 연속 발사됩니다. 각 피해는 원래 최종 피해의 {ValuePercent}%입니다.";
                card.simpleDescription = "{term:alphan-active}이 2회 연속 발사됩니다. 각 발사의 피해는 감소합니다."; break;
            case DroneSelectionKind.ZeltanColdStorage:
                card.description = "{term:zeltan-production}이 {ValuePercent}% 증가합니다.";
                card.simpleDescription = "{term:zeltan-production}이 증가합니다."; break;
            case DroneSelectionKind.ZeltanMaintenance:
                card.description = "젤탕의 드론당 군단 식량 생산량이 {ValuePercent}% 증가합니다.";
                card.simpleDescription = "젤탕의 드론당 군단 식량 생산량이 증가합니다."; break;
            case DroneSelectionKind.AlphanGoldenMonocle:
                card.description = "{term:alphan-active}이 확정 치명타로 {Value}배 피해를 줍니다.";
                card.simpleDescription = "{term:alphan-active}이 확정 치명타로 강화됩니다."; break;
            case DroneSelectionKind.BetanRepairKit:
                card.description = "{term:bomb-drone}이 폭발할 때마다 배치된 베탕의 남은 스킬 쿨타임이 {Value}초 감소합니다.";
                card.simpleDescription = "{term:bomb-drone}이 폭발할 때마다 배치된 베탕의 남은 스킬 쿨타임이 감소합니다."; break;
            case DroneSelectionKind.MergeSupport:
                card.description = "{term:merge} 시 {ValuePercent}% 확률로 노말 랜덤 유닛을 생성합니다. 빈칸이 없으면 대기 후 자리가 생기면 생성합니다.";
                card.simpleDescription = "{term:merge} 시 확률적으로 노말 랜덤 유닛을 생성합니다. 빈칸이 없으면 자리가 생길 때까지 대기합니다."; break;
            case DroneSelectionKind.ExtraCombatDrone:
                card.description = "에픽 이상 베탕·감망·델탕의 {term:combat-drone}이 {Count}기 추가됩니다. 추가 드론은 일반 공격과 {term:alphan-active}에 참여하며, 본체 스킬은 사용하지 않습니다.";
                card.simpleDescription = "에픽 이상 베탕·감망·델탕의 {term:combat-drone}을 추가합니다. 일반 공격과 {term:alphan-active}에 참여하며 본체 스킬은 사용하지 않습니다."; break;
            default:
                switch (card.specialEffect)
                {
                    case LevelUpSpecialEffect.GuaranteeNextLegend:
                        card.description = "다음 선택지 한 번은 확정적으로 레전더리 등급이 나옵니다.";
                        card.simpleDescription = "다음 선택지 한 번은 레전더리 등급이 확정됩니다."; break;
                    case LevelUpSpecialEffect.UpgradeDiscountAndRefund:
                        card.description = "강화 비용이 {specialValue}% 감소하고, 이번 런에서 지금까지 강화에 실제로 쓴 식량의 {specialValue}%를 환급받습니다.";
                        card.simpleDescription = "강화 비용이 감소하며, 이번 런에서 이미 강화에 쓴 식량 일부를 환급받습니다."; break;
                    case LevelUpSpecialEffect.RerollChoices:
                        card.description = "즉시 등급을 다시 추첨하여 선택지 3장을 새로 뽑고, 선택 제한시간을 초기화합니다.";
                        card.simpleDescription = "등급과 선택지 3장을 다시 뽑고, 선택 제한시간을 초기화합니다."; break;
                }
                break;
        }
    }
}
