# 인게임 레벨업 선택지 시스템

> 출처: [레벨업 선택지 시트](https://docs.google.com/spreadsheets/d/1f8RdzwKxB25ydOxOatfc9AdYX0vlOYmT-LYEC4Rm5cc/edit?gid=743942560)
> 담당 스크립트:
> - `LevelUp/LevelUpEffectType.cs` — 효과 타입 enum
> - `LevelUp/LevelUpManager.cs` — 선택지 뽑기 + 효과 적용
> - `LevelUp/LevelUpData.cs` — 카드 1장 ScriptableObject
> - `Totem/TotemBuffManager.cs` — 수치 보관

---

## 개요

레벨업 시 `LevelUpManager.GetRandomChoices(3)`으로 카드 3장을 랜덤 뽑아 플레이어에게 제시.
선택 시 `ApplyEffect(data)`로 즉시 버프 적용. 선택지는 ScriptableObject(`LevelUpData`) 배열로 관리.

---

## 카드 등록 방법

1. Unity Editor 우클릭 → **Create → Game → LevelUpData** 로 `.asset` 생성
2. `effectType`, `value`(%), `description`, `icon`, `animationFrames` 설정
3. 씬의 `LevelUpManager` 인스펙터 → `levelUpPool` 배열에 전체 에셋 할당

---

## 구현된 효과 타입 (LevelUpEffectType)

| enum | 설명 | 카드 예시 | 연결 경로 |
|------|------|----------|----------|
| `AttackPercent` | 전체 공격력 N% | 기초 근력(노말), 근력 강화(레어) | `TotemBuffManager.AttackMultiplier` |
| `AttackSpeedPercent` | 전체 공격 속도 N% | 바람의 손길(노말), 질풍(레어) | `TotemBuffManager.SpeedMultiplier` |
| `GaugeSpeedPercent` | 게이지 회복 속도 N% | 활력 충전(노말), 순환의 고리(레어) | `TotemBuffManager.GaugeSpeedMultiplier` |
| `TotemEfficiencyPercent` | 토템 효율 N% | 공명하는 토템(노말), 토템 마스터리(레어) | `TotemBuffManager._totemEfficiencyBonus` |
| `CritChancePercent` | 치명타 확률 N% | 관찰력(노말), 통찰력(레어) | `LevelUpManager.CritChance` |
| `CritDamagePercent` | 치명타 데미지 N% | 치명적 타격(노말), 치명적 일격(레어) | `LevelUpManager.CritDamageMultiplier` |
| `FoodSpeedPercent` | 식량 생산 속도 N% | 식량 비축(노말) | `TotemBuffManager.FoodSpeedMultiplier` |
| `FrontRowAttackPercent` | 전방 2줄 공격력 N% | 전방 화력(노말) | `LevelUpManager._rowAttackMult[0,1]` |
| `BackRowAttackPercent` | 후방 2줄 공격력 N% | 후방 지원(노말) | `LevelUpManager._rowAttackMult[last]` |
| `FrontRowSpeedPercent` | 전방 2줄 공격 속도 N% | 전방 침투(노말) | `LevelUpManager._rowSpeedMult[0,1]` |
| `BackRowSpeedPercent` | 후방 2줄 공격 속도 N% | 후방 가속(노말) | `LevelUpManager._rowSpeedMult[last]` |
| `ProjectileSizePercent` | 투사체 크기 N% | 비대(노말), 거대화(레어) | `TotemBuffManager.ProjectileSizeMultiplier` → `Projectile.Launch()` |

---

## 수치 흐름 — UnitBase 게이지 계산식

```
interval = gaugeDuration
         × gaugeMultiplier          // 유닛 개인 배율
         × SpeedMultiplier          // 공격 속도 (낮을수록 빠름)
         × GaugeSpeedMultiplier     // 게이지 회복 속도 (낮을수록 빠름)
         × FoodSpeedMultiplier      // 식량 생산 속도 (낮을수록 빠름)
         × cellSpeedModifier        // 셀 디버프
         ÷ rowSpeedMult             // 줄별 속도 배율
```

---

## 미구현 항목

| 카드 | 효과 | 이유 |
|------|------|------|
| 위엄(노말) | 족장 공격력 N% | "족장" 유닛 식별 로직 미정 — 추후 구현 |

---

## 시트 기준 카드 목록 (스탯 강화, 노말/레어)

| # | 한국어 | 영문 키 추천 | 희귀도 | effectType | value |
|---|--------|------------|--------|-----------|-------|
| 1 | 기초 근력 | `BasicStrength` | 노말 | AttackPercent | 10 |
| 2 | 바람의 손길 | `WindTouch` | 노말 | AttackSpeedPercent | 10 |
| 3 | 공명하는 토템 | `ResonatingTotem` | 노말 | TotemEfficiencyPercent | 20 |
| 4 | 관찰력 | `SharpEye` | 노말 | CritChancePercent | 10 |
| 5 | 치명적 타격 | `DeadlyStrike` | 노말 | CritDamagePercent | 10 |
| 6 | 식량 비축 | `FoodStockpile` | 노말 | FoodSpeedPercent | 10 |
| 7 | 비대 | `Hypertrophy` | 노말 | ProjectileSizePercent | 10 |
| 8 | 활력 충전 | `VitalCharge` | 노말 | GaugeSpeedPercent | 10 |
| 10 | 전방 화력 | `FrontFirepower` | 노말 | FrontRowAttackPercent | 15 |
| 11 | 후방 지원 | `RearSupport` | 노말 | BackRowAttackPercent | 15 |
| 12 | 전방 침투 | `FrontAssault` | 노말 | FrontRowSpeedPercent | 15 |
| 13 | 후방 가속 | `RearAcceleration` | 노말 | BackRowSpeedPercent | 15 |
| 33 | 근력 강화 | `PowerEnhancement` | 레어 | AttackPercent | 20 |
| 34 | 질풍 | `Gale` | 레어 | AttackSpeedPercent | 20 |
| 35 | 토템 마스터리 | `TotemMastery` | 레어 | TotemEfficiencyPercent | 30 |
| 36 | 통찰력 | `Insight` | 레어 | CritChancePercent | 20 |
| 37 | 치명적 일격 | `FatalBlow` | 레어 | CritDamagePercent | 20 |
| 38 | 거대화 | `Gigantism` | 레어 | ProjectileSizePercent | 30 |
| 39 | 순환의 고리 | `CycleLoop` | 레어 | GaugeSpeedPercent | 20 |
