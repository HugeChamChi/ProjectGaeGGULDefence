---
system: combat
title: 공격·스킬·생산 (유닛 전투 루프·피해 계산·투사체)
keywords: 기본 공격, 스킬, skill, 스킬 게이지, 마나, 피해, 데미지, damage, 피해 계산, 치명, crit, 공격력, 공격 속도, 공속, 투사체, projectile, 발사체, 식량 생산, food, unitbase, unitcombat, unitstats, unitdata, skilldata, iskillaction, ieffect, 패시브, passive, 스탯 버프, buffmanager, 개구리, frog, 디시그망, disigman, 딜 그래프, 딜그래프, 하단 HUD, 스와이프, 딜표
verified: 2026-10-30
commit: de2cbd00
---
# 공격·스킬·생산 카드

**한 줄 요약**: 모든 유닛은 `UnitBase` + 세 컴포넌트(전투 루프 `UnitCombatComponent`, 수치 `UnitStatsModifier`, 식량 `UnitResourceComponent`)로 돈다. 기본 공격·스킬의 동작은 `UnitData`의 `SkillData`(행동 `ISkillAction` + 적중 효과 `IEffect`)로 조립하고, 배치·제거 같은 생애 주기 동작만 `UnitBase` 훅을 재정의한다.

## 2026-10-30 정적 대조
UnitDependencies가 StatusPopupService를 전달하고 UnitBuffVisualController는 봉인되지 않은 유닛의 새 공격/공속 효과에만 문구를 표시한다. 튜토리얼 토템 선택 복귀 안내도 SwipeRight로 맞춰졌다. 전투 계산 변경은 없다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
10-22 미커밋 입력 보정: HUD 버튼에는 별도 드래그 어댑터를 붙여 부모 ScrollRect의 10px 자동 클릭 취소를 피한다. SO의 시작폭3%·가로/세로1.5배부터 명확한 가로 전환으로 수락한다. 소환 시점/연출은 그대로이며 실제 소환5회·연타12회·그래프 왕복 포함 Play52검사 PASS. [결과·실기기 미검증 경계](../../../outputs/sortie-tap-fix-20261022/RESULT.md).

1. 배치 `UnitBase.OnPlaced` → `UnitCombatComponent.StartLoops` → 제어 루프가 매 프레임 스킬 준비(`UnitBase.IsSkillChargeReady`)를 먼저 보고, 아니면 공격 간격이 찼을 때 기본 공격.
2. 기본 공격 `ExecuteAttack`: 칸 공격 불가 확인 → `TotemBuffManager.ApplyAttackDebuff` → `basicAttackData.action.Execute(...)`(없으면 기본 투사체) → `UnitBase.InvokeOnAttack` → 선택지 보너스 공격.
3. 스킬 `ExecuteSkill`: `UnitBase.InvokeOnSkillFull`(공통 `DebuffBinding` 적용 → 가상 `OnSkillFull` 훅) → `skillData.action.Execute(...)` → 선택지 버스트/추가 공격.
4. 피해 수치 `UnitStatsModifier.ComputeDamage...`: (기본+고정 보너스) × (1+칸 공격%) × 칸 디버프 × 토템 보정 × 행 보정 × 투사체 크기 보너스 × 버스트 → 편차 → 치명(선택지+칸+영구 강화) → 영구 강화 공격% → 런 패널티 공격 배율 → 정수화.
5. 보스 쪽 `BossBase.TakeDamage`가 방어력/디버프/패턴 보정을 적용한 뒤 보호막→HP 순으로 소모한다. 기록 피해의 재생 경로는 방어를 다시 적용하지 않는다. 보호막 흡수분은 딜표에 포함하되 전투 EXP는 실제 HP 감소분만 받는다.
6. 투사체 `UnitCombatComponent.LaunchProjectile` → `ProjectilePool` → `Projectile`(이동 방식 `IMovement`: 직선·베지어·유도). 일반 식량은 `UnitResourceComponent.TickFoodProduction`, 젤탕 묶음 지급은 같은 컴포넌트의 `TickBatchedFoodProduction`이 누적액·시계를 소유한다. `UnitBase.UsesExternalFoodPayout`이 켜지면 일반 틱은 이중 지급하지 않는다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/IngameUI/TimerBonusLab/TimerBonusHack.cs` | 디시그망 시간 회복 타이머 글리치·회복량 표시. 인게임 하단 HackBar는 숨김 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/IngameDamageChart.cs` | 공통 옵션 프리팹의 실시간 하단 딜 그래프와 결과 확정 팝업, 집계·행 뷰 공유 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/IngameHudDamagePager.cs` | 네 전투 버튼과 딜표 왕복 전환. 식량은 고정, 메뉴는 그래프 우측 상단 중첩 방지로 그래프 페이지에서 숨김 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/IngameHudMotion.cs` | 선택지/판매의 소유자별 공용 HUD 수납·복원, 페이지 전환과 소유권 연동 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/HudDamageSwipeArea.cs` | 가로 페이지 전환·세로 목록 방향 분리, 드래그 클릭 취소 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/HudDamageButtonGesture.cs` | 버튼 자체에서 짧은 흔들림·세로 이동의 클릭 유지. HUD폭3%·가로/세로1.5배부터 페이지 드래그로 인계 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/PressToClickButton.cs` | 소환 버튼 press 즉시 클릭(10-22). 하단 HUD는 좌우 어느 방향이든 12% 밀면 토글, 나가는 페이지 축소·페이드·정착 튐(`DamageChartSettings` Hud* 값) |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/DamageContributionRowView.cs` | 하단은 초상·피해·막대만, 결과 팝업은 기존 이름·DPS 복원 |
| `Assets/WorkSpace/USW/Tutorial/Scripts/IngameTutorialDirector.cs` | OptionsMenu 수업에서 실제 HUD 왼쪽/오른쪽 스와이프를 기다린 뒤 기존 메뉴 안내 |
| `Assets/WorkSpace/USW/Tutorial/Scripts/IngameTutorialOverlay.cs` | 기존 손가락 안내에 오른쪽 스와이프 제스처 추가, enum 기존 값 보존 |
| `Assets/WorkSpace/USW/Scripts/Combat/Visual/SpineAttackSpeedSync.cs` | G04 공용 Spine 속도. 최종 공격 간격 기반 공격/대기 트랙, 공격 가속 상한2.5, 기절 시계 우선 |
| `Assets/WorkSpace/USW/Scripts/Drone/SpineActorVisual.cs` | 유닛/전투 드론의 일반 Spine 모션·속도 연결. start/loop/end를 한 공격 주기로 계산 |
| `Assets/WorkSpace/USW/Scripts/Unit/AtkCoefficientDamage.cs` | 기존 공격력 계수 피해·그림자 재현. Apply/Capture는 시각 파생형을 허용하는 virtual API |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BossHitReactionDamage.cs` | 디시그망 스킬의 동일 계수 피해 뒤 실제 HP 감소 시 악어 큰 자세 요청 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitBase.cs` | 유닛 기반. 배치/제거, 임시 등급, 기절, 훅(`OnUnitPlaced`/`OnUnitRemoved`/`OnSkillFull`/`OnInitialized`), 표시용 수치 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitCombatComponent.cs` | **공용** 공격/스킬 루프와 투사체 발사. 공통 동작이 아니면 고치지 않는다 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitStatsModifier.cs` | 공격력·치명·공격/스킬 간격의 최종 계산 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitResourceComponent.cs` | 식량 생산 틱 |
| `Assets/WorkSpace/USW/Scripts/Effects/CurrencyFloaterManager.cs` | 생산 식량 합산 → 하단 보유량 위 획득 팝업, 정수도 `+1.0` 형식 |
| `Assets/WorkSpace/USW/Scripts/Effects/CurrencyFloater.cs` | 식량 팝업 아이콘·텍스트 애니메이션과 풀 반환 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitDependencies.cs` | 유닛에 넘기는 씬 서비스 묶음(팩토리가 채움) |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitData.cs` | 유닛 SO: 등급별 수치, `basicAttackData`/`skillData`, `passive` |
| `Assets/WorkSpace/USW/Scripts/Unit/SkillData.cs` | 행동 + 적중 효과 + 추가 효과 + 시전 연출 |
| `Assets/WorkSpace/USW/Scripts/Unit/ISkillAction.cs` | 행동 인터페이스(`MultiShotSkillAction`, `BuffAllyInRangeSkillAction` 등) |
| `Assets/WorkSpace/USW/Scripts/Unit/IEffect.cs` | 적중 효과 인터페이스(`AtkCoefficientDamage`, `FixedDamage` 등) |
| `Assets/WorkSpace/USW/Scripts/Unit/PassiveData.cs` | 패시브(자기/파티 효과) |
| `Assets/WorkSpace/USW/Scripts/Unit/DamageCalculator.cs` | 편차, 방어력 적용, 반올림 |
| `Assets/WorkSpace/USW/Scripts/Unit/Disigman.cs` | 디시그망. 자기 기본 공격으로 충전해 시간 회복 |
| `Assets/WorkSpace/USW/Scripts/Combat/Projectile.cs` | 투사체(풀 객체) |
| `Assets/WorkSpace/USW/Scripts/Combat/ProjectilePool.cs` | 투사체 풀 |
| `Assets/WorkSpace/USW/Scripts/Combat/CombatSettings.cs` | 피해 편차·치명 피해 편차 등 전투 공용 값 |
| `Assets/WorkSpace/HSD/Scripts/Buff/BuffManager.cs` | 유닛 스탯 버프(배율·스택 정책) |
| `Assets/WorkSpace/HSD/Scripts/UI/Unit/UI_UnitInfoPresenter.cs` | 인게임 강화 전후 동일 런타임 계산으로 기본+초록 증가분, 공속은 초당 횟수, 젤탕은 드론수/자체+군단 식량 |
| `Assets/WorkSpace/HSD/Scripts/UI/Unit/UI_UnitInfoPanel.cs` | 두 기존 칸 아이콘 전환/복원·한 줄 수치·기존 정보창 추적 |
| `Assets/WorkSpace/HSD/Scripts/Data/UI/UnitStatPalette.cs` | 기존 enum 번호 보존, DroneCount/FoodPerSecond 아이콘 구분 |
| `Assets/WorkSpace/USW/Scripts/FrogUnits/*.cs` | 개구리 영웅(빈 클래스, 데이터 조립형) |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Prefab/UI/IngameOptionsMenu.prefab` | NORMAL·테스트·튜토리얼의 공통 메뉴·IngameDamageChart 컴포넌트 |
| `Assets/WorkSpace/USW/Resources/DamageChartSettings.asset` | 최근 DPS5초, HUD높이400/3행/글자1.35/전환0.22초/폭12% |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DamageChartLab/DamageChartLabSettings.cs` | 실험실·런타임 딜 그래프 SO 필드 |
| `Assets/WorkSpace/USW/Tutorial/Data/IngameTutorialSettings.asset` | 하단 그래프 왕복 스와이프 안내 문구 |
| `Assets/WorkSpace/USW/Tutorial/Scripts/IngameTutorialSettings.cs` | 튜토리얼 안내 문구 SO 스키마 |
| `Assets/Imports/GGD_ArtWork/JSY_Artwork/Icon/SRP_TutorialPointer01.png.meta` | 기존 튜토리얼 손 이미지 Sprite Single 임포트. 일반 텍스처이면 저작 참조가 null |
| `Assets/Imports/GGD_ArtWork/JSY_Artwork/Icon/SRP_TutorialPointer02.png.meta` | 눌린 손 이미지 Sprite Single 임포트 |
| `Assets/WorkSpace/USW/Data/DroneUnits/UnitData_*.asset` | 드론 계열·디시그망 UnitData |
| `Assets/WorkSpace/USW/Data/DroneUnits/Disigman*.asset` | 디시그망 공격·스킬 SkillData |
| `Assets/WorkSpace/USW/Data/HeroUnits/*.asset` | 개구리 영웅 UnitData |
| `Assets/WorkSpace/HSD/Data/Skill/*.asset` | 개구리 영웅 SkillData |
| `Assets/WorkSpace/HSD/Resources/Data/UnitStatPalette.asset` | 드론/초당 식량 아이콘 원본. 기존 FoodPerTick 별 아이콘은 보존 |
| `Assets/WorkSpace/HSD/Prefab/CurrencyTextPrefab.prefab` | Addressables 식량 팝업. Icon의 Sprite 참조가 있어야 흰 사각형이 뜨지 않음 |
| `Assets/Imports/GGD_ArtWork/JSY_Artwork/Icon/SRP_FoodIcon03.png.meta` | 식량 이미지의 Sprite Single 임포트 설정 |

## 불변 규칙·함정
- 10-28 정적 대조: 튜토리얼 HUD 왕복도 실제 IngameHudDamagePager를 기다린다. 복귀 손가락 안내는 SwipeRight이며 하드/실기기 전체 Play를 이번에 재실행하지 않았다. 기준 커밋과 현재 미커밋 D12 변경의 구분은 [대조 기록](../../../outputs/harness-refresh-20261028/RESULT.md)을 따른다.
- 하단 딜 그래프는 공통 옵션 프리팹에서 실행 중 연결한다. 씬의 네 버튼만 마스크 안으로 옮기며 설정·식량·판매 영역은 그대로다. 하단은 정적 초상/이름 숨김, 가로 왕복·세로 스크롤이다. 집계/정지/피해 입력을 변경하지 않는다. 결과 팝업은 확정 집계와 기존 이름 표시를 유지한다. [현재 원본](../../technical/damage-chart.md).
- G04(10-21 미커밋): 본체는 최종 공격 간격, 전투 드론은 EffectiveAttackInterval을 읽는다. 기본보다 느리면 공격/대기 모두 B/I, 빠르면 대기1·공격 clamp(L/I,1,2.5). 기절은 timeScale0 우선, 선택/드래그는 기존 연출 속도. 스킬/발사/전투 수치/이동은 변경하지 않는다. 기본부터 상한인 모션이 있으므로 [원본과 실측표](../../technical/spine-attack-speed.md)를 확인한다.
- 도마뱀 위협은 `UnitBase.PatternAttackSpeedMultiplier`로 일반 공격 빈도만 조정한다. 스킬 충전·생산에 이 배율을 섞지 않는다. 잠복 중 보스는 IsTargetable=false여서 새 공격/드론 시전을 보류한다. 이 보스 작업은 현재 별도 미커밋 변경이다.
- 디시그망 스킬 피해는 `BossHitReactionDamage : AtkCoefficientDamage`이며 기존 계수3/난수/치명/재현/정보창 조회를 보존한다. 실제 HP 감소 뒤 Crocus 프리팹의 `BossHitReaction`만 큰 자세를 시작한다. 일반 적중 색은 보스 피해 이벤트에서 별도 처리한다. [현재 원본](../../technical/boss-hit-reaction.md), [실제Play15·정보창/기존 회귀 한계](../../../outputs/boss-hit-production-20261020/RESULT.md).
- 확장 방법 두 가지가 공존한다. 공격·스킬 자체는 **SkillData 조립**이 기본이고, 배치·제거·소환 같은 부수 동작만 **훅 재정의**(드론 스포너 방식, [드론 카드](drone.md)). 어느 쪽도 레거시가 아니다.
- `InvokeOnSkillFull`이 공통 디버프를 이미 적용한다. `OnSkillFull` 재정의에서 같은 디버프를 다시 걸지 않는다. 같은 유닛에 SkillData 행동과 훅을 둘 다 쓰면 같은 효과가 두 번 나지 않게 한다.
- L09 기본값은 `UnitBase.GetDisplayAttackDamageBeforeUpgrade` / `UnitBase.GetDisplayAttackIntervalBeforeUpgrade`로 인게임 강화만 제외한 동일 경로다. 드론은 각 기체의 동일 반올림·군단 버프를 적용해 합산한다. UI의 초당 빈도 변환은 실제 간격/속도 상한을 반영한다. [최종 결과](../../../outputs/infinite-upgrade-info-20261020/RESULT.md).
- 표시값(`UnitBase.GetDisplayAttackDamage`, `UnitStatsModifier.GetNonCriticalAttackDamage`)은 편차·치명을 굴리지 않는다. UI는 이 런타임 계산을 쓰고 SO 원본 값만 읽지 않는다(기본 스탯 표시는 예외).
- 영구 강화(`ResearchRunBonuses`)는 전투 시작 때 고정된 값이다. 런 패널티 배율 오버플로는 0 피해 + 실행 실패 보고.
- 수치는 SO에 둔다. 코드에 매직 넘버를 넣지 않는다(기존 폴백 1.5 치명 배율 등은 선택지가 없을 때의 기본값).
- 식량 아이콘은 에디터 원본 참조 확인만으로 APK 정상 표시를 보장하지 않는다. 2026-10-17 1.0061 APK에서 팝업 `m_Sprite`가 0인 캐시 번들을 확인했고, 일반 재빌드도 재사용했다. 프로젝트 `Library/BuildCache`를 SBP API로 비운 뒤 Addressables를 재빌드하자 실제 번들의 Sprite 참조와 텍스처가 복구됐다. 수정 반영에는 새 APK가 필요하며, APK 빌드는 별도 사용자 요청이 있어야 한다.
- 족장은 유닛이 아니다. 기본 공격·스킬 루프에 연결하지 않는다([족장 카드](chieftain.md)).

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/HudDamageSwipeChecks.cs` | NORMAL·테스트 Play의 왕복/짧은·세로 드래그/실시간 갱신/초상/설정·식량 고정/최종 팝업 검증 |
| `Tools/verification/HudDamageSwipeProbe.cs` | 세 씬의 공통 옵션 프리팹·HUD 배치 읽기 전용 대조 |
| `Tools/verification/HudDamageSwipeAuthoring.cs` | 원본 PNG를 유지하며 기존 두 손 이미지의 Sprite 임포트 복구 |
| `Assets/WorkSpace/USW/Tutorial/Editor/IngameTutorialChecks.cs` | 실제 튜토리얼 진행 및 하단 그래프 왕복·메뉴 안내 검사 |
| `Tools/verification/run-regression.ps1` | 5종 현행 회귀+디시그망 실제 투사체 Play, 실패/누락/시간초과 집계와 상태 복원. [운영 원본](../../technical/regression-checks.md) |
| `Tools/verification/DisigmanL5Checks.cs` | SO→실제 수치·제어된 적중·충전/회복/UI 격리 검사. 옛 베탕 DPS 동등 가정 제거 |
| `Tools/verification/DisigmanL5PlayChecks.cs` | 원본 전용 투사체·자연시간 자동 스킬·팩토리 통합 검사 |
| `Tools/verification/SpineAttackSpeedAudit.cs` | 실제 Spine 모션 길이·2.5 상한 임계값·발사 이벤트 읽기 전용 조사 |
| `Tools/verification/SpineAttackSpeedChecks.cs` | BossLab_Lizard 위협0.6/복귀·강화/카드·상한 이하 완료·기절·일시정지/드래그/풀 재사용 |
| `Tools/verification/BurnTuningChecks.cs` | 화상 피해 튜닝 확인 |
| `Tools/verification/DamageChartChecks.cs` | 유닛/토템 피해 집계(딜표) |
| `Assets/WorkSpace/USW/Scripts/Editor/UnitInfoDisplayChecks.cs` | 유닛 정보창 표시값 = 런타임 계산 |
| `Tools/verification/FoodProductionChecks.cs` | 식량 지급·합산·팝업 텍스트·풀 재사용(에디터 AssetDatabase 경로) |
| `Tools/verification/FoodPopupSummonAudit.cs` | 저장 참조·APK BuildReport 확인, Addressables 전용 재빌드/프로젝트 SBP 캐시 복구 |
| `Tools/verification/audit_food_apk.py` | 실제 1.0061 APK 또는 생성된 Android 번들의 팝업 Sprite 포인터 검사 (`--content --expect-present`) |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/unit-combat-system.md` | 전투 GDD |
| `docs/technical/damage-formula-reference.md` | 피해 공식 전체 |
| `design/gdd/projectile-system.md` | 투사체 |
| `design/gdd/disigman-unit.md` | 디시그망·시간 회복 |
| `docs/technical/damage-chart.md` | 실전 딜표 |

## 보완 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Effects/StatusPopupService.cs` | 대상별 상태 문구/재표시 대기와 선택지/보스 디버프 매핑 서비스 |
| `Assets/WorkSpace/HSD/Scripts/Effects/StatusPopup/StatusPopupSpawner.cs` | 몸통 추적 팝업 묶음 풀과 행 추가 |
| `Assets/WorkSpace/HSD/Scripts/Effects/StatusPopup/StatusPopupCatalog.cs` | 효과별 문구/색/배율 카탈로그 SO |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/UnitBuffVisualController.cs` | 새 토템 효과의 외형과 상태 문구 진입 |
