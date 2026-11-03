# 뒤끝 연결 유지보수

공통 통신·전송 형식과 기능별 적용 정책을 분리합니다. 게임 데이터 로더에서 툴이나 Unity CLI를 호출하지 않습니다. 공식 콘솔은 별도 창이며 기획툴의 파일·IPC 권한을 갖지 않습니다.

## 책임과 의존성

| 영역 | 진입 파일 | 책임 |
|---|---|---|
| 화면 | `app/ui/index.html`, `app/ui/backend-deployment.js` | 정적인 화면·이벤트·진행 상태. 데이터 변환과 SDK 호출 없음 |
| IPC 등록 | `app/main.cjs`, `app/preload.cjs` | 허용 채널과 호출자 확인, 기능별 모듈 조립 |
| DEV 파일 준비 | `app/backend-deployment.cjs` | 지원 대상 제한, 기능별 직렬화 호출, 준비 파일·해시 기록 |
| 기능별 직렬화 | `app/unit-runtime.cjs`, `app/content-workbook.cjs` | 게임에서 지원하는 데이터만 출력 |
| 전송 형식 | `app/chart-payload.cjs` | 공통 스냅샷 조각·버전·SHA-256 검증. 미션/유닛 의미를 해석하지 않음 |
| 서버 파일 대조 | `app/backend-verification.cjs` | 선택한 Unity 프로젝트에서 읽기 전용 조회 후 버전·내용 비교 |
| SDK 조회 원본 | `app/backend-chart-reader.cs` | 앱과 MCP가 함께 쓰는 고정된 조회 코드. 로그인·서버 쓰기 없음 |
| 공식 콘솔 창 | `app/backend-console.cjs` | 원격 창의 권한·수명 관리. 전용 세션, preload와 Node 권한 없음 |

게임 쪽의 `Scripts/BackendCharts/BackendChartReader.cs`는 SDK 조회만 담당합니다. 이벤트·유닛·연구 로더가 이 코드를 재사용하며, 각 로더는 자기 수명·계정 검사·취소·로컬/원격 정책을 유지합니다. `ChartSnapshotPayload.cs`는 미션과 유닛의 공통 전송 형식만 검증합니다. 연구 차트의 기존 행 형식은 바꾸지 않습니다.

유닛 도메인은 `UnitBalanceSnapshot`에서 키·등급·수치와 런타임 사본을 관리합니다. 원본 SO, 스킬·프리팹 참조, 계정 강화 진행을 서로 섞지 않습니다. 원격 사용을 켰는데 로드되지 않았다면 원본 수치로 조용히 대체하지 않습니다.

전투·팩토리·강화 화면은 `UnitBalanceRuntime`에만 의존합니다. `UnitBalanceChartLoader`는 공통 SDK 조회와 검증을 마친 뒤 런타임에 스냅샷을 설치합니다. 따라서 네트워크 구현을 바꿔도 전투 코드의 조회 지점은 유지됩니다. 앱 종료 시 로더가 진행 중인 요청을 무효화하고 런타임 사본을 정리합니다.

## 지원 대상을 추가할 때

먼저 출력 규약과 게임의 실제 소비 지점을 연결합니다. 그다음 `backend-deployment.cjs`에 대상과 직렬화 함수를 추가하고 화면 선택지를 엽니다. 준비·서버 파일 일치·실제 게임 검증은 서로 다른 상태로 유지합니다. 파일 생성만 된 대상을 적용 완료로 표시하지 않습니다.

공통 조회·스냅샷 코드를 기능별 폴더에 복제하지 않습니다. 새 기능이 계정 상태를 소유한다면 해당 기능에서 저장·복원·지급 정책을 구현하며, 공통 차트 통신에 섞지 않습니다.

## 검증과 배포

```powershell
node tools/planner-studio/check-unit-runtime.cjs
node tools/planner-studio/check-backend-verification.cjs
node tools/planner-studio/check-content.cjs
node tools/planner-studio/check-workbook.cjs
node tools/planner-studio/check-excel-contract.cjs
node tools/planner-studio/check-missions.cjs
npm.cmd test --prefix tools/backnd-mcp
dotnet build tools/backnd-mcp/test/BridgeCompile.csproj
```

Unity 컴파일 후 `tools/backnd-mcp/test/UnitBaseStatsEditorChecks.cs`의 `Run`으로 화면·전투 수치와 SO 보존을 검증합니다. `check-unit-runtime.cjs`가 만드는 격리 검증 자료를 사용하고, 실제 계정 데이터는 쓰지 않습니다. 실행본 패키징 후 `check-ui.cjs`와 내장 `--smoke`를 실행합니다. 최종 전달 ZIP은 `release-team.py`로 생성하며 개인 작업·콘솔 세션·임시 백업을 포함하지 않습니다.

실서버 검증은 로그인된 Editor에서 `verify-config-chart.mjs` 또는 툴의 「서버 적용 파일 확인」으로 수행합니다. 조회 성공을 보상 지급이나 플레이 검증 완료로 해석하지 않습니다.

Excel의 보상·비용은 `RewardCore.parseItems`를 공유합니다. 기능별로 별도 구분자나 수량 규칙을 만들지 않습니다. 공격 속도는 새 Excel과 게임 출력에서 `AttackFrequency`(회/초)를 사용하며 기존 저장 JSON의 `AttackInterval` 호환은 입출력 경계에서 처리합니다. 작성 규칙은 `EXCEL-CONTRACT.md`를 따릅니다.
