---
system: levelup
title: EXP·선택지 (경험치·레벨업·카드 추첨·효과 적용)
keywords: expmanager, exp 구슬, 경험치, 레벨업, levelup, level up, 선택지, 카드 효과, 선택 카드, choice, 추첨, 재추첨, reroll, levelupdata, levelupmanager, selection, 획득 선택지, 보관함, 설명 문구, 용어, 오버드라이브, particleimage, quaternion, aabb, assertion
verified: 2026-10-30
commit: 2f719c91
---
# EXP·선택지 카드

## J 추천 뱃지 표시부 (2026-10-30)
사용자 확정 J 빼꼼 개구리(개구리가 살짝 내다보는 추천 뱃지)를 공용 카드에 연결했다. `LevelUpCardUI.SetRecommended(bool)`는 외부 판정의 표시만 담당하며 `LevelUpCardUI.IsRecommended`로 표시 상태를 읽는다. `Setup`마다 숨김으로 초기화하며 자동 추천 판정은 아직 없다. 첫 카드에만 예시 표시한 [인게임 캡처와 Play12/전체 회귀 결과](../../../outputs/choice-j-ingame-20261030/RESULT.md). 사용자는 기존 분류를 이용한 테마별 추천을 원하지만 실제 시스템 설계는 Claude와 별도로 진행하기로 했다. 아래 G01 기록의 분류 데이터 연결과 추천 판단 구현 완료를 혼동하지 않는다.

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Prefab/UI/LevelUp_Selection 1.prefab` | 일반/하드/튜토리얼 공용 카드. J 뱃지 기본 비활성, raycast 없음, 레이아웃 제외 |
| `Assets/WorkSpace/USW/Data/UI/ChoiceRecommendation/SPR_ChoiceRecommendation_J.png` | 사용자 확정 J 시안의 투명 추천 표시 스프라이트 |

병합으로 중복된 `FieldViewEye`의 실험실 코드 사본과 meta는 제거했다. 실험실과 게임이 기존 공용 `LevelUpPresentation/FieldViewEye.cs`를 사용하며 공용 GUID와 씬 참조는 유지된다. 실험실 worktree의 원본 시안 파일은 그대로 보존한다.

10-28 정적 대조: 일반·하드·튜토리얼의 카드 홀드와 필드 보기 버튼은 같은 `UI_Peekthrough`로 0.3초 InOutSine 페이드·터치 해제 후 입력 복원을 공유한다. 카드 홀드 대상 강조는 유지한다. `IngameHudMotion`은 판매/선택지의 소유자별 HUD 수납을 담당하며 전투·토템·딜 그래프 아래/햄버거 오른쪽, InBack0.3/OutBack0.4초로 숨김·복원한다. `LevelUpFieldView`는 토글·실드·문구, LevelUpUI는 선택·정지를 소유한다. 이 구현은 HEAD에 포함된다. 아래 날짜별 미커밋 설명은 작성 당시 기록이다. [이식 검증](../../../outputs/field-view-shared-20261027/RESULT.md), [이번 대조](../../../outputs/harness-refresh-20261028/RESULT.md).

10-24 확정9113~9124 반영(미커밋): 웨이브 배당(`WaveFoodDividendDefinition`/`WaveFoodDividendRatio`)은 젤탕 배치 시 완료 잔고3%를 전장1회 지급하고 기존 생산 배율에서 분리했다. `WaveManager.TryCompleteRound`→`DroneManager.SettleWaveFoodDividend`에서 런/라운드 중복을 막는다. 수리키트는 발사 베탕과 개체 수명을 폭탄에 전달해 정상/해킹 적중에서 발사자만0.5초 회복한다. 백도어는 `ManaPerCast`/`OverflowManaPerCast`로 초과 여부당 자기 마나1. 레전더리 보장은 후보 소진 필터·성공한 제시 후 소비·후보0이면 보장 유지/일반 후보 제시. Excel 문구24장/분류Note4행 정합. [결과·검증](../../../outputs/selection-final-20261024/RESULT.md).

10-24 추가 확정·검증: 수리 키트는 이미 발사한 자폭의 원 베탕이 이동·교환해도 환급을 유지한다. `_bombRequestLifetime`은 미발사 예약 취소용, `_bombOwnerLifetime`은 발사된 폭탄의 환급용이며 OnDisable에서 무효화한다. 제거 중은 currentCell=null, 판매·합성은 본체 Destroy로 차단한다. 실제 풀 재사용 대상은 자폭 드론이다. Play47(델탕 비행/접촉·판매/합성·자폭 재사용·이동/교환·자동스킬·UI·이전런), Edit47, 전체회귀 PASS. [검증과 입력 범위](../../../outputs/selection-integration-20261024/RESULT.md).

10-23 정산 구조 리팩토링(미커밋): `HasBatchedFoodPayout` 경제 정책과 지급 완료 이벤트를 분리했다. 9104 런타임 reader는 `StackDamageBonusPerDrone`/`MaximumStackDamageBonus`로 의미를 명시한다. 기존 `StackSkillFrequencyDefinition` 직렬화명·SO·카드ID는 유지한다. [변경·검증](../../../outputs/selection-refactor-20261023/RESULT.md).

10-23 미커밋: 확정9101~9112의 게임 기능/문구 반영을 카드별로 표시했다([현재 구현표](../../../design/quick-specs/selection-card-discussion-2026-10-23.md), [검증](../../../outputs/selection-agreed-20261023/RESULT.md)). 9102는 모든 소유자의 전투 드론 공속+20%,9104는 드론당 감망 스택 추가 피해+0.5%/무상한,9105는 족장 소비스택당3%,9109는 `HackingCompressionDefinition`(기본 충전9·생산2배). 9106/9112는 젤탕 자체·비례 생산에 합산 적용하며9106은 두 몫의 실제 발생분을3초마다 함께 지급한다. 9113의10-24 채택/구현은 위 최신 기록을 따른다. Excel은10-24 selection-text-20261024-001/drone-concepts-20261024-001로 갱신; 분류 태그/추첨 확률은 변경하지 않았다. 시뮬레이터의 과거 불변 캡처·EXE는 갱신하지 않았다.

G01 단계2(10-21, 미커밋): 드론24장 분류만 Excel→생성 카탈로그로 연결했다. 공용12/자폭만3/해킹만8/9122복수1, 글리치0. 풀의 선택적 ConceptCatalog 참조는 추첨/효과/추천/시뮬 정책에서 아직 소비하지 않는다. [계약](../../data/drone-choice-concepts.md), [실제 Importer53검사·보존](../../../outputs/drone-concepts-20261021/RESULT.md).

## 분류 데이터 진입 파일 (G01 단계2)
| 파일 | 역할 |
|---|---|
| `Data/Balance/DroneChoiceConcepts.xlsx` | Choices 고정ID/태그0·1 및 Meta 스키마/버전/풀 저작 원본 |
| `Assets/WorkSpace/USW/Scripts/Selection/DroneChoiceConcept.cs` | 공용·자폭·글리치·해킹 복수 태그 |
| `Assets/WorkSpace/USW/Scripts/Selection/DroneChoiceConceptCatalog.cs` | ID→태그 조회·버전/출처 SHA/분류 해시 |
| `Assets/WorkSpace/USW/Scripts/Selection/LevelUpPoolData.cs` | 선택적 카탈로그 참조. 추첨 가중치와 독립 |
| `Assets/WorkSpace/USW/Scripts/Editor/DroneChoiceConceptImporter.cs` | 풀ID집합 완전 일치·중복/누락/미등록·태그 검증 후 원자적 Import |
| `Assets/WorkSpace/USW/Scripts/Editor/DroneChoiceConceptCatalogEditor.cs` | 생성 카탈로그 읽기 전용 Inspector |
| `Assets/WorkSpace/USW/Scripts/Editor/DroneChoiceConceptBuildValidator.cs` | 빌드 전 원본/풀/연결 SO 사전 검증 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneChoiceConceptCatalog.asset` | Pool9100·Schema1·drone-concepts-001 산출물 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneLevelUpPool.asset` | 24개 원본 Cards/조건/가중치 보존, 카탈로그 참조1개 |
| `Tools/verification/DroneChoiceConceptChecks.cs` | 실제 Importer53검사·불량 입력 시 파일/메모리/GUID 불변·최종 빌드 사전 검증 |
| `docs/data/drone-choice-concepts.md` | 저작/Import/원자성·SHA와 canonical 분류 해시 규격 유지 원본 |

**한 줄 요약**: 보스 피해 → EXP 구슬 연출 → `ExpManager`가 레벨업 → `GameManager`가 LevelUp 상태로 바꾸고 선택 화면을 연다 → 선택된 족장의 카드 풀에서 3장 추첨 → 고른 카드의 조합형 정의(`LevelUpData.Composition`)가 지속 효과와 일회 명령으로 나뉘어 적용된다. 소비자(유닛·드론·경제·족장)는 기능별 reader 인터페이스로만 효과를 읽는다.

## 2026-10-30 정적 대조
Opening의 필드 입력 취소가 커밋에 포함됐다. 선택 효과를 확정하고 변경 알림을 보낸 뒤 StatusPopupService.ShowSelection으로 점유 유닛에 능력치 문구를 표시하고 명령을 실행한다. 능력치가 아닌 효과는 이 문구 대상이 아니다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. EXP: 보스 피해 → `ExpEffectController`가 구슬을 날린 뒤 `ExpManager.AddExp`(배율은 현재 BossData). 레벨 표는 `ExpLevelData` SO.
2. 레벨업: `ExpManager.OnLevelUp` → `GameManager.OnLevelUpStateEntered` → 선택 UI(`InGameInstaller`가 연결). 튜토리얼 등은 `ExpManager.DeferLevelUps`로 미룬다. 선택이 끝나면 `GameManager.OnLevelUpChoiceMade` → `ExpManager.FlushPendingLevelUp`.
   - 선택 화면 Opening에서는 기존 필드 누르기·드래그를 `InputManager.CancelCurrentInteraction`으로 취소한다(10-28 현재 미커밋 연결). [실제 Play45검사](../../../outputs/selection-drag-cancel-20261028/RESULT.md).
   - 2026-10-20 사용자 결정/미커밋 수정: 보류권은 횟수로 보존하며, 선택 완료마다 보류권 1개 또는 잔여 EXP의 다음 레벨 1개를 처리한다. 잔량은 EXP 바에 유지한다. [회귀 근거](../../../outputs/ingame-qa-followup-20261020/RESULT.md).
3. 풀 확정: `LevelUpManager.Init`이 `ChieftainSelection.GetSelectedLevelUpPool`로 런 시작 때 한 번 풀을 고정한다.
4. 추첨: `LevelUpManager.GetRandomChoices` → 씬 조건 수집(부족 존재 등) → 순수 추첨기 `LevelUpDrawer`. 선택지 수는 런 패널티로 줄 수 있다(`EndlessRunService.GetChoiceCount`).
5. 설명: `LevelUpManager.PrepareChoice`/`LevelUpManager.GetChoiceDescriptionSnapshot`이 **런 캐시의 확정 값**으로 문구를 만든다. 반복 조회해도 재추첨하지 않으며, 실제 획득도 같은 값을 쓴다.
6. 적용: `LevelUpManager.TryApplyChoice` → `LevelUpRunState.TryAcquire`(중복 획득 거절) → 지속 효과 재계산(`SelectionEffectReader`) → 변경 알림 → `SelectionCommandExecutor.Execute`(식량 지급·유닛 지급·토템 요청·재추첨 등 일회 명령).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs` | 레벨업/토템 Opening 연결, 필드 입력 취소 및 정보창 정리 |
| `Assets/WorkSpace/USW/Scripts/IngameEtc/InputManager.cs` | 선택 화면 진입 시 기존 포인터 소유권 해제, 드롭/클릭 없이 취소 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpManager.cs` | EXP 누적·레벨업·지연/취소 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpLevelData.cs` | 레벨별 필요 EXP SO |
| `Assets/WorkSpace/HSD/Scripts/UI/Effect/ExpEffectController.cs` | EXP 구슬 연출과 실제 지급 시점 |
| `Assets/Imports/AssetKits/ParticleImage/Runtime/Particle.cs` | Particle Image1.2.0 managed 시뮬레이션, 0시간/비유한 속도 방어 로컬 패치 |
| `Assets/Imports/AssetKits/ParticleImage/Runtime/Jobs/ParticleJob.cs` | 동일한 속도 방어를 적용한 Jobs 경로, 패키지 재수입 시 보존 필요 |
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
| `Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpUI.cs` | 선택지 생명주기·카드 홀드 프리뷰·필드 보기 버튼 연결·정지/선택 타이머 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/IngameHudMotion.cs` | 판매·선택지 공용 HUD 수납/소유권·원래 상태 복원 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/DragSellBarUI.cs` | 판매 띠 연출·판정 영역. HUD 수납은 공통 컴포넌트에 요청 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpPresentation/LevelUpFieldView.cs` | 필드 보기 토글·투명 실드·문구. 공통 Peekthrough/HUD 수납기에 요청 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpPresentation/FieldViewEye.cs` | FxLab과 meta를 보존한 공용 눈 아이콘 Graphic |
| `Assets/WorkSpace/HSD/Scripts/UI/Utils/UI_Peekthrough.cs` | 카드 홀드/버튼 공통 페이드·입력 복원. 빈 곳 누르기는 비활성화 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/Descriptions/DescriptionLinkInteraction.cs` | 링크 우선·줄별 18 로컬 단위 확장/가장 가까운 키워드. 취소된 용어 누름은 선택으로 전환하지 않음 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/SelectionInventory/SelectionInventoryUI.cs` | 획득 선택지 보관함 |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/SelectionData/*.asset` | 카드 24장 + `DroneLevelUpPool` |
| `Data/Text/SelectionDescriptions.xlsx` | 카드 문구·용어 사전·설명 용어의 편집 원본(10-21~). [사용법](../../../Data/Text/README.md) |
| `Assets/WorkSpace/USW/Scripts/Editor/SelectionTextImporter.cs` | 위 Excel → 카드/`TermGlossary`/`DescriptionTermCatalog` 글자 필드만 원자적 Import·Validate |
| `Assets/WorkSpace/USW/Data/UI/TermGlossary.asset` | 카드 용어 링크·팝업 문구. 링크 Id(`chief_skill` 등)는 고정 |
| `Assets/WorkSpace/USW/Resources/DescriptionTermCatalog.asset` | 설명 모드 용어 카탈로그 |

## 불변 규칙·함정
- 10-28 ParticleImage 조사/수정: 게임 정지로 deltaTime=0이어도 목표·레이아웃은 움직일 수 있다. 속도를0으로 취급하고 Infinity/NaN을 곡선 평가에 넘기지 않는다. managed/Jobs 두 경로를 함께 유지한다. 이는 과거 장시간 실행의 정확한 조작을 입증하거나 필드 보기 UI가 원인임을 단정한 결과가 아니다. [재현·수정·검증](../../../outputs/particleimage-assertion-20261028/RESULT.md).
- 용어(10-21 사용자): 족장 버튼 스킬 표기는 **액티브 스킬**, "전투 드론"은 자폭 드론과 구분해 유지. 카드·용어 문구는 SO 직접 수정 대신 Excel 수정 → Import. `DroneSelectionPresets` 재실행은 카드 문구를 덮어쓰므로 이후 Import를 다시 한다.
- 설명 링크 터치 여백은 텍스트 로컬18 단위다. 줄바꿈된 링크는 줄별 영역을 사용하며 짧은 동일 링크 터치/멀티터치 취소 계약을 유지한다. EXP 바 트윈은 정지 중에도 잔량을 갱신한다(10-20 별도 미커밋 QA 수정).
- 효과 구조는 [ADR-0001](../../architecture/adr-0001-levelup-effects.md)을 따른다. 소비자는 `LevelUpManager.Effects`(디버그용) 대신 **기능별 reader를 주입**받는다.
- 카드 저작은 `Composition`(EffectSchemaVersion 1)만 읽는다. 숨긴 옛 필드(`primaryEffect` 등)와 동시 적용하지 않는다. schema 0 변환은 Editor 이관·역사 fixture 전용이다.
- 표시와 실제 지급은 같은 런 캐시의 확정 값을 쓴다. 설명을 만들 때 난수를 새로 굴리지 않는다.
- 획득 이력은 효과를 제거해도 남는다(`LevelUpManager.RemoveEffect`는 활성 기여만 뺀다). 같은 카드는 한 런에 한 번.
- 적용 중 다시 적용 요청이 오면 큐에 넣는다. 게임 종료 후 요청은 거절한다.
- 카드 ID·GUID·직렬화 enum 값은 바꾸지 않는다(풀·튜토리얼·씬이 참조).
- 오버드라이브(시간 소비) 카드는 설계만 있고 미구현이다([오버드라이브 덱](../../../design/gdd/overdrive-deck.md)).

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/SelectionDragCancelChecks.cs` | 레벨업/토템 보상 Opening에서 유닛/토템의 누르기·드래그 취소, 실제 Play45검사 |
| `Tools/verification/ParticleImageAssertionChecks.cs` | 실제 EXP프리팹의 dt0/정상·목표정지/이동·managed/Jobs 격리16조건, 기존 경로29검사와 실제 인게임 정지/재개 검사 |
| `Tools/verification/patch_particleimage_zero_delta.py` | ignore된 vendor1.2.0 소스 패치 재적용/--check. 다른 버전·예상과 다른 소스는 거절 |
| `Tools/verification/FieldViewGameChecks.cs` | 실제 인게임 필드 보기/카드 홀드/정지/연속 선택/HUD 복원 Play 및 세 씬 직렬화 검사 |
| `Tools/verification/InstallFieldView.cs` | 확정 FxLab 배치를 일반/하드/튜토리얼 씬에 설치. 재실행 시 기존 컨트롤 교체 |
| `Tools/verification/SelectionTextImportChecks.cs` | 문구 Import 계획/실행·불량 입력8종 거절·카드 용어 링크 확인 |
| `Tools/verification/run-regression.ps1` | 현행 5종 회귀의 일괄 게이트. [운영 원본](../../technical/regression-checks.md) |
| `Tools/verification/RegressionEnvironment.cs` | 씬/Play-start 비교·난수/파티/시간 복원·Unity 오류 집계 |
| `Assets/WorkSpace/USW/Scripts/Editor/SelectionExpansionChecks.cs` | 설명 TMP·설정 주입 포함 전체 재선택 UI/경제/드론 회귀 |
| `Tools/verification/LevelUpL5RegressionRunner.cs` | 역사적 선택지 7개 묶음. 5종 전체 게이트가 아니며 과거 출력 경로 사용 |
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
| `docs/technical/regression-checks.md` | 현행 5종 회귀 명령·범위·실패 판정·복원·역사 도구 구분 |
| `docs/architecture/adr-0001-levelup-effects.md` | 카드 효과 구조 결정 |
| `docs/implementation/levelup-refactor/README.md` | L0~L5 이관 작업판(L5 Editor/Play 검증 기록은 `outputs/levelup-refactor-20261010/L5/RESULT.md`) |
| `docs/technical/exp-system.md` | EXP |
| `design/gdd/drone-hacking-deck.md` | 해킹 카드(감망·델탕) |

## FxLab 필드 보기 버튼 시안 (2026-10-27)

실험실 `work/unity-ui` 전용, 기준 commit `2da1a287` 위 미커밋 시안의 당시 기록. 본게임 이식은 위10-27 항목과 RESULT를 따른다. 기존 `UI_Peekthrough`/카드 입력과 `LevelUpPresentation`을 읽어, 복사 씬에서만 홀드를 해제하고 버튼 토글을 적용했다. 버튼은 기존 하단 HUD 스프라이트·색상·테두리를 사용한다. 0.18초 unscaled 전환, alpha 0, 투명 입력 실드, 모든 터치 해제 후 카드 입력 복원. 같은 카드 유지, TimeScaleService 별도 소유권으로 정지 유지. Play 검증24개(토글22+종료 복원2)/콘솔 오류0, Android 실기기·전체 회귀 미실행. Lab 검증일2026-10-27; 위 production verified/commit과 구분한다.

후속 하단 공간 조정: 선택지 진입 시 출격·토템·스킬·강화 HUD를 0.28초 unscaled로 아래 수납, 두 토글 상태 동안 유지, 선택지 종료 시 복원. 버튼은 하단 중앙 `(0,210)`/`600×120`으로 확대. 기존 배치는 RESULT의 comparison-before-footer.png에 보존.

| 파일 | 역할 |
|---|---|
| `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scene/FxLab_FieldView.unity` | 실제 IngameScene 복사본, Play 자동 준비·6유닛·기존 선택지 |
| `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpPresentation/Fx/Lab/FieldViewFxLab.cs` | 실험실 전용 버튼 전환·정지·입력 실드 |
| `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpPresentation/Fx/Lab/FieldViewEye.cs` | 작은 눈 아이콘 벡터 Graphic |
| `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scripts/Editor/Fx/FieldViewLabBuilder.cs` | Tools/USW/Fx/Build Field View Lab. 원본 씬 저장 없이 복사 씬만 생성 |
| `outputs/workspaces/unity-ui/Tools/verification/FieldViewChecks.cs` | targeted Play 검증24개, 두 상태·복귀 캡처 |
| `outputs/workspaces/unity-ui/outputs/field-view-20261027/RESULT.md` | 결과·시연법·범위·변경/참조 파일 목록 |
