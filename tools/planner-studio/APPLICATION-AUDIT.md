# 기획툴 실무 적용 판단 — 2026-10-28

## 판정

전체 운영 적용 불가. 편집/검토 도구로 사용 가능하며 기능별로 검증 후 적용한다.
Excel 생성, 서버 파일 일치, 실제 게임 반영, 계정 저장/지급은 별개의 완료 조건이다.
최근 실서버 테스트는 UnitBaseStats의 9개 유닛/36개 등급 다운로드·해시·형식 검증 성공이다. 전투 전체 회귀나 운영 승인으로 해석하지 않는다. MissionEvents는 마지막 조회에서 적용 차트가 없었다.

## 데이터별 적용 경로

| 데이터 | 현재 경로 및 판단 | 적용 전 필요한 작업 |
|---|---|---|
| 기존 무한모드 밸런스 | 기존 편집기 → EndlessBalance → BackendEndlessBalanceGateway/EndlessBalanceService | 기존 양식 유지, 실제 런 시작/라운드 보상 회귀 검증 |
| 기존 계정 성장 | AccountProgression.xlsx → Unity Importer → AccountProgressionConfig | 서버 업로드 대상이 아님. 새 AccountLevel/BattleReward 표와 동일 원본으로 통합 필요 |
| 유닛 기본 스탯 | unit-runtime.cjs → DEV_UnitBaseStats → UnitBalanceChartLoader → UnitBalanceRuntime | 실제 기획 프로젝트로 재생성, 원본 매핑·화면·전투 수치 검증 |
| 유닛 강화 비용/성장/보너스 | 편집·Excel 출력. 기본 스탯 로더 대상 아님 | 강화 레벨 저장, 비용 차감, 중복 구매 방지, 효과 실행, 화면/전투 연결 |
| 족장 정보 | 편집 데이터에 있으나 unitRuntime은 Kind=Unit만 출력 | 족장 전용 적용 계약 및 소비 지점 정의 |
| 영구강화 정의 | research.mjs → ResearchBalance → ResearchChartService → 화면/전투 | 별도 변환, IconAddress 확인, DEV 다운로드와 실제 전투 검증 |
| 영구강화 진행 | 기존 로컬 저장·무료 강화 정책 | 운영 계정 저장 요구가 있으면 서버 이관. 기존 키 변경 시 이관 정책 필요 |
| 미션 이벤트 | DataBindings/MissionEvents/Missions 및 카탈로그 → 단일 MissionEvents 스냅샷 → EventDataRuntime | 서버 적용, 기간/초기화/계정 격리 검증. 진행 저장·복원·수령·보상 지급 구현 |
| 출석 이벤트 | AttendancePlan/Attendance를 새 이벤트 Excel로 출력 | 신규 EventKey 기반 게임 로더, 서버 시간, 일차 계산, 중복 수령 방지·지급 연결 |
| 파티·덱 | 편집·Excel 출력 | PartyDataSO/해금/선택과 새 출력의 변환 계약·참조 검증 |
| 재화·스태미나 | 편집·Excel 출력 | 신규 양식 로더, 기존 계정에 초기값을 재지급하지 않는 정책, 회복/상한 변경 검증 |
| 새 계정 성장 표 | AccountLevel/BattleReward 편집·출력 | 기존 AccountProgressionConfig와 중복 원본 제거, 레벨 변경 이관 검증 |
| 프로필 | 편집·Excel 출력 | 실제 프로필 표시/해금 데이터 소비 경로 연결 |
| 튜토리얼 | 텍스트 편집·Excel 출력 | 신규 규약 로더, 실제 단계 키/리치텍스트 검증, 기존 완료 상태 보존 |
| Item/실행 지점 카탈로그 | 참조 데이터 및 코드가 제공하는 실행 지점 | 새 키 입력만으로 기능이 생기지 않음. 지원 키와 게임 구현을 함께 변경 |

## 유지할 구조

- 공통 BackendChartReader는 SDK 조회만 담당한다. 도메인별 로더가 검증·수명·적용을 담당한다.
- 유닛/미션의 단일 스냅샷과 해시 검증은 연관 표의 버전 혼합을 막는다.
- 원본 SO와 런타임 사본을 분리한다. 전투 중 정의 교체보다 재시작 시 적용을 기본으로 한다.
- 미션 이벤트와 출석 이벤트는 서로 다른 진행·수령 규칙을 유지한다. 공통 EventKey/기간/활성 여부는 규약을 맞추되 하나의 거대한 차트로 합칠 필요는 없다.

## 먼저 해결할 구조 문제

1. **원본 단일화**: 기존 계정 성장 편집기와 콘텐츠 AccountLevel/BattleReward의 편집 원본을 하나로 정한다. 현재 자동 동기화되지 않으므로 양쪽 수정 금지.
2. **배포 경로 단일화**: 현재 유닛 로더의 입력은 UnitBaseStats다. MCP의 Units/BaseStats/UpgradeStats 다중 차트는 현재 로더와 호환되지 않는다. 운영 메뉴에서 두 경로를 같은 적용 기능으로 안내하면 안 된다.
3. **계정 상태와 정의 분리**: 차트는 공통 설정만 갖는다. 강화 레벨, 미션 진행, 출석 일차, 수령 기록은 계정 저장소가 소유해야 한다.
4. **지급 일관성**: 비용 차감/레벨 증가, 수령 기록/재화 지급이 재시도·중복 요청·재접속에도 한 번만 반영되도록 서버 처리 계약과 테스트를 먼저 마련한다. 클라이언트 버튼만 연결하면 운영 불가.
5. **버전·키 변경**: EventKey/MissionKey/UnitKey/TreeKey/NodeId 변경과 삭제 시 기존 계정 데이터 처리 정책을 마련한다. 서버 파일 롤백은 이미 지급된 보상이나 진행도를 되돌리지 않는다.
6. **배포 복원성**: 2026-10-28 소스 수정 완료. 새 배포는 source-project.json과 해시, 작업 폴더의 deployment-history.json을 보존하며 재시작 후 복원한다. 원본/작성 Excel/업로드 파일 변조를 거부한다. 이전 버전에서 생성한 파일은 이력에 자동 등록되지 않으므로 원본 프로젝트로 다시 준비한다. 실행본 갱신은 앱 파일 잠금으로 아직 미완료다.
7. **검증 상태 표시**: 설정 준비/서버 일치/게임 검증/운영 가능을 구분한다. 현재 서버 테스트 씬은 다운로드·검증용으로 플레이/지급 보증이 아니다.

## 실제 적용 순서

1. 기획자의 현재 작업을 프로젝트 JSON으로 저장하고 릴리스 원본으로 고정한다. Studio.create()로 만든 기본 예시 파일을 실제 기획 변경본으로 사용하지 않는다.
2. 지원 대상은 툴의 DEV 적용 파일 준비로 작성용 Excel·backend.xlsx·규약·deployment.json을 함께 보관한다. 영구강화는 별도 research.mjs 경로를 사용한다.
3. DEV 차트에 해당 파일을 업로드하고 적용한다. 업로드용 파일은 직접 수정하지 않고 원본 프로젝트에서 재생성한다.
4. 툴의 서버 적용 파일 확인으로 릴리스/해시를 대조한다. 테스트 씬의 형식 통과만으로 원본과 동일하다고 판단하지 않는다.
5. 실제 앱 경로에서 해당 UseRemote를 켜고 재시작하여 로그인→화면→전투를 검증한다. 설정은 빌드에 포함되므로 LIVE 차트 없이 일반 출시 빌드로 배포하지 않는다.
6. 저장/지급 기능은 재접속·중복 요청·계정 변경·날짜 경계·통신 실패를 검증한다. 미구현 기능은 운영 이벤트로 활성화하지 않는다.
7. 통과한 동일 파일을 LIVE에 적용하고 이전 파일 ID와 원본·해시·클라이언트 버전·검증 기록을 보관한다.

## 구현 순서

### 실행본 검증 갱신

2026-10-28: `outputs/planner-production-review/GGD-Planner.exe` 별도 패키징 완료. 실제 Electron 콘텐츠 UI 검사 20개 통과(새로고침 후 동일 배포 버전 복원 포함). 백엔드 세션 재등록을 통한 재시작 복원·변조 거부는 check-deployment-history.cjs로 별도 통과했다. 기존 설치 경로는 app.asar 파일 잠금이 남아 있어 갱신하지 않았다.

강화 연결 확인: DeckUpgradeProgress는 초기 money=12000, 고정 Cost=150×Level, 고정 성장률, 로컬 미리보기 저장이다. BackendGameData의 PlayerData 저장에 강화 상태를 단순히 덧붙이는 것만으로 중복 구매/동시 쓰기 문제는 해결되지 않는다. 현재 미리보기 진행을 실계정으로 이관하거나 자동 지급하면 안 된다. 다음 구현은 서버에서 검증하는 비용·레벨 변경과 요청 중복 방지 계약이 먼저다.

P0: 적용 범위/원본/배포 경로 정리 → P1: 유닛 기본 스탯 실제 게임 DEV 검증 → P2: 강화 정의 및 계정 저장 연결 → P3: 미션 저장/수령/지급과 출석 연결 → P4: 기본 설정/튜토리얼/프로필 로더.
무료/유료 강화, 상시/일일/주간 미션, 출석 누락일 처리 등 제품 규칙은 현재 코드에서 추측해 확정하지 않는다.

## 확인한 코드

app/backend-deployment.cjs, app/backend-verification.cjs, app/content-service.cjs, app/content-workbook.cjs, app/unit-runtime.cjs, app/schema.cjs, app/ui/content/current-scope.js.
게임: BackendChartReader, UnitBalanceChartLoader/UnitBalanceSnapshot/UnitBalanceRuntime, EventChartLoader/EventDataRuntime/EventDataBridge, ResearchChartService.
운영 문서: TEAM-GUIDE.md, MAINTENANCE.md, ../backnd-mcp/RESEARCH-CHART.md.
