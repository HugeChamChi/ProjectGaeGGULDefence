---
system: grid
title: 그리드·소환·합성·판매
keywords: 그리드, grid, gridcell, 셀, 칸, 빈칸, 소환, summon, spawn, 소환 비용, 합성, merge, 머지, 와일드카드, wildcard, 조커, 판매, sell, 드래그, drag, 이동, 봉인, seal, unitfactory, unitspawner, mergemanager
verified: 2026-10-30
commit: de2cbd00
---
# 그리드·소환·합성 카드

**한 줄 요약**: 고정 격자(크기는 GameConfig `gridColumns`×`gridRows`)의 각 `GridCell`이 유닛 1기 또는 토템 1개를 점유한다. 소환 버튼은 식량을 쓰고 빈 칸 하나에 노말 유닛을 놓으며, 같은 유닛·같은 등급 2기를 드래그로 겹치면 다음 등급 랜덤 유닛 1기가 된다.

## 2026-10-30 정적 대조
GridManager는 UnitStatusBadgeOverlay와 Resources/UnitStatusBadge를 사용한다. 승인된 등급색 숫자/반짝임과 레전드 금별/빛 쓸기 설정은 PR 병합에서도 Develop 값을 유지했다. 선택 화면 Opening은 InputManager.CancelCurrentInteraction으로 기존 필드 입력을 취소한다. 배지 폴더의 Lab 하위는 비교 시안이며 실제 게임 연결과 구분한다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

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
| `Assets/WorkSpace/USW/Scripts/IngameUI/UnitStatusBadge/` | 발밑 등급 숫자/레전드 금별과 스킬 충전 바. Graphic, 공용 Overlay, 설정 SO, 셰이더/머티리얼, Resources 기본 프리팹 |
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
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs` | 레벨업/토템 보상 Opening → CancelCurrentInteraction, 기존 누르기·드래그를 드롭 없이 취소 |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/WorldInteractionSettings.cs` | 기준 화면 너비/드래그 시작 거리/놓기 경계 허용 거리와 원근·직교 투영 환산 |
| `Assets/WorkSpace/USW/Resources/WorldInteractionSettings.asset` | 1080 너비 기준 시작12px·경계10px. InGameLifetimeScope 등록 |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/DragHandler.cs` | 유닛·토템 공용 드래그(클릭·이동·합성·토템 홀드 회전) |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DragSellService.cs` | 드래그 판매 띠(띠가 있는 씬에서만 동작) |

## 불변 규칙·함정
- 10-28 승인된 배지 2안 이식(미커밋): `GridManager.RegisterUnitStatus`가 `UnitStatusBadgeOverlay`를 만들고 null 프리팹은 `Resources/UnitStatusBadge`를 사용한다. 노말/레어/에픽은 등급색 숫자와 가끔 반짝임, 레전드는 금별과 빛 쓸기. `UnitStatusBadge.Refresh`는 `HasSkillCooldownGauge`/`SkillGaugeProgress`, `TryGetVisualBounds`, `DragOffset`을 읽는다. 기존 `UnitStatusGraphic`/`UnitStatusOverlay`/`UnitStatus.prefab`/`UnitSkillGauge`와 에디터는 보존. [이식 결과](../../../outputs/unit-status-badge-port-20261028/RESULT.md).
- 10-28 선택 화면 진입 시 기존 필드 누르기·드래그를 취소한다(사용자 결정, 미커밋). `InGameInstaller`가 레벨업/토템 보상 `Opening`에서 `InputManager.CancelCurrentInteraction()`을 호출한다. 원위치·정렬·판매/회수/범위 표시를 정리하고 손가락 소유권을 지운다. 놓기나 클릭으로 처리하지 않는다. [실제 Play45검사](../../../outputs/selection-drag-cancel-20261028/RESULT.md), 검사 진입 `Tools/verification/SelectionDragCancelChecks.cs`.
- 10-23 상태 점검(미커밋): 생성 연출 중 비활성 점유 유닛은 타일 잡기·교환 대상에서 제외한다. 소환 지연/궤적과 판매 보너스 대기는 씬 파괴 토큰을 사용하고, 완료 시 런 종료·칸 소유권을 재검사한다. 새 보스 재바인딩도 생성 중 유닛의 첫 `OnPlaced`를 선행하지 않는다. [재현·검증](../../../outputs/ingame-state-audit-20261023/RESULT.md).
- 10-22 소환 탭 보존(미커밋): 버튼 자체의 `HudDamageButtonGesture`가 짧은 흔들림/세로 이동을 탭으로 유지한다. HUD폭3% 이상·가로/세로1.5배 이상일 때만 스와이프로 인계한다. 페이지 확정은 기존 폭12%. 실제 Play52검사에서 소환5회·흔들림12연타·그래프 왕복 PASS. 소환 쿨다운 없음/release 클릭/0.25초 등장 연출은 기존 그대로다. [적용·검증·경계](../../../outputs/sortie-tap-fix-20261022/RESULT.md), [이전 원인 증거](../../../outputs/sortie-latency-20261022/RESULT.md).
- 10-22 출격 누르는 즉시 소환(미커밋): `UIManager`가 소환 버튼에 `PressToClickButton`을 런타임 부착, press에서 onClick·release 클릭 끔. 출격 위에서 시작한 스와이프도 페이지는 넘기되 소환은 이미 1회 실행. [결과](../../../outputs/hud-press-swipe-20261022/RESULT.md).
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
| `Tools/verification/UnitStatusBadgePortChecks.cs` | 실제 IngameScene 오프라인 배치/드래그/합성, 칸 내부 경계, 패시브 회색, 등급별 셰이더 애니메이션 픽셀 변화 확인 |
| `Tools/verification/TouchTypographyDragChecks.cs` | 실제 포인터 좌표로 잡기/피벗 보존/취소/빠른 놓기/경계/교환/합성10검사 |
| `Tools/verification/SelectionDragCancelChecks.cs` | 실제 선택 화면 두 종류에서 유닛/토템의 누르기·이동 드래그 취소, 위치/정렬/포인터 소유권 검사 |
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

## 보완 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/UI/SummonButtonReactionData.asset` | 소환 버튼 누름 축소/복원과 입력 반응 저작 SO |
