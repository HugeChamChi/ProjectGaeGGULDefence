# Session State — 2026-05-18

<!-- STATUS -->
Epic: 인게임 시스템
Feature: UI 아키텍처 개선
Task: UI 싱글톤 제거 + InGameInstaller DI 배선 완료
<!-- /STATUS -->

---

## 작업 브랜치
`USW_IngameScene`

---

## 아키텍처 원칙 (이번 세션 결정)

UI 컴포넌트에서 싱글톤 제거 → 순수 MonoBehaviour 뷰로 전환.
시스템(Manager)은 싱글톤 유지, UI만 DI 적용.

```
System (도메인 이벤트 발행)       UI (뷰 이벤트 발행)
   OnUnitSelected ──┐      ┌── OnMergeRequested
   OnSelectionCleared─┤  ├── OnSellRequested
                     ↓  ↓
                InGameInstaller  ← 배선만, 로직 없음
```

- PopupAnimator 등 팝업 내부 구조 분리는 현재 인터페이스 안정 후 2단계 리팩토링으로 미룸

---

## 구현 완료 항목 (이번 세션)

### UnitActionPopupUI
- `InGameSingleton` → `MonoBehaviour` 변환
- 외부 API: `Show(unit, canMerge)` / `Hide()` / `OnDismissRequested` event
- 내부 구현(DOTween·위치계산·외부클릭감지) 내부 디테일로 유지

### MergeManager
- UI 참조 완전 제거
- 도메인 이벤트 추가: `OnUnitSelected(UnitBase, bool)` / `OnSelectionCleared`
- 족장(Chieftain) 클릭 시 팝업 차단 추가
- `HideButton()` → `ClearSelection()` 위임 (DragHandler 호환 유지)

### MergeButtonUI
- `Manager.Merge.ExecuteMerge()` 직접 호출 → `OnMergeRequested` event 발행

### SellButtonUI
- `Manager.Spawner.SellUnit()` 직접 호출 → `OnSellRequested(UnitBase)` event 발행

### TotemActionPopupUI
- `InGameSingleton` → `MonoBehaviour` 변환
- 판매: `Manager.Totem.SellTotem()` 직접 호출 → `OnSellTotemRequested(TotemBase)` event 발행
- 회전: 순수 UI 내부 동작이므로 이벤트화 하지 않음

### DragHandler
- 토템 클릭: `Manager.TotemInfo?.Toggle()` → `OnTotemClickedGlobal` static event 발행
- 유닛 클릭: `Manager.Merge.OnUnitClicked()` 유지 (시스템 호출)

### Manager.cs
- `TotemInfo` 프로퍼티 제거

### InGameInstaller (신규)
- `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs`
- Unit Action / Totem Info 두 팝업 배선 담당

---

## 씬에서 해야 할 작업 (미완료)

### UnitActionPopup GameObject (Canvas 하위)
```
UnitActionPopup
  ├── UnitActionPopupUI (mergeButton, sellButton, rootCanvas 연결)
  ├── MergeButton — Button + MergeButtonUI
  │     Button.OnClick → OnMergeButtonClicked()
  └── SellButton  — Button + SellButtonUI
        Button.OnClick → OnSellButtonClicked()
```

### InGameInstaller GameObject
- `InGameInstaller` 컴포넌트
- Inspector: _unitActionPopup / _mergeButton / _sellButton / _totemInfoPopup 연결

### TotemActionPopupUI
- 기존 InGameSingleton 컴포넌트 제거 확인 (코드는 MonoBehaviour로 변경됨)

---

## 지난 세션 미완료 항목 (2026-05-15)

- `3046` 낙뢰 마법 — 번개 스킬 시각/데미지 정의 미결, 별도 작업
- LevelUpData 에셋 60개 생성 (Tools > USW > Generate LevelUp Assets)
- LevelUpManager Inspector에 에셋 60개 등록
