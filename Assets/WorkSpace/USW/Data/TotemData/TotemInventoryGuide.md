# 토템 인벤토리와 회전 조작

인게임 `토템` 버튼은 인벤토리를 열고 닫습니다. 보상 토템은 최대 5개까지 저장되며, 오래된 토템이 오른쪽 끝에 남습니다. 배치로 빈자리가 생기면 HorizontalLayoutGroup이 남은 아이콘을 오른쪽으로 모읍니다.

- 선택 보상: 그리드 자동 배치 대신 인벤토리 추가.
- 인벤토리가 가득 차면: TotemSelectPanel의 `Fallback Food`만큼 식량 지급.
- 아이콘 드래그 → 비어 있고 봉인되지 않은 그리드 칸에 놓으면 배치. 다른 UI 위, 점유 칸, 그리드 바깥, 취소한 드래그는 소비하지 않습니다.
- 배치 후 짧은 누름 + 드래그: 회전 가능 토템만 마름모 방향 UI 표시. 상하좌우 네 방향의 효과 범위를 미리 표시하며, 실제 버프는 아직 바뀌지 않습니다. 손을 떼면 확정, 중앙에서 떼면 취소합니다.
- 배치 후 길게 누름 + 드래그: 기존 그리드 이동·교환 동작.
- 배치된 토템의 인벤토리 회수 기능은 없습니다. 기존 판매는 유지하고, 씬의 회전 버튼은 제거했습니다.
- 저장 내용은 이번 인게임 씬의 런에만 속합니다. 영구 저장 기능은 없습니다.

## 편집 위치

- `Assets/WorkSpace/USW/Prefabs/UI/TotemInteractionUI.prefab`
  - TotemInventoryPanel / SlotsRightAligned: 패널 위치·크기·아이콘 간격. 자식은 Slot4 → Slot0 순서이고 Slot0이 오른쪽 끝입니다.
  - DraggedTotem: 손가락을 따라가는 비상호작용 이미지.
  - RotationOverlay / Diamond, DirectionDiamond: 반투명 바깥 마름모와 내부 방향 마름모의 크기·색상.
  - 런타임 패널은 시작 시 닫힙니다. 편집 중 미리 보려면 자식 패널을 활성화하세요.
- `Assets/WorkSpace/USW/Resources/TotemInteractionSettings.asset`
  - Capacity: 기본 5, 최대 5.
  - Move Hold Seconds: 기본 0.45초. 이 시간 이상 누른 뒤 드래그하면 위치 이동.
  - Rotation Dead Zone Pixels: 기본 24px. 중심 취소 영역.
- IngameScene의 TotemInteractionUI > TotemInventoryUI > Toggle Button: 기존 MainUI 토템 버튼 연결.

현재 IngameScene에 UI 인스턴스와 버튼을 연결했습니다. 별도 테스트 씬에도 이 기능을 쓰려면 TotemInteractionUI 프리팹을 Canvas 아래에 배치하고 Toggle Button을 연결해야 합니다. InGameLifetimeScope는 두 UI 컴포넌트를 씬에서 등록합니다.

## 검증

Editor/TotemInventoryChecks.Run은 새 Play Mode 세션에서 실제 보상 선택, 초과 식량 지급, 배치, 취소, 회전과 위치 이동을 검사합니다. 테스트는 런타임 상태를 바꾸므로 테스트용 새 세션에서만 실행하고 종료하세요. 결과는 Temp/totem-inventory-checks.txt에 기록됩니다.
