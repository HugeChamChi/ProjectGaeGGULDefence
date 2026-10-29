---
system: drone
title: 드론 (베탕·감망·델탕·젤탕, 해킹)
keywords: 드론, drone, 베탕, betan, betang, 감망, gamman, 델탕, deltan, 젤탕, zeltan, 자폭, 해킹, hacking, 해킹 스택, 궤도, orbit, 집결, rally
verified: 2026-10-30
commit: de2cbd00
---
# 드론 카드

10-24 젤탕 검증 후속 완료: 구형 강화 검사3종을 최신 SO/5단계로 갱신, Edit152·Play67·FGT48 PASS. 기절은 자체분만 멈추며 비례분 유지, 봉인은 해당 젤탕 자체/기당 몫0. 유효 젤탕0이면 독립 기본 드론생산0.15 유지. 실제 지급·열린 정보창·버튼 MAX·여러 젤탕 합산·9106/9112 자연3초 지급 확인. [결과·검사 입력·한계](../../../outputs/zeltan-verification-20261024/RESULT.md).

10-24 확정9113~9124 반영(미커밋): 웨이브 배당(`WaveFoodDividendDefinition`/`WaveFoodDividendRatio`)은 젤탕 배치 시 완료 잔고3%를 전장1회 지급하고 기존 생산 배율에서 분리했다. `WaveManager.TryCompleteRound`→`DroneManager.SettleWaveFoodDividend`에서 런/라운드 중복을 막는다. 수리키트는 발사 베탕과 개체 수명을 폭탄에 전달해 정상/해킹 적중에서 발사자만0.5초 회복한다. 백도어는 `ManaPerCast`/`OverflowManaPerCast`로 초과 여부당 자기 마나1. 레전더리 보장은 후보 소진 필터·성공한 제시 후 소비·후보0이면 보장 유지/일반 후보 제시. Excel 문구24장/분류Note4행 정합. [결과·검증](../../../outputs/selection-final-20261024/RESULT.md).

10-24 추가 확정·검증: 수리 키트는 이미 발사한 자폭의 원 베탕이 이동·교환해도 환급을 유지한다. `_bombRequestLifetime`은 미발사 예약 취소용, `_bombOwnerLifetime`은 발사된 폭탄의 환급용이며 OnDisable에서 무효화한다. 제거 중은 currentCell=null, 판매·합성은 본체 Destroy로 차단한다. 실제 풀 재사용 대상은 자폭 드론이다. Play47(델탕 비행/접촉·판매/합성·자폭 재사용·이동/교환·자동스킬·UI·이전런), Edit47, 전체회귀 PASS. [검증과 입력 범위](../../../outputs/selection-integration-20261024/RESULT.md).

10-23 정산 구조 리팩토링(미커밋): 젤탕은 생산량 계산·지급 완료 연출만 담당하며, `UnitResourceComponent`가 자체 지급과9106의 자체+군단 누적액·시계·지급 이벤트를 소유한다. `DroneManager`는 배치 정산에 포함된 생산자 몫만 개별 드론 지급에서 제외한다. `HasBatchedFoodPayout`은 경제 정책이고 연출은 `OnFoodProduced` 구독자다. 이동/보스 재연결은 같은 인스턴스의 누적액을 보존, 판매/합성으로 인스턴스 제거 시 폐기, 효과 해제는 다음 유효 군단 틱에서 정산하는 기존 동작을 유지한다. [변경·검증](../../../outputs/selection-refactor-20261023/RESULT.md).

G01 단계2(10-21, 미커밋): 24장 콘셉트 분류만 별도 Excel→SO로 구현했다. 기존 드론 효과/수치/조건/확률을 변경하지 않았다. [분류 진입 파일](levelup.md), [분류 데이터 계약](../../data/drone-choice-concepts.md). 추천 UI와 시뮬레이터 소비는 다음 단계다.

별도10-21 족장 변경(미커밋): 집결은 참가 드론의 현재 공격력 합 기반,9105는 족장 자체가 사용 가능한 공유 해킹 스택을 전량 예약·유효 적중에서 소비한다. 일반 감망의 소비 한도/피해식은 유지한다. [족장 카드](chieftain.md), [결정·수식](../../../design/quick-specs/chief-skill-cards-2026-10-21.md), [82검사](../../../outputs/chief-damage-20261021/RESULT.md). 과거 `ApplyEmergencyBuffs` 추가 감망 시전은 실제 족장 경로에서 쓰지 않는다.

**한 줄 요약**: 배치형 스포너 3종(베탕·감망·델탕)은 전투 드론을 소유하고 일반 공격을 드론으로 수행한다. 젤탕은 드론·공격·스킬이 없는 식량 생산자이며 자체 생산과 군단 식량 기여를 제공한다. 감망·델탕은 보스별 공유 해킹 스택을 생산·소비한다.

## 2026-10-30 정적 대조
인게임 스코프에 StatusPopupCatalog 필수 로드와 Scoped StatusPopupService 등록이 추가됐다. 드론 수치·스택 정책과는 별개인 표시 서비스다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. 배치 `DroneSpawnerBase.OnUnitPlaced` → `DroneSpawnerBase.RefreshCombatDrones`가 전투 드론을 상한까지 보충(`DroneSpawnerBase.CreateCombatDrone`) → `DroneManager.RegisterDrone`. 제거 시 `RM.Destroy`로 반환한다. 별도 DronePool은 없다.
2. 전투 드론 `DroneUnit`이 오너 스탯으로 자동 사격하고, SO 설정의 H 궤도로 오너 주위를 돈다(`DroneUnit.StartOrbit`).
3. 베탕 스킬: `Drone_Betan.OnSkillFull` → Spine 버튼 이벤트 → `Drone_Betan.LaunchPendingSkillBombs` → `SelfDestructDrone.Initialize(int damage, bool critical, …)`. 피해 계수는 `UnitData.SelfDestructAttackCoefficient`(D4 구현 완료). 해킹 선택지에서는 `SelfDestructDrone.InitializeHacking`이 폭발 피해 대신 `DroneHackingRuntime.AddFromBomb`으로 스택을 생산하고, `HackingHud`가 만든 `BombHackFeedback`이 9122 B 글리치 적중·개별 수신을 그린다(HUD 숫자=장부−미도착 신호). 9103 일제 출격은 `DroneManager.TickSelections` → `Drone_Betan.SpawnFleetBomb(순번)`으로 순번×`_volleySpacing` 이어서 출발한다([결과](../../../outputs/bomb-hack-ingame-20261021/RESULT.md)).
4. 해킹: 델탕이 소유 드론의 일반 공격 횟수로 충전(`Drone_Deltan.RequiredAttacks`)하고 스택을 생산한다. 감망은 `Drone_Gamman.CastHacking`에서 `DroneHackingRuntime.Reserve`로 스택을 확보한 뒤 `DroneHackingRuntime.Commit` 또는 `DroneHackingRuntime.Refund`로 마무리한다. 장부는 `HackingStackLedger`.
5. 젤탕은 공격·스킬이 없는 식량 생산자다(10-23 비례 생산, 미커밋). 자체 생산 `UnitData.foodProduction` 4/5/6/8 × 자체 선택지 배율(강화 없음) + 전투 드론1기당 `UnitData.fleetFoodPerDrone` 0.25/0.35/0.5/0.7 + `UpgradeManager.ZeltanFoodPerDroneBonus`(단계당 +0.04). `DroneManager.BaseFoodPerDrone`은 유효(배치·비봉인) 젤탕의 기당 계수를 **모두 합산**하고, 없으면 0.15. 젤탕 강화만 `UpgradeSettings.ZeltanCosts` 100/400/800/1400/2300 최대5단계(`UpgradeManager.IsMaxLevel`, UI MAX), 나머지4종은 100×2 무한. `DroneManager.RegisterFoodProducer` / `DroneManager.UnregisterFoodProducer`로 등록·해제한다. [결과](../../../outputs/zeltan-proportional-20261023/RESULT.md)
6. 족장 집결: `DroneManager.ExecuteRallyAsync`가 드론을 직접 이동시키고 복귀시킨다(`DroneUnit.MoveToAsync`, `DroneUnit.ReturnToHomeAsync`).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/DroneUnits/DroneSpawnerBase.cs` | 스포너 기반(UnitBase). CanBasicAttack=false, 드론 보충·반환, 등급 변화 반영, 선택지 구독 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/HackingDroneSpawner.cs` | 감망·델탕의 기반. 해킹 시전·취소 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Betan.cs` | 베탕. 자폭 드론 예약·발사(Spine 이벤트 연동) |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Gamman.cs` | 감망. 소비 스택 추가 피해에 현재 전투 드론 수 배율 적용. 여운 코드는 옛 정의 호환용이며 현행9109는 델탕 압축 업로드 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Deltan.cs` | 델탕. 공격 횟수 충전, 스택 생산, 9108 확률 2배 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/Drone_Zeltan.cs` | 젤탕. 자체 생산·기당 계수(강화 포함)·이 젤탕 몫 군단 식량·드론 기수 표시값 |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeManager.cs` | 런 강화 상태·할인·성공 시 효과음. 젤탕만 5단계 상한, 나머지 무한 강화, 반복 효과 O(1), float 비용 오버플로 결제 거절 |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeSettings.cs` | 공통 100×2 비용·전투 증가량, 젤탕 단계별 비용표·기당 증가량 SO |
| `Assets/WorkSpace/USW/Scripts/Upgrade/UpgradeKeyOverrides.cs` | 등급별 characterId를 독립 강화 키로 연결 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UpgradeModel.cs` | 동일 강화 서비스에서 단계·비용·효과 설명 조회 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UI_UpgradePanel.cs` | 5개 강화 카드의 외곽·비율을 보존해 폭에 맞춰 배치 |
| `Assets/WorkSpace/HSD/Scripts/UI/Upgrade/UI_UpgradeItem.cs` | 강화 버튼·큰 비용 축약·동일 SO 기반 효과 안내, 젤탕 최대 단계 MAX |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/DroneHackingRuntime.cs` | 보스별 공유 해킹 스택 추가·예약·확정·환불. 옛 여운 정의의 잔여 틱 호환 경로도 남아 있음 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/HackingStackLedger.cs` | 스택 장부(용량·예약) |
| `Assets/WorkSpace/USW/Scripts/Drone/DroneManager.cs` | 드론 등록, 식량 틱, 군단 버프, 집결 폭발 |
| `Assets/WorkSpace/USW/Scripts/Drone/DroneUnit.cs` | 영구 잔존 전투 드론. 자동 사격, H 궤도/고정 슬롯 |
| `Assets/WorkSpace/USW/Scripts/Drone/SelfDestructDrone.cs` | 자폭 드론. 차징 후 돌진·폭발 또는 해킹 스택 생산, 9103 출격 순번 |
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BombHackFeedback.cs` | 9122 적중 글리치·자폭당 HACK 수신 표시. 설정 `Resources/BombHackFeedbackSettings.asset` |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/BetangSpineVisual.cs` | 베탕 Spine 모션 |
| `Assets/WorkSpace/USW/Scripts/Combat/Visual/SpineAttackSpeedSync.cs` | 본체/전투 드론의 공용 공속 시각 보정·기절 우선. 전투 드론은 EffectiveAttackInterval 사용 |
| `Assets/WorkSpace/USW/Scripts/Drone/SpineActorVisual.cs` | 일반 Spine 모션. 드론 start/loop/end 전체 길이로 속도 계산 |
| `Assets/WorkSpace/USW/Scripts/DroneUnits/SpineSkillLaunchEvent.cs` | Spine 이벤트 → 스킬 발사 연결 |
| `Assets/WorkSpace/USW/Scripts/Selection/DroneSelection/DroneSelectionState.cs` | 드론 선택지의 런 상태 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IDroneEffectReader.cs` | 스포너가 읽는 드론 선택 효과 인터페이스 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/HackingCompressionDefinition.cs` | 9109 넹...(델탕 압축 업로드), 필요 공격 수와 생산 배수 정의 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitResourceComponent.cs` | 젤탕 묶음 생산의 누적액/지급 시계/이벤트, 일반 생산과 이중 지급 차단 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameLifetimeScope.cs` | DroneManager 등록. `isDron` 조건 안에서만 등록되니 주의 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/DroneUnits/UnitData_*.asset` | 유닛별 UnitData(등급별 PerTier 값, 자폭 계수, Hacking 참조) |
| `Assets/WorkSpace/USW/Resources/UpgradeSettings.asset` | 공통 100×2 비용·공격력8%/공속2%·젤탕 기당+0.04·비용100/400/800/1400/2300 |
| `Assets/WorkSpace/USW/Resources/UpgradeKeyOverrides.asset` | 젤탕 1004~1007 포함 강화 키 매핑 |
| `Assets/WorkSpace/HSD/Resources/Data/UpgradeModel.asset` | 델탕/감망/베탕/디시그망/젤탕 5개 강화 UI 목록·아이콘 |
| `Assets/WorkSpace/USW/Prefab/Unit/DronUnit/DroneUnit_Zeltan.prefab` | 젤탕 프리팹. 군단 생산량은 강화 SO로 이관 |
| `Assets/WorkSpace/USW/Data/DroneUnits/DroneHacking.asset` | 공유 해킹 설정(DroneHackingData) |
| `Assets/WorkSpace/USW/Data/DroneUnits/*HackingSkill.asset` | 감망·델탕 해킹 SkillData |
| `Assets/WorkSpace/USW/Data/DroneUnits/CombatDroneOrbit.asset` | H 궤도 설정(크기·위상·정렬·좌우) |
| `Assets/WorkSpace/USW/Data/DroneUnits/*FormationSettings.asset` | 궤도를 끈 경우의 고정 슬롯 대형 |

## 불변 규칙·함정
- 10-28 정적 대조: 위10-21~24의 미커밋 표기는 당시 기록이다. 현재 HEAD의 정산/강화/수리 키트/백도어 구현과 일치하는지 대조했다. **9109 넹...(델탕 기본 충전9, 생산2배인 압축 업로드)**의 현재 진입은 Drone_Deltan.RequiredAttacks/ExactProduction과 HackingCompressionDefinition이다. 감망 여운을 현행9109로 안내하지 않는다. [이번 대조·과거 검증 경계](../../../outputs/harness-refresh-20261028/RESULT.md).
- G04(10-21 미커밋)는 일반 공격/대기 트랙만 공속에 동기화한다. 베탕 스킬 버튼0.9초와 발사/피해/궤도는 유지한다. 전투 드론3종에는 발사 프레임 이벤트가 없으며 추가하지 않았다. [규칙·실측](../../technical/spine-attack-speed.md).
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
| `Tools/verification/run-regression.ps1` | 현행 5종 회귀 단일 명령. Edit/Play 완료 대기·실패 집계·원상 복원. [운영 원본](../../technical/regression-checks.md) |
| `Tools/verification/DroneCardsChecks.cs` | Composition 확률 0/1·충전/카탈로그 유지 검사 |
| `Tools/verification/DroneSizeSpeedChecks.cs` | H 궤도 한 주기와 고정 슬롯 모두 검사. 크기/방향/정렬/풀/총구/복귀/SO 공속 |
| `Tools/verification/SpineAttackSpeedChecks.cs` | 실제 BossLab 위협/복귀, 소유 드론 속도·스턴·풀 재사용 |
| `Tools/verification/SpineAttackSpeedAudit.cs` | 본체/전투 드론 모션 길이·상한·Spine 이벤트 조사 |
| `Tools/verification/InfiniteUpgradeChecks.cs` | 전투4종 12단계/고단계·젤탕5단계 비용/MAX·할인환급·수치 경계152검사 |
| `Tools/verification/InfiniteUpgradeLiveChecks.cs` | 실제 강화 버튼/MAX·자체/기당·다중 젤탕·지급/정보창·기절/봉인·9106/9112 자연3초 지급67검사 |
| `Tools/verification/FgtPolishLiveChecks.cs` | D07~D11 조작/오디오/레이아웃 회귀. 현재 저작5단계 비용/MAX로 갱신한48검사 |
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

## 보완 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Combat/Fx/BombHackFeedbackSettings.cs` | 9122 해킹 자폭의 글리치 흡수 표시 설정, 스택 수치 정책과 분리 |
