# 유닛 레벨 보너스 · 기획툴 / Excel / 원격 런타임 설계

2026-10-30. 상태: 구현 규약과 임시 밸런스 제안. 아래 신규 기능은 아직 구현되지 않았다.

## 현재 코드와 변경 범위

- `UnitLevelBonusDefinition.Parts`는 여러 `UnitLevelBonusPart`를 합성한다.
- `StatModifierBonus`는 AttackPercent / BasicAttackFrequencyPercent / CriticalDamagePoints만 처리한다.
- `EveryNthCycleBonus`는 베탕 기본 자폭 주기에 연결되어 있다.
- `UnitBaseStats` exporter / `UnitBalanceSnapshot`에는 보너스가 없다.
- `GameInitializer`가 `UnitFactory.Init()`을 호출하므로 계정 레벨 전달이 없고 전투 레벨은 1이다.
- 강화 화면 설명은 원본 SO이며 계정 강화 구매는 아직 미연결이다.

## 설계 원칙

엑셀 EffectType에는 실제 클래스와 같은 짧은 키를 적는다. C#에서는 문자열을 Enum으로 검증하고 명시적인 factory로 연결한다. 임의 클래스명 reflection / Activator / assembly-qualified name을 받지 않는다. 기존 키는 클래스 이름을 바꿔도 유지한다. 새 클래스를 추가하려면 enum, factory, 툴 카탈로그, 테스트를 함께 추가해야 한다.

유닛별 클래스는 만들지 않는다. 하나의 레벨 보너스에 여러 Part를 추가해 합성한다. 설명은 효과 수치에서 생성하고 별도의 표시 문구는 선택적으로 덮어쓴다. 설명만 바꾸는 것으로 효과가 바뀌지 않는다.

## 효과 키와 클래스 연결

| UnitBonusEffectType (문자열 저장) | 연결 클래스 | 툴 표시 | 매개변수 |
|---|---|---|---|
| StatModifierBonus | StatModifierBonus | 스탯 증가 | Stat, ValueMode, Value |
| EveryNthCycleBonus | EveryNthCycleBonus | N번째 행동 강화 | CycleId, Every, DamageBonusPercent, ChargeReductionPercent |

`UnitBonusValueMode`: Flat / Percent / PercentagePoints. 숫자 enum 값은 파일에 쓰지 않는다.

| UnitBonusStat 신규 계약 | 한글 | 허용 모드 | 의미 |
|---|---|---|---|
| Attack | 공격력 | Flat, Percent | 기본 공격력 증가 |
| BasicAttackFrequency | 일반 공격속도 | Flat, Percent | 초당 공격 횟수 증가 |
| CriticalChance | 치명타 확률 | PercentagePoints | 확률에 퍼센트포인트 가산 |
| CriticalDamage | 치명타 피해 | PercentagePoints | 피해 배율에 퍼센트포인트 가산 |
| SkillCooldownReduction | 스킬 쿨타임 감소 | Flat, Percent | 초 감소 / 시간 비율 감소 |
| FoodProduction | 초당 식량 생산 | Flat, Percent | 생산량 증가 |
| MaxDroneCount | 최대 드론 수 | Flat | 음이 아닌 정수 |

기존 AttackPercent / BasicAttackFrequencyPercent / CriticalDamagePoints는 마이그레이션 입력 alias로만 수용하고 canonical 데이터는 위 규약으로 변환한다. SO 기존 값 0.05는 툴 Percent=5로 변환한다. 기존 베탕 효과의 실행 결과를 기준으로 동등성을 검사한다.

## 수치 단위와 계산 순서

툴과 Excel은 사람 단위를 쓴다. Percent=5는 5%, PercentagePoints=50은 +50%p이다. 런타임 DTO 파서가 한 번만 100으로 나눈다. Flat은 원래 단위다. 0.05를 5%로 쓰는 기존 규약과 구분하기 위해 새 스키마 버전을 사용한다.

- 공격력 / 공격빈도 / 생산: `(기본값 + 고정 합계) × (1 + 퍼센트 합계)`.
- 공격빈도를 계산한 뒤 공격 간격은 `1 / 공격빈도`. 일반 공속은 스킬 충전에 임의로 전파하지 않는다.
- 쿨타임: `max(기존 최소 간격, (기본 초 - 고정 감소 합계) × (1 - 감소율 합계))`.
- 치명타 확률: 기존 합계 + 포인트 합계, 최종 0~1 제한. 치명타 피해는 기존 배율에 포인트 가산.
- 최대 드론: 기존 수 + 고정 정수 합계. 드론 슬롯 생성 전에 반영한다.
- N번째 피해: DamageBonusPercent 합산 후 배율화. 충전 감소는 각 Part의 `(1 - 감소율)`을 곱한다. 기본 행동만 카운터를 진행시키고 추가 행동은 제외한다.
- 유닛 보너스를 기본 수치에 적용한 뒤 기존 전투 강화·토템·영구강화 경로에 전달한다. 동일 효과를 두 경로에서 재적용하지 않는다.

## 임시 보너스 설계

해금은 Lv.3 / 6 / 9 / 12 / 15, 누적이다. 베탕은 기존 승인 데이터 유지. 나머지는 제안값이며 기존 계정에 레벨을 지급하지 않는다. 이름 매칭 대신 AssetKey를 사용한다. 구체 스킬 없는 4종은 공통 공격 역할에 맞춘다.

| AssetKey | Lv.3 | Lv.6 | Lv.9 | Lv.12 | Lv.15 |
|---|---|---|---|---|---|
| UnitData_Betan | 기존 공격 +5% | 기존 2번째 기본 자폭 피해 +40% | 기존 일반 공속 +5% | 기존 2번째 기본 자폭 충전 -30% | 기존 치명타 피해 +50%p |
| UnitData_Deltan | 공격 +5% | 최대 드론 +1 | 일반 공속 +5% | 공격 +10% | 최대 드론 +1 |
| UnitData_Gamman | 일반 공속 +5% | 공격 +5% | 치명타 확률 +3%p | 일반 공속 +10% | 치명타 피해 +30%p |
| UnitData_Disigman | 공격 +5% | 스킬 쿨타임 -5% | 치명타 확률 +3%p | 공격 +10% | 치명타 피해 +30%p |
| UnitData_Zeltan | 식량 생산 +5% | 식량 생산 +1/초 | 식량 생산 +10% | 식량 생산 +1/초 | 식량 생산 +15% |
| UD_Frog_Mage | 공격 +5% | 스킬 쿨타임 -5% | 공격 +10% | 스킬 쿨타임 -5% | 치명타 피해 +30%p |
| UD_Frog_Rogue | 일반 공속 +5% | 치명타 확률 +3%p | 공격 +10% | 일반 공속 +10% | 치명타 피해 +30%p |
| UD_Frog_Archer | 공격 +5% | 일반 공속 +5% | 치명타 확률 +3%p | 공격 +10% | 일반 공속 +10% |
| UD_Frog_Priest | 일반 공속 +5% | 공격 +5% | 스킬 쿨타임 -5% | 일반 공속 +10% | 공격 +10% |

족장은 이 표 대상에서 제외한다. 일반 유닛과 다른 스킬 실행 계약이 필요하다. 드론 수 보너스는 Deltan 생성 경로가 실제 슬롯 상한을 소비하는지 검증한 뒤 Supported 상태로 노출한다. 회복·해킹 스택 등 신규 특수 행동은 구현 전까지 선택 목록에 노출하지 않는다.

## Excel 표

기존 LevelBonuses 시트는 설명 전용 v1 읽기를 유지한다. v2는 아래 두 시트를 함께 생성한다. 여러 효과를 JSON 문자열 하나로 직접 작성시키지 않는다.

`LevelBonuses`: LevelBonusKey, Level, DescriptionOverride.

`LevelBonusParts`: LevelBonusKey, Level, PartIndex, EffectType, Stat, ValueMode, Value, CycleId, Every, DamageBonusPercent, ChargeReductionPercent.

- 복합 키: 보너스 `(LevelBonusKey, Level)`, Part `(LevelBonusKey, Level, PartIndex)`.
- PartIndex는 0부터 연속. 스탯 효과는 주기 열을 비우고 주기 효과는 스탯 열을 비운다.
- 예: `bonus_zeltan,6,0,StatModifierBonus,FoodProduction,Flat,1,,,,`.
- 예: `bonus_betan,6,0,EveryNthCycleBonus,,,,basic_skill,2,40,0`.
- 한 해금에 공격+공속을 주려면 같은 보너스/레벨에 PartIndex 0,1 두 행을 작성한다.
- 빈 값과 숫자 0을 구분한다. 수식·NaN·Infinity·중복·미지원 enum·잘못된 단위를 거부한다.
- 효과 없는 레벨은 내보내기 오류. 기존 DescriptionOnly 데이터는 편집은 가능하지만 실서버 배포에서는 거부한다.
- 해금 레벨은 유닛 MaxLevel 이하. 공유 LevelBonusKey는 내용이 동일해야 한다.
- 감소율은 0 이상 100 미만, 증가 수치는 0 이상, Every는 1 이상 정수. 지원하지 않는 CycleId와 유닛 조합을 거부한다.

## 기획툴 편집

유닛 → 레벨 보너스 → 해금 레벨 → 효과 추가.

효과를 '스탯 증가'로 고르면 공격력/일반 공격속도/식량 등 지원 스탯 목록이 나온다. 스탯을 고르면 허용되는 '고정값 / 퍼센트 / 퍼센트포인트'만 표시한다. 값 입력 옆에 공격/초, 초, %, %p를 표시한다. 드론 수에는 정수 입력만 제공한다. 쿨타임은 증가/감소 방향을 문구에 명시한다.

'N번째 행동 강화'는 기본 자폭 등 실제 구현된 행동 목록을 먼저 고르고 N, 피해 증가 %, 충전시간 감소 %를 입력한다. 한 해금에 효과 추가/제거/순서 변경이 가능하다. 자동 설명 예시와 합산 후 최종 값을 미리 보여준다.

내부 JSON을 원본으로 보존하고 위 표는 같은 DTO의 투영으로 만든다. 현재 UnitMilestone EffectId/EffectParams는 이관용으로만 읽는다. 저장된 사용자 초안에 신규 임시 밸런스를 자동 덮어쓰지 않는다.

## 서버 차트와 런타임

차트 수를 늘리지 않는다. 기존 DEV/LIVE_UnitBaseStats 단일 스냅샷을 schema v2로 확장한다. `Units[].LevelBonuses[] = {Level, Description, Parts:[{EffectType, Stat, ValueMode, Value, ...}]}`. 기존 수치와 보너스가 같은 ReleaseKey/hash로 원자적으로 배포된다. 업로드는 기존 단일 시트 payload 형식이다. 작성용 Excel 여러 시트를 그대로 뒤끝에 올리지 않는다.

구버전 앱과 v2 차트의 호환을 확인한 후 전환한다. 새 파서는 v1을 명시적으로 읽되 v1에는 원격 보너스가 없음을 표시한다. v2에서는 누락된 보너스를 SO로 자동 대체하지 않는다. 미지원 효과는 전체 스냅샷 거절로 처리한다.

순서: 로그인 → 다운로드 → DTO 검증 → enum/factory 변환 → 원본 SO의 런타임 사본에 보너스 설치 → 로비 표시 → 계정별 유닛 레벨을 UnitLevelSnapshot으로 복사 → UnitFactory.Init(snapshot) → UnitBase의 보너스 컨트롤러 초기화.

현재 없는 '계정 유닛 레벨 저장' 경계도 연결해야 한다. 전투 스냅샷은 계정의 확정 레벨만 읽고 강화 화면의 로컬 미리보기 레벨은 읽지 않는다. 이 연결 전에는 테스트에서 명시적으로 주입한 레벨만 검증하고 계정 연동 완료로 표시하지 않는다. 강화 구매/재화 차감의 서버 원자성은 별도 작업이다.

## 구현 순서와 통과 기준

1. DTO/enum/factory와 베탕 이관: 레벨별 기존 공격·주기·충전·치명타 결과가 일치.
2. 추가 스탯 계산: 고정/퍼센트 순서, 치명타 포인트, 쿨타임 하한, 드론 상한 소비 검증.
3. 툴 편집과 Excel 왕복: 9종×5해금=45개 정의, 복합 Part와 사용자 수정 보존.
4. exporter/parser v2: Excel→payload→서버→Unity의 45개 정의와 모든 Part 값 일치. 미지원 enum/오염 데이터 거절.
5. 로비/전투: 계정 스냅샷 레벨 2/3/5/6/15에서 해금 경계, 화면 설명과 실제 계산 일치. 전투 도중 정의·레벨은 고정.
6. DEV에서 동일 파일 검증 후 LIVE 반영. v1 앱 호환 또는 클라이언트 배포 순서를 먼저 확정.

이 문서만으로 새 보너스가 배포되거나 적용되지는 않는다. 기존 서버 차트 변경과 계정 레벨/재화 변경은 이번 설계에 포함되지 않는다.
