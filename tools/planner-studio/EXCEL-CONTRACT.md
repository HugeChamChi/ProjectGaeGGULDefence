# 기획 데이터 Excel 작성 규약

같은 의미의 데이터는 기능에 관계없이 같은 열 이름·단위·셀 형식을 사용합니다. 기능별 고유 항목까지 같은 표로 합치지는 않습니다. 실제 열 순서와 타입은 내보내는 `.schema.json`이 기준입니다.

## 공통 작성 규칙

첫 행은 영문 열 이름, 두 번째 행부터 데이터입니다. 입력 셀 수식과 병합 셀은 사용하지 않습니다. 키는 텍스트로 보관하고 이름·설명·행 순서는 식별자로 쓰지 않습니다. 중복 키·없는 참조·누락 등급·누락 레벨은 검사에서 거절합니다.

| 데이터 | 작성 방식 | 적용 예 |
|---|---|---|
| 이벤트 식별 | EventKey | AttendanceEvents, AttendanceRewards, MissionEvents, Missions |
| 보상 | Rewards = `Gold:500;Diamond:10;Stamina:30` | 출석 일차, 미션 완료 |
| 비용 | Costs = `Gold:500;Diamond:10` | 강화 비용. 보상과 같은 파서 사용 |
| 기간 | StartAt, EndAt, 시간대를 포함한 ISO 시각 | `2026-11-01T05:00:00+09:00`. 시작 포함·종료 제외 |
| 활성 | Enabled = 0 또는 1 | 출석·미션 이벤트 |
| 공격 속도 | AttackFrequency, 공격 횟수/초 | 2 = 초당 두 번. 엑셀·게임 데이터 동일 |

아이템 수량은 1~2,147,483,647의 정수이며 프로젝트의 아이템 목록에 있는 키를 사용합니다. 한 셀에서 같은 아이템 키를 두 번 쓰지 않습니다. 보상은 한 개 이상 필요하고 비용은 빈 셀(무료)을 허용합니다. 소수·음수·0·초과 수량은 모두 거절합니다.

## 유닛 — UnitBalance.xlsx

| 시트 | 행 식별 키 | 내용 |
|---|---|---|
| Units | UnitKey | 이름·종류·서버 매핑·아이콘·최대 레벨·BaseStatKey·UpgradeKey·LevelBonusKey |
| BaseStats | BaseStatKey + Tier | Attack, AttackFrequency, SkillCooldown(초), FoodProduction(초당), MaxDroneCount |
| UpgradeStats | UpgradeKey + TargetLevel | Costs, MultiplierIncrease, GlobalBonusIncrease |
| LevelBonuses | LevelBonusKey + Level | Description, EffectStatus, EffectKey, EffectParams |

BaseStats는 normal / rare / epic / legend 네 등급을 모두 작성합니다. 같은 BaseStatKey를 참조하는 유닛은 기본 스탯을 공유합니다. 강화는 Lv.2부터 MaxLevel까지 연속이어야 하며 Costs는 목표 레벨에 도달하는 비용입니다. 증가량 0.1은 기준 배율에 +0.1입니다. 같은 공유 키의 정의가 다르면 거절합니다.

EffectStatus=DescriptionOnly는 설명만 있는 항목이며 EffectKey는 빈칸, EffectParams는 `{}`입니다. Defined는 효과 키와 JSON 객체를 작성한 상태입니다. 게임에 해당 효과 처리기가 구현됐다는 뜻은 아닙니다. 설명 문구를 자동으로 전투 효과로 해석하지 않습니다.

이전 BaseStats의 AttackInterval 열은 실제로 공격 횟수/초를 저장했습니다. 가져올 때 같은 값을 AttackFrequency로 취급하며 역수 변환하지 않습니다. 두 열을 함께 넣으면 모호한 중복으로 거절합니다. 기존 프로젝트 JSON의 내부 필드명은 보존하므로 저장된 작업도 계속 열 수 있습니다.

## 출석 — AttendanceEvent.xlsx

| 시트 | 행 식별 키 | 열 |
|---|---|---|
| AttendanceEvents | EventKey | EventKey, Name, StartAt, EndAt, ClaimEndAt, CycleDays, RepeatMode, Enabled |
| AttendanceRewards | EventKey + Day | EventKey, Day, Rewards |

CycleDays는 1~365이며 모든 일차를 빠짐없이 작성합니다. ClaimEndAt 빈칸은 종료 시각을 뜻합니다. RepeatMode는 Once 또는 Cycle입니다. 새 양식에 별도 보상 묶음이나 ResetHourKst 열은 없습니다. 신규 출석 이벤트의 게임 진행·수령 연결은 아직 미구현입니다.

## 미션 — MissionEvents.xlsx

| 시트 | 행 식별 키 | 내용 |
|---|---|---|
| MissionEvents | EventKey | Name, StartAt, EndAt, Enabled |
| Missions | MissionKey | EventKey, Name, DataKey, Target, Period, Rewards |
| DataBindings | BindingKey | HookKey, DataKey, ValueSource, ConstantValue, Enabled |
| EventData | DataKey | 코드에서 제공하는 데이터 목록, 읽기 전용 |
| ExecutionHooks | HookKey | 코드에서 제공하는 실행 지점 목록, 읽기 전용 |

Period는 Event(이벤트 누적) 또는 Daily(공통 일일 초기화)입니다. 미션은 활성 DataBindings에 연결된 DataKey를 참조합니다. 같은 실행 지점에서 같은 데이터를 중복 갱신하도록 작성할 수 없습니다. 보상은 출석과 똑같은 Rewards 셀 규칙을 사용합니다.

## 영구강화와 기타 자료

PermanentUpgrade.xlsx의 ResearchTree / ResearchNode는 TreeKey와 TreeKey + NodeId로 연결합니다. 비율 수치 0.02는 +2%, 고정 스탯은 원값입니다. 선행 노드 목록은 세미콜론 구분이며 순환 참조를 거절합니다. 파티는 에셋 경로 대신 ChiefId로 족장을 연결합니다. Source·Note·ChiefAsset 등 원본 추적 필드는 출력에서 제외합니다.

기존 무한모드·계정 성장 Excel은 기존 게임이 소비하는 양식을 보존합니다. 다른 게임 계약을 확인 없이 새 표 이름으로 바꾸지 않습니다.

## 작성용과 뒤끝 업로드용

작성은 위 양식으로 합니다. DEV 파일 준비는 동일 프로젝트 스냅샷에서 작성용 `.xlsx`, 열 규약 `.schema.json`, 업로드용 `.backend.xlsx`, 해시가 있는 `deployment.json`을 같은 폴더에 만듭니다. 유닛 작성용의 강화·보너스도 검토할 수 있지만 현재 업로드 대상은 기본 스탯만입니다.

업로드용은 미션과 유닛 모두 `SchemaVersion, ReleaseKey, PartIndex, PartCount, Sha256, Payload`의 공통 전송 형식입니다. 직접 작성·수정하는 양식이 아닙니다. 작성용을 수정했다면 툴로 다시 가져와 적용 파일을 다시 만듭니다. 여러 표를 하나의 검증된 스냅샷으로 전달하기 위한 구조이며 파일 생성은 서버 적용 완료를 뜻하지 않습니다.

이전 Unit / UnitUpgrade / UnitMilestone, AttendancePlan / Attendance / RewardGroup 양식은 가져오기 호환을 유지합니다. 새로 내보내면 현재 양식을 사용합니다. `.data.json`은 개발용 변환 결과입니다. 프로젝트 복원은 프로젝트 저장 JSON 또는 작성용 Excel을 사용합니다.

## 검증

`node tools/planner-studio/check-excel-contract.cjs`는 작성용 표의 열 규약 일치, 보상·비용의 공통 입력 규칙, 이전 공격 속도 열 호환, 작성용 원본과 뒤끝 전송 데이터의 값 일치를 검사합니다. 게임 원격 로드·저장·보상 지급 검증과는 구분합니다.
