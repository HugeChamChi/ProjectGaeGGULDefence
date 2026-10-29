---
system: totem
title: 토템
keywords: 토템, totem, td10, 인벤토리, 토템 배치, 토템 회전, 토템 판매, 토템 보상, 버프 셀, totemdata, 뿌리내림, 그림자 토템
verified: 2026-10-30
commit: de2cbd00
---
# 토템 카드

**한 줄 요약**: 어려운 보스 구간을 완료하면 보상 3택 → 인벤토리(최대 5칸) → 배치·회전·이동·회수·맞교체. 효과는 "범위 안 셀에 버프를 칠하는" 방식으로만 적용된다.

## 2026-10-30 정적 대조
공용 보관함 닫기 버튼과 일반/하드/튜토리얼의 null override 제거가 커밋에 포함됐다. 슬롯 드롭 판정 중에는 패널을 숨겨 배경의 입력 가로채기를 피하고, 잘못 놓기·비동기 배치 실패 시 보관함을 다시 연다. OnDisable 취소는 보관함을 열지 않는다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. `WaveManager.GrantsTotemReward` → `WaveManager.OnTotemSelectionRequested`. 레벨업 경로는 `LevelUpManager.OnTotemSelectionRequested`. 둘 다 `InGameInstaller`가 `TotemRewardUI.Show`에 연결한다.
2. `TotemRewardUI`가 `TotemChoiceRoller.Roll`로 후보를 뽑고, 선택하면 `TotemInventory.TryAdd`.
3. 보관 토템 드래그는 `TotemInventoryUI` → `TotemInventory.TryPlaceAsync` → `TotemSpawner.PlaceOrExchangeTotemAsync`. 지정한 빈 칸은 배치, 기존 토템 칸은 맞교체한다. 유닛/봉인 칸은 거절하며 로딩 뒤 대상/제한을 재검증한다.
   필드 이동 드래그는 `TotemInventoryUI.BeginFieldDrag`로 기존 보관함을 열고, `TotemInventoryUI.TryCompleteFieldDrag` → `TotemInventory.TryRecall` → `TotemSpawner.RemoveTotemFromField`로 회수한다. 가득 차면 회수는 거절하고 맞교체는 허용한다. 토템 이동은 판매 띠로 넘어가지 않는다.
4. `TotemBase.SetTotemData` → `TotemBase.OnPlaced` → `TotemBuffManager.RegisterTotem` → `TotemBuffManager.RebuildCellBuffFlags`.
5. 재계산할 때마다 셀을 초기화한 뒤 각 토템의 `PaintAffectedCells`를 다시 실행한다(상태 없음). 회전은 `TotemBase.SetRotationStep`, 판매는 `TotemSpawner.SellTotem`. 판매 제거/환급은 유지하며 성공 경로에서 중앙 안내 `토템이 판매되었습니다`를 표시한다(2026-10-20).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Totem/TotemBase.cs` | 모든 토템의 기반. 배치·제거·회전·이동/배치 미리보기, 봉인(점유는 유지하고 효과만 중단) |
| `Assets/WorkSpace/USW/Scripts/Totem/TotemData.cs` | 토템 SO 정의. 효과 그룹, 범위, 설명 문구(`[대괄호]` 색칠), 성장 단계, 디버프 연결 |
| `Assets/WorkSpace/USW/Scripts/Totem/TotemBuffManager.cs` | 셀 버프 최종 재계산, 전역 보너스, 선택지 효과 교체, 보스 등장 시간 조건 |
| `Assets/WorkSpace/USW/Scripts/Totem/TotemSpawner.cs` | 생성·배치·판매 서비스. `data.prefab`이 없으면 공용 프리팹에 SO를 주입 |
| `Assets/WorkSpace/USW/Scripts/Totem/TotemInventory.cs` | 씬 범위 보관함(일반 C# 클래스, VContainer Scoped). 원본 SO/방향 보관, 회수·맞교체, 성공 시에만 소비 |
| `Assets/WorkSpace/USW/Scripts/Totem/RangedBuffTotemBase.cs` | 범위형 토템 기반. 하위 클래스는 `PaintRangeBuffs` 하나만 구현 |
| `Assets/WorkSpace/USW/Scripts/Totem/GenericBuffTotem.cs` | 수치+범위 토템의 공용 스크립트. 효과 그룹별로 자기 셀에만 적용 |
| `Assets/WorkSpace/USW/Scripts/Totem/TotemEffectGroup.cs` | 색·범위·기능을 독립으로 가진 효과 그룹 |
| `Assets/WorkSpace/USW/Scripts/Totem/ITotemFunction.cs` | 셀 효과 인터페이스(무상태 재계산) |
| `Assets/WorkSpace/USW/Scripts/Totem/ITotemRange.cs` | 범위 인터페이스(실제 셀 / 미리보기 오프셋) |
| `Assets/WorkSpace/USW/Scripts/Totem/Totem*.cs` | 개별 효과·범위·조건 구현(TotemSpeedBuff, TotemShadowAttack 등) |
| `Assets/WorkSpace/USW/Scripts/IngameUI/TotemReward/TotemRewardUI.cs` | 보상 선택창(튜토리얼 탐색 모드 포함) |
| `Assets/WorkSpace/USW/Scripts/IngameUI/TotemInventoryUI.cs` | 기존 인벤토리 슬롯·드래그 배치/맞교체·필드 회수 드롭 UI |
| `Assets/WorkSpace/USW/Prefabs/UI/TotemInteractionUI.prefab` | 보관함 공용 닫기 버튼과 슬롯. 씬의 null close override를 다시 만들지 않음 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs` | 보상 Opening의 필드 드래그 취소와 정보창 정리 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/TotemActionPopupUI.cs` | 배치된 토템의 선택·판매 팝업(회전은 필드 홀드 조작) |
| `Assets/WorkSpace/HSD/Scripts/UI/Totem/TotemInfoPresenter.cs` | 토템 정보창 Presenter |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameLifetimeScope.cs` | 등록: TotemSpawner, TotemInventory(Scoped), TotemInventoryUI, TotemBuffManager |

| `Assets/WorkSpace/USW/Scripts/IngameEtc/WorldInteractionSettings.cs` | 필드/인벤토리 공용 터치 거리와 투영 환산 |
| `Assets/WorkSpace/USW/Resources/WorldInteractionSettings.asset` | 1080 기준 시작12px·경계10px |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/TotemData/Playable/TD*Data.asset` | 실제로 쓰는 토템별 TotemData SO(TD1001~) |
| `Assets/WorkSpace/USW/Data/TotemData/Playable/TD*.prefab` | 토템별 프리팹(특수 동작/외형) |
| `Assets/WorkSpace/USW/Data/TotemData/GeneralTotemPrefab.prefab` | `data.prefab`이 없는 토템이 쓰는 공용 프리팹 |
| `Assets/WorkSpace/USW/Data/TotemData/Playable/README.md` | Playable 폴더 구성 안내 |
| `Assets/WorkSpace/USW/Data/TotemData/TotemEffectGroupsGuide.md` | 효과 그룹 저작 가이드 |

## 불변 규칙·함정
- D13 10-28 수정(미커밋): 공용 `TotemInteractionUI.prefab`에 닫기 버튼 추가, 일반/하드/튜토리얼의 null 참조 override 제거. 슬롯 드래그 취소/잘못 놓기/배치 거절은 아이템과 보관함을 복원하고, 성공한 배치/맞교체만 닫는다. 가로 끌기는 배치 드래그이며 좌우 스크롤은 아니다. 만재 보상3종 좌우/뒤로/확정 및 기존5개 유지+식량500 대체 보존. [수정/Play63검사](../../../outputs/totem-d13-fix-20261028/RESULT.md), [최초 재현](../../../outputs/totem-d13-20261028/RESULT.md). 검사 진입 `Tools/verification/TotemFullInventoryD13Checks.cs`.
- 필드 이동은 InputManager/DragHandler의 타일 잡기·잡은 위치 보존·마지막 놓기 좌표를 공유한다. 인벤토리 드롭 경계도 주입한 WorldInteractionSettings의 1080 기준10px를 사용한다. 보상 목록의 이름/설명은 UiTypography 본문32 이상이다.

- 수치는 TotemData SO만 쓴다. 토템 시트 런타임 동기화(G04)는 2026-10-13에 제거됐다.
- 지급 시점: 어려운 보스 구간을 완료했을 때만(NORMAL/HARD는 역할 SO 기준 3·6·10·13·16·20…, 19는 아님). 일반 구간은 선택창을 건너뛴다.
- 셀 버프는 재계산 사이클로만 적용한다. RangedBuffTotemBase의 ApplyBuff/RemoveBuff는 일부러 비워 둔 것이니 거기에 효과를 넣지 않는다.
- 미리보기(`TotemBase.PreviewMoveTo`, `TotemBase.PreviewRotation`, `TotemBase.PreviewPlacement`)는 실제 칸·버프·확정 회전을 바꾸지 않는다.
- 공격 디버프(아머 등)는 토템이 겹쳐도 공격 시도당 1스택이다(`TotemBuffManager.ApplyAttackDebuff`).
- 정보창·미리보기·실제 범위는 같은 범위 규칙을 쓴다. 범위를 바꿀 때는 셋 다 확인한다.

- 회수는 원본 SO와 방향만 보관하고 필드 효과/점유를 해제한다. 성장/수확은 기존 이동의 제거·재배치 생명주기대로 초기화된다. 가이드 배치 제한 중 회수로 우회하지 않는다. 회수·맞교체는 식량을 지급하지 않는다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/TotemFullInventoryD13Checks.cs` | 만재5칸 닫기/취소 복원/배치 거절/성공 배치/맞교체/보상 좌우와 확정, 10-28 Play63검사 |
| `Tools/verification/SelectionDragCancelChecks.cs` | 토템 보상/레벨업 진입 시 기존 필드 누르기·드래그 취소, 10-28 Play45검사 |
| `Tools/verification/TotemTransferLiveChecks.cs` | 기존 IngameScene 서비스·UGUI 회수/맞교체·방향·취소·가득 참·성장 초기화 Play 검증 |
| `Tools/verification/TotemLiveChecks.cs` | 실제 플레이 흐름 검증(run_script) |
| `Tools/verification/TotemGrowthLiveChecks.cs` | TD1002 성장 단계 실행 검증 |
| `Assets/WorkSpace/USW/Scripts/Editor/TotemBehaviorChecks.cs` | 셀 버프 재계산·효과 동작 에디터 검증 |
| `Assets/WorkSpace/USW/Scripts/Editor/TotemInventoryChecks.cs` | 인벤토리 추가·배치·취소 검증 |
| `Assets/WorkSpace/USW/Scripts/Editor/TotemDataValidator.cs` | TotemData SO 저작 오류 검사 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/totem-system.md` | 토템 GDD(지급 규칙, 결정 이력) |
| `docs/technical/totem-so-authoring-guide.md` | TD1001~1006 SO 저작 |
| `docs/technical/totem-expansion-td1007-1010.md` | TD1007~1010 |
| `docs/technical/totem-td1002-rooting-redesign.md` | TD1002 뿌리내림(시간 성장) |
| `docs/technical/persistent-shadow-totem.md` | 그림자 토템 상시 부착 |
