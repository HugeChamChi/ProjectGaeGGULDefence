# 유닛 선택·합성·판매

현재 선택 UI는 [UI_UnitInfoPanel](../../Assets/WorkSpace/HSD/Scripts/UI/Unit/UI_UnitInfoPanel.cs)이다. 예전 UnitActionPopupUI/MergeButtonUI 클래스는 현재 소스에 없다.

- [MergeManager](../../Assets/WorkSpace/USW/Scripts/Unit/MergeManager.cs)는 DragHandler의 클릭·드래그 이벤트를 받고 OnUnitSelected/OnSelectionCleared를 발행한다.
- [InGameInstaller](../../Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs)가 선택 이벤트를 정보 패널의 SetData/닫기와 연결한다. 정보 패널의 판매 요청은 UnitSpawner.SellUnit으로 전달한다.
- 합성은 **유닛 2개**의 드래그 합성이다. TryMergeByDrag는 UnitMergeRules.CanPair, 셀 점유·봉인·스턴 등을 검사하고 대상 칸에 다음 소유 등급 결과를 생성한다. Legend는 합성하지 않는다. 노말 조커의 호환은 UnitMergeRules가 판정한다.
- 임시 승급을 받은 유닛도 합성·판매는 OriginalData/OriginalTier를 기준으로 한다. 전투 표시 등급으로 재료를 판정하지 않는다.
- [SellButtonUI](../../Assets/WorkSpace/USW/Scripts/UI/SellButtonUI.cs)는 OnSellRequested를 알리는 UI다. [DragSellService](../../Assets/WorkSpace/USW/Scripts/IngameUI/DragSellService.cs)는 드래그 판매 영역과 취소/해제 흐름을 담당한다.

그리드 배치·소환은 [유닛 GDD](../../design/gdd/unit-system.md), UI 기반 클래스는 [UI 구조](../../Assets/WorkSpace/HSD/Docs/UI_Architecture_Specs.md)를 참조한다. 족장 선택/스킬 버튼은 이 그리드 유닛 선택 경로와 별개다.
