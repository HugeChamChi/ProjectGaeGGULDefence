---
system: grid
title: 그리드·소환·합성·판매
keywords: 그리드, grid, gridcell, 셀, 칸, 빈칸, 소환, summon, spawn, 소환 비용, 합성, merge, 머지, 와일드카드, wildcard, 조커, 판매, sell, 드래그, drag, 이동, 봉인, seal, unitfactory, unitspawner, mergemanager
verified: 2026-10-21
commit: 2e76748e
---
# 그리드·소환·합성 카드

**한 줄 요약**: 고정 격자(크기는 GameConfig `gridColumns`×`gridRows`)의 각 `GridCell`이 유닛 1기 또는 토템 1개를 점유한다. 소환 버튼은 식량을 쓰고 빈 칸 하나에 노말 유닛을 놓으며, 같은 유닛·같은 등급 2기를 드래그로 겹치면 다음 등급 랜덤 유닛 1기가 된다.

## 흐름
1. 소환: `UnitSpawner.OnSpawnButtonPressed` → Playing 상태 확인 → 할인 적용 비용으로 `CurrencyManager.Spend` → 빈 칸 랜덤 선택(튜토리얼은 고정 칸·고정 유닛) → `UnitFactory.CreateRandomNormalUnit`(파티 SO 균등 풀) 또는 소환 확률 경로 → `UnitSpawner.PlaceUnitWithEffect` → 비용 증가.
2. 실패하면 쓴 식량을 돌려준다(빈 칸 없음·생성 실패). 거절 사유는 `UnitSpawner.OnSpawnRejected`로 UI에 전달한다.
   시작 준비/선택 중/전투 종료/준비 실패는 각 상태에 맞는 안내를 표시한다. Faulted에서는 GameManager.FailureReason을 표시하며 비용 계산 전에 거절한다. 에디터 직접 Play는 별도 `EDITOR_PREVIEW` 런으로 자동 시작한다([게임 진행](game-flow.md)).
3. 생성: `UnitFactory`가 프리팹을 만들고 `UnitBase.Init`에 `UnitDependencies`를 넘긴다. 위치는 `UnitFactory.InitUnitTransform`(유닛 SO Y 보정)으로 통일한다.
4. 합성: `InputManager` → `DragHandler.OnEndDrag` → `MergeManager.TryMergeByDrag` → `UnitMergeRules.CanPair` → 재료 2기 `OnRemoved`·제거 → 드롭한 칸에 `UnitFactory.CreateRandomUnitOfTier`(다음 등급) → `UnitSpawner.RequestMergeSupport`(합성마다 보너스 추첨).
5. 이동: 같은 드래그가 빈 칸이면 `DragHandler.PlaceSelfAt`. 판매: `UnitSpawner.SellUnit`(판매 버튼) 또는 판매 띠 위에서 놓기(`DragSellService.TrySell`, 같은 경로).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Grid/GridManager.cs` | 격자 생성·조회(빈 칸/점유 칸/좌표), 보스 예고 표시, 토템 범위 미리보기 |
| `Assets/WorkSpace/USW/Scripts/Grid/GridCell.cs` | 칸의 점유(유닛·토템) API와 토템 효과 설정 진입점 |
| `Assets/WorkSpace/USW/Scripts/Grid/GridCellModel.cs` | 칸 상태: 토템 버프 플래그·보정치, 보스 디버프, 봉인/영구 봉인, 미리보기 상태 |
| `Assets/WorkSpace/USW/Scripts/Grid/GridCellView.cs` | 칸 상태 표시 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitSpawner.cs` | 소환 버튼·비용·판매·외부 보상 배치(`PlaceUnitWithEffect`)·합성 보너스 대기열 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/UnityAudioProvider.cs` | 소환/클릭 재요청 시 큐 상한 안에서 가장 오래된 보이스 교체. 같은 프레임은 시작 순번으로 구분 |
| `Assets/WorkSpace/USW/Data/Audio/StudioDartPalette.asset` | 소환음 한 번분 연결·동시2/간격0 저작, BGM은 독립 루프 |
| `Assets/WorkSpace/USW/Audio/StudioDartEdited/SFX_UnitSummon_OneShot.wav` | 05원본 첫0.32초 파생본. 원본/배경음 보존, 등장 약0.25초에 맞춘 소환1회 SFX |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitFactory.cs` | 유닛 생성(랜덤 노말/등급별/부족별/SO 직접), 위치 초기화 |
| `Assets/WorkSpace/USW/Scripts/Unit/MergeManager.cs` | 선택·드래그 합성, 진로 계승 보너스 |
| `Assets/WorkSpace/USW/Scripts/Unit/UnitMergeRules.cs` | 합성 가능 조건(원래 데이터·원래 등급 기준, 노말+와일드카드) |
| `Assets/WorkSpace/USW/Scripts/Unit/WildcardUnit.cs` | TD1004가 만드는 노말 합성 재료(공격·스킬·생산 없음) |
| `Assets/WorkSpace/USW/Scripts/Unit/MergeEffectPlayer.cs` | 합성 연출(잔상 흡입·먼지구름) |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/InputManager.cs` | 터치 입력 → 점유 타일 우선 잡기·마지막 놓기 위치 판정 → `IDraggable` 호출 |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/WorldInteractionSettings.cs` | 기준 화면 너비/드래그 시작 거리/놓기 경계 허용 거리와 원근·직교 투영 환산 |
| `Assets/WorkSpace/USW/Resources/WorldInteractionSettings.asset` | 1080 너비 기준 시작12px·경계10px. InGameLifetimeScope 등록 |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/DragHandler.cs` | 유닛·토템 공용 드래그(클릭·이동·합성·토템 홀드 회전) |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DragSellService.cs` | 드래그 판매 띠(띠가 있는 씬에서만 동작) |

## 불변 규칙·함정
- 합성 조건은 **임시 강화와 무관하게** `OriginalData`·`OriginalTier`로 본다. 레전드는 합성 재료가 아니다. 판매 전에 `UnitBase.RestoreOriginalTier`로 원래 등급을 되돌린다.
- 유닛을 칸에서 뺄 때는 `OnRemoved` → `GridCell.RemoveUnit` → 파괴 순서를 지킨다(효과·구독 정리). 보스가 바뀌면 `WaveManager`가 배치 유닛을 `OnRemoved`/`OnPlaced`로 다시 묶는다.
- 봉인된 칸(`GridCellModel.IsSealed`)은 합성 결과를 받지 않는다. 영구 봉인은 런 패널티(`GridCellModel.SetPermanentSeal`).
- 위치 보정은 `UnitFactory.InitUnitTransform`/`UnitFactory.InitTotemTransform` 하나로 통일. 개별 코드에서 Y값을 따로 넣지 않는다.
- 레거시 시트: 소환 비용 증가·판매가·소환 확률은 `GameDataManager`(Google Sheets CSV)가 로드됐을 때 그 값을 쓰고, 아니면 기본값(20/5)으로 대체한다. 수정 시 SO 우선 정책(AGENTS.md)에 맞춰 이관 범위를 확인한다.
- 족장은 그리드 유닛이 아니다. 칸 점유·소환·판매·합성에 연결하지 않는다.
- 소환음은 궤적 출발에서 한 번 재생한다. 05원본1.8초 연속 샘플을 소환마다 통째로 붙이지 않는다. 파생본을 쓰며 빠른 요청은 최대2보이스 안에서 가장 오래된 소환음 꼬리를 교체한다([수정·36검증](../../../outputs/summon-audio-fix-20261020/RESULT.md)).

- 드래그는 누른 지점의 피벗 간격을 보존한다. 타일 전체에서 점유자를 잡고, Move 없는 Began→Ended도 마지막 위치로 판단한다. 칸 콜라이더 판정이 없을 때만 설정 거리 안의 가까운 시각 경계를 사용한다. 봉인/튜토리얼/판매/회수 제한은 기존 최종 경로에서 검사한다([L24 원인·Play 검증](../../../outputs/touch-typography-20261020/RESULT.md)).

L24의 잡기/놓기 거리와 L31 글씨 확대는 2e76748e에 포함됐다. [실제 드래그·화면비 결과](../../../outputs/touch-typography-20261020/RESULT.md). 소환음/전투 준비 관련 다른 작업의 미커밋 변경은 별도로 남아 있다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/TouchTypographyDragChecks.cs` | 실제 포인터 좌표로 잡기/피벗 보존/취소/빠른 놓기/경계/교환/합성10검사 |
| `Assets/WorkSpace/USW/Scripts/Editor/TotemInventoryChecks.cs` | 칸 배치·취소(토템 쪽 점유 확인 포함) |
| `Tools/verification/IngameEditorPreviewChecks.cs` | 실제 IngameScene 직접 Play→소환/배치, 식량 차감, 준비 실패 후 미차감 확인 |
| `Tools/verification/SummonAudioLiveChecks.cs` | 실제 소환5회/0.08초 간격, 같은 프레임6요청, SFX상한·BGM·기존 HUD/전투음 회귀36검사 |
| `Tools/verification/SummonAudioExtract.py` | 공유05원본 보존·소환1회0.32초 파생본과해시/파형검증 생성 |

위 검사와 별도로, 합성·이동·판매를 바꿨으면 해당 Play 흐름을 직접 확인하고 결과에 적는다.

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `design/gdd/grid-system.md` | 그리드 GDD |
| `design/gdd/unit-system.md` | 유닛·등급·합성 GDD |
| `docs/technical/server-client-data-architecture.md` | 잔존 시트(GameDataManager) 경계 |
