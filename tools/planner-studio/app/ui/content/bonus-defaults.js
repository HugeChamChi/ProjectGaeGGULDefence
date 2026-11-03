globalThis.UNIT_BONUS_MODEL_DEFAULTS={
  "BonusDefinition": [
    {
      "BonusId": "betan_lv3",
      "UnitKey": "UnitData_Betan",
      "Level": 3,
      "DescriptionTemplate": "공격력 +{attack.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "betan_lv6",
      "UnitKey": "UnitData_Betan",
      "Level": 6,
      "DescriptionTemplate": "매 {cycle.Every}번째 기본 [자폭드론] 피해 +{cycle.DamageBonusPercent}% (선택지 추가 자폭 제외)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "betan_lv9",
      "UnitKey": "UnitData_Betan",
      "Level": 9,
      "DescriptionTemplate": "일반공격 속도 +{basicAttackFrequency.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "betan_lv12",
      "UnitKey": "UnitData_Betan",
      "Level": 12,
      "DescriptionTemplate": "매 {cycle.Every}번째 기본 [자폭드론] 충전시간 -{cycle.ChargeReductionPercent}% (선택지 추가 자폭 제외)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "betan_lv15",
      "UnitKey": "UnitData_Betan",
      "Level": 15,
      "DescriptionTemplate": "일반공격·자폭 치명타 피해 +{criticalDamage.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "deltan_lv3",
      "UnitKey": "UnitData_Deltan",
      "Level": 3,
      "DescriptionTemplate": "공격속도 +{basicAttackFrequency.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "deltan_lv6",
      "UnitKey": "UnitData_Deltan",
      "Level": 6,
      "DescriptionTemplate": "스킬 충전 필요 공격 횟수 -{chargeAttackReduction.Value}",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "deltan_lv9",
      "UnitKey": "UnitData_Deltan",
      "Level": 9,
      "DescriptionTemplate": "공유 [해킹] 최대 스택 +{hackingCapacity.Value}",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "deltan_lv12",
      "UnitKey": "UnitData_Deltan",
      "Level": 12,
      "DescriptionTemplate": "스킬 사용 시 [해킹] 추가 생산: 노말 {hackingProducedNormal.Normal} / 레어 {hackingProducedNormal.Rare} / 에픽 {hackingProducedNormal.Epic} / 전설 {hackingProducedNormal.Legend}",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "deltan_lv15",
      "UnitKey": "UnitData_Deltan",
      "Level": 15,
      "DescriptionTemplate": "[해킹] 스킬 사용 후 {hackingPostSpeed.Duration}초간 자신의 공격속도 +{hackingPostSpeed.Value}% (재발동 시 시간 갱신)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "gamman_lv3",
      "UnitKey": "UnitData_Gamman",
      "Level": 3,
      "DescriptionTemplate": "공격력 +{attack.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "gamman_lv6",
      "UnitKey": "UnitData_Gamman",
      "Level": 6,
      "DescriptionTemplate": "[해킹] 스택 부분 피해 +{hackingStackDamage.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "gamman_lv9",
      "UnitKey": "UnitData_Gamman",
      "Level": 9,
      "DescriptionTemplate": "스킬 쿨타임 -{skillCooldownReduction.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "gamman_lv12",
      "UnitKey": "UnitData_Gamman",
      "Level": 12,
      "DescriptionTemplate": "[해킹] 추가 소모 한도: 노말 {hackingConsumedNormal.Normal} / 레어 {hackingConsumedNormal.Rare} / 에픽 {hackingConsumedNormal.Epic} / 전설 {hackingConsumedNormal.Legend}",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "gamman_lv15",
      "UnitKey": "UnitData_Gamman",
      "Level": 15,
      "DescriptionTemplate": "치명타 확률 +{criticalChance.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "disigman_lv3",
      "UnitKey": "UnitData_Disigman",
      "Level": 3,
      "DescriptionTemplate": "공격력 +{attack.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "disigman_lv6",
      "UnitKey": "UnitData_Disigman",
      "Level": 6,
      "DescriptionTemplate": "시간 회복 후 다음 정규 일반공격 피해 +{recoveryNextBasic.Value}% (1회 저장, 중첩 없음)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "disigman_lv9",
      "UnitKey": "UnitData_Disigman",
      "Level": 9,
      "DescriptionTemplate": "공격속도 +{basicAttackFrequency.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "disigman_lv12",
      "UnitKey": "UnitData_Disigman",
      "Level": 12,
      "DescriptionTemplate": "시간 회복량 +{timeRecovery.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "disigman_lv15",
      "UnitKey": "UnitData_Disigman",
      "Level": 15,
      "DescriptionTemplate": "일반공격 피해 +{basicDamage.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "zeltan_lv3",
      "UnitKey": "UnitData_Zeltan",
      "Level": 3,
      "DescriptionTemplate": "자체 식량 생산속도 +{foodSpeed.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "zeltan_lv6",
      "UnitKey": "UnitData_Zeltan",
      "Level": 6,
      "DescriptionTemplate": "보스 처치 시 자체 식량 {bossFoodSeconds.Value}초분 지급",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "zeltan_lv9",
      "UnitKey": "UnitData_Zeltan",
      "Level": 9,
      "DescriptionTemplate": "자체 식량 생산량 +{foodProduction.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "zeltan_lv12",
      "UnitKey": "UnitData_Zeltan",
      "Level": 12,
      "DescriptionTemplate": "매 5번째 자체 식량 생산량 {foodFifthMultiplier.Value}배",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "zeltan_lv15",
      "UnitKey": "UnitData_Zeltan",
      "Level": 15,
      "DescriptionTemplate": "매 5번째 자체 식량 생산량 {foodFifthMultiplier.Value}배로 교체",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "alphan_lv3",
      "UnitKey": "AlphanChief",
      "Level": 3,
      "DescriptionTemplate": "[액티브 스킬] 쿨타임 -{chiefCooldown.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "alphan_lv6",
      "UnitKey": "AlphanChief",
      "Level": 6,
      "DescriptionTemplate": "전투 시작 시 [액티브 스킬] {chiefStartsReady.Value}회 충전 완료",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "alphan_lv9",
      "UnitKey": "AlphanChief",
      "Level": 9,
      "DescriptionTemplate": "전체 유닛 공격력 +{chiefGlobalAttack.Value}% (족장 고정 집결 피해·식량 제외)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    },
    {
      "BonusId": "alphan_lv12",
      "UnitKey": "AlphanChief",
      "Level": 12,
      "DescriptionTemplate": "[액티브 스킬] 사용 시 {chiefSpeed.Duration}초간 전체 유닛 {공격속도효과} +{chiefSpeed.Value}% (시간 갱신)",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "ChiefAttackSpeed"
    },
    {
      "BonusId": "alphan_lv15",
      "UnitKey": "AlphanChief",
      "Level": 15,
      "DescriptionTemplate": "집결 적중 시 {chiefDamageTaken.Duration}초간 보스가 받는 피해 +{chiefDamageTaken.Value}%",
      "HighlightNumbers": true,
      "LinkTerms": true,
      "EffectLink": "None"
    }
  ],
  "BonusEffect": [
    {
      "BonusId": "betan_lv3",
      "EffectKey": "attack",
      "EffectType": "StatModifierBonus",
      "Stat": "Attack",
      "ValueMode": "Percent",
      "Value": "5"
    },
    {
      "BonusId": "betan_lv6",
      "EffectKey": "cycle",
      "EffectType": "EveryNthCycleBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "2,40,0"
    },
    {
      "BonusId": "betan_lv9",
      "EffectKey": "basicAttackFrequency",
      "EffectType": "StatModifierBonus",
      "Stat": "BasicAttackFrequency",
      "ValueMode": "Percent",
      "Value": "5"
    },
    {
      "BonusId": "betan_lv12",
      "EffectKey": "cycle",
      "EffectType": "EveryNthCycleBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "2,0,30"
    },
    {
      "BonusId": "betan_lv15",
      "EffectKey": "criticalDamage",
      "EffectType": "StatModifierBonus",
      "Stat": "CriticalDamage",
      "ValueMode": "PercentagePoints",
      "Value": "50"
    },
    {
      "BonusId": "deltan_lv3",
      "EffectKey": "basicAttackFrequency",
      "EffectType": "StatModifierBonus",
      "Stat": "BasicAttackFrequency",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "deltan_lv6",
      "EffectKey": "chargeAttackReduction",
      "EffectType": "StatModifierBonus",
      "Stat": "ChargeAttackReduction",
      "ValueMode": "Flat",
      "Value": "1"
    },
    {
      "BonusId": "deltan_lv9",
      "EffectKey": "hackingCapacity",
      "EffectType": "StatModifierBonus",
      "Stat": "HackingCapacity",
      "ValueMode": "Flat",
      "Value": "10"
    },
    {
      "BonusId": "deltan_lv12",
      "EffectKey": "hackingProducedNormal",
      "EffectType": "HackingProductionBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "1,1,2,3"
    },
    {
      "BonusId": "deltan_lv15",
      "EffectKey": "hackingPostSpeed",
      "EffectType": "HackingAttackSpeedBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "15,2"
    },
    {
      "BonusId": "gamman_lv3",
      "EffectKey": "attack",
      "EffectType": "StatModifierBonus",
      "Stat": "Attack",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "gamman_lv6",
      "EffectKey": "hackingStackDamage",
      "EffectType": "StatModifierBonus",
      "Stat": "HackingStackDamage",
      "ValueMode": "Percent",
      "Value": "20"
    },
    {
      "BonusId": "gamman_lv9",
      "EffectKey": "skillCooldownReduction",
      "EffectType": "StatModifierBonus",
      "Stat": "SkillCooldownReduction",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "gamman_lv12",
      "EffectKey": "hackingConsumedNormal",
      "EffectType": "HackingConsumptionBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "1,1,2,4"
    },
    {
      "BonusId": "gamman_lv15",
      "EffectKey": "criticalChance",
      "EffectType": "StatModifierBonus",
      "Stat": "CriticalChance",
      "ValueMode": "PercentagePoints",
      "Value": "10"
    },
    {
      "BonusId": "disigman_lv3",
      "EffectKey": "attack",
      "EffectType": "StatModifierBonus",
      "Stat": "Attack",
      "ValueMode": "Percent",
      "Value": "5"
    },
    {
      "BonusId": "disigman_lv6",
      "EffectKey": "recoveryNextBasic",
      "EffectType": "StatModifierBonus",
      "Stat": "RecoveryNextBasic",
      "ValueMode": "Percent",
      "Value": "30"
    },
    {
      "BonusId": "disigman_lv9",
      "EffectKey": "basicAttackFrequency",
      "EffectType": "StatModifierBonus",
      "Stat": "BasicAttackFrequency",
      "ValueMode": "Percent",
      "Value": "5"
    },
    {
      "BonusId": "disigman_lv12",
      "EffectKey": "timeRecovery",
      "EffectType": "StatModifierBonus",
      "Stat": "TimeRecovery",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "disigman_lv15",
      "EffectKey": "basicDamage",
      "EffectType": "StatModifierBonus",
      "Stat": "BasicDamage",
      "ValueMode": "Percent",
      "Value": "15"
    },
    {
      "BonusId": "zeltan_lv3",
      "EffectKey": "foodSpeed",
      "EffectType": "StatModifierBonus",
      "Stat": "FoodSpeed",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "zeltan_lv6",
      "EffectKey": "bossFoodSeconds",
      "EffectType": "StatModifierBonus",
      "Stat": "BossFoodSeconds",
      "ValueMode": "Flat",
      "Value": "5"
    },
    {
      "BonusId": "zeltan_lv9",
      "EffectKey": "foodProduction",
      "EffectType": "StatModifierBonus",
      "Stat": "FoodProduction",
      "ValueMode": "Percent",
      "Value": "15"
    },
    {
      "BonusId": "zeltan_lv12",
      "EffectKey": "foodFifthMultiplier",
      "EffectType": "StatModifierBonus",
      "Stat": "FoodFifthMultiplier",
      "ValueMode": "Flat",
      "Value": "2"
    },
    {
      "BonusId": "zeltan_lv15",
      "EffectKey": "foodFifthMultiplier",
      "EffectType": "StatModifierBonus",
      "Stat": "FoodFifthMultiplier",
      "ValueMode": "Flat",
      "Value": "2.5"
    },
    {
      "BonusId": "alphan_lv3",
      "EffectKey": "chiefCooldown",
      "EffectType": "StatModifierBonus",
      "Stat": "ChiefCooldown",
      "ValueMode": "Percent",
      "Value": "10"
    },
    {
      "BonusId": "alphan_lv6",
      "EffectKey": "chiefStartsReady",
      "EffectType": "StatModifierBonus",
      "Stat": "ChiefStartsReady",
      "ValueMode": "Flat",
      "Value": "1"
    },
    {
      "BonusId": "alphan_lv9",
      "EffectKey": "chiefGlobalAttack",
      "EffectType": "StatModifierBonus",
      "Stat": "ChiefGlobalAttack",
      "ValueMode": "Percent",
      "Value": "5"
    },
    {
      "BonusId": "alphan_lv12",
      "EffectKey": "chiefSpeed",
      "EffectType": "ChiefAttackSpeedBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "20,10"
    },
    {
      "BonusId": "alphan_lv15",
      "EffectKey": "chiefDamageTaken",
      "EffectType": "RallyVulnerabilityBonus",
      "Stat": "",
      "ValueMode": "",
      "Value": "10,5"
    }
  ]
};
