# 유닛 레벨 보너스 Value 규약

2026-10-31. 보너스 하나가 대상·해금 레벨·설명을 소유하고, 효과 하나당 BonusEffect 한 행을 사용한다. 여러 독립 효과는 여러 행으로 추가한다. 기존 HTML 밸런스 수치와 전투 구현은 유지한다.

## 표 구조

- BonusDefinition: BonusId, UnitKey, Level, DescriptionTemplate, HighlightNumbers, LinkTerms, EffectLink.
- BonusEffect: BonusId, EffectKey, EffectType, Stat, ValueMode, Value.
- Name·UnitName은 보너스 엑셀에 저장하지 않는다. 기획툴은 Unit의 이름을 조회해 표시한다.
- Stat·ValueMode는 StatModifierBonus에서만 사용한다. 복합 효과는 빈 셀이다.
- Value는 문자열이다. 쉼표로 구분하고 아래 순서를 고정한다. 열 선언 순서에 의존하지 않는다.

| EffectType | Value 순서 | 예 |
|---|---|---|
| StatModifierBonus | 수치 | 5 |
| EveryNthCycleBonus | 몇 번째, 피해 증가율(%), 충전 감소율(%) | 2,40,0 |
| HackingAttackSpeedBonus | 공속 증가율(%), 지속시간(초) | 15,2 |
| ChiefAttackSpeedBonus | 전체 공속 증가율(%), 지속시간(초) | 20,10 |
| RallyVulnerabilityBonus | 받는 피해 증가율(%), 지속시간(초) | 10,5 |
| HackingProductionBonus | 노말, 레어, 에픽, 전설 추가 생산 | 1,1,2,3 |
| HackingConsumptionBonus | 노말, 레어, 에픽, 전설 추가 소모 한도 | 1,1,2,4 |

고정 규약의 순서를 바꿀 때는 새 효과 종류 또는 명시적 데이터 이관이 필요하다. 인수 개수 오류·빈 값·음수·비유한수·잘못된 정수·충전 감소율 100% 이상은 거부한다. 등급별 생산·소모는 0~10000 정수다.

## 설명과 실행

델탕 Lv.15의 한 행: `deltan_lv15 / hackingPostSpeed / HackingAttackSpeedBonus / (빈칸) / (빈칸) / 15,2`.

설명: `[해킹] 스킬 사용 후 {hackingPostSpeed.Duration}초간 자신의 공격속도 +{hackingPostSpeed.Value}% (재발동 시 시간 갱신)`.

기획툴은 공속 증가율·지속시간을 각각 한글 입력란으로 표시한다. 설명 토큰도 버튼으로 넣는다. Unity의 UnitBonusValueType Enum과 UnitBonusValueContracts가 같은 고정 순서를 해석한 뒤 기존 StatModifierBonus·EveryNthCycleBonus 클래스로 투영한다. 복합 클래스와 기존 실행 클래스를 중복 구현하지 않는다. 실제 39개 부품을 30개 효과 행으로 저장한다.

이전 Part/Description 차트와 숫자 열 기반 Bonus/Effect 차트도 읽는다. 기획툴은 이전 작성용 엑셀을 가져오면 규약 구조로 이관한다. 여러 독립 효과와 사용자 정의 수치는 보존한다.

## 에디터 검사

`Tools > Backend Tests > 보너스 Value 규약 검사`에서 업로드용 `.backend.xlsx`를 선택한다. Node가 엑셀 표를 읽고 실제 게임 파서와 UnitLevelBonusController로 검사한다. 유닛·레벨별 잠금/해금, 설명, 누적 능력치를 확인한다. 베탕은 자폭 완료 버튼으로 다음 주기를 확인한다. 엑셀 수정 후 다시 불러온다. 서버·계정에는 기록하지 않는다. Node가 PATH에 있어야 한다.

작성용은 BonusDefinition/BonusEffect 두 시트다. 업로드용은 단일 UnitLevelBonuses 시트의 Bonus/Effect 60행이다. 차트 이름은 DEV_UnitLevelBonuses. 작성용 파일을 뒤끝에 올리지 않는다. 새 파일을 서버에 적용한 뒤 Play를 재시작해야 서버 다운로드 경로에 반영된다.

## 검증

- Node: 7종 규약, 잘못된 CSV, 여러 효과 행, 미완성 입력 검증, v1/v2 엑셀 이관 및 왕복 저장.
- 기획툴 UI: 22개 흐름 통과. 공속/지속시간 편집과 설명 연동 확인.
- Unity: 레벨 경계/기존 차트 비교 1735개, 실제 유닛 전투 99개, XLSX·규약·검사 창 17개 통과.
- 전체 Node 검사 중 구형 데이터 전제의 6개 검사는 변경 전 HEAD에서도 실패한다. 이번 변경의 회귀로 간주하지 않았으며 별도 정비가 필요하다.
