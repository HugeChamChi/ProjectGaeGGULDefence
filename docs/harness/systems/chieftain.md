---
system: chieftain
title: 족장 (선택·카드 풀·알팡 액티브 스킬)
keywords: 족장, chieftain, chief, 알팡, alphan, 족장 스킬, 액티브 스킬, active skill, 족장 버튼, 쿨타임, 쿨다운, 집결 사격, 카드 풀, 덱 선택
verified: 2026-11-04
commit: ef31f410
---

## 2026-11-04 현재 코드 대조

확인함: PlayerChiefManager의 선택 저장은 PlayerData snapshot으로 통합됐으며 기존 별도 행은 읽기 호환만 유지한다. 계정 세대/소유자를 확인해 오래된 콜백을 배제한다. 시간 금고 중 알팡 액티브 발동은 차단한다. 족장은 계속 그리드 유닛이 아니며 기존 액티브 수치/쿨타임은 유지한다.

기준 커밋 ef31f410과 미커밋 작업 트리를 정적으로 대조했다. Unity/서버/Android 재실행 없음. 이전 날짜 설명과 충돌하면 이 항목이 우선한다. [대조 결과와 미등록 분류](../refresh-20261104.md).


11-04 시간 금고 조작 정책 확정/구현: 유예 중 월드 조작, 출격/판매/합성, 강화창/결제, 족장 발동, 토템/선택지 보관함 진입을 차단한다. 햄버거의 딜표와 설정은 유지한다. 진행 중 드래그를 취소하고 열린 강화창도 닫는다. 조작 불가 연출은 TODO G08 후속이다. 실제 씬23 PASS. [결과](../../../outputs/time-vault-input-20261104/RESULT.md).

# 족장 카드

**한 줄 요약**: 족장은 **그리드 유닛이 아니다**. 하는 일은 두 가지뿐이다: 이번 런의 선택지 카드 풀(`ChieftainData.LevelUpPool`)을 정하고, HUD에 독립 액티브 스킬 버튼을 준다. 현재 스킬은 알팡의 드론 집결 사격 하나다.

10-28 코드/SO 대조: 9111 전술적 프로토콜(최대2회분 순차 충전·보관), 집결은 참가 드론 공격력 합×SO 계수(현재1)다. **9105 긴급 교신(공유 해킹 스택 전량 소비 후 집결 피해 추가)의 현재 SO는 스택당3%**다. 10-21 결과의1%는 당시 값이다. 감망 없이 족장 자체가 스택을 예약·소비한다. [결정 기록](../../../design/quick-specs/chief-skill-cards-2026-10-21.md), [당시 검증](../../../outputs/chief-damage-20261021/RESULT.md), [이번 정적 대조](../../../outputs/harness-refresh-20261028/RESULT.md). 실서버 난도·Android는 이번에 재검증하지 않았다.

## 흐름
1. 선택 결정: `ChieftainSelection.ResolveInitialSelection` — 파티 SO의 족장(`GlobalData.SelectedParty`) → 테스트 선택 → 저장된 `Player.Chief.SelectedChiefId` 순.
2. 카드 풀: 선택지 시스템이 `ChieftainSelection.GetSelectedLevelUpPool`로 **선택된 족장의 풀만** 쓴다(다른 덱과 섞지 않음).
3. 게임 시작: `ChieftainSelection.Init`이 `GameManager.OnGameStart`를 구독 → `ChieftainSelection.Select` → `ChieftainSelection.OnAlphanSkillSelected` → `AlphanActiveSkill.Configure`(처음엔 충전 필요).
4. 충전: `AlphanActiveSkill.Tick` → Playing이고 시간이 흐를 때만 순차 충전. 쿨타임 = SO 값 × (1 − 선택지 쿨감). 기본1회분,9111의 `ChiefChargeCapacityDefinition`이 있으면 최대2회분. 획득/사용 시 진행 중 시간을 보존한다.
5. 발동: HUD 버튼(`UI_ChiefSkillPresenter`) → `AlphanActiveSkill.TryActivate`가 완료1회분 소비 → `DroneManager.ExecuteChiefRallyAsync`가 공격력/스택 예약 고정 → `ChiefActiveSkillSignals.RaiseActivated`(판정 전 지진 카운터). 빔 적중에서 스택 확정과 합산 피해1회를 처리한다.9111 자체는 자동 연발/피해 감소를 적용하지 않는다.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Chieftain/ChieftainData.cs` | 족장 SO: 저장 ID, 이름, 아이콘, 카드 풀, 알팡 스킬 설정 |
| `Assets/WorkSpace/USW/Scripts/Chieftain/ChieftainSelection.cs` | 선택 결정·카드 풀 제공·스킬 연결(씬 MonoBehaviour) |
| `Assets/WorkSpace/USW/Scripts/Chieftain/AlphanActiveSkill.cs` | 알팡 액티브 런타임(씬 entry point). 충전·발동·취소 |
| `Assets/WorkSpace/USW/Scripts/Chieftain/AlphanSkillData.cs` | 쿨타임·집결 공격력 계수·공유 해킹 SO·아이콘·사운드·빔 연출 |
| `Assets/WorkSpace/USW/Scripts/Chieftain/IChiefActiveSkill.cs` | HUD가 쓰는 스킬 계약(그리드·유닛·등급 없음) |
| `Assets/WorkSpace/USW/Scripts/Chieftain/ChiefActiveSkillSignals.cs` | 발동 알림(스킬 ↔ 보스 패턴 분리) |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/ChiefChargeCapacityDefinition.cs` | 9111 최대 보관 횟수 SO 정의 |
| `Assets/WorkSpace/USW/Scripts/Selection/Composition/ChiefHackingDetonationDefinition.cs` | 9105 소모 스택당 집결 추가 피해 비율 정의 |
| `Assets/WorkSpace/HSD/Scripts/UI/Chief/UI_ChiefSkillPresenter.cs` | 족장 스킬 버튼 Presenter |
| `Assets/WorkSpace/HSD/Scripts/UI/Chief/UI_ChiefSkillButtonView.cs` | 버튼 View(다음 회분 쿨타임·보관 횟수·9114 금색 테두리) |
| `Assets/WorkSpace/HSD/Shaders/UI/ChiefMonocleFrame.shader` | 원본 버튼 테두리 색만 금색으로 치환. 배경/초상화 분리 유지 |
| `Assets/WorkSpace/USW/Resources/ChiefMonocleFrame.mat` | View가9114 보유 시 로드하는 재질. 원본 테두리색/금색 설정 |
| `Assets/WorkSpace/HSD/Scripts/Chief/PlayerChiefManager.cs` | 아웃게임 저장 족장 ID(`Player.Chief`) |

## 데이터 (SO)
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Data/Chieftain/AlphanChieftain.asset` | 알팡 족장(카드 풀 + 스킬) |
| `Assets/WorkSpace/USW/Data/Chieftain/TestPartyChieftain.asset` | 테스트 파티용 |
| `Assets/WorkSpace/USW/Data/DroneUnits/AlphanActiveSkillData.asset` | 기본120초·참가 공격력 합 계수1·공유 해킹 SO |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection11.asset` | 9111의 최대2회 보관·설명, ID/등급 유지 |
| `Assets/WorkSpace/USW/Data/SelectionData/DroneSelection5.asset` | 9105 긴급 교신, DamageRatioPerStack=0.03·전량 소모·족장 피드백 |

## 불변 규칙·함정
- (사용자 결정 2026-10-05) 족장을 칸 점유·소환·판매·합성·등급·기본 공격·식량 생산에 연결하지 않는다. 그리드 위 알팡이 함께 있다는 옛 문서는 대체됐다.
- 개구리 전사 같은 일반 영웅과 그 파티장 패시브는 족장이 아니라 **유닛**이다.
- 직렬화된 enum의 예약 값은 번호를 다시 매기지 않는다.
- 알팡 피해·120초 쿨타임·집결·카드는 구조 결정과 별개로 유지된다. 값을 바꾸려면 SO를 고친다(코드 기본값 14초는 새 에셋용).
- 발동 조건: Playing, timeScale > 0, 시전 중 아님, 집결 중 아님, 집결 가능한 드론 1기 이상. 드론이 0기여도 충전은 계속된다.
- 씬 종료 때 `AlphanActiveSkill`이 진행 중인 연출을 취소·정리한다. 시전 CTS는 `ExecuteAsync`의 finally에서 Dispose한다.

## 검증
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Editor/AlphanActiveSkillChecks.cs` | 충전·발동·쿨감 에디터 검증 |
| `Tools/verification/ChiefDisigmanLiveChecks.cs` | 족장 스킬 + 디시그망 실행 확인 |
| `Tools/verification/ChiefCardsChecks.cs` | 충전 보존·용량·수동 사용·기존 선택지 회귀 |
| `Tools/verification/ChiefCardsLiveChecks.cs` | 실제 인게임 HUD2/2→1/2→0/2·정상 단발·복원 |
| `Tools/verification/ChiefDamageChecks.cs` | 참가 공격력·강화·SO·모노클·예약·설명 검사 |

## 더 읽을 곳 (필요할 때만)
| 파일 | 역할 |
|---|---|
| `docs/technical/alphan-active-skill.md` | 족장/알팡 원본 |
| `docs/technical/chieftain-refactor-20261005.md` | 그리드 분리 이관·검증 기록 |
