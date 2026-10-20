---
system: levelup
title: EXP·선택지 (경험치·레벨업·카드 추첨·효과 적용)
keywords: expmanager, exp 구슬, 경험치, 레벨업, levelup, level up, 선택지, 카드 효과, 선택 카드, choice, 추첨, 재추첨, reroll, levelupdata, levelupmanager, selection, 획득 선택지, 보관함, 설명 문구, 용어, 오버드라이브
verified: 2026-10-21
commit: 2e76748e
---
# EXP·선택지 카드

**한 줄 요약**: 보스 피해 → EXP 구슬 연출 → `ExpManager`가 레벨업 → `GameManager`가 LevelUp 상태로 바꾸고 선택 화면을 연다 → 선택된 족장의 카드 풀에서 3장 추첨 → 고른 카드의 조합형 정의(`LevelUpData.Composition`)가 지속 효과와 일회 명령으로 나뉘어 적용된다. 소비자(유닛·드론·경제·족장)는 기능별 reader 인터페이스로만 효과를 읽는다.

## 흐름
1. EXP: 보스 피해 → `ExpEffectController`가 구슬을 날린 뒤 `ExpManager.AddExp`(배율은 현재 BossData). 레벨 표는 `ExpLevelData` SO.
2. 레벨업: `ExpManager.OnLevelUp` → `GameManager.OnLevelUpStateEntered` → 선택 UI(`InGameInstaller`가 연결). 튜토리얼 등은 `ExpManager.DeferLevelUps`로 미룬다. 선택이 끝나면 `GameManager.OnLevelUpChoiceMade` → `ExpManager.FlushPendingLevelUp`.
   - 2026-10-20 사용자 결정/미커밋 수정: 보류권은 횟수로 보존하며, 선택 완료마다 보류권 1개 또는 잔여 EXP의 다음 레벨 1개를 처리한다. 잔량은 EXP 바에 유지한다. [회귀 근거](../../../outputs/ingame-qa-followup-20261020/RESULT.md).
3. 풀 확정: `LevelUpManager.Init`이 `ChieftainSelection.GetSelectedLevelUpPool`로 런 시작 때 한 번 풀을 고정한다.
4. 추첨: `LevelUpManager.GetRandomChoices` → 씬 조건 수집(부족 존재 등) → 순수 추첨기 `LevelUpDrawer`. 선택지 수는 런 패널티로 줄 수 있다(`EndlessRunService.GetChoiceCount`).
5. 설명: `LevelUpManager.PrepareChoice`/`LevelUpManager.GetChoiceDescriptionSnapshot`이 **런 캐시의 확정 값**으로 문구를 만든다. 반복 조회해도 재추첨하지 않으며, 실제 획득도 같은 값을 쓴다.
6. 적용: `LevelUpManager.TryApplyChoice` → `LevelUpRunState.TryAcquire`(중복 획득 거절) → 지속 효과 재계산(`SelectionEffectReader`) → 변경 알림 → `SelectionCommandExecutor.Execute`(식량 지급·유닛 지급·토템 요청·재추첨 등 일회 명령).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpManager.cs` | EXP 누적·레벨업·지연/취소 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpLevelData.cs` | 레벨별 필요 EXP SO |
| `Assets/WorkSpace/HSD/Scripts/UI/Effect/ExpEffectController.cs` | EXP 구슬 연출과 실제 지급 시점 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/ExpBarUI.cs` | EXP 바 |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpManager.cs` | 씬 진입점. 추첨·상태·지급 조정(전투 기능 자체는 실행 안 함) |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpDrawer.cs` | 순수 추첨기 |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpData.cs` | 카드 SO. `Composition`(schema 1)이 정본, 옛 필드는 숨김 |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpPoolData.cs` | 카드 풀 SO(족장별) |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/LevelUpRunState.cs` | 런 상태: 획득 이력·확정 프리뷰·활성 기여 |
| `Assets/WorkSpace/USW/Scripts/Selection/SelectionEffectReader.cs` | 지속 효과 계산 결과. 기능별 reader 구현 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/ISelectionCombatReader.cs` | 전투 소비자용(치명·버스트·행 보정 등) |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/ISelectionEconomyReader.cs` | 경제 소비자용(소환 할인·판매 보너스 등) |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IDroneEffectReader.cs` | 드론 소비자용 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IChiefSelectionReader.cs` | 족장 스킬용(쿨감·연속 사격) |
| `Assets/WorkSpace/USW/Scripts/Selection/SelectionCommandExecutor.cs` | 일회 명령 실행 |
| `Assets/WorkSpace/USW/Scripts/Selection/SelectionDescriptionFormatter.cs` | 설명 토큰 → 확정 수치 문구 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/*Definition.cs` | 효과·명령 정의 타입들(`StatModifierDefinition`, `GiveFoodCommandDefinition` 등) |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpCardUI.cs` | 선택 카드 한 장 UI |
| `Assets/WorkSpace/USW/Scripts/IngameUI/Descriptions/DescriptionLinkInteraction.cs` | 링크 우선·줄별 18 로컬 단위 확장/가장 가까운 키워드. 취소된 용어 누름은 선택으로 전환하지 않음 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/SelectionInventory/SelectionInventoryUI.cs` | 획득 선택지 보관함 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/SelectionData/*.asset` | 카드 24장 + `DroneLevelUpPool` |

## 불변 규칙·함정
- 설명 링크 터치 여백은 텍스트 로컬18 단위다. 줄바꿈된 링크는 줄별 영역을 사용하며 짧은 동일 링크 터치/멀티터치 취소 계약을 유지한다. EXP 바 트윈은 정지 중에도 잔량을 갱신한다(10-20 별도 미커밋 QA 수정).
- 효과 구조는 [ADR-0001](../architecture/adr-0001-levelup-effects.md)을 따른다. 소비자는 `LevelUpManager.Effects`(디버그용) 대신 **기능별 reader를 주입**받는다.
- 카드 저작은 `Composition`(EffectSchemaVersion 1)만 읽는다. 숨긴 옛 필드(`primaryEffect` 등)와 동시 적용하지 않는다. schema 0 변환은 Editor 이관·역사 fixture 전용이다.
- 표시와 실제 지급은 같은 런 캐시의 확정 값을 쓴다. 설명을 만들 때 난수를 새로 굴리지 않는다.
- 획득 이력은 효과를 제거해도 남는다(`LevelUpManager.RemoveEffect`는 활성 기여만 뺀다). 같은 카드는 한 런에 한 번.
- 적용 중 다시 적용 요청이 오면 큐에 넣는다. 게임 종료 후 요청은 거절한다.
- 카드 ID·GUID·직렬화 enum 값은 바꾸지 않는다(풀·튜토리얼·씬이 참조).
- 오버드라이브(시간 소비) 카드는 설계만 있고 미구현이다([오버드라이브 덱](../../design/gdd/overdrive-deck.md)).

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/LevelUpL5RegressionRunner.cs` | L5 통합 회귀(최신 기준) |
| `Tools/verification/SelectionFlowChecks.cs` | 추첨·선택·명령 흐름 |
| `Tools/verification/SelectionRunStateChecks.cs` | 런 상태·중복 거절 |
| `Tools/verification/SelectionCatalogChecks.cs` | 카드 카탈로그·풀 |
| `Tools/verification/SelectionDescriptionL5Checks.cs` | 설명 문구 토큰 |
| `Assets/WorkSpace/USW/Scripts/Editor/LevelUpPoolChecks.cs` | 풀 저작 검사 |
| `Assets/WorkSpace/USW/Scripts/Editor/ExpEffectRouteChecks.cs` | EXP 구슬 경로 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `docs/technical/levelup-system.md` | 선택지·필드 보기·정지 원본 |
| `docs/architecture/adr-0001-levelup-effects.md` | 카드 효과 구조 결정 |
| `docs/implementation/levelup-refactor/README.md` | L0~L5 이관 작업판(L5 Editor/Play 검증 기록은 `outputs/levelup-refactor-20261010/L5/RESULT.md`) |
| `docs/technical/exp-system.md` | EXP |
| `design/gdd/drone-hacking-deck.md` | 해킹 카드(감망·델탕) |
