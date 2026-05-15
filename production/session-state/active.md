# Session State — 2026-05-15

<!-- STATUS -->
Epic: 인게임 시스템
Feature: 레벨업 선택지 시스템
Task: TODO 구현 완료 (낙뢰 마법 제외)
<!-- /STATUS -->

---

## 작업 브랜치
`USW_Ingame`

---

## 구현 완료 항목 (전체)

### 지난 세션
- `LevelUpEffectType.cs` — 15개 효과 타입 확장
- `LevelUpSpecialEffect.cs` — 32개 특수 효과 열거형 신규 생성
- `LevelUpData.cs` — 구조 전면 개편 (chooseId, grade, spawnRate, primaryEffect 등)
- `LevelUpManager.cs` — 전면 재작성 (가중치 필터 뽑기, ApplyStatEffect/ApplySpecialEffect)
- `UnitBase.cs` — 히트카운트, 버스트 타이머, TriggerBonusAttacks, 데미지 배율 확장
- `UnitSpawner.cs` — 소환 할인, 판매 패시브
- `ExpManager.cs` — 경험치 배율 곱셈
- `PopulationManager.cs` — MaxBonus 추가
- `UnitFactory.cs` — CreateRandomUnitByTribeAndTier 추가
- `LevelUpDataGenerator.cs` (Editor) — 60개 에셋 자동 생성 툴

### 이번 세션
| 선택지 | 변경 파일 | 내용 |
|--------|-----------|------|
| `3041` 진로 계승 | `MergeManager.cs` | `HasMergeKeepsTribe` 활성화 시 같은 직업 유닛으로 합성 |
| `3055` 물리 마법사 | `FrogWizard.cs` | `HasWizardPhysicalMode` 활성화 시 `OnSkillFull` skip |
| `3056` 식충이 | `FrogUnemployed.cs` + `UnitBase.cs` | 스킬마다 `_unemployedAtkBonus` 누적, 식량 생산 제거 |
| `3008` 위엄 | `ChieftainSpawner.cs` + `Manager.cs` + `UnitBase.cs` + `LevelUpManager.cs` | 족장 유닛 참조(`ChieftainUnit`) 추가, 족장 전용 공격력 배율, 전역 우회 제거 |

---

## 미구현 — 보류

| 선택지 | 이유 |
|--------|------|
| `3046` 낙뢰 마법 | 번개 스킬 시각/데미지 정의 미결. FrogWizard 행동 변경 필요. 별도 작업 |

---

## Unity 에디터 작업 필요 (코드로 불가)

아래 두 단계는 Unity 에디터에서 직접 수행해야 합니다:

1. **에셋 생성**: 상단 메뉴 `Tools > USW > Generate LevelUp Assets` 실행
   - 생성 경로: `Assets/WorkSpace/USW/Data/LevelupSelection/`
   - 파일명 형식: `LevelUpData_{id}_{이름}.asset` (60개)
   - 기존 에셋 자동 삭제 후 재생성

2. **풀 등록**: `LevelUpManager` 오브젝트 Inspector에서
   - `levelUpPool` 배열에 `LevelupSelection/` 폴더의 에셋 60개 전부 드래그 등록
   - 또는 폴더 전체 선택(Ctrl+A) 후 배열 슬롯에 드롭

---

## 주요 결정 사항

- `LevelUpEffectType`에 `ExpGainPercent`, `ChieftainAttackPercent` 추가. 기존 enum int값 순서 변경으로 구버전 에셋 15개는 무효화됨 → Generator 툴로 전부 재생성 방식 채택
- `grade` 필드에 `Tier` enum 재사용 (Normal/Rare/Epic/Legend 동일 구조)
- `BurstOnSkillFull` 타이머는 `Time.time` 사용 (스케일 기반) — 레벨업 UI 표시 중(timeScale=0) 버스트 일시정지가 의도된 동작
- `ChieftainAttackPercent`(3008) — 전역 우회 제거, `Manager.Chieftain.ChieftainUnit == this` 비교로 족장 전용 적용
- `WizardPhysicalMode`(3055) — 스킬 제거는 `FrogWizard.OnSkillFull` skip으로 처리. 공격력/속도 버프는 `LevelUpManager.ApplySpecialEffect`에서 전역 적용
- `UnemployedFoodNegate`(3056) — `_unemployedAtkBonus`는 UnitBase protected 인스턴스 필드. `ComputeDamage`에서 baseDamage에 덧셈 후 배율 적용
- `MergeKeepsTribe`(3041) — `ExecuteMerge` 시점에 `_selectedUnit.unitData.unitTribe` 캡처 후 HideButton 이전에 저장
- `LaunchProjectile()` — `private` → `protected` 변경 (FrogUnemployed 서브클래스 접근용)
- `족장 개구리 구현` — directory-structure.md에 TODO 5항목 기록

---

## 플레이테스트 체크리스트 (에디터 작업 후)

- [ ] 선택지 3장 표시 확인
- [ ] 부족 필터링 동작 (닌자 없을 때 닌자 전용 카드 미등장)
- [ ] 가중치 확률 체감 (Legend < Normal)
- [ ] `3041` 진로 계승: 합성 후 같은 직업 유닛 등장
- [ ] `3055` 물리 마법사: 마법사 스킬 게이지 차도 공격/식량 없음
- [ ] `3056` 식충이: 스킬마다 무직 공격력 누적 증가, 식량 미생산
- [ ] `3008` 위엄: 족장만 공격력 증가, 일반 유닛 영향 없음
