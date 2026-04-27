# PlayerData 책임분리 기술 문서

**작업 브랜치**: `USW_LobbySceneRecover`  
**작업 범위**: `Assets/WorkSpace/USW/Scripts/OutGame/`  
**대상 파일**: `BackendGameData.cs`, `PlayerDataController.cs`

---

## 배경

TopPanel UI(골드/다이아/스태미나 표시) 작업 과정에서 플레이어 데이터 변경 경로가 여러 곳에 분산되어 있는 문제가 발견됨.

핵심 문제는 두 가지였다.

**1. 데이터 이중 원본**  
`BackendGameData.userData`와 `PlayerDataController._data`가 같은 `PlayerData`를 각각 보유.  
스태미나 회복 시 두 곳을 동시에 수정해야 했고, 한 곳이 빠지면 불일치가 발생하는 구조였다.

```csharp
// 변경 전 — 두 곳을 동시에 수정
_data.Stamina = newStamina;
BackendGameData.Instance.userData.Stamina = newStamina;        // 이중 수정
BackendGameData.Instance.userData.LastStaminaRecoveryTime = newRecoveryTime;
BackendGameData.Instance.GameDataUpdate();
```

**2. 변경 경로 미분리**  
Gold / Diamond / Stamina 값을 어디서든 직접 수정할 수 있는 구조.  
문제 발생 시 추적 범위가 코드베이스 전체가 되는 위험.

---

## 결정 사항

> `BackendGameData` = 저장/로드 책임만  
> `PlayerDataController` = 런타임 데이터 단일 소유자 + 변경 경로 전담

데이터 저장 구조를 나누는 것보다 **데이터 변경 경로를 나누는 것**을 우선한다.

---

## 변경 내용

### BackendGameData.cs

**`userData` 공개 프로퍼티 제거**

```csharp
// 변경 전
public PlayerData userData { get; private set; }

// 변경 후
// 제거 — 외부에서 BackendGameData.userData에 직접 접근 불가
```

**`GameDataUpdate` 시그니처 변경**

```csharp
// 변경 전
public void GameDataUpdate(Action onComplete = null)
// → 내부의 userData 필드를 읽어서 저장

// 변경 후
public void GameDataUpdate(PlayerData data, Action onComplete = null)
// → 저장할 데이터를 파라미터로 받음. 내부 상태 없음
```

**`CalculateStaminaRecovery` 시그니처 변경**

```csharp
// 변경 전
public (int, long) CalculateStaminaRecovery(int currentStamina, long lastRecoveryTime)
// → 내부에서 userData.MaxStamina 참조

// 변경 후
public (int, long) CalculateStaminaRecovery(int currentStamina, long lastRecoveryTime, int maxStamina)
// → maxStamina를 파라미터로 받음. 외부 상태 의존 없음
```

---

### PlayerDataController.cs

**변경 경로 전용 메서드 추가**

| 메서드 | 설명 |
|--------|------|
| `AddGold(int amount)` | 골드 증가 |
| `SpendGold(int amount) → bool` | 골드 소모. 부족 시 false 반환 |
| `AddDiamond(int amount)` | 다이아 증가 |
| `SpendDiamond(int amount) → bool` | 다이아 소모. 부족 시 false 반환 |
| `UseStamina(int amount) → bool` | 스태미나 소모. 부족 시 false 반환 (기존 유지) |

**`SaveAndRefresh()` 공통 경로 도입**

모든 변경 메서드는 내부적으로 `SaveAndRefresh()`를 호출한다.  
저장과 UI 갱신이 반드시 함께 일어남을 보장한다.

```csharp
private void SaveAndRefresh()
{
    BackendGameData.Instance.GameDataUpdate(_data);
    RefreshUI(_data);
}
```

**직접 수정 코드 전면 제거**

```csharp
// 제거된 코드
BackendGameData.Instance.userData.Stamina = newStamina;
BackendGameData.Instance.userData.LastStaminaRecoveryTime = newRecoveryTime;
BackendGameData.Instance.GameDataUpdate();
```

---

## 변경 후 데이터 흐름

```
BackendGameData
  GameDataGet()  →  콜백으로 PlayerData 전달 (이후 관여 없음)
  GameDataUpdate(data)  ←  저장 요청 시 데이터 수신

PlayerDataController
  _data  →  런타임 단일 원본
  AddGold / SpendGold
  AddDiamond / SpendDiamond
  UseStamina / RecoverStamina
    └→ SaveAndRefresh()
         ├→ BackendGameData.GameDataUpdate(_data)
         └→ RefreshUI(_data) → OnUpdateUI 이벤트 발행

TopPanelSystem / PlayerDataView
  OnUpdateUI 구독 → 화면 표시
```

---

## 규칙 — 앞으로 지켜야 할 것

**Gold / Diamond / Stamina 값을 직접 수정하지 않는다.**

```csharp
// 금지
playerData.Gold += 100;
playerData.Diamond -= 10;

// 올바른 경로
_playerDataController.AddGold(100);
_playerDataController.SpendDiamond(10);
```

**`BackendGameData.userData`에 접근하지 않는다.**  
해당 프로퍼티는 제거되었다. 데이터가 필요하면 `PlayerDataController.Data`를 사용한다.

**새로운 재화나 수치 필드가 추가될 경우**, `PlayerDataController`에 전용 Add/Spend 메서드를 추가한 뒤 `SaveAndRefresh()`를 통해 처리한다.

---

## 향후 고려사항

- `PlayerDataView`의 Reflection + `FindObjectsOfType` 기반 이름 매칭 방식 개선 (후순위)
- `TopPanelSystem` Start() 순서 문제 — 구독 후 `Data != null` 시 즉시 반영 처리 (후순위)
