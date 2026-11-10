# GGD 기획 작업실 · 팀 사용 안내

2026-11-05 소스 기준. 기능별 적용 범위와 검증 기록은 [APPLICATION-AUDIT.md](APPLICATION-AUDIT.md)를 확인한다. 이 안내를 수정하면서 실서버 검사를 다시 수행한 것은 아니다.

## 실행·저장·전달

1. 배포 폴더 전체를 압축 해제하고 GGD-Planner/GGD-Planner.exe를 실행한다. EXE 하나만 옮기지 않는다.
2. 편집 작업은 PC별 사용자 폴더에 자동 저장된다. 다른 PC로 옮기기 전 프로젝트 JSON을 저장하고, 받는 PC도 현재 작업을 백업한다.
3. Excel은 작성용과 업로드용을 구분한다. 파일 추출만으로 서버나 게임이 바뀌지 않는다.
4. 업데이트 전 실행 중인 앱을 종료한다. 새 실행본에서 JSON 복원·Excel 왕복·재시작 복원을 확인한다.

## 계정 데이터 관리 위치

계정 경험치 곡선과 전투 EXP는 **밸런스 편집 → 계정 성장**에서 관리한다. AccountProgression.xlsx를 개발자에게 전달하고 기존 Unity Importer로 반영한다. 뒤끝 업로드용 차트가 아니다.

기본 설정의 AccountLevel/BattleReward 중복 편집 메뉴는 제거했다. 이전 콘텐츠 JSON의 값은 보존하지만 Excel/ZIP 출력에서는 제외한다. 자동 병합은 하지 않는다. 과거에 이 표를 별도로 수정했다면 JSON을 보관하고 계정 성장 원본에 필요한 값을 검토해 반영한다. AccountSettings.xlsx에는 Economy만 들어가며 이전 혼합 Excel에서도 Economy만 가져온다. 기본 설정에 남은 시작 레벨/EXP 스냅샷은 계정 성장과 자동 동기화되지 않는다.

유닛 레벨 보너스는 BonusDefinition/BonusEffect에서 편집한다. UnitMilestone은 생성 요약이므로 별도로 고치지 않는다.

## DEV 파일 준비와 적용

뒤끝 DEV 적용에서 대상을 선택하고 파일을 준비한다. 업로드용 파일을 직접 고치지 말고 원본 프로젝트에서 다시 생성한다. 원본 JSON·작성용 Excel·규약·deployment.json·해시를 함께 보관한다.

| 대상 | 업로드 차트 |
|---|---|
| 유닛 기본 스탯 | DEV_UnitBaseStats |
| 유닛 레벨 보너스 | DEV_UnitLevelBonuses |
| 미션 | DEV_Events와 DEV_Missions 두 파일 |

미션의 MissionEvents.xlsx는 작성용 Events/Missions 두 시트다. 예전 단일 DEV_MissionEvents 스냅샷 업로드 설명을 따르지 않는다. 파일 준비 결과의 대상 차트명을 확인한다.

공식 콘솔에서 업로드·적용한다. 내장 창 로그인이 동작하지 않으면 기본 브라우저로 연다. 서버 파일 확인은 준비 파일과 실제 적용 파일을 대조하는 조회이며 플레이 검증을 대신하지 않는다. 이 조회에는 연결된 Unity Editor/Pipeline 및 기존 로그인 세션이 필요하다.

게임의 EventChartSettings/UnitBalanceChartSettings 등 기능별 설정과 실제 빌드의 DEV/LIVE 선택을 확인하고 재시작한다. 설정 기본값을 추정해 바꾸지 않는다. 상점 목록/확률과 영구강화는 아래 별도 안내를 따른다.

## 기능별 주의점

- 유닛 기본 스탯은 원본 SO를 보존한 런타임 사본으로 적용한다. AttackFrequency는 회/초다.
- 보너스는 고정 Value 규약과 지원 효과를 사용한다. 임의 EffectId 입력으로 새 효과가 실행되지는 않는다.
- 본 로비의 유닛 골드 강화·영구강화 계정 저장은 구현되어 있다. 툴 강화 시안의 모든 비용·성장 값이 실제 구매 정책에 연결됐다는 뜻은 아니다.
- 미션 진행 저장·수령·보상 처리 코드도 구현되어 있다. 변경 이벤트마다 계정 격리·재접속·기간 경계·중복 요청을 확인한다.
- 상점은 목록과 ID 확률표를 함께 관리한다. Android 현금 구매·가격 수신은 별도 확인 대상이다.
- 기타 기본 설정의 Excel 출력은 기능별 게임 연결 확인이 필요하다.

[보너스 Value 규약](../../docs/technical/unit-bonus-authoring-v2.md) · [상점 차트](../../docs/technical/shop-catalog.md) · [상점 구매](../../docs/technical/shop-purchase-20261104.md) · [영구강화 차트](../backnd-mcp/RESEARCH-CHART.md)
