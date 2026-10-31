---
system: init
title: 초기화 (DI 범위·앱 시작·씬 초기화)
keywords: 초기화, 이니셜, initializ, lifetimescope, 라이프타임, vcontainer, 의존성 주입, di 등록, bootscene, 부트, appinitialization, gameinitializer, 씬 전환, scenechange, 로딩 화면
verified: 2026-10-30
commit: de2cbd00
---
# 초기화 카드

**한 줄 요약**: 앱 전역 서비스는 `RootLifetimeScope`(VContainerSettings의 루트 프리팹), 씬 매니저는 `InGameLifetimeScope`에 등록한다. 인게임 매니저는 `GameInitializer`(USW)의 `Initialize()`가 정해진 순서로 `Init()`을 부르고, `StartAsync()`가 끝나야 `IsReady`가 된다.

## 2026-10-30 정적 대조
InGameLifetimeScope는 Resources/StatusPopupCatalog가 없으면 초기화를 거절하고 StatusPopupService를 Scoped로 등록한다. InGameInstaller는 선택 화면 Opening에서 필드 포인터를 먼저 취소한 뒤 정보창을 정리한다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. APK 시작: 일반 Player 씬은 `BootScene` 하나. `AppLoadingScreen`은 `IngameTutorialProgress.IsCompleted`가 false면 Addressables `TutorialScene`, true면 `AppInitialization.InitializeAsync` 완료 후 Addressables `LobbyScene`을 연다. 완료 기록은 기존 기기 키이며 서버 튜토리얼 저장과 별개다. 실패는 로딩 커버의 재시도로 남긴다.
2. 튜토리얼 완료 → `IngameTutorialDirector`가 beforeLoad에서 `AppInitialization.InitializeAsync`를 기다린 뒤 LobbyScene으로 간다(Title 진입도 같은 서비스). 순서: Backend 초기화·미연동 게스트/연동 Google 자동 로그인 → `Table.InitializeAsync` → `GameDataManager.LoadAllAsync` → `Player.InitializeAsync`.
3. 인게임 씬: `InGameLifetimeScope.Configure`가 씬 매니저를 `RegisterComponentInHierarchy`로 등록하고 `GameInitializer`·`AlphanActiveSkill`·`IngameAudioBinding`을 entry point로 등록한다.
   계정 성장 설정 SO/PlayerDataController는 Root에, 보스 진행 AccountExperienceRun은 씬에 둔다. Root는 Player 로드 전 현재 곡선을 전달한다([계정 경험치 카드](account-progression.md), 현재 미커밋 구현).
4. `GameInitializer` `Initialize()`: GameDataManager → Currency → Exp → Grid → TotemBuff → Buff → Merge → LevelUp → Upgrade → GameManager → Wave → Timer → TotemSpawner → Boss → CurrencyFloater → UnitFactory → UnitSpawner → ChieftainSelection.
5. `GameInitializer` `StartAsync()`: 투사체 테이블 → `ResearchRunBonuses.LoadAsync` → 모드의 `RequiresRemoteBalance`가 참이면 `EndlessBalanceService.PrepareSceneAsync`(입장 전 확보한 동일 스냅샷 인계) → 오디오 프리로드 → LastPostLateUpdate → 전환 완료/페이드 해제 → `IsReady`. **IngameScene과 HardIngameScene 모두** `GameManager.StartGame(true)`를 자동 호출한다. 준비 예외는 취소를 제외하고 `GameManager.FailPreparation`으로 Faulted와 실패 안내를 남긴다. 튜토리얼과 독립 실험실은 각자 시작 순서를 유지한다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/RootLifetimeScope.cs` | 앱 전역 등록(Backend·AppInitialization·GameDataManager·Audio·SceneChange·Research 저장·Debuff 카탈로그·튜토리얼 진행). 정적 주입 콜백(UI_Base, Player) |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/AppInitialization.cs` | 기본 게스트, 연동 후 저장된 Google 세션 자동 로그인(실패 시 새 게스트 금지)→테이블→게임 데이터→Player. 계정 변경은 인증된 새 계정 데이터를 로드하고 로비 재생성 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/AppLoadingScreen.cs` | BootScene 로딩 커버, 튜토리얼 완료 여부로 첫 실행/복귀 분기·실패 재시도 |
| `Assets/WorkSpace/USW/Tutorial/Scripts/IngameTutorialProgress.cs` | 모든 수업 성공 후 즉시 기기 완료 저장; 재실행 생략 분기 |
| `Assets/WorkSpace/HSD/Prefab/UI/AppLoadingScreen.prefab` | 로딩 커버·재시도·튜토리얼/로비 씬 주소와 문구 |
| `Assets/AddressableAssetsData/AssetGroups/Scenes.asset` | TutorialScene/LobbyScene/IngameScene/HardIngameScene/OutgameUpgrade 로컬 Addressables |
| `Assets/AddressableAssetsData/AddressableAssetSettings.asset` | 씬 주소를 포함한 Addressables 설정·카탈로그 해시 |
| `ProjectSettings/EditorBuildSettings.asset` | BootScene만 일반 Player 씬으로 포함 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/SceneChangeManager.cs` | 페이드·beforeLoad/afterLoad 훅이 있는 씬 전환 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameLifetimeScope.cs` | 인게임/튜토리얼 씬 등록. `isDron`일 때만 DroneManager. `GlobalResolver`는 남은 호환 진입점 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/GameInitializer.cs` | 인게임 Init 순서와 비동기 준비, `IsReady` |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs` | 선택 화면 Opening의 필드 입력 취소/정보창 정리, 보상·족장·실패 안내 이벤트 연결 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/IngameEditorPreview.cs` | 일반 씬 직접 Editor Play의 무료 복제 프로필·보상/계정 기록 격리 |
| `Assets/WorkSpace/USW/Scripts/VerificationHarnesses/HardIngameEntry.cs` | 하드 씬 직접 Editor Play에만 같은 미리보기 적용, 로비 유료 전환에는 미적용 |
| `Assets/WorkSpace/USW/Scripts/Endless/Runtime/BackendEndlessBalanceGateway.cs` | Root의 IEndlessBalanceGateway 등록 구현, 운영 차트 조회 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/SceneComponentRegistration.cs` | 씬에 있을 수도 없을 수도 있는 컴포넌트(비활성 포함, 복수 가능) 등록 |
| `Assets/WorkSpace/HSD/VContainerSettings.asset` | 루트 스코프 프리팹 지정 |
| `Assets/WorkSpace/HSD/Prefab/Scope/*.prefab` | 루트 스코프 프리팹(이름과 달리 RootLifetimeScope가 들어 있음) |
| `Assets/Scenes/BootScene.unity` | APK 첫 씬 |

| `Assets/WorkSpace/USW/Scripts/IngameEtc/WorldInteractionSettings.cs` | 터치 거리 공용 SO 형식 |
| `Assets/WorkSpace/USW/Resources/WorldInteractionSettings.asset` | 인게임 씬 주입에 필수인 터치 거리 원본 |

## 불변 규칙·함정
- 10-28 정적 대조 기준: 일반 직접 Play 미리보기는 GameInitializer의 UNITY_EDITOR 분기, 하드는 HardIngameEntry가 준비한다. 로비 전환은 운영 잔고/연구/예약과 동일 원격 스냅샷을 사용한다. 선택 화면 입력 취소의 현재 미커밋 연결도 대조했다([대조 기록](../../../outputs/harness-refresh-20261028/RESULT.md)).
- 직접 `.Instance`·싱글턴 기반 클래스·옛 Manager 파사드 호출 금지. 씬 매니저는 MonoBehaviour + `RegisterComponentInHierarchy`, 앱 서비스는 루트에. 씬 매니저를 `DontDestroyOnLoad`로 올리지 않는다.
- 새 인게임 매니저에 `Init()`이 필요하면 `GameInitializer`에 주입·호출을 추가하고 **앞 매니저에 의존하는지 확인해 위치를 정한다**.
- 씬에 없을 수 있는 UI/실험실 컴포넌트는 `RegisterComponentInHierarchy` 대신 `SceneComponentRegistration.RegisterOptional`을 쓴다. 전자는 없으면 컨테이너 빌드가 실패한다.
- `GameInitializer`가 두 개다: 인게임용은 전역 네임스페이스 `GameInitializer`(USW), `GaeGGUL.Core.GameInitializer`(HSD, static)는 옛 테스트 경로. 혼동하지 않는다.
- VContainer `IAsyncStartable`의 `StartAsync`는 Unity `Awaitable` 시그니처를 유지한다(VContainer 연동). 그 안의 작업은 UniTask + 토큰.
- Player/Table/Chart/RM은 없앤 Manager 파사드와 별개인 데이터·리소스 진입점이다. 초기화·정리 계약을 유지한다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/BootLoadingPlayChecks.cs` | BootScene → 튜토리얼 로딩 흐름 Play 검증 |
| `Tools/verification/BootLoadingAuthoring.cs` | BootScene·로딩 프리팹 저작 확인 |
| `Tools/verification/TutorialStartupAuthoring.cs` | Boot-only/Addressables 설정·완료 상태 격리·원복 후 튜토리얼 열기 |
| `Tools/verification/TutorialStartupChecks.cs` | 첫 실행/완료 후 재실행의 실제 씬·커버·게스트/Player 준비 검사 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `docs/technical/server-client-data-architecture.md` | DI 범위·초기화 순서·데이터 진입점(Player/Table/Chart/RM)·잔존 시트 원본 |
| `docs/technical/ingame-tutorial.md` | APK 시작 → 튜토리얼 → 로비 흐름 |
| `docs/technical/player-data-architecture.md` | PlayerData 변경·저장 |
