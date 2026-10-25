# 콘텐츠 Excel / JSON 규약 v4

출석은 **이벤트 → 출석 이벤트**로 분류합니다. 최신 `AttendanceEvent.xlsx`와 `AttendanceEventExample.xlsx`는 `AttendanceEvents` / `AttendanceRewards` 두 시트이며 `EventKey`로 연결합니다. 기간·수령 마감·반복·비활성 예시는 [ATTENDANCE-EVENT.md](ATTENDANCE-EVENT.md)에 정리했습니다. 아래 AttendancePlan / Attendance 설명은 이전 형식의 이관 참고입니다.

## 유닛 — UnitBalance.xlsx

유닛은 한 Excel의 4개 시트로 관리합니다. 숫자 ID나 SO 경로 대신 문자열 키를 사용합니다.

| 시트 | 행 키 | 연결·내용 |
|---|---|---|
| Units | UnitKey | 이름·종류·MaxLevel·BaseStatKey·UpgradeKey·LevelBonusKey |
| BaseStats | BaseStatKey + Tier | Attack, AttackInterval(초), SkillCooldown(초), FoodProduction(초당), MaxDroneCount |
| UpgradeStats | UpgradeKey + TargetLevel | Costs, MultiplierIncrease, GlobalBonusIncrease |
| LevelBonuses | LevelBonusKey + Level | Description, EffectStatus, EffectKey, EffectParams |

유닛 키는 영문/밑줄로 시작하고 영문·숫자·밑줄·점·하이픈으로 구성합니다. 기존 숫자 서버 매핑은 BackendKeys에 보관하며, 시트 간 연결 키와 구분합니다. 동일한 BaseStatKey / UpgradeKey / LevelBonusKey를 여러 유닛이 참조할 수 있습니다.

BaseStats는 실제 UnitData에서 읽은 9개 유닛 × normal/rare/epic/legend 4등급 = 36행입니다. 족장 SO에 없는 일반 유닛 기본 스탯은 만들어 넣지 않으며 족장의 BaseStatKey는 비워 둡니다.

UpgradeStats는 레벨별 **증가량**입니다. MultiplierIncrease `0.1`은 누적 배율에 +0.1, GlobalBonusIncrease `0.02`는 전체 보너스에 +2%p입니다. Lv.1 기준 배율 1 / 보너스 0에서 해당 레벨까지 합산합니다. Excel을 가져오면 기존 UI의 누적 수치로 복원합니다. Costs는 해당 목표 레벨에 도달하는 비용입니다. 강화 키가 있는 유닛은 Lv.2부터 MaxLevel까지 빠짐없이 있어야 합니다.

기존 보너스의 EffectId는 새 시트에서 EffectKey로 표기합니다. DescriptionOnly / Defined와 JSON 객체 규칙은 동일합니다. 없는 참조·중복 복합 키·누락 등급·누락 레벨은 거절합니다. 같은 성장/보너스 키의 서로 다른 정의도 거절합니다.

개별 보너스 Excel은 백업 후 UnitBalance.xlsx로 통합했습니다. 예전 Unit / UnitUpgrade / UnitMilestone 시트 형식의 가져오기는 호환 유지합니다. 아래의 기존 시트 설명은 이전 형식 참고입니다. 최신 열 순서·타입은 내보내는 `.schema.json`이 기준입니다.

첫 행은 고정 영문 열 이름, 두 번째 행부터 데이터입니다. 병합 셀과 입력 셀 수식은 사용하지 않습니다. ID를 텍스트로 보관하세요. 이름·설명은 표시용이며 파싱 키로 쓰지 않습니다. 행 순서 대신 아래 복합 키로 식별합니다.

## 출석 — Attendance.xlsx

별도 보상 표나 보상 묶음 ID가 없습니다. 출석 행의 Rewards가 지급 데이터 원본입니다.

| 시트 | 키 | 열 |
|---|---|---|
| AttendancePlan | AttendanceId | AttendanceId, Name, CycleDays |
| Attendance | AttendanceId + Day | AttendanceId, Day, Rewards |

`ResetHourKst`: 0~23 정수. `CycleDays`: 1~365 정수. 각 계획은 Day 1부터 CycleDays까지 중복·누락 없이 구성합니다.

`Rewards`: `Gold:500;Diamond:10;Stamina:30`. 항목은 세미콜론, 아이템 ID와 수량은 콜론으로 구분합니다. 수량은 1~2147483647 정수이며 프로젝트 아이템 목록에 존재하는 ID만 허용합니다. 빈 셀은 보상 없는 일차입니다. 같은 셀의 아이템 ID 중복은 거절합니다.

파싱 결과 `Attendance.data.json`:

```json
{
  "schemaVersion": 3,
  "attendance": [{
    "attendanceId": "DefaultAttendance",
    "resetHourKst": 5,
    "cycleDays": 7,
    "days": [{"day": 1, "rewards": [{"itemId": "Gold", "amount": 500}]}]
  }]
}
```

위 days는 형식 설명을 위한 일부 예시입니다. 실제 파일에는 모든 일차가 들어갑니다.

## 강화 — EnhancementBalance.xlsx

| 시트 | 키 | 용도 |
|---|---|---|
| Unit | UnitId | 이름·종류·최대 레벨·원본 정보 |
| UnitUpgrade | UnitId + TargetLevel | 해당 레벨 강화 비용·누적 배율·족장 전체 보너스 |
| UnitMilestone | UnitId + Level | 보너스 해금 레벨·표시 문구·효과 정의 |
| ResearchTree | TreeKey | 영구강화 트리 설정 |
| ResearchNode | TreeKey + NodeId | 노드 수치·배치·선행 조건 |

서버용 Excel·data.json·schema.json에는 `Source`, `Note`, `ChiefAsset`, `LegacySaveName`, `GrowthPerLevel`을 내보내지 않습니다. 원본 추적·메모·SO 경로·참고 성장률은 편집 프로젝트 JSON에만 보관합니다. 실제 성장 수치는 UnitUpgrade가 원본입니다. 파티의 족장은 경로 대신 `ChiefId`로 연결합니다. `AlphanChief`, `TestPartyChieftain`은 현재 프로젝트의 족장 키이며 뒤끝 숫자 ID와 같다는 뜻은 아닙니다.

`Multiplier` 1.1은 ×1.1, `GlobalBonus` 0.02는 +2%입니다. 영구강화의 비율 스탯 `ValuePerLevel` 0.02는 +2%, 고정 스탯은 원값입니다. 목록은 세미콜론 구분, bool은 0/1로 내보냅니다. 열 전체 목록·타입·범위·enum은 함께 생성하는 `.schema.json`에서 확인합니다.

### 레벨업 보너스

고정 열:

```text
UnitId | Level | Description | EffectStatus | EffectId | EffectParams
```

- `Level`: 1 이상 정수이며 해당 유닛 MaxLevel 이하. 유닛 ID + 레벨 중복 불가.
- `Description`: 화면 표시 문구. 이를 정규식이나 자연어 해석으로 효과에 변환하지 않습니다.
- `EffectStatus=DescriptionOnly`: 현재 문구만 존재하는 기존 보너스. EffectId는 빈칸, EffectParams는 `{}`.
- `EffectStatus=Defined`: 효과 데이터를 작성한 항목. EffectId는 영문자로 시작하는 영문·숫자·밑줄 키, EffectParams는 JSON 객체.
- JSON 숫자는 유한해야 합니다. 수치 단위·대상·조건은 해당 효과 키를 처리하는 게임 코드와 합의해야 합니다.

정의 예시:

```text
UnitData_Betan | 3 | 스킬 쿨타임 20% 감소 | Defined | skill_cooldown_reduction | {"percent":20}
```

이 효과 키는 규약 설명 예시입니다. 게임에 이 이름의 처리기가 구현됐다는 의미가 아닙니다. 기존 보너스 설명을 임의로 효과 데이터로 변환하지 않습니다.

`EnhancementBalance.data.json`의 보너스:

```json
{
  "unitId": "UnitData_Betan",
  "level": 3,
  "description": "스킬 쿨타임 20% 감소",
  "effectStatus": "Defined",
  "effect": {"id": "skill_cooldown_reduction", "parameters": {"percent": 20}}
}
```

DescriptionOnly이면 `effect`는 null입니다. Defined도 데이터가 정의됐다는 뜻이며 게임 적용 완료 표시는 아닙니다.

## 가져오기·검사·호환

시트에 포함된 표만 교체합니다. 나머지 프로젝트 데이터는 유지합니다. 잘못된 타입·필수 열 누락·수량·ID 참조·중복 키·트리 순환·출석 일차 누락·효과 JSON 오류는 적용 전에 거절합니다.

이전 출석 Excel의 RewardGroupId 연결은 직접 Rewards로 변환합니다. 이전 JSON도 같은 방식으로 이관합니다. 이전 레벨 보너스 파일에 효과 열이 없으면 DescriptionOnly로 읽습니다.

내보낼 때 `.xlsx`, `.data.json`, `.schema.json`이 생성됩니다. 기획작업실 재편집은 Excel 또는 프로젝트 저장 JSON을 사용합니다. `.data.json`은 개발용 파싱 결과이고, 프로젝트 복원 파일이 아닙니다.

독립 변환:

```powershell
node Tools/planner-studio/parse-content.cjs Attendance.xlsx Attendance-parsed.json
node Tools/planner-studio/parse-content.cjs EnhancementBalance.xlsx EnhancementBalance-parsed.json
```

변환은 동일한 검증기를 사용하며 기존 출력 파일을 덮어쓰지 않습니다. 게임 SO 변경·서버 업로드·실제 보상 지급은 수행하지 않습니다.
