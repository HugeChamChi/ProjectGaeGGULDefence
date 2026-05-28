# Session State — 2026-05-27

<!-- STATUS -->
Epic: 인게임 시스템
Feature: 드론 유닛 시스템
Task: Inspector 연결 + characterId 시트 맞추기
<!-- /STATUS -->

---

## 작업 브랜치
`Develop`

---

## 이번 세션에서 완성한 것

### 신규 스크립트
| 파일 | 설명 |
|------|------|
| `Drone/DroneManager.cs` | 드론 등록/해제, 식량 틱, 버프/디버프, 집결(ExecuteRallyAsync) |
| `Drone/DronePool.cs` | DroneUnit(50) + SelfDestructDrone(20) 오브젝트 풀 |
| `Drone/DroneUnit.cs` | 궤도 맴돌기 + 자동 공격 + 집결 이동 |
| `Drone/SelfDestructDrone.cs` | 보스 방향 비행 후 폭발 |
| `Drone/DroneHoverAnimation.cs` | DOTween 상하 호버 |
| `DroneUnits/DroneProducer.cs` | 스킬마다 드론 1마리 추가 (최대 N마리 캡) |
| `DroneUnits/DroneBuffer.cs` | 배치 시 드론 1마리, 스킬마다 전체 버프 |
| `DroneUnits/DebuffDroneUnit.cs` | 배치 시 드론 1마리, 스킬마다 보스 디버프 |
| `DroneUnits/DroneFoodProducer.cs` | 패시브 — 고정 식량 + 드론당 기여 증가 |
| `DroneUnits/DroneChieftain.cs` | 스킬마다 전 드론 집결 → 일제 사격 → 귀환 |
| `DroneUnits/*Data.cs` | 각 유닛 전용 SO (DroneProducerData 등 5종) |
| `Editor/DroneUnitAssetCreator.cs` | Tools > USW > Create Drone Unit Assets (SO 40개 자동 생성) |

### 기존 파일 수정
| 파일 | 변경 내용 |
|------|-----------|
| `CoreSystem/Manager.cs` | `Manager.Drone`, `Manager.DronePool` 추가 |
| `Boss,Enemy/BossBase.cs` | `TakeDamage`에 `Manager.Drone?.BossDebuffMultiplier` 적용 |
| `Unit/UnitBase.cs` | `GetBaseFoodPerSecond()` → `protected virtual`, `ExecuteAttack()`에 `atk<=0` 조기 return 추가 |
| `Unit/UnitFactory.cs` | `unit.animator.Initialize` → `unit.animator?.Initialize` (NullRef 방지) |

### 아키텍처 결정
- 드론 유닛 5종 모두 `_dataByTier[]` 배열 패턴 사용 → 프리팹 1개로 4등급 수치 분기
- DroneBuffer / DebuffDroneUnit: **배치 시** 드론 1마리 생성, 제거 시 반환
- DroneProducer: **스킬마다** 1마리 추가, 최대 Normal=1 / Rare=2 / Epic=3 / Legend=4
- `unitData.atk <= 0` 인 유닛은 UnitBase에서 공격 루프 건너뜀

---

## Unity에서 완료한 것
- DroneUnit, SelfDestructDrone 프리팹 제작
- DronePool GameObject + Inspector 연결
- DroneManager GameObject 씬 추가
- 유닛 프리팹 5종 제작

---

## 남은 작업 (다음 스레드)

### Unity Inspector 작업
1. **Tools > USW > Create Drone Unit Assets 재실행**
   - DroneProducerData `normalDroneCount` → `maxDroneCount` 반영
   - UnitData SO 20개 신규 생성 (attackSpeed=9999 포함)

2. **유닛 프리팹 5종 — `_dataByTier[]` 배열 연결**
   - [0]=Normal / [1]=Rare / [2]=Epic / [3]=Legend 순서로 각 SO 연결

3. **UnitData SO 20개 — `prefab` 필드 연결**
   - 5종 프리팹 각각 해당 UnitData SO에 연결

4. **UnitFactory — unitDataList에 UnitData SO 20개 추가**

5. **DroneManager Inspector 수치 확인**
   - Rally Spacing: 50 / Rally Boss Offset: 200 (플레이테스트 후 튜닝)

### characterId 시트 맞추기 (코드 작업 필요할 수 있음)
- 현재 소환 버튼이 백엔드 시트 characterId(1000, 1012 등)로 유닛을 요청하는데
  로컬 UnitData SO의 characterId와 불일치 → `CreateUnitByCharacterId` 경고 + NullRef
- 시트 URL: `https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/edit#gid=1984586417`
- 시트의 1열 characterId 목록을 확인 후 로컬 UnitData SO의 characterId 필드를 맞춰야 함
- 기존 Frog/Gunner/Ninja/Wizard UnitData SO 16개 + 드론 유닛 UnitData SO 20개 모두 해당

---

## 핵심 수치 요약

### DroneProducer
| 등급 | 최대드론 | 자폭드론 | 드론공격력 | 공격간격 | 쿨타임 |
|------|:---:|:---:|---:|---:|---:|
| Normal | 1 | 0 | 8 | 1.5s | 12s |
| Rare | 2 | 0 | 12 | 1.4s | 12s |
| Epic | 3 | 0 | 17 | 1.3s | 12s |
| Legend | 4 | 2 | 20 | 1.2s | 12s |

### DroneBuffer 버프 / 쿨타임
Normal: +15%/+12% 5s / 20s → Legend: +55%/+45% 12s / 12s

### DebuffDroneUnit 디버프 / 쿨타임
Normal: +15% 6s / 25s → Legend: +52% 14s / 16s

### DroneFoodProducer (패시브)
Normal: 1.5/s 고정 + 드론당 0.40 → Legend: 6.0/s + 1.00

### DroneChieftain 집결 / 쿨타임
Normal: 드론당 25 / 25s → Legend: 드론당 110 / 15s
