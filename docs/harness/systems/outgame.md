---
system: outgame
title: 아웃게임 (로그인·로비·파티·유저 데이터·영구 강화)
keywords: 계정 경험치, 계정 레벨, 전투 경험치, AccountProgression.xlsx, 로컬 알림, 알림, notification, 아웃게임, 출석, attendance, 계정, account, 회원코드, 구글 연동, 푸시, outgame, 로비, lobby, 로그인, login, 뒤끝, backend, 파티, party, 파티 선택, 출격, 스태미나, stamina, 유저 데이터, playerdata, 유저 저장, 데이터 저장, 영구 강화, 강화 트리, research, 우편, mail, 쿠폰, 프로필, 상점, shop, 가챠, gacha, 일일 초기화
verified: 2026-10-30
commit: de2cbd00
---
# 아웃게임 카드

**한 줄 요약**: 뒤끝(TheBackend) 기본 게스트/연동 후 자동 Google 로그인 뒤 `Player`(유저 데이터 매니저 묶음)·`Table`(마스터)이 초기화된다. 로비에서 파티(`PartyDataSO`: 유닛 명단 + 족장)를 고르고 스태미나를 써서 `IngameScene`으로 간다. 영구 강화(코드 이름 Research, 화면 이름 "강화")는 로비 강화 탭에 직접 표시하는 트리이며, 전투 시작 때 합계가 고정된다.

2026-10-20 L31 글씨 기준 확대와 로비 이름·레벨·추가 표시 칸 보정을 적용했다. 저장된 UI 시각 복사본의 프로젝트 스크립트를 활성화 전에 제거해 3화면비를 확인했으며 로그인/계정 흐름을 다시 검증했다는 뜻은 아니다. [글씨·화면비 검증](../../../outputs/touch-typography-20261020/RESULT.md).

2026-10-22 로비 햄버거 메뉴: TMP 누락 수정 후 사용자 요청으로 각 정사각 타일의 아이콘+제목(+공지 상태) 묶음을 가로·세로 가운데로 정돈했다. 타일184/행간196/시트616, 아이콘 실제 그림의 긴 변52, 제목 고정34/영역152×64, 상태28/영역152×48. 투명 여백을 보정해 계정 아이콘도 중앙이다. 실제 Play44검사(전체 glyph·중앙·칸 내부·포인터/닫기) PASS. [현재 결과](../../../outputs/lobby-menu-polish-20261022/RESULT.md), [이전 잘림 원인·원격23검사](../../../outputs/lobby-menu-text-20261022/RESULT.md).

## 2026-10-30 정적 대조
설정 호스트는 게시 자산의 BakedScreen 프리팹을 복제한다. NativeSourceInstalled이면 기존 프리팹 경로를 사용하며 XML 해석은 명시적 에디터 비교에만 남는다. Settings.asset의 구운 프리팹/아틀라스 참조와 IAP의 파괴 토큰 UniTaskVoid 연결을 대조했다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름

10-28 정적 대조: 아래10-22의 모드 통합은 HEAD에 포함된다. 현재 미커밋 공용 설정 화면은 `UI_SettingPanel_Base` → `SettingsGameBindings`와 UiAuthoring/Settings 자산에서 시작하며, 자세한 제작/게시 경로는 [UI 제작실 카드](ui-authoring.md)를 따른다. 기존 설정 프리팹이 진입 호스트이고 기본 표시 경로는 게시된 프리팹이다. 이번 대조는 로그인/결제/서버 요청을 실행하지 않았다.
2026-10-20 결과창 재시작 계약을 확인·수정했다(미커밋). 운영 다시하기도 서버 설정 조회 → authored 스태미나 예약 저장 → 재입장이며, 예약 없는 운영 시작은 차단한다. 다른 기능의 검증 날짜를 갱신한 의미는 아니다. [검증](../../../outputs/ingame-qa-followup-20261020/RESULT.md).
1. 시작·로그인: [초기화 카드](init.md)의 `AppInitialization.InitializeAsync`(Backend → `Table.InitializeAsync` → `GameDataManager.LoadAllAsync` → `Player.InitializeAsync`) → LobbyScene.
2. 로비: `LobbyLifetimeScope`가 `LobbyPresenter`·`UI_PartySelectPresenter`를 등록. 게임 시작 → `LobbyModeSelectView`에서 Normal/테스트 선택 → `GlobalData.SelectedParty` 설정 → (Normal만 튜토리얼 미완이면 튜토리얼로) → 양 모드 기존 NORMAL 원격 설정/설치 콘텐츠 검증 → `PlayerDataController.ReserveEntryAsync`로 스태미나 차감·예약 저장 → `SceneChangeManager.TransitionToSceneAsync`(Normal=`IngameScene`, 테스트=`HardIngameScene`). 기록도 기존 NORMAL을 공유한다. 이동 실패 후 재시도는 기존 예약을 재사용하며 실제 `GameManager.StartGame`에서 1회 소모한다. 10-22 미커밋 구현/검증은 `outputs/mode-integration-20261022/RESULT.md`.
3. 인게임은 `GlobalData.SelectedParty`로 소환 명단(`UnitFactory`)과 족장(`ChieftainSelection`)을 정한다.
4. 저장: 각 매니저는 `IClearable`(Clear/IsDirty/SaveAsync). 잦은 변경은 `Player.RequestDebouncedSave`(3초 디바운스), 중요한 시점은 `Player.UpdateDirtyDataAsync`.
5. 영구 강화: LobbyScene의 강화 페이지에 배치된 `ResearchScreen`이 `ResearchTreeData` 트리를 코드로 그린다. `_embeddedInLobby`에서는 로비 상단/하단 메뉴를 사용하며 별도 진입 버튼/씬 이동/로비 복귀 버튼이 없다. 진행은 `ResearchSaveService` → `IResearchSaveStore`(현재 `PlayerPrefsResearchSaveStore`, 로컬). 전투 시작 전 `ResearchRunBonuses.LoadAsync`가 합계를 복사하고 `ResearchRunBonuses.Get`으로 읽는다(튜토리얼은 0). 단독 OutgameUpgrade 씬은 같은 트리/화면 설정으로 유지한다.
6. 캐릭터 강화(별도 로컬 시안): LobbyLifetimeScope가 `DeckUpgradeService`/`IDeckUpgradeStore`를 등록하고 화면에 주입. `DroneUpgradeConceptScreen.BuildView`가 기본→목록→디자인을 명시 순서로 구성하며 `DeckUpgradePresenter`의 타입 데이터를 목록/상세/3탭에 직접 전달한다. 실제 계정/전투는 미연결, 기존 DeckUpgradePreview.v1 저장 유지. 과거 저장 씬의 RuntimeBindings는 Play 재생성 시 제거된다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `tools/planner-studio/app/ui/content/item-catalog.js` | 아이템→기능→선택목록→옵션 저작 스키마, 수치/참조/순환/동일 목록 검사. 게임 아이템 사용 실행은 미연결 |
| `tools/planner-studio/app/ui/content/catalog-picker.js` | 공통 검색·종류 필터·중첩 즉시 추가·트랜잭션 취소·동일 선택 목록 재사용 UI |
| `tools/planner-studio/ITEM-CATALOG.md` | 아이템 4개 표, ID/Value 규약, Excel 개별·합본과 게임 연동 경계 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/UIManager.cs` | 결과창 재시작: 최신 설정·스태미나 예약 저장·연타 차단·실패 재시도 |
| `Assets/WorkSpace/HSD/Scripts/Data/Global/Player.cs` | 유저 데이터 진입점(PlayerData·Profile·Mail·Character·Chief·Shop·Daily·Tutorial), 일괄/디바운스 저장 |
| `Assets/WorkSpace/HSD/Scripts/Data/Global/Table.cs` | 공통 마스터 테이블(Addressables) |
| `Assets/WorkSpace/HSD/Scripts/Data/Global/GlobalData.cs` | 씬 간 선택값(`SelectedParty` 등) |
| `Assets/WorkSpace/HSD/Scripts/Data/Chart.cs` | 뒤끝 차트 이름↔ID 캐시 |
| `Assets/WorkSpace/USW/Scripts/OutGame/PlayerDataController.cs` | 기본 유저 데이터(스태미나·표시 설정 등) |
| `Assets/WorkSpace/USW/Scripts/OutGame/BackendGameData.cs` | 뒤끝 게임 데이터 읽기/쓰기 |
| `Assets/WorkSpace/USW/Scripts/OutGame/BackendManager.cs` | 직접 로비 실행용 Editor 로그인 경로(APK 경로와 별개) |
| `Assets/WorkSpace/USW/Scripts/OutGame/MailManager.cs` | 우편 |
| `Assets/WorkSpace/USW/Scripts/OutGame/WebCouponService.cs` | UID 자동 입력 웹 등록 바로 열기·계정별 복귀 조회·중복 실행 방지 |
| `Assets/WorkSpace/USW/Scripts/OutGame/WebCouponGateway.cs` | 플랫폼 브라우저·BACKND 계정·쿠폰 우편 조회 경계 |
| `Assets/WorkSpace/USW/Scripts/OutGame/WebCouponConfig.cs` | 웹 쿠폰 URL SO 정의 |
| `Assets/WorkSpace/USW/Resources/WebCouponConfig.asset` | 사용자가 제공한 뒤끝 웹 쿠폰 URL |
| `Assets/WorkSpace/USW/Scripts/OutGame/CouponReceipt.cs` | 계정 저장 지급 영수증, 우편 제목 보존 |
| `docs/technical/web-coupon.md` | 뒤끝/클라이언트 책임·RewardItems 매핑·GGD 실제 수령/저장 검증 및 Android 복귀 미검증 경계 |
| `docs/technical/mailbox.md` | 통합 우편함·RewardItems 매핑·수령/저장/복구 경계 |
| `Tools/verification/WebCouponChecks.cs` | URL/계정 변경/실패/중복/내역 격리 검사 |
| `Tools/verification/WebCouponMailChecks.cs` | 격리된 우편 저장 실패/재접속/중복 지급 방지 검사 |
| `Tools/verification/WebCouponPresentationChecks.cs` | 실제 포인터 직접 웹 진입·팝업 부재·복귀 조회 검사 |
| `Tools/verification/WebCouponSceneBuilder.cs` | 폐기된 쿠폰 팝업 정리용 Editor 도구 |
| `Tools/verification/WebCouponSceneChecks.cs` | 저장 씬 재로드·필수 참조·쿠폰 팝업 부재 검사 |
| `Tools/verification/WebCouponBackendInspect.cs` | 운영 쿠폰/차트 목록 읽기 전용 확인 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/LobbyLifetimeScope.cs` | 로비 씬 등록 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/LobbyPresenter.cs` | 로비 화면 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/BottomPanelController.cs` | 로비 5탭 연결, 방향별 페이지 슬라이드와 마지막 입력 대기. `LobbyLockedTab`이 붙은 봉인 탭은 선택하지 않고 거절 연출만 재생 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyLockedTab.cs` | 봉인 탭(현재 길드): 자물쇠 흔들림 + "아직 잠겨 있어요" 안내. `_locked`를 끄면 일반 탭으로 동작 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyBottomMotion.cs` | 본 로비 최종 D: 배경·밑줄·내부 면 0.28초 OutCubic, 아이콘만 0.34초 탄력. 씬 수정 없이 기존 참조로 장식 생성 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyBattleView.cs` | 본 로비 전투 탭: 구름 배경 위 중앙 웨이브 숫자와 점선, 족장 위치, 보스 옆 보상 안내. 편성·게임 시작은 기존 경로, 비용은 StaminaConfig. Player.RoundRecords의 NORMAL 기록/저장 상태를 표시하며 미조회는 —. 보상은 준비 중 |
| `Assets/WorkSpace/USW/Scripts/OutGame/TopPanelSystem.cs` | 기존 재화/프로필 구독·수치 갱신. EXP는 기존 참조/원본 외형 유지(사용자 요청으로 이번 EXP 재디자인 취소). 상단 UI 생성/재배치 없음 |
| `docs/technical/profile.md` | 로비 프로필 현행 원본. 우리 보라색/노란 버튼, 최고 라운드, 이모티콘 없음, 무료 닉네임 2~10자 |
| `docs/technical/account-progression.md` | 계정 경험치 현행 원본. Editor Importer→SO→HP 비율 결과 지급→영수증/기존 저장/표시. 세부 수치·종료 정책은 시험값. 전용 카드 `docs/harness/systems/account-progression.md` |
| `Data/Balance/AccountProgression.xlsx` | 계정 설정·레벨별 필요량·전투 경험치·계산 예시의 현재 Excel. 구형 Account_레벨 빈 틀은 별도 보존 |
| `Tools/verification/create_account_workbook.py` | 기존 파일을 덮어쓰지 않는 최초 계정 Excel 틀 작성 |
| `Tools/verification/configure_account_workbook.py` | 사용자 HP 비율 선택·시험용 곡선 반영, 수식 캐시/표 헤더/필터 검사 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Profile/LobbyProfilePopup.cs` | 프로필 본문·프로필/프레임 변경·닉네임 입력·복사·열기/닫기 연출 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Profile/LobbyProfilePresenter.cs` | 기존 Player/Profile/RoundRecords와 SO 카탈로그 구독/표시 데이터 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Profile/ProfileCatalogSlot.cs` | 보유/선택/장착 상태와 빈 비활성 칸 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Profile/ProfilePopupConfig.cs` | 닉네임 길이·기본 개구리 이미지·기존 카탈로그 그림 노출 정책 SO |
| `Assets/WorkSpace/USW/Resources/ProfilePopupConfig.asset` | 2~10자, 기본 헤더 이미지 재사용, 실험용 카탈로그 그림 숨김 |
| `Assets/WorkSpace/USW/Prefabs/UI/ProfilePopup.prefab` | 로비 좌측 상단 전체 클릭 → 주입된 호스트에서 생성하는 독립 프로필 팝업 |
| `Assets/WorkSpace/USW/Scripts/OutGame/ProfileNicknameService.cs` | 무료 변경 입력 검사·중복 방지·실패/저장 결과 안내 |
| `Assets/WorkSpace/USW/Scripts/OutGame/ProfileNicknameGateway.cs` | BACKND 닉네임 변경/조회, 지연 응답의 원래 계정·동일 데이터 세션 검사 |
| `Assets/WorkSpace/HSD/Scripts/Profile/ProfileDataManager.cs` | PlayerProfile 조회/보유 선택/저장, 계정 세션·저장 중 새 변경 보존 |
| `Assets/WorkSpace/HSD/Scripts/Profile/ProfileItemTable.cs` | 기존 SO 카탈로그 로드·미등록 ID 안전 조회 |
| `Tools/verification/ProfilePopupAuthoring.cs` | GIF 배치 참고·우리 로비 스타일 프로필 프리팹 저작·헤더 배선·관련 없는 레이아웃 값 복원 |
| `Tools/verification/ProfilePopupChecks.cs` | 저장 참조/실제 포인터/스크롤/격리 닉네임과 선택 검사. 실제 닉네임·프로필 저장 소비 없음 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyHeaderLayout.cs` | 직렬화한 탭 참조로 햄버거를 전투(index2)의 전환 완료 상태에서만 표시. 프로필/재화는 공용, 배치는 씬 앵커/LayoutGroup 소유 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyCurrencyNavigation.cs` | 씬 참조 기반 재화 터치: 스태미나 충전창, Gold/Dia 기존 상점 탭. 실제 상품 구역은 준비 중 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyUtilityMenu.cs` | 보조 메뉴 DOTween 열기/닫기. SetAvailable로 전투 외 트리거 숨김/직접 열기 차단/즉시 닫기. 기능은 각 칸 실행 컴포넌트 |
| `Assets/WorkSpace/USW/DeckUpgrade/README.md` | 단독 프리팹과 로비 01 Clean 원본의 구별 및 로컬 시안/계정 미연결 경계 |
| `Assets/WorkSpace/USW/DeckUpgrade/Prefabs/UnitUpgradeUI.prefab` | 별도 유닛 강화 프리팹. 사용자 정정에 따라 로비에서는 사용하지 않음 |
| `Assets/WorkSpace/USW/DeckUpgrade/UnitUpgradeUI.unity` | 단독 확인용 프리팹 인스턴스와 EventSystem |
| `Assets/WorkSpace/USW/DeckUpgrade/Concepts/UnitUpgrade_01_Clean.unity` | 사용자 선택 로비 캐릭터 강화 원본. 로비 CharacterUpgrade는 이 씬의 screen/selector/design 설정을 복사하여 variant1 적용 |
| `Assets/WorkSpace/USW/DeckUpgrade/DroneUpgradeConceptScreen.cs` | 로비 index1 목록·상세의 입력/연출·명시 순서 구성. 주입된 서비스로 Presenter 생성, 타입 데이터로 갱신. embedded는 공용 Canvas/안전 영역, 단독 모드만 Canvas 생성 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckSelectorConcept.cs` | 3열·보유/미보유·족장 우선 목록과 상세 |
| `Assets/WorkSpace/USW/DeckUpgrade/DroneSpinePreview.cs` | UI 전용 Spine/대체 Sprite 미리보기. maskable=true·생성 후 클리핑 갱신으로 목록 스크롤 및 탭 재활성화 때 부모 RectMask2D를 따름 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeDesignVariant.cs` | Screen이 명시 호출하는 01 Clean 디자인. 미사용 비용/원본 라벨 제거, 필터 이벤트로 배치 갱신. 공용 팔레트/Noto 상세창, lobbyTopTemplate=null |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeStatsTabs.cs` | 타입 데이터 Render로 현재 수치/비용 갱신. 숨긴 TMP 파싱/매프레임 동기화 없음. 성공 명령으로 스탯 탭 이동, Clean은 가격 게이지를 생성하지 않음 |
| `Assets/WorkSpace/USW/DeckUpgrade/UnitUpgradeGoldFocus.cs` | 로비 유닛 상세창이 열릴 때 상하단을 딤 처리하고 기존 GoldCard만 은은하게 강조. 팝업 닫기/탭 이탈 시 해제. 재화 값 복제/구독 없음 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeProgress.cs` | 기존 JSON 호환 시안 진행·재화·규칙 모델. 저장 I/O는 Store로 분리, 실계정 데이터 아님 |
| `Assets/WorkSpace/USW/DeckUpgrade/IDeckUpgradeStore.cs` | 시안 Load/Save 저장 경계 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradePreviewStore.cs` | 기존 DeckUpgradePreview.v1 PlayerPrefs 키/JSON 읽기·쓰기 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeService.cs` | 씬 수명 시안 진행·강화 명령·저장·변경 이벤트 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradePresenter.cs` | SO 카탈로그+시안 서비스를 받아 표시 데이터 생성. 서비스 이벤트 구독/Dispose 해제 |
| `Assets/WorkSpace/USW/DeckUpgrade/DeckUpgradeItemViewData.cs` | 목록·상세·3탭이 함께 받는 레벨/잔액/비용/표시 수치 |
| `Tools/verification/UnitUpgradeStructureChecks.cs` | 메모리 저장소로 기존 JSON/DI/강화/단일 차감/재화부족/MAX/표시 독립/팝업/필터 및 실제 저장 보존 검증 |
| `Tools/verification/DeckUpgradeImport.py` | 원격 feat/deck-unit-upgrade-ui의 새 DeckUpgrade 폴더만 원본/meta 함께 가져오기 |
| `Tools/verification/DeckUpgradeLobbyAuthoring.cs` | 현재 01 Clean 씬 설정을 로비 index1에 이식. 잘못 연결한 단독 프리팹 인스턴스 교체·백업 |
| `Tools/verification/UnitUpgradeLobbyAuthoring.cs` | 이전 잘못 선택한 단독 프리팹 연결 도구. 현재 로비에 재실행하지 않음 |
| `Tools/verification/UnitUpgradeLobbyChecks.cs` | 저장 씬 재로드·01 Clean variant/통계탭·단독 프리팹 부재·실제 탭/상세/보유 필터/복귀 포인터 확인. 재화 소비 없음 |
| `Tools/verification/UnitUpgradePolishAuthoring.cs` | Clean 원본/로비 동기 재생성, 공용 폰트/스프라이트/기존 노란 버튼 참조, 골드 포커스 연결, 전투 전용 햄버거 참조/캐릭터 페이지 전체 배경 저작. EXP 저작 안 함 |
| `Tools/verification/UnitUpgradePolishChecks.cs` | 실제 카드/상세/3탭 포인터·텍스트 대비·걸친 스파인 포함 컴팩트 영역·필요 비용만 있는 노란 버튼·상단 골드 강조/닫기 해제·전투 전용 햄버거/전체 배경 확인. 계정/강화 저장 변경 없음 |
| `Tools/verification/UnitUpgradeClippingChecks.cs` | 이전 마스킹 해제 정책의 탭 왕복 후 돌출 재현, 실제 목록 드래그/영역 밖 숨김/복귀/상세창 걸침/저장 보존 검사 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AttendanceService.cs` | 7일 출석: 열기는 로비 공유 서버 시간으로 즉시 표시, 받기 직전에 재조회·지급·저장. 규칙 `AttendanceRules`, 보상 `Resources/AttendanceConfig.asset`(시험값). 원본 `docs/technical/attendance.md` |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Attendance/AttendancePopup.cs` | 출석 팝업(2×3 + 7일차), 프리팹 `USW/Prefabs/UI/AttendancePopup.prefab`. 저장된 OpenAttendance 칸에서 LobbyPopupAction으로 진입 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Account/AccountPopup.cs` | 계정 팝업(회원 코드·Google 연동·푸시·저장 복구). 하단 삭제/계정 변경, 기기 확인 문구 제거, 프리팹 `USW/Prefabs/UI/AccountPopup.prefab`. 원본 `docs/technical/account.md` |
| `Assets/WorkSpace/USW/Scripts/OutGame/GoogleAccountAuth.cs` | 로그인 선호/확인 owner/변경 pending과 기존 GPGS 인증. 신규 연동은 GoogleIdentityAuth, 이전 gpgs 자동 로그인은 보존 |
| `Assets/WorkSpace/USW/Scripts/OutGame/GoogleIdentityAuth.cs` | Credential Manager의 앱 안 계정 선택/ID token 메모리 경계 |
| `Assets/Plugins/Android/GoogleIdentityBridge.java` | AndroidX Credentials 1.6.0 / Google ID 1.2.1 네이티브 bridge |
| `Assets/WorkSpace/USW/Scripts/Editor/GoogleIdentityDependencies.xml` | Google identity 고정 버전 Android 의존성 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountSwitchService.cs` | 기존 Google 계정 선택·저장·로그아웃·데이터 재로드 및 지연 응답/재시도 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountSwitchGateway.cs` | 뒤끝 계정 존재 확인/세션 변경·Root 초기화/씬 재생성 |
| `Tools/verification/PopupPolishBuilder.cs` | 계정 하단 배치 authoring 및 저장 프리팹 검사 |
| `Tools/verification/PopupPolishAccountChecks.cs` | 실제 로그인 없는 격리 계정 전환17/로그인 선호5 검사 |
| `Tools/verification/PopupPolishViewChecks.cs` | 계정·우편·출석 실제 포인터 및 준비된 로비 시간 표시14 검사 |
| `Assets/WorkSpace/USW/Scripts/OutGame/ServerClock.cs` | 서버 시간 기준 시계. 스태미나 회복·출석이 기기 시계 대신 사용 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyPopupAction.cs` | 칸별 독립 프리팹 진입 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyPopupHost.cs` | 씬 수명 팝업 캐시, Open 후 저작 캔버스 정렬 재적용 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyWebCouponAction.cs` | 쿠폰 칸 실행 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyWebCouponController.cs` | 쿠폰 직접 웹 진입/복귀 조회 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbySettingsPopup.cs` | 공용 설정 프리팹 진입 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/UI_SettingPanel_Base.cs` | 로비/인게임 공용 설정 호스트. 저작 자산이 있으면 게시 프리팹과 실제 설정 바인딩 사용 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/SettingsGameBindings.cs` | 설정 화면의 오디오/체크/씬 이동을 기존 Presenter·서비스에 연결 |
| `Assets/WorkSpace/HSD/Resources/UiAuthoring/Settings.asset` | 현재 설정 화면 게시 자산·프리팹/리소스 참조, 상세는 UI 제작실 카드 |
| `Assets/WorkSpace/HSD/Scripts/IAP/IAPManager.cs` | 기존 IAP 테스트 화면. 스토어 연결은 파괴 토큰을 받는 UniTaskVoid, 테스트 더미 데이터 경로를 운영 지급으로 오해하지 않음 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Account/AccountDeletionPopup.cs` | 삭제 확인/결과. 캔버스125 > 계정120, Editor 실제 탈퇴 차단 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountDeletionService.cs` | 명시적 탈퇴·중복/지연 응답·계정 경계 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountDeletionGateway.cs` | 뒤끝 탈퇴 API와 성공 후 세션 정리 |
| `Assets/WorkSpace/USW/Scripts/OutGame/LocalNotificationConfig.cs` | 로컬 알림 문구/시각 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/OutGame/LocalNotificationPlanner.cs` | 서버 시간 기반 스태미나 가득 참/출석 미수령 예약 계산 |
| `Assets/WorkSpace/USW/Scripts/OutGame/LocalNotificationService.cs` | Root 수명, 동의/계정/권한 경계 및 예약 갱신 |
| `Assets/WorkSpace/USW/Scripts/OutGame/LocalNotificationGateway.cs` | Mobile Notifications 2.4.2 Android 로컬 예약 경계. FCM 없음 |
| `Assets/WorkSpace/USW/Resources/LocalNotificationConfig.asset` | 사용자 문구 및 KST 00시. 출석 리셋은 05시 유지 |
| `Assets/WorkSpace/USW/Scripts/OutGame/DeviceSettingsService.cs` | 기기 동의/OS 권한, 로컬 알림 변경 이벤트. 기존 Android 13+ 권한 요청 |
| `Tools/verification/LocalNotificationChecks.cs` | 격리 예약·취소·수령·계정·시간 및 실제 Root DI 검사 |
| `Tools/verification/AccountDeletionChecks.cs` | 삭제 서비스 8개·실제 메뉴/계정/삭제 포인터·재열기 검사 |
| `Tools/verification/LobbyHandoffChecks.cs` | 저장 씬·PrefabStage 상태 확인/재로드 |
| `Tools/verification/LobbyMenuTextChecks.cs` | 메뉴 TMP 전체 잘림 원인 검사·글꼴 줄 높이에 맞춘 씬 저작·실제 포인터/글자/원격 사전 조회 검사 |
| `Tools/verification/LobbyMenuPolishAuthoring.cs` | 메뉴 타일의 실제 아이콘·glyph 묶음 중앙 정렬 저작·전후 렌더·Play44검사 |
| `Tools/verification/TestModeBalanceChecks.cs` | 현재 적용 차트 읽기 전용 NORMAL/TEST 검증. Live 출력 폴더 인수로 과거 응답/실패 증거 보존 |
| `Tools/verification/StaminaServerChecks.cs` | 실제 서버 스태미나 검사(원래 값 복구) |
| `Tools/verification/AccountPopupChecks.cs` | 계정/출석 칸 Play 포인터 검사 |
| `Tools/verification/AttendanceChecks.cs` | 출석 격리 검사(규칙·05시 경계·중복·저장 실패·다른 기기·계정 전환) |
| `Assets/WorkSpace/USW/Scripts/OutGame/StaminaInsufficientPopup.cs` | 씬에 배치된 스태미나 상태/충전 팝업, 첫 열기 초기화·닫기/재열기 트윈 처리 |
| `Tools/verification/LobbyCurrencySceneBuilder.cs` | 상단/메뉴/충전창을 LobbyScene에 저장한 Editor 일회성 작성 도구 |
| `Tools/verification/LobbyCurrencyHeaderChecks.cs` | 실제 pointer/raycast·재화 변경 없음·6 기기/안전 영역·최대 수량·씬 재로드 검사 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyUtilityVisuals.cs` | 로비·계정/출석·Clean 정보창 공용 팔레트 및 로비 보조 UI 소유 스프라이트/텍스트 스타일 |
| `Assets/WorkSpace/USW/Scripts/Editor/UI/LobbyPopupAuthoring.cs` | 공용 팔레트 상수를 참조하는 계정/출석 팝업 저작 도구. 기존 색상 값 유지 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/LobbyUtilityAssets.cs` | 설정 프리팹·아이콘 참조 SO |
| `Assets/WorkSpace/USW/Resources/LobbyUtilityAssets.asset` | 실제 인게임 설정 프리팹과 8개 기존 아이콘 참조 |
| `Assets/Scenes/LobbyScene.unity` | 한 줄 프로필/스태미나/Gold/Dia 상단·햄버거 메뉴·충전창이 직접 배치된 씬. 하단은 상점·캐릭터 강화·전투·강화·길드. 길드 봉인 유지, 실제 Gold/Dia 상품 구역은 준비 중 |
| `Assets/Imports/Background2.png` | 로비 전투 탭의 구름 배경 원본. Sprite Single, 기존 GUID 1d66a2a2db9cfb643b4f28f30e31c1a7 유지 |
| `Assets/Imports/Background2.png.meta` | 배경 Sprite 임포트·기존 씬 참조 GUID. 새 GUID/Default texture로 초기화하면 로비 배경 누락 |
| `Assets/Imports/background1.png` | Crocus 깊이 실험실 전경 원본. Sprite Single, 기존 GUID f09fee835b5cd584f9b0074c76a08ce7 유지 |
| `Assets/Imports/background1.png.meta` | 전경 Sprite 임포트·기존 참조 GUID. 원본 PNG와 경로 쌍 유지 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Lab/LobbyBottomMotionLab.cs` | 하단 모션 비교 전용. 현재/A/B/C/D(B 탄력+C 흐름+어두운 면 밀기). 기본 D; 최종 D는 본 로비 LobbyBottomMotion에도 적용 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Lab/LobbyTabFlowGraphic.cs` | D안의 불투명한 내부 면. 선택색과 어두운 면 사이 곡선 경계를 좌우로 이동시키는 마스크 UI 메시; CanvasRenderer 필수 |
| `Assets/WorkSpace/USW/Scripts/Editor/UI/LobbyBottomMotionLabBuilder.cs` | 로비 외형 복제·게임 동작 제거·실험실 생성 메뉴. 원본 로비와 기존 실험실 덮어쓰기 거부 |
| `Assets/WorkSpace/USW/Scene/FxLab_LobbyBottomMotion.unity` | 실제 하단 5탭으로 비교. 자동 재생 및 페이지 이동 토글; Build Settings 미등록 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/UI_PartySelectPresenter.cs` | 파티·모드 선택 후 공통 NORMAL 원격 사전 확인/스태미나 예약·출격 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/UI_PartySelectView.cs` | 기존 게임 시작 경로에서 공통 모드 선택 화면 호출 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/LobbyModeSelectView.cs` | Normal/테스트 두 선택과 취소. 선택 전 스태미나 예약 없음 |
| `Assets/WorkSpace/HSD/Scripts/Data/PartyDataSO.cs` | 파티 SO: 유닛 명단, 균등 소환 여부, 족장, 전용 선택지 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchScreen.cs` | 로비 강화 탭에 직접 표시하는 트리/정보 패널. 단독 씬도 공유. 전투 트리와 같은 TreeKey는 Root 주입 인스턴스로 통일하여 APK의 번들/기본 리소스 중복 객체가 저장 세션 충돌을 일으키지 않게 한다 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchBackground.cs` | MAX 행까지 색 배경 상승, 보이는 영역에만 검은 개구리 실루엣 대각선 이동 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchViewSettings.cs` | 노드·KHJ 정보판·폰트 머티리얼·실루엣 배경 SO 설정 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchInfoPanel.cs` | KHJ 원본 카드/프레임/노랑·파랑 버튼의 고정 패널. 스와이프/페이지 점/추천 따라가기 모드 없음. MAX 옆 추천 강화 유지. 효과는 다음 레벨 값 하나(예: 0.2%), 버튼 하단 여백 8 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchUi.cs` | 공유 TMP 생성에 강화 전용 폰트 머티리얼 전달 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchNodeView.cs` | 트리 노드 표시와 강화 전용 글꼴 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchTreeView.cs` | 스크롤 트리·관문/줄기 글꼴 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchRecommendationDesign.cs` | 트리 추천 표시와 강화 전용 글꼴 |
| `Assets/WorkSpace/USW/Scripts/Editor/ResearchSetup.cs` | 현재 고정 두 버튼의 Editor 입력 도구. 비교 모드 메뉴 제거 |
| `Assets/WorkSpace/USW/Scripts/Editor/ResearchUpgradeChecks.cs` | 강화 로직·최대 레벨 고정 버튼 표시 검사 |
| `Tools/verification/ResearchArtPanelAuthoring.cs` | KHJ 아트와 사용자 지정 KCC-Ganpan/강화 전용 검은 외곽선 머티리얼 연결 |
| `Assets/WorkSpace/HSD/Scripts/UI/Lobby/LobbyFeatureNavigation.cs` | 본게임 씬 진입 및 기존 강화 탭 선택(별도 강화 씬 이동 없음) |
| `Tools/verification/ResearchPanelBuilder.cs` | 로비 강화 페이지 작성·단독 씬 설정 일치·저장 씬 재로드 검사 |
| `Tools/verification/ResearchPanelChecks.cs` | 실제 탭/강화/스크롤 터치·메모리 저장 격리·배경 상승·3기기 배치 검사 |
| `docs/technical/outgame-upgrade-save-and-import.md` | 현재 강화 진입·SO/Excel·로컬 저장/전투 적용 계약 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchTreeData.cs` | 트리 SO(노드·연결·수치·해금) |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchSaveService.cs` | 계정/트리별 저장 세션 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchRunBonuses.cs` | 한 전투에 고정된 영구 강화 합계 |
| `Assets/WorkSpace/HSD/Docs/UI_Architecture_Specs.md` | UI 기본 클래스·Presenter/View 규칙 |

## 플레이어 라운드 기록
- `Player.InitializeAsync`에서 계정별 로드, `ClearAll`에서 세션 취소, 중요 시점 저장에 참여한다.
- `GameManager`는 본게임 승패 확정 시 이전 최고를 결과에 캡처한 후 도달 라운드를 저장한다. 튜토리얼/실험실/중도 이탈/Faulted는 제외한다.
- 10-22 사용자 최종 결정: Normal→IngameScene/테스트→HardIngameScene 둘 다 기존 NORMAL 밸런스·최고 라운드 기록을 공유하고 스태미나/계정 EXP를 적용한다. 테스트는 악어·도마뱀 교대를 유지하되 공통 NORMAL 원격 수치를 사용한다. 기존 HARD 칼럼은 이번 진입에서 사용하지 않는다. LobbyBattleView 배지는 공유 NORMAL 기록을 표시한다. 결과와 실제 서버 유료 왕복/Android 미검증 경계: `outputs/mode-integration-20261022/RESULT.md`.
- 기기에 pending을 먼저 기록하고 서버 확인 뒤 제거한다. 로비/앱 복귀 및 실패 시 30초 간격 재시도한다. 저장 실패를 성공이나 0으로 처리하지 않는다.

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/OutGame/RoundRecordManager.cs` | 계정 세션·최고값·pending 저널·상태 이벤트 |
| `Assets/WorkSpace/USW/Scripts/OutGame/IRoundRecordStore.cs` | 계정별 클라우드 저장 계약 |
| `Assets/WorkSpace/USW/Scripts/OutGame/RoundRecordBackendStore.cs` | 기존 PlayerData 행의 모드별 컬럼 부분 갱신·재조회 |
| `docs/technical/player-round-records.md` | 기록 기준·계정/기기 변경·서버 제약의 유지 원본 |
| `Tools/verification/RoundRecordChecks.cs` | 격리 계정/실패/재시도·로비 연결 검사 |
| `Tools/verification/RoundRecordBackendChecks.cs` | 실제 서버 조회·0 기본값 생성·조건부 갱신 검사 |
| `Assets/WorkSpace/USW/Scripts/Editor/FakeRoundRecordStore.cs` | 실패/늦은 응답을 재현하는 Editor 전용 저장소 |

## 우편함 (2026-10-16)

웹 쿠폰 후속(2026-10-17): 쿠폰 아이콘은 현재 계정 UID를 넣어 웹 등록 페이지를 **바로** 연다. 중간 네이티브 등록창은 띄우지 않는다. 복귀 시 쿠폰 우편을 조회한다. 게임 내 쿠폰 팝업/내역창/기존 직접 입력 자식은 최종 사용자 결정으로 모두 제거했다. 수령은 통합 우편함의 같은 목록을 이용하고 저장 영수증은 유지한다. 2026-10-18 RewardItems 매핑 후 사용자 승인 GGD를 실제 headless Edge 공식 페이지에서 UID 자동 입력/등록 성공, 새 Coupon 우편 수령/서버 저장/새 Play 로그인 유지 확인. 실제 Gold1000+Diamond1000 지급. Android 브라우저 전환/복귀는 미검증. 유지 원본 `docs/technical/web-coupon.md`, 실제 근거 `outputs/backend-coupon-live-20261018/RESULT.md`.

최신 상단 변경(2026-10-17): `design/상단UI인터렉트.mp4` 기준 프로필 옆 한 줄 재화/터치를 LobbyScene에 저장했다. TopPanelSystem/LobbyHeaderLayout의 런타임 생성·재배치를 제거하고, 앵커·HorizontalLayoutGroup과 직렬화 참조를 사용한다. 보조 메뉴 6타일도 씬에 저장했다. `LobbyCurrencyNavigation`은 스태미나 기존 충전창 및 Gold/Dia 상점 탭을 연다. 174 화면/터치 검사와 씬 재로드 통과; `outputs/lobby-currency-header-20261017/RESULT.md`. 아래 10-16의 Play 생성 설명은 변경 이전 기록이다.

로비 보조 메뉴 후속(2026-10-16): 이전 전투 페이지의 독립 우편 아이콘은 런타임에 숨기고 우측 상단 햄버거 메뉴로 옮겼다. 설정은 본게임과 같은 `InGame_Setting_Popup.prefab`를 씬 DI로 생성하고, 로비 인스턴스에서만 인게임 이동 버튼을 숨긴다. 유지 원본 `docs/technical/lobby-utilities.md`, 검증 `Tools/verification/LobbyUtilityChecks.cs`/`LobbyHeaderInspect.cs`, 자산 구성 `LobbyUtilityAssetsBuilder.cs`, 근거 `outputs/lobby-utility-20261016/RESULT.md`. Assets/Scenes/LobbyScene.unity의 기존 변경은 유지하며 이번 배치는 Play에서 생성한다.

`MailManager`는 계정별 요청/성공 응답 저널과 순차 수령을 관리한다. `MailGateway`가 UPost와 기존 PlayerDataController를 연결하고, `MailParser`는 `MailRewardConfig`의 명시적 차트/ID 매핑만 허용한다. 영상 배치 후속(2026-10-17, 미커밋): LobbyScene에 저장한 단일 창을 `UI_MailboxPanel`/`UI_MailItem`이 표시한다. 제목/닫기·통합 스크롤 목록·전체 받기 구성. 운영=Admin, 시스템=Rank, 쿠폰=Coupon을 최신순으로 합치고 본문/개별 받기도 행 안에서 처리한다. 탭/상세 모달/수동 새로고침/저장 재시도 버튼은 없다. 열기/앱 복귀 조회, 실패한 조회와 저장 가능한 성공 응답만 열린 창에서 30초 자동 복구. 정상 상태 반복 조회 없음. 부분 조회 실패는 기존 종류 목록을 보존하며 쿠폰 단독 갱신도 다른 종류를 보존한다. 읽음은 기기/계정별 로컬 상태다.

`Assets/WorkSpace/USW/Resources/MailRewardConfig.asset`는 RewardItems 차트/itemID 컬럼의 Gold/Diamond/Stamina를 같은 재화로 매핑한다(2026-10-18). 첨부 itemCount를 재화량으로 사용하며 미등록 차트/항목은 수령 차단. 실제 서버 수령은 미검증. 성공 응답을 받았으나 저장 실패한 경우는 재시도하며, 응답 자체가 유실된 경우 자동 재지급 없이 확인 필요 상태로 남긴다. UPost와 재화 저장의 서버 원자성은 보장하지 않는다. 상세 유지 원본은 `docs/technical/mailbox.md`, 현재 검증은 `Tools/verification/UnifiedMailboxChecks.cs`/`UnifiedMailboxViewChecks.cs`와 `outputs/mailbox-unified-20261017/RESULT.md`. 이전 MailboxPresentationChecks 및 10-16 근거는 변경 전 UI 기록이다.

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/OutGame/MailGateway.cs` | 뒤끝/계정 재화 경계, 격리 테스트 가능 |
| `Assets/WorkSpace/USW/Scripts/OutGame/MailResponse.cs` | 수령/조회 응답 |
| `Assets/WorkSpace/USW/Scripts/OutGame/MailParser.cs` | 목록·보상 검증 |
| `Assets/WorkSpace/USW/Scripts/OutGame/MailClaimJournal.cs` | 계정별 수령 증거 |
| `Assets/WorkSpace/USW/Scripts/OutGame/MailRewardConfig.cs` | 차트 항목 매핑 SO |
| `Assets/WorkSpace/USW/Resources/MailRewardConfig.asset` | RewardItems/itemID의 Gold·Diamond·Stamina → 게임 재화 매핑 |
| `Tools/verification/RewardItemsMappingChecks.cs` | 실제 Resources 매핑을 합성 Coupon/Admin/Rank 우편으로 검증. 서버/계정/재화 변경 없음 |
| `Tools/verification/RewardItemsLiveChecks.cs` | 사용자 승인 GGD 실제 테스트의 기준 잔액/우편 식별·수령·서버 재조회·새 Play 재로그인 검증. 실제 재화 지급됨 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Post.cs` | 우편 상태·표시 모델 |
| `Assets/WorkSpace/HSD/Scripts/UI/Mail/UI_MailboxPanel.cs` | 씬 참조 기반 통합 목록·입력·열기/복귀/실패 자동 갱신 |
| `Assets/WorkSpace/HSD/Scripts/UI/Mail/UI_MailItem.cs` | 제목·본문·보상·만료·개별 받기를 포함한 가변 높이 행 |
| `Tools/verification/UnifiedMailboxSceneBuilder.cs` | 영상 기준 통합 창을 LobbyScene에 저장하는 일회성 Editor 도구 |
| `Tools/verification/UnifiedMailboxChecks.cs` | 통합 목록·부분 실패·보상 저장·계정 경계 검사 |
| `Tools/verification/UnifiedMailboxViewChecks.cs` | 실제 창 포인터·스크롤·자동 복구·기기 배치 격리 검사 |
| `Assets/WorkSpace/HSD/Scripts/UI/Mail/UI_MailboxOpenButton.cs` | 기존 로비 진입 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/HSD/Data/Party/*.asset` | 파티(드론·농부·테스트) |
| `Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeTree_*.asset` | 영구 강화 트리 |
| `Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset` | 로비/단독 강화 화면 공용 설정. 그림 배경 없음, 검은 한 마리 개구리 실루엣 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchViewSettings.cs` | 스탯 표시와 BASIC 화면 검토용 15개 항목·예시 수치. PreviewItems는 게임 합계와 분리 |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchInfoPanel.cs` | 항목명·현재/다음 수치·얇은 꺾쇠. 선택 MAX는 추천 버튼을 숨기고 중앙 Max |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchValueArrow.cs` | 수치 사이 입력을 받지 않는 작은 꺾쇠 Graphic |
| `Assets/WorkSpace/USW/Scripts/OutGame/Research/ResearchScreen.cs` | 로비 강화 화면·선택·기존 저장 경로, 알림도 검토용 표시와 일치 |
| `Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeFont_WhiteBlack.mat` | KCC-Ganpan 강화 전용: 흰 face, 검은 outline 0.4, face dilation 0.18. 공용 원본 폰트 미변경 |
| `Assets/Imports/Font/KCC-Ganpan SDF.asset` | 사용자 지정 기존 글꼴·atlas 원본 |
| `design/1080_x_1921.png` | 사용자가 지정한 KHJ 하단 아트·색·배치 기준 |
| `Assets/Imports/GGD_ArtWork/KHJ_Artwork/Out-game/SPR_UI3600_CardBg.png` | 잎 장식·노랑/주황 제목 띠·남색 정보판 원본 |
| `Assets/Imports/GGD_ArtWork/KHJ_Artwork/Out-game/SPR_UI3600_IconFrame.png` | 하단 금색 아이콘 프레임 원본 |
| `Assets/Imports/GGD_ArtWork/KHJ_Artwork/Out-game/SPR_UI3600_UpgradeButton.png` | 파란 강화/MAX 버튼 원본 |
| `Assets/Imports/GGD_ArtWork/KHJ_Artwork/Out-game/SPR_UI3600_MaxLabel.png` | 노란 추천 강화 버튼 원본 |
| `Assets/WorkSpace/HSD/Sprites/UnitData Icon.png` | 강화 배경에 검게 틴트하는 기존 개구리 한 마리 얼굴 원본 |
| `Assets/WorkSpace/HSD/Sprites/UnitData Icon.png.meta` | 기존 빈 Multiple 설정을 Single Sprite로 변경하여 얼굴을 SO에서 참조 |
| `Assets/WorkSpace/USW/Data/OutgameUpgrade.unity` | 같은 BASIC/화면 설정을 사용하는 단독 확인용 강화 씬. 로비 강화 탭은 씬 이동하지 않음 |

## 불변 규칙·함정
- 2026-10-18 빌드 전 수정: 기본값 EXP 영수증이 PlayerData 전체 파싱을 막던 문제, 손상 로컬 저널의 변경 허용, 오래된 계정 조회의 닉네임 덮어쓰기를 수정했다. 일일 상점은 기존 행을 확보한 뒤 갱신하고 조회 실패/중복 행에서는 쓰기·구매를 막되 나머지 로비 초기화는 계속한다. 일일 초기화는 동기화된 ServerClock만 사용한다. 기존 중복 행은 삭제·병합하지 않았으며 상점 복구는 별도다. [검증 근거](../../../outputs/outgame-prebuild-20261018/RESULT.md).
- Player/Table/Chart/RM은 정적 진입점이지만 없앤 Manager 파사드와 별개다. 주입·초기화·정리 계약(`Player.Inject`, `Player.ClearAll`)을 유지한다.
- 유저 데이터를 바꾸면 해당 매니저의 IsDirty를 켜고 저장 경로를 탄다. 뒤끝을 직접 호출하지 않는다.
- 영구 강화는 재화를 쓰지 않는다. 저장은 지금 **로컬 PlayerPrefs**뿐이다(서버 저장 아님). 트리 `TreeKey`는 저장 키라서 파일명이 바뀌어도 바꾸지 않는다.
- 2026-10-17 화면 검토용 항목은 `ResearchViewSettings.PreviewItems`에만 작성. 사용자 요청으로 게임 효과 미연결. 기존 트리의 Stat/ValuePerLevel·저장 키·ResearchRunBonuses는 변경하지 않았으며 화면 예시 수치를 실제 전투 수치로 해석하지 않는다.
- 전투 중 강화 화면·저장이 바뀌어도 진행 중인 전투 수치는 바뀌지 않는다(시작 때 고정). 로드 전에는 `GameManager.StartGame`이 시작을 막는다.
- 혼동 주의: 인게임 식량 강화는 `UpgradeManager`(USW/Upgrade, 씬마다 0강 시작)이고 영구 강화가 아니다.
- 상점·가챠·IAP·일일 초기화는 HSD에서 인수한 코드다(`Assets/WorkSpace/HSD/Scripts/` 아래 Shop·Gacha·IAP·Data/DailyManager). 카드에 없는 상세는 원본을 읽는다.
- 라이브 서비스 후속 방향은 승인 후 보류된 계획이다. 현재 구현과 구분한다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/ResearchPreviewAuthoring.cs` | 기존 트리 변경 없이 BASIC 21노드에 15개 표시 항목·예시 수치 작성 |
| `Tools/verification/ResearchPreviewChecks.cs` | 실제 로비 UI·문구·수치 분리·MAX 중앙·원본 진행/트리/저장 보존 62검사 |
| `Tools/verification/SpriteMissingProbe.cs` | 실제 이미지 누락 조사, 두 배경 Sprite/GUID 및 저장 씬 참조 재확인. 기존 Play/dirty 상태 보존 |
| `Tools/verification/SpriteMissingRestore.py` | 백업 후 두 배경 meta를 이전 정상 커밋에서 복구. PNG 해시 보존 |
| `Tools/verification/SpriteMetaRecovery.py` | 재생성된 GUID의 기존 참조·대체 참조·충돌·원본 존재를 확인해 실제 끊긴 meta만 백업 복구 |
| `Tools/verification/SpriteMetaRecoveryApply.cs` | 복원 목록만 Unity 재임포트. Play/씬 저장 없음 |
| `Tools/verification/BootLoadingPlayChecks.cs` | 부트 → 튜토리얼 로딩(로그인 전 단계) |
| `Tools/verification/OutgamePrebuildChecks.cs` | 빈 EXP 영수증·쿠폰 기록·손상 저널·일일 상점 읽기/저장/연타/늦은 콜백 격리 회귀 |
| `Tools/verification/OutgamePrebuildInspect.cs` | 실제 기존 계정 읽기·프로필/상단 EXP 표시 및 값 보존 검사 |
| `Tools/verification/OutgamePrebuildAuthoring.cs` | 로비 EXP 참조만 기존 자식 채움 Image로 수정; 백업 후 저장 |
| `Tools/verification/LobbyBottomMotionLiveChecks.cs` | 실험실에서 5안×5 실제 raycast 클릭, 연타, 탭 너비·선택 배경 위치, D 내부 면 렌더러·메시·불투명도, 비활성화/재활성화 검사 |
| `Tools/verification/LobbySettledMotionChecks.cs` | 본 로비 실제 raycast/pointer 클릭, 연타·좌우 반전·컴포넌트/객체 disable 복귀, 204 모션 표본·불투명 메시·길드 표시 검사 |
| `Tools/verification/LobbyNaturalMotionChecks.cs` | MCP 호출 반환 후 실제 프레임으로 전투↔길드 중간 탭 노출·단조 이동·연타·disable 복귀 확인. 시간 seek/pause 없음 |
| `Tools/verification/LobbyBattleIntegrationBuilder.cs` | 실험실 시각 트리만 본 로비 전투 탭에 복사. 실험 코드/가짜 수치 제거, 기존 파티·우편 팝업 연결. 이미 이식된 루트 덮어쓰기 거부 |
| `Tools/verification/LobbyBattleIntegrationChecks.cs` | 실제 클릭·팝업·드래그·현재 위치 복귀·228 하단 유지·16:9/20:9 화면 검사. 임시 Game View 크기 복원, 출격/재화 지출 없음 |

로비 전환 검증: `Tools/verification/LobbyNavigationChecks.cs`를 Play에서 Begin → 프레임 경과 → Finish(1), Left → Finish(0), Disable → Finish(2) 순으로 실행한다. 클릭과 컴포넌트 활성 상태를 변경하는 검증이다. 파티·저장 전용 자동 검증은 없다. 뒤끝 저장은 실제 계정 기준으로 확인 범위를 적는다.

2026-10-15: 공사 중 이미지를 교체하고 기존 상단/하단 HUD를 활성화했다. 농장은 사용자 결정에 따른 후속 시스템 자리이며 생산·보상 기능은 미구현이다. 기존 상점·파티·가챠 패널은 비활성 상태로 보존했다. 참고 영상 타이밍을 기준으로 0.32초 페이지 전환과 0.24초 탭 선택을 적용했다. 검증 기록: `outputs/lobby-navigation-20261015/RESULT.md`. Android 실기기 성능은 미검증.

같은 날 사용자 검토 후: 캐릭터 강화 Button 컴포넌트가 꺼져 있던 것을 활성화했다. `onClick.Invoke()`는 비활성 Button도 실행하므로 입력 검증이 아니다. `Tools/verification/LobbyInputAudit.cs`는 실제 화면 중심 raycast → IPointerClickHandler 경로를 검사한다. 5탭 모두 통과. 로비 씬의 TMP 73개를 NotoSansKR-Bold와 기본 공유 머티리얼로 통일했고, 탭 배경/테두리 및 빈 페이지 배경을 통일했다. 마을 강화에 남은 DORestart persistent callback 제거. 기록: `outputs/lobby-navigation-20261015/FIX.md`.

2026-10-16: 하단 연출 개선 요청으로 독립 모션 실험실을 추가했다. 현재 방식은 기존 수치를 재현하며 원본 컨트롤러를 실행하는 회귀 검사는 아니다. A/B/C는 시각 비교 제안이며 본 로비에 적용하지 않았다. 페이지 이동 기본 꺼짐(전투 배경 고정), 자동 비교 기본 꺼짐. VContainer 자동 앱 루트는 유지되지만 실험실은 로그인/저장/출격을 실행하지 않는다. 자동 생성되는 FadeScreen 캔버스만 실험실 수명 동안 숨기고 종료 시 이전 enabled 상태를 복원한다. 실제 Game View 캡처는 ScreenCapture 사용(Pipeline screenshot은 Overlay UI를 담지 못함). Editor 검사 29 PASS, 실기기 미검증. 기록: `outputs/lobby-bottom-motion-20261016/RESULT.md`.

같은 날 후속 요청: B 탄력+C 흐르는 선택에 내부 어두운 면 밀기를 합친 D를 추가했다. 선택 배경 0.34초 이동, B 아이콘 압축/상승/안착, 내부 곡선 경계 0.38초 밀기. 좌우 이동 방향을 따르며 연타 시 현재 노출 비율에서 이어진다. 초기 내부 Graphic의 CanvasRenderer 누락으로 투명하게 보이던 문제는 필수 컴포넌트 지정과 생성 순서 수정으로 해결했다. 현재 기본 D, 비교 버튼 5개. 기존 A/B/C는 보존하며 실제 로비에 이식하지 않았다. 후속 검증/참조: `outputs/lobby-bottom-motion-20261016/HYBRID.md`.

이후 D 체감 검토에서 배경 자체의 탄력이 어색하다는 피드백으로 조정했다. **현재 D**는 C와 같은 0.28초 OutCubic 배경 이동이며 목적지를 넘기지 않는다. 밑줄과 내부 면도 지연 없이 같은 0.28초로 맞췄다. 내부 곡선 강도는 0.16→0.08, B의 아이콘 탄력만 유지(0.34초). 새 비교 버튼을 늘리지 않고 D를 갱신했다. 이전 시안 근거는 HYBRID.md, 현행 조정 근거는 `outputs/lobby-bottom-motion-20261016/SETTLED.md`.

## 더 읽을 곳 (필요할 때만)
2026-10-16 전투 탭 실제 이식(사용자 범위 선택: 화면·배치·기존 버튼 연결): `LobbyScene`의 전투 페이지에 `LobbyBattleView`를 추가했다. 원본 실험실은 수정하지 않았으며 LAB/가짜 우편3/시안 보상 수치를 제거했다. 가운데 족장 크기206, 바닥 위치 링과 안내를 추가하고 실제 보이는 영역에 초점을 맞춘다. 화면이 짧으면 라운드 간격을 줄여 다음 목표 말풍선까지 보인다. 최고 기록 저장/보상 데이터·수령/재화A·B 지급은 별도 미구현 범위로 남겨 —/준비 중 표시. 편성·게임 시작 모두 기존 `UI_PartySelectView.Open`으로 들어가 기존 Presenter가 선택 이후 출격을 처리한다. 우편함은 기존 패널, 비용은 authored StaminaConfig 읽기. 실제61검사 PASS; `outputs/lobby-battle-production-20261016/RESULT.md`.

최신 높이 정정(2026-10-16): 사용자 의도는 모든 탭을 200으로 맞추는 것이 아니라 **선택 배경과 프레임을 함께 228로 올리고 나머지는 200 유지**하는 것이다. `LobbyBottomMotion`은 이동 선택 영역의 노출 비율로 각 탭의 공통 부모 높이를 200~228 사이에서 매끄럽게 변경한다. 먼 탭 전환의 중간 탭도 색과 프레임이 함께 지나가며 배경/프레임 탄력은 없다. MCP 자연 프레임·높이 범위·최종228/200·연타·disable 복귀 통과. 현행 근거 `outputs/lobby-bottom-production-20261016/raised-frame/RESULT.md`; 아래 all-200 해석은 이전 단계 기록이다.

같은 날 프레임/순간이동 피드백 후: 선택 배경의 고정 228 높이를 제거하고 실제 `_background` RectTransform 치수/위치에 맞췄다. 불투명 내부 면 뒤의 공통 배경은 이동이 가려졌으므로, 이제 공통 이동 사각형의 겹친 구간을 각 탭 내부에 렌더링한다. 전투→길드 때 중간 마을 강화 탭에도 선택색이 연속해서 지나간다. 이동 0.28초 OutCubic, 곡선 0.08, 아이콘 탄력 유지. 실제 프레임 왕복 512 표본·연타·disable 복귀 통과. `outputs/lobby-bottom-production-20261016/frame-fit/RESULT.md` 참조.

2026-10-16 본 로비 이식: 사용자가 최종 D를 승인하여 `LobbyBottomMotion`으로 적용했다. 배경·밑줄·내부 면 0.28초 OutCubic, 곡선 0.08, 아이콘만 B 탄력 0.34초. 기존 페이지 전환·연타 마지막 요청 큐를 유지하고 하단 표시는 즉시 새 클릭으로 이어진다. 농장 탭/제목/설명은 런타임에 길드 표시로 바꾸며 기능과 기존 그림은 그대로다. 사용자 지시에 따라 Claude가 편집 중인 LobbyScene은 저장하지 않았다. 본 로비 raycast/pointer·연타·좌우 반전·disable 복귀·204 모션 표본 검사 통과. `outputs/lobby-bottom-production-20261016/RESULT.md` 참조. 위 실험실 미적용 기록은 이식 전의 역사적 기록이다.

| 파일 | 역할 |
|---|---|
| `design/gdd/outgame.md` | 아웃게임 GDD |
| `docs/technical/player-data-architecture.md` | PlayerData 변경·저장 |
| `docs/technical/server-client-data-architecture.md` | 데이터 진입점·잔존 시트 |
| `docs/technical/outgame-upgrade-save-and-import.md` | 영구 강화 저장·엑셀 가져오기 |
| `docs/technical/live-service-refactor-plan.md` | 라이브 서비스 후속(보류된 계획) |

2026-10-18 경로 정리: [데이터 원본 안내](../../../Data/README.md), [이동·참조 변경 근거](../../../outputs/data-organization-20261018/RESULT.md). 위 commit은 기준 커밋이며 이번 이관은 미커밋이다.

## 과거 시각 참고의 보존 경계
현재 체크아웃에는 `design/연구이런느낌.png`와 `design/아웃게임은이런느낌으로되어야해.png`가 없다. 당시 수치 꺾쇠/다음 수치 및 정보판 밀착 배치의 참고 이름으로만 보존한다. 현재 구현은 위 LobbyScene·ResearchViewSettings·UpgradeViewSettings.asset으로 확인하며 누락 이미지를 설치 필수 파일로 취급하지 않는다.

## 보완 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/OutGame/AttendanceConfig.cs` | 7일 보상 SO, KST 05시 기준 서버 날짜와 다음 초기화 계산 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AttendanceRules.cs` | 누적 출석 수령 판정, 결석 유지와 7일 완료 후 재시작 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Attendance/AttendanceDaySlot.cs` | 보상 칸의 아이콘/수량과 잠김/수령 가능/수령 완료 표시 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/LobbyDeviceSettingsView.cs` | 기기별 그래픽/언어 보조 설정 View |
