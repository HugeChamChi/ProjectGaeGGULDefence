---
system: drone
title: 드론 (베탕·감망·델탕·젤탕, 해킹)
keywords: 드론, drone, 베탕, betan, betang, 감망, gamman, 델탕, deltan, 젤탕, zeltan, 자폭, 해킹, hacking, 해킹 스택, 궤도, orbit, 집결, rally
verified: 2026-10-21
commit: 2e76748e
---
# 드론 카드

**한 줄 요약**: 배치형 스포너 3종(베탕·감망·델탕)은 전투 드론을 소유하고 일반 공격을 드론으로 수행한다. 젤탕은 드론·공격·스킬이 없는 식량 생산자이며 자체 생산과 군단 식량 기여를 제공한다. 감망·델탕은 보스별 공유 해킹 스택을 생산·소비한다.

## 흐름
1. 배치 `DroneSpawnerBase.OnUnitPlaced` → `DroneSpawnerBase.RefreshCombatDrones`가 전투 드론을 상한까지 보충(`DroneSpawnerBase.CreateCombatDrone`) → `DroneManager.RegisterDrone`. 제거 시 `RM.Destroy`로 반환한다. 별도 DronePool은 없다.
2. 전투 드론 `DroneUnit`이 오너 스탯으로 자동 사격하고, SO 설정의 H 궤도로 오너 주위를 돈다(`DroneUnit.StartOrbit`).
3. 베탕 스킬: `Drone_Betan.OnSkillFull` → Spine 버튼 이벤트 → `Drone_Betan.LaunchPendingSkillBombs` → `SelfDestructDrone.Initialize(int damage, bool critical, …)`. 피해 계수는 `UnitData.SelfDestructAttackCoefficient`(D4 구현 완료). 해킹 선택지에서는 `SelfDestructDrone.InitializeHacking`이 폭발 피해 대신 스택을 생산한다.
4. 해킹: 델탕이 소유 드론의 일반 공격 횟수로 충전(`Drone_Deltan.RequiredAttacks`)하고 스택을 생산한다. 감망은 `Drone_Gamman.CastHacking`에서 `DroneHackingRuntime.Reserve`로 스택을 확보한 뒤 `DroneHackingRuntime.Commit` 또는 `DroneHackingRuntime.Refund`로 마무리한다. 장부는 `HackingStackLedger`.
5. 젤탕은 공격·스킬이 없는 식량 생산자다. `UpgradeManager.ZeltanFoodPerDrone`은 SO의 0.55 고정이다. 5종 모두 100부터 ×2 비용으로 단계 제한 없이 강화한다. 젤탕은 `UpgradeManager.ZeltanFoodBonus`를 자체 4/8/12/20에 단계당 +0.4/초로 더한 뒤 선택지 배율을 적용한다. 미배치 드론당 0.15는 유지한다. `DroneManager.RegisterFoodProducer` / `DroneManager.UnregisterFoodProducer`로 등록·해제한다.
6. 족장 집결: `DroneManager.ExecuteRallyAsync`가 드론을 직접 이동시키고 복귀시킨다(`DroneUnit.MoveToAsync`, `DroneUnit.ReturnToHomeAsync`).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/DroneUnits/DroneSpawnerBase.cs` | 스포너 기반(UnitBase). CanBasicAttack=false, 드론 보충·반환, 등급 변화 반영, 선택지 구독 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/HackingDroneSpawner.cs` | 감망·델탕의 기반. 해킹 시전·취소 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Betan.cs` | 베탕. 자폭 드론 예약·발사(Spine 이벤트 연동) |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Gamman.cs` | 감망. 스택 소비 공격(순간이동 기폭) |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Deltan.cs` | 델탕. 공격 횟수 충전, 스택 생산 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Zeltan.cs` | 젤탕. 자체 식량 강화·군단 식량·현재 드론 기수/생산 표시값 |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeManager.cs` | 런 강화 상태·할인·성공 시 효과음. 5종 모두 무한 강화, 반복 효과 O(1), float 비용 오버플로 결제 거절 |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeSettings.cs` | 공통 100×2 비용, 단계별 전투/자체 식량 증가량, 고정 군단 생산량 SO |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeKeyOverrides.cs` | 등급별 characterId를 독립 강화 키로 연결 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UpgradeModel.cs` | 동일 강화 서비스에서 단계·비용·효과 설명 조회 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UI_UpgradePanel.cs` | 5개 강화 카드의 외곽·비율을 보존해 폭에 맞춰 배치 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UI_UpgradeItem.cs` | 강화 버튼·큰 비용 축약·동일 SO 기반 효과 안내, MAX 없음 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/DroneHackingRuntime.cs` | 보스별 공유 해킹 스택 런타임(추가·예약·확정·환불) |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/HackingStackLedger.cs` | 스택 장부(용량·예약) |
| `Assets/WorkSpace/USW/Scripts/Drone/DroneManager.cs` | 드론 등록, 식량 틱, 군단 버프, 집결 폭발 |
| `Assets/WorkSpace/USW/Scripts/Drone/DroneUnit.cs` | 영구 잔존 전투 드론. 자동 사격, H 궤도/고정 슬롯 |
| `Assets/WorkSpace/USW/Scripts/Drone/SelfDestructDrone.cs` | 자폭 드론. 차징 후 돌진·폭발 또는 해킹 스택 생산 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/BetangSpineVisual.cs` | 베탕 Spine 모션 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/SpineSkillLaunchEvent.cs` | Spine 이벤트 → 스킬 발사 연결 |
| `Assets/WorkSpace/USW/Scripts/Selection/DroneSelection/DroneSelectionState.cs` | 드론 선택지의 런 상태 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IDroneEffectReader.cs` | 스포너가 읽는 드론 선택 효과 인터페이스 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameLifetimeScope.cs` | DroneManager 등록. `isDron` 조건 안에서만 등록되니 주의 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/DroneUnits/UnitData_*.asset` | 유닛별 UnitData(등급별 PerTier 값, 자폭 계수, Hacking 참조) |
| `Assets/WorkSpace/USW/Resources/UpgradeSettings.asset` | 공통 100×2 비용·공격력8%/공속2%·젤탕 자체+0.4/초·군단0.55 고정 |
| `Assets/WorkSpace/USW/Resources/UpgradeKeyOverrides.asset` | 젤탕 1004~1007 포함 강화 키 매핑 |
| `Assets/WorkSpace/HSD/Resources/Data/UpgradeModel.asset` | 델탕/감망/베탕/디시그망/젤탕 5개 강화 UI 목록·아이콘 |
| `Assets/WorkSpace/USW/Prefab/Unit/DronUnit/DroneUnit_Zeltan.prefab` | 젤탕 프리팹. 군단 생산량은 강화 SO로 이관 |
| `Assets/WorkSpace/USW/Data/DroneUnits/DroneHacking.asset` | 공유 해킹 설정(DroneHackingData) |
| `Assets/WorkSpace/USW/Data/DroneUnits/*HackingSkill.asset` | 감망·델탕 해킹 SkillData |
| `Assets/WorkSpace/USW/Data/DroneUnits/CombatDroneOrbit.asset` | H 궤도 설정(크기·위상·정렬·좌우) |
| `Assets/WorkSpace/USW/Data/DroneUnits/*FormationSettings.asset` | 궤도를 끈 경우의 고정 슬롯 대형 |

## 불변 규칙·함정
- 보스 잠복 중 IsTargetable=false이면 전투 드론의 새 일반 공격과 해킹 시전을 보류한다. 예약 해킹 스택은 기존 취소/환불 경로로 처리하며 젤탕 생산에는 이 대상 조건을 넣지 않는다(별도 미커밋 보스 작업).
- 스포너 본체는 일반 공격을 하지 않는다. 피해 집계와 표시는 소유 드론 기준이다.
- 알팡은 독립 액티브 스킬이다. 배치·등급·식량을 가진 유닛이 아니다(AGENTS.md 족장 규칙).
- 해킹 스택은 예약 → 확정/환불 순서를 지킨다. 시전이 취소됐는데 Commit하면 스택이 사라진다.
- 일부 고유 값(자폭 프리팹 직렬화 필드 등)은 아직 프리팹에 있다. SO 우선 정책과 실제 이관 상태를 혼동하지 않는다.
- `design/gdd/drone-unit-system.md` 마지막 문단의 "자폭은 Initialize(float), D4는 남은 구현" 내용은 낡았다. 코드 기준으로는 D4가 구현돼 있다(2026-10-13 확인).
- 궤도 규칙은 전투 드론에만 적용한다. 자폭 드론과 전투 수치는 제외다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/InfiniteUpgradeChecks.cs` | 5종 12단계·고단계 비용·할인환급·수치 경계 격리160검사 |
| `Tools/verification/InfiniteUpgradeLiveChecks.cs` | 실제 강화 버튼·12강·젤탕 등급별 생산·지급/표시·기절/봉인·드론 증감59검사 |
| `Tools/verification/FgtPolishLiveChecks.cs` | D07~D11 1차의 역사적 도구. 젤탕 1강/MAX 가정은 최종 규칙으로 폐기 |
| `Tools/verification/DroneOrbitChecks.cs` | H 궤도 배치 검증 |
| `Tools/verification/HackingChecks.cs` | 해킹 스택 생산·소비 검증 |
| `Tools/verification/HackingPlayChecks.cs` | 해킹 플레이 흐름 검증 |
| `Tools/verification/BetangReturnFlowChecks.cs` | 베탕 자폭·복귀 흐름 |
| `Tools/verification/hacking-pipeline.ps1` | 해킹 검증 일괄 실행 |
| `Assets/WorkSpace/USW/Scripts/Editor/DroneTotemIntegrationChecks.cs` | 드론×토템 셀 버프 연동 검증 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/drone-unit-system.md` | 드론 GDD(마지막 문단은 낡음, 위 함정 참고) |
| `design/gdd/drone-hacking-deck.md` | 해킹 수치·카드 전환·부분 소비 규칙 |
| `docs/technical/drone-damage-variance-design.md` | D4 피해 규격 |
| `docs/technical/drone-orbit-presentation.md` | H 궤도 연출 |
| `docs/technical/betang-spine.md` | 베탕 Spine·버튼 자폭 이벤트 |
