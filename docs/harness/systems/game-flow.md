---
system: game-flow
title: 게임 진행 (상태·조우 순서·무한 런·타이머)
keywords: 게임 진행, gamemanager, 게임 상태, gamestate, 게임 시작, 게임 종료, 승리, 패배, wavemanager, 웨이브, 라운드, 조우, 무한모드, 무한 모드, endless, 런 패널티, 패널티, penalty, 타이머, timer, 남은 시간, 시간 추가, 보스 처치 보너스, gameconfig, timescale, 일시정지, 게임 속도
verified: 2026-11-04
commit: ef31f410
---

## 2026-11-04 현재 코드 대조

확인함: WaveManager는 무한 런 조우 흐름을 소유하고 튜토리얼 유한 웨이브는 TutorialWaveFlow로 위임한다. 시간 금고는 별도15초 시계로 실제 전투 경과를 발행하며 보스 전환 대기에서만 정지한다. 처치 연출/내부 fallback/연출 없는 지급은 모두 AddBossKillTime을 사용한다. 보너스 액수와 무관하게 실제 지급 뒤 제물을 결제한다. 금고 조작 제한은 구현 완료, G08은 차단 연출이고 V12는 Android 복귀 실측이다.

기준 커밋 ef31f410과 미커밋 작업 트리를 정적으로 대조했다. Unity/서버/Android 재실행 없음. 이전 날짜 설명과 충돌하면 이 항목이 우선한다. [대조 결과와 미등록 분류](../refresh-20261104.md).


11-04 결함 수정(현재): 시간 금고 유예 중 실제 전투 경과와 화상/방어감소/딜표 이벤트가 진행된다. 처치 보너스는 AddBossKillTime으로 제물을 숫자와 무관하게 정산한다. 과열 넘침 나머지는 같은 보스에서 누적, 보스 변경 시 초기화한다. 해킹 표식은 런타임 상한까지 확장하고 5스택당 한 묶음의 과열 비율을 색에 반영한다. 수리 키트는 빗나간 폭발에도 원 발사자에게 한 번 적용한다. 조작 차단 구현 완료, 차단 연출 TODO G08. [수정과 검증](../../../outputs/new-choice-fixes-20261104/RESULT.md).


11-04 시간 금고 조작 정책 확정/구현: 유예 중 월드 조작, 출격/판매/합성, 강화창/결제, 족장 발동, 토템/선택지 보관함 진입을 차단한다. 햄버거의 딜표와 설정은 유지한다. 진행 중 드래그를 취소하고 열린 강화창도 닫는다. 조작 불가 연출은 TODO G08 후속이다. 실제 씬23 PASS. [결과](../../../outputs/time-vault-input-20261104/RESULT.md).

# 게임 진행 카드

11-04 18:18 제물 후속 정정: 앞선 변경에서 `BossClearPresentation`의 실제 연출 지급과 내부 fallback 두 곳을 빠뜨렸다. 두 곳도 `AddBossKillTime`으로 연결하고 설치된 IngameScene에서 연출 충돌17초/연출 불가45초 각각 제물 한 번 정산을 검증했다. [2경로 PASS와 정정](../../../outputs/new-choice-fixes-20261104/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/IngameUI/BossClearPresentation.cs` | 실제 처치 연출 충돌 및 연출 불가 보너스 지급. 둘 다 AddBossKillTime 사용 |
| `Tools/verification/SacrificePresentationChecks.cs` | 설치된 실제 씬에서 정상 연출과 연출 불가 경로의 제물 정산 검사 |

11-04: `TimerController`의 시간 금고는 별도15초 유예 시계를 사용한다. 가속 운전은 일반 시계의 감소만1.1배로 하고 실제 전투 경과는 유지한다. 제물은 실제+30 지급 뒤-10과 런 공격력 가산을 한 번 처리한다. 금고 중 보스 처치는 보상/패널티 선택 없이 다음 라운드로 진행하며, `WaveManager`의 다음 보스 전3초에서만 유예를 정지한다. [현재 구현](../../../outputs/overdrive-four-20261104/RESULT.md).

**한 줄 요약**: 남은 시간이 곧 체력이다. `GameManager`가 상태(Idle→Playing⇄LevelUp→Win/Lose)를 소유하고, `WaveManager`가 라운드마다 보스 조우 → 처치 보너스 시간 → (어려운 구간이면) 토템 보상 → (예약됐으면) 런 패널티 → 다음 라운드를 진행한다. 런 수명·라운드 번호·패널티는 `EndlessRunService`가 가진다.

## 흐름
1. 로비 시작 → 모드 선택: Normal=`IngameScene`, 테스트=`HardIngameScene`. 두 씬은 `GameInitializer`(USW 전역 클래스)의 `StartAsync`의 준비/페이드 완료 후 자동 시작(별도 시작 버튼 없음). `GameManager.StartGame` → `WaveManager.TryPrepareRun`(모드·밸런스 스냅샷으로 `EndlessRunService.TryStart`) → 예약1회 소비·계정 EXP 시작·시작 식량 지급 → `WaveManager.StartWave`. 사용자 최종 결정으로 양쪽 모두 **기존 NORMAL 뒤끝 밸런스·기록**을 공유한다. HARD_INGAME은 테스트 보스 순서(악어/도마뱀 교대)를 담고 수치는 필수 NORMAL 스냅샷을 사용한다. 다시하기도 원격 사전 확인·예약 후 SceneChangeManager를 통해 같은 씬을 연다.
   사용자 확정(10-20): `#if UNITY_EDITOR`에서 씬 직접 Play(진행 중 씬 전환 없음)는 자동 오프라인 미리보기. 원본 NORMAL을 메모리에 복제한 `EDITOR_PREVIEW` 프로필과 로컬 SO 풀/수치를 사용한다. 별도 메뉴 없음. 강화 저장 로드·스태미나 예약 소비·계정 EXP·최고 라운드 저장 제외, 화면에 미리보기 표시. 로비 출격 및 Player의 NORMAL 원격 필수 계약은 유지한다([원본](../../technical/ingame-editor-preview.md)).
2. 조우: `WaveManager.SpawnNextBossAsync`가 첫 조우에서 타이머를 시작(`TimerController.StartTimer`), 소환 대기 동안 정지 → `BossManager.TrySpawnSingleBoss`(무한) → 타이머 재개 → 배치된 유닛을 새 보스 기준으로 다시 `OnPlaced`. 생성 연출 중 비활성 유닛은 제외하며 소환 완료 콜백이 첫 배치를 담당한다(10-23 미커밋, [재현·검증](../../../outputs/ingame-state-audit-20261023/RESULT.md)).
   현재 NORMAL은 `ShuffleFromStart`로 1라부터 난도별 셔플백을 사용한다. 입장 전 뒤끝 EndlessBalance 전체 설정을 필수 검증하고 동일 스냅샷으로 난도/라운드 HP·고정 방어·줄 수·전투 EXP를 계산한다. SO/직전값 대체는 금지하며 실패하면 스태미나 예약 전에 출격을 막는다. GrowthRateFinalized=0은 성장률 시험 중을 뜻한다.
3. 처치: `WaveManager.OnSingleBossDefeated` → `GameConfig.TryGetBossKillBonusSeconds`(역할별 보너스, 원격 스냅샷이 있으면 그 값) → 클리어 연출 중 `TimerController.AddTime`. 마지막 보스의 완료는 `WaveManager.TryCompleteRound`가 승인한 뒤에만 `DroneManager.SettleWaveFoodDividend`를 호출한다. 런/라운드 중복 정산을 막으며 보너스 시간·토템 지급과 별개다.
4. 보상: `WaveManager.GrantsTotemReward`(무한은 슬롯 역할 Hard일 때만) → `WaveManager.OnTotemSelectionRequested` → 완료 콜백 `OnRewardDone`.
5. 패널티: `EndlessRunService.OnPenaltyReserved`로 예약된 경우 `PresentPenaltyAsync`가 시간 정지 후 3택(`EndlessRunService.TryChoosePenalty`) → 연출 → 다음 보스/라운드(`EndlessRunService.TryAdvanceRound`).
6. 종료: 시간 0 → `GameManager.HandleTimeUp` → `EndGame`(딜표 확정·결과 스냅샷·계정 경험치 동결/지급·`WaveManager.StopRun`) → 패배 연출 → `UIManager.ShowResult`. 유한 스테이지는 마지막 웨이브 후 `GameManager.OnAllWavesCleared`. 계정 경험치는 [계정 성장 카드](account-progression.md)를 따른다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/CoreSystem/GameInitializer.cs` | 준비 완료 신호·전환 페이드 대기·본게임 자동 시작. 준비 예외는 기기 로그에도 명시 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/IngameEditorPreview.cs` | `UNITY_EDITOR` 전용 직접 Play 프로필 복제·오프라인 표시·씬 종료 시 복제 해제. Player에서는 제외 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/SceneChangeManager.cs` | 씬 로드 후 Preparation을 기다리고 전환 페이드 해제 |
| `Tools/verification/ModeIntegrationAuthoring.cs` | 로비 Normal/테스트 선택 UI·모드 참조·Hard 씬 Addressables 연결 저작 |
| `Tools/verification/ModeIntegrationChecks.cs` | 실제 선택 raycast·양 씬 격리 유료/EXP·공유 NORMAL 기록/24라운드 수치·재도전 실패 검사 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/GameManager.cs` | 게임 상태 머신, 시작·레벨업 상태·종료·결과 전달, 실행 실패(Faulted) 처리 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/WaveManager.cs` | 라운드·조우 단계(`EncounterPhase`), 처치 보너스·토템 보상·패널티 순서, 영구 봉인 셀 |
| `Assets/WorkSpace/USW/Tutorial/Scripts/TutorialWaveFlow.cs` | 튜토리얼 전용 유한 웨이브, 보스 소환과 보상, 마지막 웨이브 승리. WaveManager는 기존 UI/Director 이벤트를 중계 |
| `Assets/WorkSpace/USW/Scripts/Drone/DroneManager.cs` | 승인된 라운드 완료의 식량 배당 정산, 런/라운드 중복 차단 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/TimerController.cs` | 남은 시간(시작·정지·재개·추가). 시간 추가 출처(`TimeAddSource`) 이벤트 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/TimeScaleService.cs` | 게임 속도 단일 소유자. 소유자별 `Pause`/`Request`/`Release` |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/EndlessRunService.cs` | 런 ID·현재 라운드·보스 런타임 스탯·패널티 상태. 이벤트 `OnPenaltyReserved`/`OnFailed` |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/EndlessBossSchedule.cs` | 라운드별 보스 역할·풀 선택 |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/RuntimeBossStatsCalculator.cs` | 라운드 성장 → 보스 HP·공격 런타임 값 |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/RunPenaltyState.cs` | 패널티 누적(공격·공속 감소, 선택지 수 감소, 보스 방어 등) |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/EndlessBalanceService.cs` | NORMAL 원격 튜닝(뒤끝 차트 `EndlessBalance`). 실패하면 출격 차단, SO/캐시 대체 금지 |
| `Assets/WorkSpace/USW/Scripts/Endless/Configuration/EndlessModeData.cs` | 모드 SO: 사이클·슬롯 역할·보스 풀·성장·패널티 풀 |
| `Assets/WorkSpace/USW/Scripts/Endless/Configuration/EndlessBossProgressionData.cs` | 오프라인 미리보기용 숫자 저작. NORMAL은 뒤끝 스냅샷 필수 |
| `Assets/WorkSpace/USW/Scripts/Endless/Contracts/EndlessBossProgressionSnapshot.cs` | 런 시작 때 고정한 난도 수치와 로그 방어 역산 |
| `Assets/WorkSpace/USW/Scripts/GameConfig.cs` | 시작 시간·식량·소환 비용·처치 보너스·소환 대기·패배 연출 값 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/DefeatPresentation.cs` | 패배 연출 |

| `Assets/WorkSpace/USW/Scripts/Endless/Contracts/IEndlessBalanceGateway.cs` | 인증된 차트 조회 인터페이스 |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/BackendEndlessBalanceGateway.cs` | 현재 적용 뒤끝 파일 조회 |
| `Assets/WorkSpace/USW/Scripts/Endless/Configuration/EndlessBalanceSnapshot.cs` | 필수 전체 운영 계약 파싱·버전·해시·전투 EXP 표 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/UI_PartySelectPresenter.cs` | 스태미나 예약 전 운영 설정 확인 |
| `Assets/WorkSpace/USW/Scripts/GameEntryButton.cs` | 구형 입장 버튼도 같은 출격 경로 사용 |
| `Tools/verification/BackendBalanceRequiredChecks.cs` | 필수 설정/실패/계정 전환/재시도 격리 검증 |
| `Tools/verification/BackendBalanceRequiredLiveCheck.cs` | 현재 운영 차트 읽기 전용 확인 |
| `Data/Balance/EndlessBalance.xlsx` | 뒤끝 업로드용 운영 설정 Excel |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/Endless/Modes/*.asset` | NORMAL·HARD 모드 |
| `Assets/WorkSpace/USW/Data/Endless/Progression/NORMAL_BossProgression.asset` | 오프라인 미리보기용 프로필. NORMAL 운영 숫자는 EndlessBalance 차트에서만 수신 |
| `Assets/WorkSpace/USW/Data/Endless/BossPools/*.asset` | 숨돌림(Breather)/어려움(Hard) 보스 풀 |
| `Assets/WorkSpace/USW/Data/Endless/Penalties/*.asset` | 런 패널티 정의 |
| `Assets/WorkSpace/HSD/Resources/Data/GameConfig.asset` | 본게임 GameConfig |
| `Assets/WorkSpace/USW/Tutorial/Data/TutorialGameConfig.asset` | 튜토리얼 GameConfig |

운영 보스 분류는 뒤끝 `BreatherBossKeys`/`HardBossKeys`가 원본이다. 10-21 저작 Excel은 악어·도마뱀 어려움2종/쉬움8종이다. **10-22 최신 실조회 적용269715/`NORMAL_BOSS_20261021_01`에는 악어·도마뱀 역할·EXP가 모두 있으며 NORMAL/TEST 검증과 실제 로비 서비스의 두 모드 사전 조회가 통과한다.** 스태미나 예약·유료 출격은 이번 확인에서 실행하지 않았다([현재 검증](../../../outputs/lobby-menu-text-20261022/RESULT.md)). 이전268735의 테스트 차단 증거는 보존한다. 라이브 후보를 로컬 Excel만으로 확정하지 않는다. SO 풀은 설치된 키/프리팹을 해석하는 등록표와 오프라인 미리보기에 남는다. 구형 시뮬레이터 스냅샷은 이전4종 어려움 구성을 담을 수 있다.

## 불변 규칙·함정
- Time.timeScale 직접 쓰기 금지 → `TimeScaleService`. 정지 주체마다 자기 키로 `Pause`/`Release`해야 겹친 정지가 서로를 풀지 않는다.
- 비동기 콜백은 반드시 `OwnsEncounter`(런 ID·라운드·보스 순번·단계)로 소유를 다시 확인한다. 런이 끝나거나 바뀐 뒤 늦게 온 콜백이 다음 런을 진행시키면 안 된다.
- 레벨업 선택 중(`GameState.LevelUp`)이거나 공격 보류 중이면 보상·패널티 화면을 띄우지 않고 기다린다. 화면 위에 화면을 덮지 않는다.
- 패널티는 전투 완료 시점에 예약되고, 보상 후 다음 라운드 진입 때 활성화된다.
- 밸런스의 기준은 무한모드다(9웨이브 유한 진행은 대체됨). 튜토리얼(`TutorialStage`)의 유한 진행은 `TutorialWaveFlow`가 소유한다. WaveManager의 null 모드 진입은 이 전용 흐름으로 위임하며 씬의 기존 StageData/GameConfig 참조와 UI 이벤트는 보존한다. 런 ID/라운드 중복 완료 방지는 공통 `EndlessRunService`의 null 모드 계약을 재사용한다. 유한 경로에 있던 보스 시트 조회(`GameDataManager`)는 2026-11-04 제거했다.
- 런 실패는 `EndlessRunService.Fail` → `GameState.Faulted`. 조용히 진행시키지 말고 오류를 남긴다.
- 전투 준비/시작 실패도 `GameManager.FailPreparation` → Faulted와 `FailureReason`/`OnRunFailed`. 소환 버튼은 실패 사유를 표시하며 Idle로 방치하거나 식량을 쓰지 않는다. Inspector에서 원본 NORMAL 키를 바꿔 원격 요구를 우회하지 않는다.
- 모바일에서 시작 버튼 없이 Idle에 남는 경우 준비 예외도 확인한다. 로비 `ResearchScreen`은 Root 주입 트리를 사용해야 한다. 씬 번들과 Root에 같은 SO가 별도 인스턴스로 포함되면 저장 세션의 트리 충돌 검사가 전투 준비를 거절할 수 있다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/EndlessTuningChecks.cs` | 무한 튜닝 값·라운드 진행 확인 |
| `Tools/verification/EndlessBossRefactorChecks.cs` | 공통 HP/방어, EXP 호환, 셔플/스냅샷/오류 경계 검증 |
| `Tools/verification/EndlessBossSpawnChecks.cs` | 격리 실제 스폰/피해와 HP바10,000 경계 검증 |
| `Tools/verification/EndlessBossRefactorAuthoring.cs` | NORMAL 진행 SO 연결·난도 중복 외형의 엔드리스 전용 분리 |
| `Tools/verification/IngameAutoStartChecks.cs` | 중복 SO 인스턴스→로비 주입→전투 준비 회귀 및 자연 자동 시작 검사. 모바일 기기 실행 증거와 구분 |
| `Tools/verification/IngameEditorPreviewChecks.cs` | 로그인 없는 자연 직접 Play·실제 유료 소환/배치·계정 무변경·준비 실패/식량 보호 검사 |
| `Tools/verification/TutorialFlowChecks.cs` | 기존 전체 튜토리얼 Play 검사 시작, 실제 3웨이브 승리 후 중복/지난 런 콜백과 종료 후 재시작 차단 검사 |
| `Tools/verification/BattleOutcomeLiveChecks.cs` | 승패 결과 화면 실행 확인 |
| `Assets/WorkSpace/USW/Scripts/Endless/Configuration/EndlessConfigurationValidator.cs` | 모드·풀·패널티 SO 저작 검사 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/endless-mode.md` | 무한모드 규칙·현재 저작값 |
| `design/gdd/ingame-system.md` | 인게임 진행 GDD |
| `docs/technical/battle-outcome-presentation.md` | 전투 종료 연출·결과 화면 |
| `design/quick-specs/endless-round-target-2026-10-13.md` | NORMAL 30~40라 목표 시험값 |

- 플레이어 최고 도달 기록: [유지 원본](../../technical/player-round-records.md). 본게임 NORMAL/HARD 승패만 저장하며 로비 NORMAL 배지에 연결한다. 랭킹/라운드 보상은 포함하지 않는다.

| `Tools/verification/TimeVaultInteractionChecks.cs` | 실제 인게임 시간 금고의 버튼/서비스 차단, 드래그 취소, 허용 메뉴와 종료 후 복원 검사 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/IngameHudMenu.cs` | 햄버거 메뉴. 시간 금고 중 선택지 요청만 무시하고 딜표/설정 허용 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/EnchantButtonOpener.cs` | 강화 버튼 진입 차단 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UI_UpgradePanel.cs` | 강화창 직접 열기와 유예 진입 시 기존 창 정리 |

## 보완 진입 파일 (11-04 대조)

| 파일 | 역할 |
|---|---|
| `design/gdd/ingame-design-baseline.md` | 사용자 확정 인게임 설계 기준 |
| `docs/proposals/twin-battlefield-hard-mode.md` | 어려움 두 전장 미채택 제안, 실제 게임 규칙과 구분 |
