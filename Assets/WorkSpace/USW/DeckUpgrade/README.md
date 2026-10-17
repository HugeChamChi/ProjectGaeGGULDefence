# 유닛 강화 UI

현재 시안 프리팹: `Prefabs/UnitUpgradeUI.prefab`

확인용 씬: `UnitUpgradeUI.unity` — 프리팹 인스턴스와 EventSystem으로 구성합니다.

- 프로젝트의 원하는 씬에 프리팹을 배치합니다. 씬에는 EventSystem과 StandaloneInputModule이 하나 필요합니다.
- 현재 시안은 3열 목록, 덱별 족장 우선, 보유/미보유 구분, 유닛 정보와 골드 강화를 포함합니다.
- 기존 `DroneUpgradeConcept2.unity`와 이전 설명은 `Legacy/`에 보관합니다. 공용 코드와 Spine 에셋은 현재 프리팹에서도 사용하므로 유지합니다.
- 현재 구현은 Play 시 `DroneUpgradeConceptScreen`과 `DeckSelectorConcept`이 UI를 재생성합니다. 프리팹에는 검토용 전체 계층과 참조 에셋을 저장했지만, 생성된 자식의 수동 배치 수정은 Play 시 덮어씁니다. 배치 변경은 `DeckSelectorConcept.cs`, 유닛/스파인/아이콘 참조 변경은 루트 컴포넌트에서 합니다.
- 강화와 보유 상태는 로컬 시안입니다. 실계정 보유·재화 데이터와 연결하지 않았습니다.
- 씬의 EventSystem은 프리팹 외부에 두어 다른 UI 씬에 배치할 때 중복되지 않도록 했습니다.

로비 연결: `Assets/Scenes/LobbyScene.unity`의 하단 **캐릭터 강화** 탭(index 1)은 **`Concepts/UnitUpgrade_01_Clean.unity`**의 컴포넌트 설정과 에셋을 이식한 `CharacterUpgrade`를 사용합니다. `DeckUpgradeDesignVariant.variant=1`로 Clean 디자인을 적용하며, 로비의 공용 Canvas·안전 영역·상하단 메뉴를 사용합니다(`_embeddedInLobby=true`, 별도 `lobbyTopTemplate` 제외). 위의 `Prefabs/UnitUpgradeUI.prefab`은 별도 시안이며 현재 로비에 배치하지 않습니다. 사용자 요청으로 캐릭터 강화 페이지의 이전 내용은 모두 제거하고 Clean 화면 하나로 다시 구성했습니다.

프리팹 갱신: 현재 확인용 씬의 프리팹 인스턴스를 선택하여 Apply하거나, `Tools/verification/DeckSelectorPrefabAuthoring.cs`의 `Export`를 실행합니다. 별도 패키지는 만들지 않습니다.

01 Clean의 현재 표현: 상단 족장 주변의 드론과 발판 장식은 제거했습니다. 목록 카드에는 비용 칸 대신 아래쪽 레벨만 표시하고, 실제 비용은 상세창 하단에서 확인합니다. 상세창은 기존 로비 팝업의 공용 보라색 팔레트와 NotoSansKR-Bold 글꼴을 사용하며, 미리보기·정보·3개 탭·강화 버튼의 구조는 유지합니다. 스파인은 팝업 상단에 반쯤 걸치는 연출을 유지하고, 별도 ‘유닛 정보’ 제목은 표시하지 않습니다. 하단 강화 버튼은 기존 KHJ 노란 버튼 원본(`SPR_UI3600_MaxLabel`)의 비율을 보존하며 위에 ‘강화하기’, 아래 어두운 칸에 골드 아이콘과 필요 비용만 표시합니다. 보유 금액은 기존 상단 골드 표시를 사용하며, 상세창을 열면 `UnitUpgradeGoldFocus`가 주변 상하단을 딤 처리하고 골드만 은은하게 강조합니다. 이 연출은 로비 상세창에만 연결되며 별도 재화 값/구독/클릭 경로를 만들지 않습니다. 본문과 여백을 늘린 패널은 튀어나온 스파인 영역까지 포함해 화면 안에 맞춥니다. `DeckUpgradeDesignVariant`의 직렬화한 글꼴/둥근 스프라이트/노란 버튼 참조를 원본 Clean 씬과 로비에 동일하게 저장합니다.
