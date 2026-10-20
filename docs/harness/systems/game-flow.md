---
system: game-flow
title: 게임 진행 (상태·조우 순서·무한 런·타이머)
keywords: 게임 진행, gamemanager, 게임 상태, gamestate, 게임 시작, 게임 종료, 승리, 패배, wavemanager, 웨이브, 라운드, 조우, 무한모드, 무한 모드, endless, 런 패널티, 패널티, penalty, 타이머, timer, 남은 시간, 시간 추가, 보스 처치 보너스, gameconfig, timescale, 일시정지, 게임 속도
verified: 2026-10-21
commit: 2e76748e
---
# 게임 진행 카드

**한 줄 요약**: 남은 시간이 곧 체력이다. `GameManager`가 상태(Idle→Playing⇄LevelUp→Win/Lose)를 소유하고, `WaveManager`가 라운드마다 보스 조우 → 처치 보너스 시간 → (어려운 구간이면) 토템 보상 → (예약됐으면) 런 패널티 → 다음 라운드를 진행한다. 런 수명·라운드 번호·패널티는 `EndlessRunService`가 가진다.

## 흐름
1. 본게임 `IngameScene`은 `GameInitializer`(USW 전역 클래스)의 `StartAsync`의 준비/페이드 완료 후 자동 시작(별도 시작 버튼 없음). `GameManager.StartGame` → `WaveManager.TryPrepareRun`(모드·밸런스 스냅샷으로 `EndlessRunService.TryStart`) → 시작 식량 지급 → `WaveManager.StartWave`.
   사용자 확정(10-20): `#if UNITY_EDITOR`에서 씬 직접 Play(진행 중 씬 전환 없음)는 자동 오프라인 미리보기. 원본 NORMAL을 메모리에 복제한 `EDITOR_PREVIEW` 프로필과 로컬 SO 풀/수치를 사용한다. 별도 메뉴 없음. 강화 저장 로드·스태미나 예약 소비·계정 EXP·최고 라운드 저장 제외, 화면에 미리보기 표시. 로비 출격 및 Player의 NORMAL 원격 필수 계약은 유지한다([원본](../../technical/ingame-editor-preview.md)).
2. 조우: `WaveManager.SpawnNextBossAsync`가 첫 조우에서 타이머를 시작(`TimerController.StartTimer`), 소환 대기 동안 정지 → `BossManager.TrySpawnSingleBoss`(무한) → 타이머 재개 → 배치된 유닛을 새 보스 기준으로 다시 `OnPlaced`.
   현재 NORMAL은 `ShuffleFromStart`로 1라부터 난도별 셔플백을 사용한다. 입장 전 뒤끝 EndlessBalance 전체 설정을 필수 검증하고 동일 스냅샷으로 난도/라운드 HP·고정 방어·줄 수·전투 EXP를 계산한다. SO/직전값 대체는 금지하며 실패하면 스태미나 예약 전에 출격을 막는다. GrowthRateFinalized=0은 성장률 시험 중을 뜻한다.
3. 처치: `WaveManager.OnSingleBossDefeated` → `GameConfig.TryGetBossKillBonusSeconds`(역할별 보너스, 원격 스냅샷이 있으면 그 값) → 클리어 연출 중 `TimerController.AddTime`.
4. 보상: `WaveManager.GrantsTotemReward`(무한은 슬롯 역할 Hard일 때만) → `WaveManager.OnTotemSelectionRequested` → 완료 콜백 `OnRewardDone`.
5. 패널티: `EndlessRunService.OnPenaltyReserved`로 예약된 경우 `PresentPenaltyAsync`가 시간 정지 후 3택(`EndlessRunService.TryChoosePenalty`) → 연출 → 다음 보스/라운드(`EndlessRunService.TryAdvanceRound`).
6. 종료: 시간 0 → `GameManager.HandleTimeUp` → `EndGame`(딜표 확정·결과 스냅샷·계정 경험치 동결/지급·`WaveManager.StopRun`) → 패배 연출 → `UIManager.ShowResult`. 유한 스테이지는 마지막 웨이브 후 `GameManager.OnAllWavesCleared`. 계정 경험치의 현재 미커밋 구현은 [계정 성장 카드](account-progression.md)를 따른다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/CoreSystem/GameInitializer.cs` | 준비 완료 신호·전환 페이드 대기·본게임 자동 시작. 준비 예외는 기기 로그에도 명시 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/IngameEditorPreview.cs` | `UNITY_EDITOR` 전용 직접 Play 프로필 복제·오프라인 표시·씬 종료 시 복제 해제. Player에서는 제외 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/SceneChangeManager.cs` | 씬 로드 후 Preparation을 기다리고 전환 페이드 해제 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/GameManager.cs` | 게임 상태 머신, 시작·레벨업 상태·종료·결과 전달, 실행 실패(Faulted) 처리 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/WaveManager.cs` | 라운드·조우 단계(`EncounterPhase`), 처치 보너스·토템 보상·패널티 순서, 영구 봉인 셀 |
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

운영 보스 분류는 뒤끝 `BreatherBossKeys`/`HardBossKeys`가 원본이다. 10-21 저작 Excel은 악어·도마뱀 어려움2종/쉬움8종이며 뒤끝 콘솔 적용은 별도 확인 대기다. 라이브 후보를 로컬 Excel만으로 확정하지 않는다. SO 풀은 설치된 키/프리팹을 해석하는 등록표와 오프라인 미리보기에 남는다. 구형 시뮬레이터 스냅샷은 이전4종 어려움 구성을 담을 수 있다.

## 불변 규칙·함정
- Time.timeScale 직접 쓰기 금지 → `TimeScaleService`. 정지 주체마다 자기 키로 `Pause`/`Release`해야 겹친 정지가 서로를 풀지 않는다.
- 비동기 콜백은 반드시 `OwnsEncounter`(런 ID·라운드·보스 순번·단계)로 소유를 다시 확인한다. 런이 끝나거나 바뀐 뒤 늦게 온 콜백이 다음 런을 진행시키면 안 된다.
- 레벨업 선택 중(`GameState.LevelUp`)이거나 공격 보류 중이면 보상·패널티 화면을 띄우지 않고 기다린다. 화면 위에 화면을 덮지 않는다.
- 패널티는 전투 완료 시점에 예약되고, 보상 후 다음 라운드 진입 때 활성화된다.
- 밸런스의 기준은 무한모드다(9웨이브 유한 진행은 대체됨). 유한 경로의 `GameDataManager.SetCurrentBossRound` 시트 조회는 유한 스테이지에만 남은 레거시다.
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
