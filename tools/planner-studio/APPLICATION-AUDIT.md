# 기획툴 적용 점검 — 2026-11-05

현재 소스와 최근 구현·검증 기록을 기준으로 정리했다. 이 문서 갱신 과정에서 실서버·Unity 플레이 검사를 재실행하지 않았다. 파일 생성, 서버 파일 대조, 실제 게임 동작, 계정 저장은 별도 완료 조건이다.

## 현재 경로

| 기능 | 구현된 범위 | 남은 확인·제약 |
|---|---|---|
| 무한모드 | 기존 EndlessBalance 편집·차트 경로 | 변경 파일별 실제 런 회귀 확인 |
| 계정 성장 | AccountProgression.xlsx → 기존 Importer | 기본 설정의 중복 AccountLevel/BattleReward는 편집·출력에서 제외. 과거 JSON 값은 보존하며 자동 병합하지 않음 |
| 유닛 기본 스탯 | DEV 파일 준비·UnitBalanceChartLoader·화면/전투 런타임 | 기존 Units/BaseStats/UpgradeStats 다중 차트는 이 로더 입력이 아님 |
| 유닛 보너스 | BonusDefinition/BonusEffect 작성·Value 규약·실행 연결 | 임의 EffectId 문자열 입력은 새 실행 기능이 아님. 지원 효과 규약 사용 |
| 유닛 강화 구매 | 실제 계정 보유·골드·레벨 저장 및 조건부 트랜잭션 | 툴 UnitUpgrade 시안의 비용·성장 수치를 구매 정책에 그대로 연결한 것으로 해석하지 않음 |
| 영구강화 | ResearchBalance 로더·전투 연결·계정 서버 저장 | 별도 변환과 IconAddress 등록. 변경 차트별 게임 확인 필요 |
| 미션 | Events/Missions 로더·진행·계정 저장·수령 처리 코드 | 기간 경계·계정 변경·재시도·중복 수령 검증 범위를 확인해야 함 |
| 상점 | 편집·목록/확률표 출력·계정 진열·로비 구매 | 무료 수령/서버 복원 검증 기록 있음. Android 현금 구매·가격 수신은 미검증 |
| 기타 기본 설정 | 재화·스태미나·파티·프로필·튜토리얼 편집·출력 | 각 출력 양식의 소비 지점을 개별 확인. 출력 성공을 게임 반영으로 표시하지 않음 |

예전 문서의 “강화 저장·미션 지급 전체 미구현” 설명은 현재 코드와 다르다. 구현 존재와 모든 운영 상황 검증 완료는 구분한다.

## 원본과 파생 데이터

- 계정 경험치·전투 EXP 편집 원본은 밸런스 편집의 계정 성장이다. 콘텐츠 JSON의 옛 AccountLevel/BattleReward는 보존 자료다.
- AccountSettings.xlsx는 Economy만 출력한다. 이전 혼합 Excel에서는 Economy만 가져온다. 시작 레벨/EXP 등 에셋 스냅샷과 AccountProgression 사이의 자동 동기화는 없다.
- 보너스 원본은 BonusDefinition/BonusEffect이고 UnitMilestone은 생성 요약이다.
- 작성용 Excel, 뒤끝 업로드 파일, 런타임 사본은 용도가 다른 파생물이다. 이름이 비슷하다는 이유로 삭제하거나 하나의 양식으로 합치지 않는다.

## 적용 순서

1. 기획 작업을 JSON으로 고정하고 지원 대상의 업로드 파일·규약·해시를 생성한다.
2. DEV에 업로드·적용하고 준비 파일과 서버 파일을 대조한다. 미션은 DEV_Events와 DEV_Missions를 함께 준비한다.
3. 동일 빌드에서 로그인 → 화면 → 전투와 계정 저장·복원을 확인한다.
4. 기능에 따라 재접속·기간 경계·중복 요청·통신 실패를 확인한다.
5. 검증된 동일 파일을 LIVE에 적용하고 이전 파일·해시·검증 기록을 보관한다. 차트 복구는 이미 지급된 재화나 계정 진행을 되돌리지 않는다.

## 근거

- 코드: app/backend-deployment.cjs, app/ui/content/current-scope.js, app/ui/content/bonus-editor.js, build-library.cjs.
- 게임: EventMissions.cs, EventChartLoader.cs, PlayerDataController.cs, BackendGameData.cs, AccountDeckUpgradeStore.cs.
- 최근 기록: [보너스 규약](../../docs/technical/unit-bonus-authoring-v2.md), [계정 저장](../../docs/technical/account-save-schema.md), [로비 강화 검증](../../docs/technical/lobby-gold-flow-validation-20261103.md), [상점 구매](../../docs/technical/shop-purchase-20261104.md).
