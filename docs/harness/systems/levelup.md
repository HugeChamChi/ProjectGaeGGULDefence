---
system: levelup
title: EXP·선택지 (경험치·레벨업·카드 추첨·효과 적용)
keywords: expmanager, exp 구슬, 경험치, 레벨업, levelup, level up, 선택지, 카드 효과, 선택 카드, choice, 추첨, 재추첨, reroll, levelupdata, levelupmanager, selection, 획득 선택지, 보관함, 설명 문구, 용어, 오버드라이브, particleimage, quaternion, aabb, assertion
verified: 2026-11-04
commit: ef31f410
---

## 2026-11-04 현재 코드 대조

확인함: 현재 운영47장/글리치8장이다. 9125~9127 반응3종과 9133~9137 원천/완성 카드의 운영 SO/풀/문구/분류 연결을 완료했다. LevelUpRunState는 제물/오류 누적의 이미 적립한 가산을 활성 카드 제거로 소급 회수하지 않는다. 시간 금고 조작 차단은 완료했고 G08은 연출만 남는다. 오버드라이브 GDD §10은 구현 답변16개/범위 밖 후보4개로 정리해 TODO D02를 닫았다. 아래 날짜별30/34/37장 및 미편입 문구는 당시 기록이다.

기준 커밋 ef31f410과 미커밋 작업 트리를 정적으로 대조했다. Unity/서버/Android 재실행 없음. 이전 날짜 설명과 충돌하면 이 항목이 우선한다. [대조 결과와 미등록 분류](../refresh-20261104.md).


11-04 결함 수정(현재): 시간 금고 유예 중 실제 전투 경과와 화상/방어감소/딜표 이벤트가 진행된다. 처치 보너스는 AddBossKillTime으로 제물을 숫자와 무관하게 정산한다. 과열 넘침 나머지는 같은 보스에서 누적, 보스 변경 시 초기화한다. 해킹 표식은 런타임 상한까지 확장하고 5스택당 한 묶음의 과열 비율을 색에 반영한다. 수리 키트는 빗나간 폭발에도 원 발사자에게 한 번 적용한다. 조작 차단 구현 완료, 차단 연출 TODO G08. [수정과 검증](../../../outputs/new-choice-fixes-20261104/RESULT.md).


11-04 시간 금고 조작 정책 확정/구현: 유예 중 월드 조작, 출격/판매/합성, 강화창/결제, 족장 발동, 토템/선택지 보관함 진입을 차단한다. 햄버거의 딜표와 설정은 유지한다. 진행 중 드래그를 취소하고 열린 강화창도 닫는다. 조작 불가 연출은 TODO G08 후속이다. 실제 씬23 PASS. [결과](../../../outputs/time-vault-input-20261104/RESULT.md).

# EXP·선택지 카드

## 9143 일제 기동 규칙 변경 (2026-11-04)

사용자 결정: 패턴 차단 성공 시 배치된 베탕마다 기본 스킬 1회분 대신 자폭 드론 1기만 발사한다. `Drone_Betan.FireCounterSkill`은 전투 드론 소환, 9138 추가탄, 9139 재무장 소비, 기본 스킬 강화 순번을 건드리지 않는다(`CounterBombsPerBetan`). 문구는 `Data/Text/SelectionDescriptions.xlsx`와 `DroneSelection43.asset`을 같이 바꿨다. BombTenChecks 기대는 24베탕 기준 58/82기. 검증 상태는 [결과](../../../outputs/counter-bomb-20261104/RESULT.md).

## 신규23장 독립 검증 (수정 전 역사 기록)

[수정 전 결함과 재현](../../../outputs/new-choice-verify-20261104/RESULT.md). 당시 결함과 검사 기대값은 위 수정으로 해소했다.

## 자폭 신규10장 운영 구현 (2026-11-04)

현재 운영47장(ID9101~9147), 자폭14장. 사용자 미정 수치/세부 규칙 위임으로9138~9147을 SO/문구 Excel/분류/풀에 편입했다. 기본 추가탄/자기적중 재무장/직접 자폭 소량 스택소비/감망 운반 대체/베탕 준비배율/패턴 차단 즉시 발사/한발강화/일시 방어감소/낮은HP 마무리/적중 식량을 구현했다. 감망 예약은 유효 적중1회 소비, 취소/발사자 수명 변경 시 환불. B23은 실제 OnPatternCountered 구독, 같은 프레임 다중 차단1회, 충전 보존. Edit59/실제비행Play29/실제차단59/공통전체회귀 PASS. 아래 운영37장 등은 이전 단계 기록이다. [적용 수치와 검증](../../../outputs/bomb-ten-20261104/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Tools/verification/InstallBombTen.cs` | 승인10장 SO/풀/공식 문구/분류 importer |
| `Tools/verification/BombTenChecks.cs` | 운영47장/피해/예약/실제 자폭/실제 차단/프레임 기록 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/BombRearmDefinition.cs` | 자기 원 자폭 적중 후 다음 기본 스킬 보너스 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/GammanBombDefinition.cs` | 감망의 소비 공격을 공유 자폭 프리팹으로 운반 |


## 글리치 반응3종 운영 편입 (2026-11-04)

직전 단계 운영37장(ID9101~9137). 기존 기능에 9125 복구 패킷(디시그망 회복당 해킹1스택, Rare), 9126 소각 신관(원인 유닛 자폭1기, Epic), 9127 오류 누적(회당 공격력0.025%, 최대1000회/25%, Rare)의 SO/문구 Excel/분류/풀을 연결했다. 카드별 spawnRate1, 기존 등급 가중치 유지, 세 장 모두 글리치 단일 테마. 아래11-03/직전 작업의 운영30/34장과 미편입 표기는 당시 기록이며 이 항목이 현재 상태다. [현재 결과](../../../outputs/glitch-reactions-operational-20261104/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection25.asset` | 9125 복구 패킷 운영 카드 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection26.asset` | 9126 소각 신관 운영 카드 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection27.asset` | 9127 오류 누적 운영 카드 |
| `Tools/verification/InstallGlitchReactions.cs` | 실제 공유 해킹/자폭 참조와 SO/풀/공식 importer 연결 |
| `Tools/verification/GlitchReactionCardsChecks.cs` | 실제 운영3장 추첨/획득/동시발동 검증 |

## 글리치 테마 미구현4종 운영 반영 (2026-11-04)

운영34장(기존30+9134~9137). 9134 가속 운전은 남은 시간만1.1배 감소/공속 임시+20%, 9135 제물은 실제 처치+30 뒤-10 별도 표시/런 공격력+2% 가산, 9136 시간 금고는 런1회15초 유예/임시 Legend/회복·토템·선택 차단/보스 전환3초만 정지, 9137 과열 압축은 초과5개/0.5초로 일반1→과열1(감망 피해7.5개분)이다. 임시 등급은 실제 소유등급과 기존 TD1005 출처를 유지한다. 반응3종 9125~9127의 운영 SO/풀은 여전히 미반영이다. [구현/검증/참조 파일](../../../outputs/overdrive-four-20261104/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IOverdriveEffectReader.cs` | 네 카드의 불변 설정과 제물 적립 조회 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/AcceleratedDriveDefinition.cs` | 가속 운전의 시간 감소/공속 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/SacrificeDefinition.cs` | 제물의 비용/공격력 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/TimeVaultDefinition.cs` | 시간 금고 유예 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/OverheatCompressionDefinition.cs` | 과열 생산량/비용/피해 가중치 SO 정의 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection34.asset` | 9134 가속 운전 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection35.asset` | 9135 제물 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection36.asset` | 9136 시간 금고 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection37.asset` | 9137 과열 압축 |
| `Tools/verification/InstallOverdriveFour.cs` | 신규4장 SO/풀과 공식 Excel importer 실행 |
| `Tools/verification/OverdriveFourChecks.cs` | 원장/시계/실제 프레임/실제 인게임 씬 검증 |

## 과부하 합성 운영 카드 (2026-11-03, HEAD 위 미커밋)

사용자 확정 C04 과부하 합성을9133/에픽/가중치1/글리치 단일 태그로 추가했다. 운영30장/해킹14장, 기존29장 SO 해시와 Excel 기존 셀/서식 보존. 비용0.25초/추가승급20%, 에픽재료 합성 제외, 실패도 지불. 시간 차감과 결과 유닛의 글리치 귀속은 그리드 카드와 [구현 결과](../../../outputs/overload-merge-20261103/RESULT.md)를 따른다. 반응3종의 운영 풀 편입은 아직 별도 미완료다.

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/OverloadMergeDefinition.cs` | 과부하 비용/확률의 조합형 저작 데이터 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection33.asset` | 9133 과부하 합성 운영 SO |
| `Tools/verification/InstallOverloadMerge.cs` | 단일 카드 저작/운영 풀 연결/정식 Excel importer |
| `Tools/verification/OverloadMergeChecks.cs` | 운영 카탈로그와 실제 합성/시간지출/소각 신관 Play 검사 |

## 신규 해킹 효과 기반 (2026-11-03, HEAD 위 미커밋)

`HackingOverflowDamageDefinition`/`HackingConsumptionDefinition`/`HackingCapacityDefinition`/`BetanAttackPenaltyDefinition`의 검증, 복사, 스냅샷 집계, 읽기와 설명 토큰을 추가했다. 델탕 넘침 피해, 감망 소비 한도/스택 추가분 배율, 공유 장부 상한과 베탕 공격력 감소에 연결했다. 기존9장은 유지하고 승인된 신규9128~9132 운영 SO/풀/Excel 편입 완료. 전체29/해킹14, 기존24장 해시 동일. 계수0.5/스택추가피해2배, 소비카드 동시획득 허용. [Edit31/운영50/실제Play21/전체회귀 및 변경](../../../outputs/hacking-choices-20261103/RESULT.md).

## 글리치 반응 기능 (2026-11-03, HEAD 위 미커밋)

확정 감지/반응 3종부터 구현하고 미정 원천 정책과 운영 카드 저작은 대기한다. 초기 구현 원천은 `Disigman.TryCast`의 회복이었다. 현재는 위 과부하 합성의 음수 글리치도 발행한다. 보스 보너스/자연 감소/표시용 Hack 태그는 제외한다. `GlitchReactionBinding`은 드론 씬 수명에 구독하고 원인 유닛/원래 보스에 반응을 귀속한다. C10은 1회 1스택 확정. 오류 누적은 최대1000회/25%, 고정피해 제외, 새 런에서 초기화한다. 반응3종 운영 풀 편입과 나머지 원천4종/시간 금고 차단은 미완료다. 반응 설정은 원천 문서§1.3에 확정돼 있으며 당시 운영30장은 위 항목을 따른다. [구현 및 검증](../../../outputs/glitch-reactions-20261103/RESULT.md).

| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Selection/GlitchReactionBinding.cs` | 실제 시간 변경에 반응하여 해킹/원인 유닛 자폭/런 공격력 적립 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/RecoveryHackingDefinition.cs` | C10 복구 패킷의 회복당 스택과 공유 해킹 SO |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/GlitchBombDefinition.cs` | G10 소각 신관의 공유 자폭 프리팹 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/GlitchAttackDefinition.cs` | 오류 누적의 회당 비율과 상한 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/IGlitchEffectReader.cs` | 반응 설정과 런 공격력 조회 |
| `Tools/verification/GlitchReactionChecks.cs` | 격리 조합형 획득/디시그망 회복/반응/실제 자폭 Play 검사 |

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
- 과부하 합성, 나머지 글리치 테마4종과 반응3종은 모두 운영 편입했다. 현재 상태는 위11-04 최신 항목을 따른다([오버드라이브 덱](../../../design/gdd/overdrive-deck.md)).

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
| `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scripts/Editor/Fx/FieldViewLabBuilder.cs` | Tools/USW/Fx/Build Field View Lab. 원본 씬 저장 없이 복사 씬만 생성 |
| `outputs/workspaces/unity-ui/Tools/verification/FieldViewChecks.cs` | targeted Play 검증24개, 두 상태·복귀 캡처 |
| `outputs/workspaces/unity-ui/outputs/field-view-20261027/RESULT.md` | 결과·시연법·범위·변경/참조 파일 목록 |
| `Tools/verification/NewChoiceRegressionChecks.cs` | 신규 선택지 시계/디버프/집계/누적/표시 집중 회귀 |

## 과거 참조 (현재 체크아웃에 없음)

- `outputs/workspaces/unity-ui/Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpPresentation/Fx/Lab/FieldViewEye.cs`: 당시 참조 이름만 보존. 현재 실행/설치 필수 파일이 아니다.

## 보완 진입 파일 (11-04 대조)

| 파일 | 역할 |
|---|---|
| `design/gdd/overdrive-deck.md` | 글리치8장과 시간 지출/금고/과열 질문별 처리 정본 |
