---
system: boss
title: 보스·디버프 (보스 스폰·피해 적용·패턴·디버프)
keywords: 보스, boss, bossbase, bossdata, bossmanager, 보스 체력, 보스 hp, 보스 방어, 방어력, defense, 보스 패턴, 패턴, pattern, 지진, 예고, telegraph, 기절, stun, 디버프, debuff, 화상, burn, 아머, armor, 받피증, 받는 피해, 보스 등장, 도마뱀, lizard, 위협, 가시방벽, 잠복
verified: 2026-10-30
commit: de2cbd00
---
# 보스·디버프 카드

**한 줄 요약**: 한 번에 보스 1마리. `BossManager`가 소환하고 디버프·피해 집계를 연결한다. 모든 피해는 `BossBase`에서 방어력·디버프·영구 강화를 반영해 고정소수 HP에서 차감된다. 디버프는 보스가 소유한 `DebuffController`가 전투 시간 기준으로 관리한다.

## 2026-10-30 정적 대조
새 디버프의 최초 생성은 DebuffController.OnStarted → BossBase.OnDebuffStarted → BossManager → StatusPopupService.ShowDebuff로 표시한다. 중첩·재부여에는 시작 이벤트를 다시 보내지 않는다. HardIngameScene은 토템 보관함의 null 닫기 버튼 override를 제거했다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. 소환: `WaveManager` → `BossManager.TrySpawnSingleBoss`(무한: 라운드 성장 `RuntimeBossStats`, BossData는 바꾸지 않음) 또는 `BossManager.SpawnSingleBoss`(유한) → 프리팹 생성 → `BossBase.ConfigureDebuffs`·`BossBase.ConfigureDamageLedger`.
   Init으로 HP 확정 직후 씬 AccountExperienceRun이 최고 HP 감소 비율을 추적한다. UI/사망 정리보다 먼저 연결되며 전투 EXP와 별도다([계정 경험치](account-progression.md), 현재 미커밋 구현).
2. 피해: 유닛·드론·토템 → `BossBase.TakeDamage` → 디버프 시간 진행 → (무적·전투 유예 아니면) 영구 강화 보스 피해% → `DamageCalculator.Calculate`(방어 × (1−방어 감소), 아머 계수, 받피증) → HP 차감 → `OnDeath`면 처치 콜백.
3. 연속 공격 재현: 같은 피해를 여러 번 쓰는 경우 `BossBase.CalculateFinalDamageUnits`로 한 번 계산하고 `BossBase.ApplyRecordedDamage`로 재현한다(그림자 토템 등).
4. 디버프: 스킬의 `DebuffBinding`(ID·발동 시점·스택) → `DebuffCatalog.TryGet` → `DebuffController.Apply`. 화상 틱 피해는 `BossDamageKind.Burn`으로 같은 차감 경로를 탄다.
5. 패턴: `BossPatternController.RegisterBoss`가 보스 프리팹의 `BossPatternData` 쿨타임·우선순위로 한 번에 한 패턴만 시전한다(예: 지진 → 칸 예고 → 판정 → 유닛 기절). 예고 중 족장 스킬이 발동하면 `BossPatternController.TryCounterTelegraphedPatterns`로 취소된다.
   새 NORMAL의 쉬운 보스는 패턴을 등록하지 않는다. HP/방어/줄 수는 필수 뒤끝 난도 진행 스냅샷이 결정하고, BossData의 기존 수치는 고정 스테이지/튜토리얼/오프라인 미리보기에 남는다. NORMAL 전투 EXP도 뒤끝 표를 사용하며 SO 대체는 없다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/HSD/Scripts/UI/Boss/UI_BossHpShield.cs` | BossBase 보호막 이벤트를 HP 줄 위 띠로 표시, 줄당 HP와 즉시 정리 경계 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossBase.cs` | 보스 기반. HP·피해 적용·디버프 소유·이벤트(`OnHpChanged`/`OnDamaged`/`OnShieldChanged`/`OnDeath`)·무적·보호막(`AddShield`, 최종 피해를 HP보다 먼저 흡수. EXP는 HP 감소분만) |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossNormal.cs` | 일반 보스 구현 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossManager.cs` | 단일 소환·현재 보스 참조·디버프/딜표 연결 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossData.cs` | 보스 외형/표시와 고정 스테이지 수치·기존 전투 EXP 호환. 계정 EXP와 별개 |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/RuntimeBossStatsCalculator.cs` | 난도 HP·방어·체력줄 계산, 기존 피해당 전투 EXP 비율 보존 |
| `Assets/WorkSpace/HSD/Scripts/UI/Boss/UI_BossHpBar.cs` | 명시적 HP/줄 단위 표시와 구형 줄 수 균등 분할 호환 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossLogic/BossPatternController.cs` | 패턴 쿨타임·시전·예고·족장 카운터 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossLogic/BossPatternData.cs` | 패턴 SO(쿨타임·예고·판정·기절·흔들림) |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossSpineAnimator.cs` | 보스 Spine 애니메이션 |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BossHitReaction.cs` | 악어의 디시그망 큰 피격 자세/일반 적중 옅은 색. 기존 Spine 위 가산, 패턴 우선·정지·복원 |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BossHitReactionProfile.cs` | 피격 시각 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BossHitReactionDamage.cs` | 기존 AtkCoefficientDamage 파생형. 동일 피해 뒤 실제 HP 감소 시에만 큰 자세 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossLogic/IBossPatternRuntime.cs` | 보스 전용 지속 효과의 전투 시계·정리 계약 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/Lizard/BossLizard.cs` | 도마뱀 위협·가시방벽·잠복 동작. 공통 스케줄러에 등록 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/Lizard/LizardPatternData.cs` | 확정 수치와 실제 Spine 클립/진입·복귀 구간. 효과 지속시간으로 시전 길이 계산 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/Lizard/LizardBossPresentation.cs` | 전투 시계로 Spine 자세 유지·복귀 및 단일 메시 피드백 |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/CombatHealth.cs` | 고정소수 HP·회복 상한 및 사망 후 회복 거부 |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/DebuffController.cs` | 대상 1개의 디버프 적용·조회·만료(전투 시간 기준) |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/DebuffCatalog.cs` | 디버프 정의 조회(루트 등록, 검증 후 원자적 교체) |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/DebuffBinding.cs` | 스킬·토템이 거는 디버프 연결(ID·시점·스택) |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/DebuffKind.cs` | 종류: 화상·아머 브레이크·받피증 |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/DebuffSettings.cs` | 정의 목록 SO(시트 GID는 비어 있으면 SO만 사용) |
| `Assets/WorkSpace/USW/Scripts/Combat/Debuff/BurnDebuff.cs` | 화상 틱 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DebuffInfoPresenter.cs` | 보스 디버프 정보 표시 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/BossData/*.asset` | 라운드별 BossData |
| `Assets/WorkSpace/USW/Data/Endless/Bosses/Breather_DummyBoss01.asset` | NORMAL 쉬움 전용 DummyBoss01. 기존 고정 스테이지 R01의 악어 외형을 양쪽 풀에 중복시키지 않음 |
| `Assets/WorkSpace/USW/Data/Endless/Progression/NORMAL_BossProgression.asset` | 오프라인 미리보기 프로필. NORMAL의 운영 설정은 뒤끝 필수 |
| `Assets/WorkSpace/USW/Data/BossData/Crocodile/*.asset` | 악어 보스·지진 패턴·기절 연출 |
| `Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocusHitReaction.asset` | 승인된 Lab과 동일한 큰 피격/옅은 색 수치. Crocus 프리팹에 연결 |
| `Assets/WorkSpace/USW/Data/BossData/Lizard/*.asset` | 도마뱀 표시/오프라인 HP·EXP 및 3패턴 SO |
| `Assets/WorkSpace/USW/Prefab/BossUnit/Lizard.prefab` | 실제 도마뱀 Spine + 몸통/손·가시 깊이 분리 + 전용 패턴 연결 |
| `Assets/WorkSpace/USW/Prefab/BossUnit/Crocus.prefab` | 악어 Spine. 시각 자식 크기0.70/Y−2.89, 몸통 Default/−10·손 Unit/0 |
| `Assets/WorkSpace/USW/Prefab/BossUnit/DummyBoss*.prefab` | NORMAL 더미8종. Default/−10으로 전장 뒤, anchoredY −0.65(02 −1.4 / 07 −1.0). 참고 이미지처럼 하단 가림 |
| `Assets/WorkSpace/USW/Scripts/Editor/BossBalance/BossBalanceRegistry.asset` | 관리용 보스/프리팹 키 등록. 도마뱀 `LizardBossData` / `Lizard` 포함 |
| `Assets/WorkSpace/USW/Resources/DebuffSettings.asset` | 디버프 설정(루트 스코프 주입) |
| `Assets/WorkSpace/USW/Resources/Debuffs/*.asset` | 화상·아머 브레이크·받피증 정의 |

운영 보스 분류는 뒤끝 `BreatherBossKeys`/`HardBossKeys`가 원본이다. 10-21 운영 설정 저작 Excel은 어려움 악어·도마뱀2종/쉬움8종이다. Claude가 LizardBossData와 전투 EXP0.01을 추가했으며 뒤끝 콘솔 업로드·적용은 사용자 확인 대기다. 카드 갱신에서 서버를 조회하거나 수정하지 않았다. 도마뱀은 사용자 확정으로 **어려운 보스**이며 설치 등록표 `DEFAULT_HARD` 및 관리용 보스/프리팹 등록표에 연결했다. 서버 NORMAL 등장은 `HardBossKeys`와 `CombatExpPerHp`의 같은 키 등록/적용이 별도로 필요하다. SO 풀은 설치된 키/프리팹을 해석하는 등록표와 오프라인 미리보기에 남는다. 구형 시뮬레이터 스냅샷은 이전4종 어려움 구성을 담을 수 있다.

악어/도마뱀의 크기·턱 접점은 [보스 시각 구도 원본](../../technical/boss-visual-composition.md)을 따른다. 도마뱀 시각 자식은1.12/Y−3.41. 두 보스는 몸통 뒤·손 앞을 분리하며, 프리팹 루트/Boss_Point/전장·그리드 위치는 유지한다. 도마뱀 생성 도구에도 같은 값을 반영했다.

## 불변 규칙·함정
- 10-28 정적 대조: `BossBase`는 방어·피해 보정 후 보호막을 먼저 소모한다. 피해 표시/딜표는 보호막 흡수분도 포함하지만 OnDamaged(전투 EXP)는 실제 HP 감소분만 전달한다. HP 줄은 원격 HpPerLine이 있으면 그 절대 단위를 사용한다. 이번 문서 대조에서 Unity/운영 서버를 재실행하지 않았다.
- 피해는 반드시 `BossBase`의 경로로 넣는다. HP를 직접 바꾸지 않는다. 반올림은 최종 경계에서만 한다.
- 무한모드 성장값은 `RuntimeBossStats`로만 전달한다. BossData SO를 런타임에 수정하지 않는다.
- 디버프 만료는 실제 시간이 아니라 **전투 시간**(`CombatCountdown`) 기준이다. 선택 화면 정지 중에는 흐르지 않는다.
- 선택 화면 유예 중에는 실제 발사된 투사체의 착탄 콜백 안에서만 피해·디버프가 허용된다.
- 같은 종류·중첩 그룹은 정의 하나만 허용한다(`DebuffCatalog.Validate`). 다른 정의가 같은 슬롯을 차지하려 하면 거부된다.
- 토템 공격 디버프는 토템이 겹쳐도 공격 시도당 1스택([토템 카드](totem.md)).
- 레거시: `DebuffSettings`에 시트 GID가 있으면 `GameDataManager`가 CSV로 정의를 교체한다. 현재 비어 있어 SO만 쓴다. 유한 스테이지 보스 HP·EXP도 시트 경로가 남아 있다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/DummyBossCompositionAuthoring.cs` | 더미8종 전장 뒤 배치 저작·전후 렌더·실제 BossManager 소환/애니메이션 확인 |
| `Assets/WorkSpace/USW/Scripts/Editor/BossDebuffUiChecks.cs` | 보스 디버프 UI |
| `Assets/WorkSpace/USW/Scripts/Editor/DebuffDataValidator.cs` | 디버프 SO 저작 검사 |
| `Tools/verification/BurnTuningChecks.cs` | 화상 튜닝 |
| `Tools/verification/EndlessBossSpawnChecks.cs` | 실제 스폰/TakeDamage·체력줄의 격리 Edit 검사 |
| `Tools/verification/EndlessBossRefactorChecks.cs` | 공통 수치/EXP/셔플/원본 보존 검사 |
| `Tools/verification/LizardBossAuthoring.cs` | Spine 조사·포즈 렌더·패턴/프리팹/오프라인 씬 저작 |
| `Tools/verification/LizardBossChecks.cs` | 실제 객체로 시전 순서·피해·회복·약화·일시정지·씬 보존 검사 |
| `Tools/verification/LizardBossLiveChecks.cs` | 실제 PlayLoop·연출/복귀·정리 검사, Game view 캡처 및 원래 씬 복원 |
| `Assets/WorkSpace/USW/Scene/BossLab_Lizard.unity` | 계정/서버와 독립된 도마뱀 Play 확인 씬 |
| `Assets/WorkSpace/USW/Scripts/VerificationHarnesses/LizardBossLab.cs` | 확인 씬 연결 및 시간/HP/정지/리셋 조작. 생산 카메라 대신 명시적 확인 씬 카메라 사용 |
| `Assets/WorkSpace/USW/Scripts/VerificationHarnesses/LizardPreviewUnit.cs` | 확인 씬 전용 비공격 유닛, 실제 스탯 보정 경로 사용 |

## 도마뱀 — 루트 구현 (2026-10-20 작업 트리)

확정 규칙은 `design/quick-specs/lizard-patterns-2026-10-20.md`. 위협은 20초마다 전체 4줄 유닛의 **일반 공격 빈도만** 5초간 40% 감소시킨다(스킬 충전/생산/공격력 불변). 가시방벽은 HP70%에서 1회, 10초간 받는 피해20%감소 + 최대HP10% 보호막(깨질 때까지, 10-22). 유지 구간은 원본 0.7~1.3초 반복+꼬리/목도리/눈 idle 겹침, 손·가시는 전장 뒤 Default/−9로 그리드를 가리지 않는다. 잠복은 HP30%에서 1회, 공격 대상 제외·무적3초 고정 및 최대HP10% 회복(1초 시점), 이후 실제 복귀 프레임을 이어 재생한다. 포효/먹이 없음.

공통 `BossPatternController.AdvanceCombat`가 `IBossPatternRuntime.AdvancePatternEffects`를 같은 전투 시계로 호출한다. 우선순위는 방벽→잠복→위협, 진행 중 시전은 끊지 않는다. `LizardPatternData.CastDuration`은 효과 지속시간과 실제 Spine 복귀 길이로 계산해 중복 숫자 튜닝을 피한다. 도마뱀은 기존 `BossSpineAnimator`와 함께 붙이지 않는다(중복 시계 금지). `BossBase.IsTargetable`로 잠복 대상 선택/착탄을 차단하며 회복은 `BossBase.Heal`로 HP 이벤트만 보낸다. 이미 계산한 재현 피해에는 방어·방벽을 두 번 적용하지 않는다.

프리팹은 `Spine_Boss_Lizard_SkeletonData`의 `skill_01/02/03`과 `idle`을 사용한다. 하늘 Default/-20, 몸통 Default/-10, 전장 Default/0이며 **방벽 손·가시는 Default/-9**로 그리드 뒤에 둔다. 다른 패턴의 부위 정렬은 LizardBossPresentation의 상태별 전환을 따른다. 설치 등록 키는 `LizardBossData`다. NORMAL 후보는 원격 HardBossKeys/CombatExpPerHp 검증을 통과해야 한다. 10-20의 서버 미확인 기록 이후 10-22 조회 증거는 [게임 진행 카드](game-flow.md)의 적용269715 기록을 따른다. 이번에 라이브 적용 상태를 새로 조회한 것은 아니다. 보스 구현 당시 결과는 `outputs/lizard-patterns-20261020/RESULT.md`.

## 악어 HP70/30 랜덤 패턴·HardIngameScene — Develop 적용 (2026-10-22)

사용자 승인으로 실험실 공용8기믹을 Develop에 이관했다. 악어의 HP70%/30%에서 각1회 skill2~6 중 하나를 무작위 선택하고 기믹도8종에서 선택한다. skill1 지진/도마뱀 기존3패턴은 유지한다. 동시 구간 통과는 순차 대기, 회복/카운터 뒤 재발동 없음. 아래 실험실 미반영 기록은 이 후속 이전의 역사이며 종지기 자체는 이번 이관 대상이 아니다.

10-22 후속 미커밋: 로비 Normal/테스트 선택, 양쪽 기존 NORMAL 뒤끝 밸런스·최고 기록/스태미나·계정 EXP 공유. 테스트는 악어/도마뱀 교대, Editor 직접 Play만 무료 복제 프로필. 원격 역할·EXP에 해당 보스가 없으면 차감 전 차단한다. [현재 통합 결과](../../../outputs/mode-integration-20261022/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Assets/Scenes/HardIngameScene.unity` | 실제 인게임 복사 씬. 검사 UI 없이 악어·도마뱀만 무한 순환 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossLogic/PlacementBossPhaseData.cs` | 여러 스킬이 공유하는 HP 구간 SO |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossLogic/PlacementBossPatternData.cs` | 스킬 후보 기믹 무작위 선택/Spine 길이 연결 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/PatternLabDriver.cs` | 스케줄러가 전투 시계를 소유하는8종 판정/정리 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/PatternLabData.cs` | 기믹별 예고·대응·페널티 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/PatternLabGeometry.cs` | 선/사각/화살표/마름모 예고 메시 |
| `Assets/WorkSpace/USW/Data/BossPatterns/Hard/*.asset` | HP70/30·skill2~6·전용씬 모드/예고 재질 |
| `Assets/WorkSpace/USW/Data/BossPatterns/Library/*.asset` | 공용8기믹 수치·목록 |
| `Assets/WorkSpace/USW/Scripts/VerificationHarnesses/HardIngameEntry.cs` | Editor 직접 진입만 연구0/무료·보상 없음 준비. 로비 유료 진입에는 적용하지 않음 |
| `Assets/WorkSpace/USW/Shaders/BossTelegraph.shader` | URP 예고 메시 셰이더 |
| `tools/verification/HardIngameAuthoring.cs` | Root 최신 씬/프리팹에 저작 |
| `tools/verification/HardIngameChecks.cs` | 실제 Play67·구간/무작위/보존 검사 |
| `tools/verification/HardPatternLibraryChecks.cs` | 기존8기믹76 검사 |
| `tools/verification/HardIngameReview.cs` | 씬/UI/스크립트 보존 확인 |
| `docs/technical/hard-boss-patterns.md` | 확정 동작·이관/커밋 범위·검증 한계 원본 |

Play67+76 PASS/Unity 컴파일0/인덱스1015소스 컴파일0. 전체 드론 회귀는 기존9109 기대값1건 실패이며 PASS로 간주하지 않는다. 새 씬 후보는 SO직접연결이며 NORMAL 서버 HardBossKeys/CombatExpPerHp 적용 상태와 별개다. 상세 근거/한계는 [구현 원본](../../technical/hard-boss-patterns.md).

## 실험실 unity-ui의 추가 구현 (2026-10-19, HEAD 61c52805 + 미커밋)

**루트 후속 적용(2026-10-20):** 사용자 승인으로 디시그망 큰 자세+일반 적중 옅은 색을 Develop의 Crocus 프리팹/DisigmanSkill에 이관했다. IngameScene 실제 소환/투사체15검사·현재 Presenter 표시 PASS, 다른 보스/족장 지속 버팀은 이관 대상이 아니다. 자세한 경계·기존 회귀 실패는 [현재 원본](../../technical/boss-hit-reaction.md), [결과](../../../outputs/boss-hit-production-20261020/RESULT.md). 아래 Lab 미반영 기록은 이 후속 이전의 역사다.

2026-10-20 사용자 요청으로 피격 실험실에 일반 적중의 옅은 붉은 기를 추가했다. 실제 양수 피해(OnDamageDealt, 화상 제외)에서22% 혼합/.16초 복귀, 최소.2초 간격으로 연타 누적을 막는다. 기존 디시그망/족장 큰 자세 경로는 유지하며 본게임에는 아직 미반영이다. 실제 일반 공격/디시그망 적중 및 색 복원·정지 등16검사 PASS. 족장 재발동/Android는 미검증. 최종 FxLab Edit/clean. [결과·참조·비교 캡처](../../../outputs/boss-hit-tint-20261020/RESULT.md).

후속 수정: `unity-ui/Assets/WorkSpace/USW/Scene/FxLab_BossHitReaction.unity`도 새 전장001/하늘002로 맞췄다. 기존 불투명 JPG가 새 악어의 Default/−10 몸통을 덮어 손만 보이던 문제를 실제 Play에서 재현/수정했다. 카메라/전투/피격 수치와 기존 다른 씬은 보존, 실제 적중5/반응5 확인. [수정 전후·원문 백업·검사](../../../outputs/boss-hit-visibility-20261020/RESULT.md).

2026-10-20 사용자 요청으로 루트 Develop의 최신 악어/도마뱀/새 IngameScene 배경을 실험실에도 동기화했다(63파일, 미커밋). 실험실 `BossPatternController`/`BossPatternData`에는 루트의 도마뱀 runtime/시전 길이 경계와 기존 종지기 범위 예고를 함께 유지한다. 실험실 시작/UI 연결·기존 다른 보스 풀 항목을 보존하고 도마뱀 설치 등록만 추가했다. 루트의 새 직접 Play 오프라인 진입/뒤끝 운영 정책 전체를 이식한 것은 아니다. 실험실 컴파일 오류0, 도마뱀46+Play8/종지기57/배치기믹76 PASS. 실험실 IngameScene Edit/clean. [동기화 결과·파일·보존 검증](../../../outputs/ingame-worktree-sync-20261020/RESULT.md).

루트 Develop의 기존 등록 경로와 별도로, 실험실에 종지기 보스와 실제 인게임 복사 조우 씬을 저작했다. 실험실 컨트롤러는 범위 예고를 설정한 패턴에 한해 시전 시작 때 칸을 고정하며, 안쪽부터 차오르는 원형 예고 → 첫 판정 다음 프레임 → 잔여 효과 만료로 진행한다. 종 파동은 위에서 아래로 각 줄을 0.65초 간격으로 판정한다. 첫 판정 전 족장 카운터·선택 유예 중 시전 정지·사망/제거 시 잔여 효과 정리를 지원한다. 기존 예고 설정이 없는 지진은 전체 칸 경고/기절 경로를 유지한다.

신규 보스 수치는 SO로 저작했으며 기존 라운드/무한 풀은 교체하지 않았다. 씬·부위 PNG·프리팹·신규 코드·검증 도구는 아직 실험실에만 있어 카드 표에는 등록하지 않았다(미등록 참조). 세부 설계와 결과는 실험실의 `design/boss-bellkeeper.md`, `outputs/bellkeeper-20261018/RESULT.md`, `outputs/bellkeeper-20261018/REFERENCES.md`에 있다. 루트 반영 시 진입 파일 표/기준 커밋도 갱신한다.

후속 실험실에는 원형 3패턴을 유지하고 보스 외형과 독립적인 8개 배치 기믹(거리로 끊는 사슬, 안전 틈이 있는 이동 벽, 폭풍 피난처, 이동 폭탄, 추적 후 고정 포격, 기억 발판, 빈칸 점령, 이동 금지 신호)을 추가했다. 선/사각/화살표/마름모 예고를 사용하며 3개는 첫 판정 전 족장 취소, 5개는 고유 배치 행동으로 해결한다. 새 비교 씬만 기존 등록을 해제해 두 시전 경로가 겹치지 않는다. 신규 드라이버는 명시적 Configure로 씬 의존성을 받는다. 76개 실제 객체 검증 PASS. 세부 내용은 실험실 `design/boss-pattern-library.md`, `outputs/pattern-library-20261019/RESULT.md` 및 REFERENCES.md. 루트 미반영이므로 신규 파일 표 등록은 보류한다.

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/boss-system.md` | 보스 GDD |
| `docs/technical/debuff-system-design.md` | 디버프 설계 |
| `docs/technical/damage-formula-reference.md` | 방어·받피증 공식 |
