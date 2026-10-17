# 유닛 강화 UI

현재 시안 프리팹: `Prefabs/UnitUpgradeUI.prefab`

확인용 씬: `UnitUpgradeUI.unity` — 프리팹 인스턴스와 EventSystem으로 구성합니다.

- 프로젝트의 원하는 씬에 프리팹을 배치합니다. 씬에는 EventSystem과 StandaloneInputModule이 하나 필요합니다.
- 현재 시안은 3열 목록, 덱별 족장 우선, 보유/미보유 구분, 유닛 정보와 골드 강화를 포함합니다.
- 기존 `DroneUpgradeConcept2.unity`와 이전 설명은 `Legacy/`에 보관합니다. 공용 코드와 Spine 에셋은 현재 프리팹에서도 사용하므로 유지합니다.
- 현재 구현은 Play 시 `DroneUpgradeConceptScreen`과 `DeckSelectorConcept`이 UI를 재생성합니다. 프리팹에는 검토용 전체 계층과 참조 에셋을 저장했지만, 생성된 자식의 수동 배치 수정은 Play 시 덮어씁니다. 배치 변경은 `DeckSelectorConcept.cs`, 유닛/스파인/아이콘 참조 변경은 루트 컴포넌트에서 합니다.
- 강화와 보유 상태는 로컬 시안입니다. 실계정 보유·재화 데이터와 연결하지 않았습니다.
- 씬의 EventSystem은 프리팹 외부에 두어 다른 UI 씬에 배치할 때 중복되지 않도록 했습니다.

프리팹 갱신: 현재 확인용 씬의 프리팹 인스턴스를 선택하여 Apply하거나, `Tools/verification/DeckSelectorPrefabAuthoring.cs`의 `Export`를 실행합니다. 별도 패키지는 만들지 않습니다.
