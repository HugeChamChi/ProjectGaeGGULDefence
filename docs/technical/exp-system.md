# 인게임 EXP 시스템

> 출처: [게임 데이터 시트](https://docs.google.com/spreadsheets/d/1KFlQREViEKVB89RemmAqr-xXNIrHGtk9G79U-c1yj2g/edit?gid=8322333)
> 담당: `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpManager.cs`

---

## 개요

인게임 레벨업 시스템. 보스에게 입힌 데미지를 EXP로 변환하고, 레벨업 시 로그라이크 선택지를 발동한다.  
아웃게임 계정 레벨(`PlayerData.PlayerLevel`)과는 **별개**의 시스템이다.

---

## EXP 획득 공식

```
획득 EXP = 보스에게 입힌 데미지 ÷ 100
```

호출 방법:
```csharp
Manager.Exp.AddExpFromDamage(damage);   // 보스 데미지 전달
Manager.Exp.AddExp(amount);             // EXP 직접 전달 (테스트용)
```

---

## 레벨별 필요 EXP 테이블

| 레벨 | 레벨업 필요 EXP | 누적 필요 EXP |
|:----:|:--------------:|:------------:|
| 1    | 100            | 0            |
| 2    | 120            | 100          |
| 3    | 140            | 220          |
| 4    | 160            | 360          |
| 5    | 180            | 520          |
| 6    | 200            | 700          |
| 7    | 220            | 900          |
| 8    | 240            | 1120         |
| 9    | 260            | 1360         |
| 10   | 280            | 1620         |
| 11   | 300            | 1900         |
| 12   | 320            | 2200         |
| 13   | 340            | 2520         |
| 14   | 360            | 2860         |
| 15   | 380            | 3220         |
| 16   | 400            | 3600         |
| 17   | 420            | 4000         |
| 18   | 440            | 4420         |
| 19   | 460            | 4860         |
| 20   | MAX            | 5320         |

공식: `필요 EXP = 100 + (레벨 - 1) × 20`

---

## 규칙

- **최대 레벨**: Lv.20 — 이후 EXP 획득 없음, `AddExpFromDamage` 호출 무시됨
- **초과 EXP 이월**: 레벨업 시 초과분은 다음 레벨로 자동 이월 (`CurrentExp -= ExpToLevelUp`)
- **레벨업 발동**: 매 레벨업 시 `OnLevelUp` 이벤트 → `LevelUpManager`가 로그라이크 선택지 1회 발동
- **보류 처리**: `GameState.Playing` 상태가 아닐 때 레벨업 발생 시 `_pendingLevelUp = true` 로 보류. `FlushPendingLevelUp()` 호출로 발동

---

## 주요 API

| 멤버 | 설명 |
|------|------|
| `AddExpFromDamage(float damage)` | 데미지 → EXP 변환 후 추가 |
| `AddExp(float amount)` | EXP 직접 추가 |
| `CurrentExp` | 현재 레벨 내 누적 EXP |
| `CurrentLevel` | 현재 레벨 (1~20) |
| `ExpToLevelUp` | 현재 레벨에서 다음 레벨까지 필요 EXP |
| `IsMaxLevel` | Lv.20 여부 |
| `FlushPendingLevelUp()` | 보류된 레벨업 즉시 발동 |
| `OnExpChanged` | EXP 변경 시 이벤트 (`float`: 현재 EXP) |
| `OnLevelUp` | 레벨업 발생 시 이벤트 |

---

## 연관 컴포넌트

| 컴포넌트 | 역할 |
|----------|------|
| `ExpBarUI` | `OnExpChanged` / `OnLevelUp` 구독, 슬라이더 업데이트 |
| `LevelUpManager` | `OnLevelUp` 구독, 로그라이크 선택지 UI 표시 |
| `BossBase` 계열 | 데미지 발생 시 `AddExpFromDamage()` 호출 필요 |

---

## 미구현 / 주의사항

- **`BossBase`에서 호출 없음**: 현재 보스 데미지가 `ExpManager`로 연결되어 있지 않음. 보스 클래스에서 `Manager.Exp.AddExpFromDamage(damage)` 호출 추가 필요.
- **Lv.20 UI**: 최대 레벨 도달 시 `ExpBarUI`가 빈 바(0/460)로 표시됨. 추후 MAX 표시로 개선 필요.
