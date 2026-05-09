# UnitActionPopup 시스템 기술 문서

**작업 브랜치**: `USW_InGameScene_Setting`  
**작업 범위**: `Assets/WorkSpace/USW/Scripts/UI/`, `Assets/WorkSpace/USW/Scripts/IngameUI/`, `Assets/WorkSpace/USW/Scripts/Unit/`  
**변경 파일**: 5개 / 신규 파일: 2개

---

## 배경

기존 `MergeButtonUI`는 합성 버튼 하나만 담당하는 `InGameSingleton`이었다.  
유닛 클릭 시 합성 외에 판매 기능도 필요해졌고, 단일 버튼에 여러 기능을 추가하면 책임이 뒤섞이는 문제가 있었다.  
팝업 컨테이너와 버튼별 로직을 분리하여 각 스크립트가 단일 책임을 갖도록 구조를 재설계했다.

---

## 변경 전 구조

```
MergeButtonUI (InGameSingleton)
  - 위치 계산
  - Show/Hide 애니메이션
  - 외부 클릭 감지
  - 합성 가능 여부 색상
  - OnMergeButtonClicked()
```

합성 버튼 하나에 팝업 생명주기 + 버튼 동작이 모두 혼재했다.  
판매 버튼 추가 시 이 클래스가 계속 비대해지는 구조였다.

---

## 변경 후 구조

```
UnitActionPopupUI (InGameSingleton)   ← 팝업 생명주기 전담
  ├── MergeButtonUI (MonoBehaviour)   ← 합성 색상 + 클릭만
  └── SellButtonUI  (MonoBehaviour)   ← 판매 클릭만
```

### 역할 분리 원칙

| 스크립트 | 책임 |
|---|---|
| `UnitActionPopupUI` | 위치 계산, DOTween Show/Hide, 외부 클릭 감지, 자식 버튼에 데이터 주입 |
| `MergeButtonUI` | 합성 가능 여부 색상 표시, `Manager.Merge.ExecuteMerge()` 호출 |
| `SellButtonUI` | 판매 대상 유닛 보관, `Manager.Spawner.SellUnit()` 호출 |

---

## 신규 / 수정 파일 목록

### 신규

#### `IngameUI/UnitActionPopupUI.cs`
팝업 컨테이너. `InGameSingleton<UnitActionPopupUI>` 상속.

```csharp
public void Show(UnitBase unit, bool canMerge)
{
    mergeButton.SetState(canMerge);   // 합성 버튼 색상
    sellButton.SetUnit(unit);         // 판매 대상 주입
    PositionAtUnitTopRight(...);
    // DOScale 0 → 1 애니메이션
}

public void Hide()
{
    // DOScale 1 → 0 애니메이션
}
```

외부 클릭 감지는 `LateUpdate`에서 `EventSystem.RaycastAll`로 처리.  
팝업 영역 안 클릭은 닫지 않고, 영역 밖 클릭은 `Manager.Merge.HideButton()` 호출.

#### `UI/SellButtonUI.cs`
판매 버튼 전담. `MonoBehaviour` 상속.

```csharp
public void SetUnit(UnitBase unit) { _targetUnit = unit; }

public void OnSellButtonClicked()
{
    Manager.Spawner.SellUnit(_targetUnit);
    Manager.Merge.HideButton();
}
```

---

### 수정

#### `UI/MergeButtonUI.cs`
`InGameSingleton` → `MonoBehaviour`로 다운그레이드.  
팝업 생명주기 코드 전량 제거. 색상 변경과 클릭 처리만 남김.

```csharp
public void SetState(bool canMerge)
{
    buttonImage.color = canMerge ? colorActive : colorInactive;
}

public void OnMergeButtonClicked()
{
    Manager.Merge.ExecuteMerge();
}
```

#### `Unit/MergeManager.cs`
`MergeButtonUI.Instance` 참조를 `UnitActionPopupUI.Instance`로 교체.

```csharp
// Before
MergeButtonUI.Instance.Show(unit, canMerge);

// After
UnitActionPopupUI.Instance.Show(unit, canMerge);
```

`HideButton()` 메서드명은 유지 — `DragHandler.OnBeginDrag`에서 `Manager.Merge.HideButton()`으로 호출 중.

#### `Unit/UnitSpawner.cs`
`OnDeleteButtonPressed()` (랜덤 유닛 삭제) 제거.  
`SellUnit(UnitBase unit)` 추가 — 특정 유닛을 지정해 판매.

```csharp
[SerializeField] private float sellRefundAmount = 5f;  // Inspector 조정 가능

public void SellUnit(UnitBase unit)
{
    var cell = FindCellByUnit(unit);   // GridManager.AllCells() 순회
    cell.RemoveUnit();
    unit.OnRemoved();
    Destroy(unit.gameObject);
    Manager.Currency.AddCurrency(sellRefundAmount);
    OnAnyUnitSold?.Invoke();
}
```

`FindCellByUnit`은 `Manager.Grid.AllCells()` 전체를 순회해 해당 유닛이 있는 셀을 반환.  
판매는 유닛 클릭(OnPointerClick) 이후에만 발생하므로 매 프레임 탐색 성능 이슈 없음.

---

## 호출 흐름

```
DragHandler.OnPointerClick
  → MergeManager.OnUnitClicked(unit)
    → UnitActionPopupUI.Show(unit, canMerge)
        ├── MergeButtonUI.SetState(canMerge)
        └── SellButtonUI.SetUnit(unit)

[합성 버튼 클릭]
  → MergeButtonUI.OnMergeButtonClicked()
    → Manager.Merge.ExecuteMerge()
      → MergeManager.HideButton()
        → UnitActionPopupUI.Hide()

[판매 버튼 클릭]
  → SellButtonUI.OnSellButtonClicked()
    → Manager.Spawner.SellUnit(unit)
    → Manager.Merge.HideButton()
      → UnitActionPopupUI.Hide()

[드래그 시작]
  → DragHandler.OnBeginDrag()
    → Manager.Merge.HideButton()
      → UnitActionPopupUI.Hide()

[외부 클릭]
  → UnitActionPopupUI.LateUpdate() 감지
    → Manager.Merge.HideButton()
      → UnitActionPopupUI.Hide()
```

---

## Scene 구성

```
Canvas
  └── UnitActionPopup        ← UnitActionPopupUI.cs 부착 (InGameSingleton)
        ├── MergeButton      ← MergeButtonUI.cs 부착 + Button.OnClick 연결
        └── SellButton       ← SellButtonUI.cs 부착 + Button.OnClick 연결
```

Inspector 연결 항목 (`UnitActionPopupUI`):
- `mergeButton` → MergeButton 오브젝트의 `MergeButtonUI` 컴포넌트
- `sellButton`  → SellButton 오브젝트의 `SellButtonUI` 컴포넌트
- `rootCanvas`  → 씬 루트 Canvas

---

## 소환 비용 정책

`UnitSpawner`의 소환 비용은 소환 성공마다 `costIncrement(기본 5f)`씩 증가한다.  
판매 시 비용은 감소하지 않으며, `sellRefundAmount(기본 5f)` 만큼 식량만 환급된다.  
비용 증감 정책 변경이 필요하면 `UnitSpawner` Inspector에서 두 값을 조정한다.
