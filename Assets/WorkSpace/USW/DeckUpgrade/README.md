# 유닛 강화 UI

현재 시안 프리팹: `Prefabs/UnitUpgradeUI.prefab`

확인용 씬: `UnitUpgradeUI.unity` — 프리팹 인스턴스와 EventSystem으로 구성합니다.

- 프로젝트의 원하는 씬에 프리팹을 배치합니다. 씬에는 EventSystem과 StandaloneInputModule이 하나 필요합니다.
- 현재 시안은 3열 목록, 덱별 족장 우선, 보유/미보유 구분, 유닛 정보와 골드 강화를 포함합니다.
- 기존 `DroneUpgradeConcept2.unity`와 이전 설명은 `Legacy/`에 보관합니다. 공용 코드와 Spine 에셋은 현재 프리팹에서도 사용하므로 유지합니다.
- 현재 구현은 `DroneUpgradeConceptScreen.BuildView()`가 기본 화면 → `DeckSelectorConcept.BuildView()` → 선택적 `DeckUpgradeDesignVariant.Apply()` 순서로 UI를 재생성합니다. 각 컴포넌트의 Start/실행 순서 설정으로 서로를 기다리지 않습니다. 프리팹에는 검토용 전체 계층과 참조 에셋을 저장했지만, 생성된 자식의 수동 배치 수정은 Play 시 덮어씁니다. 저작 도구도 루트의 `BuildView()` 한 번만 호출합니다. 배치 변경은 각 레이아웃 빌더, 유닛/스파인/아이콘 참조 변경은 루트 컴포넌트에서 합니다.
- 강화와 보유 상태는 로컬 시안입니다. 실계정 보유·재화 데이터와 연결하지 않았습니다.
- 씬의 EventSystem은 프리팹 외부에 두어 다른 UI 씬에 배치할 때 중복되지 않도록 했습니다.

로비 연결: `Assets/Scenes/LobbyScene.unity`의 하단 **캐릭터 강화** 탭(index 1)은 **`Concepts/UnitUpgrade_01_Clean.unity`**의 컴포넌트 설정과 에셋을 이식한 `CharacterUpgrade`를 사용합니다. `DeckUpgradeDesignVariant.variant=1`로 Clean 디자인을 적용하며, 로비의 공용 Canvas·안전 영역·상하단 메뉴를 사용합니다(`_embeddedInLobby=true`, 별도 `lobbyTopTemplate` 제외). 위의 `Prefabs/UnitUpgradeUI.prefab`은 별도 시안이며 현재 로비에 배치하지 않습니다. 사용자 요청으로 캐릭터 강화 페이지의 이전 내용은 모두 제거하고 Clean 화면 하나로 다시 구성했습니다.

내부 데이터 경계(2026-10-18): `LobbyLifetimeScope`가 `IDeckUpgradeStore` → `DeckUpgradePreviewStore`와 `DeckUpgradeService`를 씬 수명으로 등록하고 비활성 화면에도 주입합니다. 화면이 생성하는 `DeckUpgradePresenter`는 서비스와 해당 화면의 SO 목록을 생성자로 받으며, `DeckUpgradeItemViewData`를 만들어 목록/상세/3탭에 직접 전달합니다. 강화 명령·저장은 서비스 소유이고, 화면은 입력·표시·연출을 담당합니다. `Lv.`·슬래시·화살표를 파싱하거나 숨긴 TMP를 데이터 통로로 쓰지 않으며, 보유 필터는 이벤트로 배치를 갱신합니다. 단독 시안/에디터 저작은 같은 서비스를 별도 VContainer 컨테이너에 구성하고 화면 파괴 시 해제합니다. 레이아웃 빌더 내부의 계층 경로와 시안 보너스 문구/수치는 아직 남아 있습니다.

`DeckUpgradePreview.v1` 키·JSON 필드·초기 잔액·강화 수치는 기존 시안을 보존합니다. 실제 계정 저장/보유/골드나 전투 효과를 연결한 작업은 아닙니다. 상단 골드는 기존 계정 표시이고 구매 가능 여부는 시안 잔액을 따릅니다. 기존 `DeckUpgradeProgress.Load/Save`와 화면의 private `progress`에 의존하는 과거 검사 도구는 이전 구현의 기록입니다. 현재 데이터/DI/강화/재화 부족/MAX/팝업/필터 검증은 `Tools/verification/UnitUpgradeStructureChecks.cs`를 사용합니다.

프리팹 갱신: 현재 확인용 씬의 프리팹 인스턴스를 선택하여 Apply하거나, `Tools/verification/DeckSelectorPrefabAuthoring.cs`의 `Export`를 실행합니다. 별도 패키지는 만들지 않습니다.

스파인 미리보기는 `DroneSpinePreview.Show`에서 `maskable=true`로 만들고 부모 클리핑을 갱신합니다. 이를 끄면 처음에는 기존 클립 등록 때문에 정상처럼 보이다가 로비 탭을 나갔다 돌아왔을 때 족장 그림이 스크롤 영역 밖으로 표시됩니다. 목록은 기존 `UnitViewport`의 RectMask2D를 따르며, 상세창에는 캐릭터를 자르는 별도 마스크를 추가하지 않습니다. `Tools/verification/UnitUpgradeClippingChecks.cs`가 실제 목록 드래그·영역 밖 숨김·탭 왕복·상세창의 상단 걸침을 확인합니다.

01 Clean의 현재 표현: 상단 족장 주변의 드론과 발판 장식은 제거했습니다. 목록 카드에는 비용 칸 대신 아래쪽 레벨만 표시하고, 실제 비용은 상세창 하단에서 확인합니다. 상세창은 기존 로비 팝업의 공용 보라색 팔레트와 NotoSansKR-Bold 글꼴을 사용하며, 미리보기·정보·3개 탭·강화 버튼의 구조는 유지합니다. 스파인은 팝업 상단에 반쯤 걸치는 연출을 유지하고, 별도 ‘유닛 정보’ 제목은 표시하지 않습니다. 하단 강화 버튼은 기존 KHJ 노란 버튼 원본(`SPR_UI3600_MaxLabel`)의 비율을 보존하며 위에 ‘강화하기’, 아래 어두운 칸에 골드 아이콘과 필요 비용만 표시합니다. 보유 금액은 기존 상단 골드 표시를 사용하며, 상세창을 열면 `UnitUpgradeGoldFocus`가 주변 상하단을 딤 처리하고 골드만 은은하게 강조합니다. 이 연출은 로비 상세창에만 연결되며 별도 재화 값/구독/클릭 경로를 만들지 않습니다. 본문과 여백을 늘린 패널은 튀어나온 스파인 영역까지 포함해 화면 안에 맞춥니다. `DeckUpgradeDesignVariant`의 직렬화한 글꼴/둥근 스프라이트/노란 버튼 참조를 원본 Clean 씬과 로비에 동일하게 저장합니다.
