# 아이템·기능·선택 보상 저작

2026-10-31. 기획툴 편집·엑셀 정의 단계다. 아이템 보유/사용 저장, 새 재화 지급, 선택 팝업과 서버 아이템 차트 소비는 아직 연결하지 않았다. 기존 게임의 Gold/Diamond/Stamina 지급 경로는 유지한다. 게임에서 지원하지 않는 새 아이템을 미션 차트로 배포하려 하면 명시적으로 거절한다. 작성용 Excel에는 저장할 수 있다.

## 표와 ID

- Item: ItemId, Name, Description, ItemType, FunctionId(선택), IconAddress. 기존 IconKey/BackendId/Note/Source도 이전 자료 호환을 위해 보존한다.
- ItemFunction: FunctionId, Name, FunctionType, SelectionListId(선택), Value.
- SelectionList: SelectionListId, Name, Description.
- SelectionOption: OptionId, SelectionListId, Name, OptionType, Rewards, DeckId.

ItemId/FunctionId/SelectionListId/OptionId는 표시 이름과 독립적인 고유값이다. 새 항목은 item_0001/function_0001/selection_0001/option_0001 형태로 발급한다. 기존 Gold/Diamond/Stamina ID는 바꾸지 않는다. ID 수정은 참조하는 표도 같이 갱신하며 참조 중 삭제는 막는다.

FunctionType은 명시적 기능 계약 키다. 현재 기획툴에 등록된 계약:

- RecoverStamina: Value = 양의 정수 회복량(예 30).
- ChooseReward: SelectionListId = 아이템 보상 옵션 목록. Value는 빈칸.
- ChooseDeck: SelectionListId = 덱 옵션 목록. Value는 빈칸.

수치 계약은 Value, 관계 ID는 명시적 참조 열로 구분한다. 추후 C# 기능 Enum/처리 클래스를 연결할 때 동일한 계약 키와 인수 규약을 사용한다. 기획툴에서 기능을 새로 추가하는 것은 등록된 종류의 파라미터를 만드는 것이며 새로운 실행 코드를 생성하지 않는다.

OptionType=Reward이면 기존 Rewards 형식(itemId/amount)을 쓰고 DeckId는 비운다. OptionType=Deck이면 등록된 Party의 DeckId를 쓰고 Rewards는 비운다. 잘못된 참조·수량·기능 값·자기 자신을 다시 주는 순환 상자는 검사에서 거절한다.

## 화면

이벤트 → 아이템·기능에서 4개 표를 편집한다. 아이템의 사용 기능 연결 버튼 → 기능 검색 → 새로 만들기 → 선택 목록 검색/추가 → 선택 옵션 추가 순으로 원래 화면을 떠나지 않고 작성할 수 있다. 각 추가 창은 변경 내용을 임시 보관하고 최상위 저장 때 함께 반영한다. 취소하면 하위에서 추가한 데이터까지 버린다.

공통 검색은 이름·ID·설명, 선택 목록은 포함된 아이템 이름·수량까지 찾는다. 아이템/기능/옵션 종류 필터와 검색 결과 수를 제공한다. 검색 팝업은 200개씩 표시한다. 동일한 보상·수량으로 구성된 선택 목록은 경고하여 기존 목록을 재사용할 수 있게 한다. 이름이 달라도 구성 비교는 같게 처리한다.

출석 보상과 미션/일반 보상 작성기도 같은 아이템 검색·종류 필터를 사용하며, 등록 중 아이템을 바로 만들고 연결할 수 있다. 기존 출석의 셀별 드래그·수량 입력도 유지한다.

## Excel

아이템 관련 표에서 Excel 내보내기를 누르면 ItemCatalog_날짜.xlsx 합본(4시트)과 Item.xlsx/ItemFunction.xlsx/SelectionList.xlsx/SelectionOption.xlsx 개별 표를 같은 폴더에 생성한다. 새 프로젝트로 전달할 때는 합본을 가져오면 참조를 한 번에 복구한다. 개별 표는 관련 참조가 이미 있는 프로젝트에서 부분 교체할 때 쓴다.

이 파일은 작성용이다. 기존 RewardItems 차트(itemID/itemName 2열)에 덮어쓰는 파일이 아니다. 향후 게임 아이템 런타임과 차트 경로가 결정되면 별도의 서버용 출력을 연결한다.

## 검증

- check-item-catalog.cjs: 네 표의 참조·ID 변경·4시트 Excel 왕복·이전 프로젝트 이관·잘못된 수량/계약/순환 보상·동일 목록 경고·내용 검색·덱 선택 정의.
- item-catalog-smoke.cjs: 실제 UI에서 중첩 생성/함께 저장, 공유 기능 재사용, 취소 롤백, 보상 내용 검색, 4개 개별 Excel과 합본 출력, 미지원 미션 배포 거절, 미션/출석 검색 통일.
- 기존 보너스/드론/미션/출석/기본 스탯 Node 검사와 Electron 전체 UI 검사.
