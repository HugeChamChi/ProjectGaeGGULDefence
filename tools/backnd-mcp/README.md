# GGD 뒤끝 관리 MCP

2026-10-26: 기획툴에 미션 설정·유닛 기본 스탯의 DEV 적용 화면과 공식 콘솔 창, 서버 파일 대조를 추가했다. 유닛 기본 스탯은 DEV/LIVE_UnitBaseStats 단일 스냅샷을 게임이 읽는다. 기존 3개 분할 차트의 강화 비용·성장·계정 레벨 연결은 별도다. 자동 업로드 API는 구현하지 않았으며 콘솔에서 업로드·적용한다. `verify-config-chart.mjs <backend.xlsx> <DEV_MissionEvents 또는 DEV_UnitBaseStats>`로 준비 파일과 서버의 현재 적용 파일을 비교할 수도 있다. Unity/Pipeline과 기존 뒤끝 로그인이 필요하고, 파일 일치는 실제 플레이 검증과 구분한다.

2026-10-25: 프로젝트 로컬 MCP 구현 및 Codex 등록 완료. 현재 범위는 **유닛 공통 밸런스 검증·배포 파일 준비·비교와 읽기 전용 SDK 연결**이다. 콘솔 테이블 생성, 차트 업로드/적용, 이벤트 exporter, 유저 저장, 게임 런타임의 SO 덮어쓰기는 포함하지 않는다. 유닛 강화의 실제 게임 적용 TODO는 유지한다.

## 설치와 연결

Node.js 20 이상과 인접 `tools/planner-studio/app/content-workbook.cjs` 및 해당 앱의 의존성이 필요하다. 기존 기획 툴의 스키마와 검증기를 재사용하며 별도의 밸런스 규칙 사본을 만들지 않는다.

XLSX 작성은 Codex 기본 런타임의 `@oai/artifact-tool`을 사용한다. 기본 경로는 사용자 홈의 `.cache/codex-runtimes/codex-primary-runtime/dependencies/node`이며 다른 설치 위치는 `GGD_ARTIFACT_DEPENDENCIES` 환경변수로 지정한다. 이 런타임이 없는 환경에서는 XLSX 생성이 오류로 끝나며 `format=csv`를 사용할 수 있다. 렌더러/작성기는 자식 프로세스에서 실행되어 MCP stdio 출력과 섞이지 않는다.

프로젝트 루트에서:

```powershell
npm.cmd ci --prefix tools/backnd-mcp --ignore-scripts
node tools/backnd-mcp/check-local.mjs
codex mcp get ggd_backnd --json
```

새 PC에 기획 툴 의존성이 없으면 `npm.cmd ci --prefix tools/planner-studio/app --ignore-scripts`도 필요하다.

현재 PC의 `.codex/config.toml`에 `ggd_backnd` stdio 서버를 등록했다. Codex에서 이 프로젝트를 다시 열거나 MCP 연결을 다시 불러오면 도구 목록을 확인할 수 있다. 현재 대화에 도구가 즉시 추가되는 것은 보장하지 않는다. 설정의 Node/프로젝트/Unity CLI 절대 경로는 다른 PC에서 수정해야 한다. 토큰·비밀번호를 설정에 저장하지 않는다.

기획 툴/MCP 소스는 Git 추적 대상이며 node_modules·실행 배포본·검증 출력은 제외한다. `.codex/config.toml`은 PC별 로컬 설정으로 유지한다. 다른 PC에서는 `codex mcp add ggd_backnd -- node <저장소의 절대 경로>/tools/backnd-mcp/server.mjs`로 등록하고 Unity CLI가 PATH에 있는지 확인한다.

## 도구

강화 트리는 [RESEARCH-CHART.md](RESEARCH-CHART.md)에 수정·적용·검증 방법을 정리했다. `validate_research_balance`와 `export_research_chart`를 추가했다. 게임은 현재 포함된 차트 데이터를 사용하며 원격 업로드는 미실행이다.

| 도구 | 기능 |
|---|---|
| `backnd_status` | 로컬 기능 및 Editor/Pipeline 연결 상태 확인 |
| `validate_unit_balance` | 작업실 `outgame-project.json` v2 유닛 밸런스 검사 |
| `export_unit_charts` | 기본 XLSX 4개 또는 선택 CSV와 로컬 manifest 생성 |
| `verify_chart_bundle` | 생성 파일의 해시·행·열·중복 키, XLSX 단일 시트/수식 없음 검사 |
| `compare_unit_chart` | 로컬 원본과 단일 시트 XLSX/콘솔 CSV/SDK rows JSON 비교 |
| `list_live_charts` | 기존 Unity 뒤끝 세션으로 차트 목록 조회 |
| `read_live_chart` | 지정한 차트의 적용 파일 조회, 결과 페이지 처리 |
| `compare_live_unit_chart` | 전체 서버 차트와 로컬 유닛 차트 비교 |
| `plan_unit_chart_deployment` | 기존 파일을 DEV(기본)/LIVE 차트명에 대응한 업로드 계획 반환, 고유 보너스 제외 |
| `verify_live_chart_bundle` | DEV(기본)/LIVE의 1차 대상 차트 3개를 실제 적용 차트와 대조 |

사용 예:

```json
{"projectFile":"tools/planner-studio/GGD-Planner/자료/내보내기/콘텐츠-2026-10-24T13-21-43-728Z-5ecac0/outgame-project.json","releaseKey":"unit-review-001","format":"xlsx"}
```

위 인자를 `export_unit_charts`에 전달하면 `outputs/backnd-mcp/unit-review-001/`에 생성한다. 사용자가 편집한 최신 프로젝트 파일을 명시해야 한다. 동봉 seed를 실제 입력 대신 자동으로 사용하지 않는다. 검증은 유닛 관련 5개 원본 표(Item/Unit/UnitBaseStat/UnitUpgrade/UnitMilestone)에 한정하며, 출석·미션·영구 강화 전체 프로젝트 검증을 의미하지 않는다.

4개 XLSX는 각각 Units/BaseStats/UpgradeStats/LevelBonuses 단일 시트를 가진다. 숫자는 숫자 셀, 키/JSON/설명은 문자열로 저장하며 수식은 넣지 않는다. 차트 이름은 기존 작업실의 키 기반 양식을 따른다. 서버에 이미 있는 이름은 조회 후 명시적으로 대응시켜야 한다. `manifest.json`은 로컬 파일 무결성 기록이며 업로드하는 차트나 이벤트 설계의 `EventRelease`가 아니다. 출력 형식을 포함한 v2 manifest를 생성하며 기존 CSV v1 manifest도 읽을 수 있다. 원본 해시와 생성 파일 해시를 기록하며 기존 release 폴더는 덮어쓰지 않는다. 일부 파일 생성 중 실패하면 그 폴더를 완성본으로 사용하지 말고 새 releaseKey로 다시 생성한다.

CSV는 UTF-8 BOM/CRLF, 표준 따옴표 이스케이프를 사용한다. 뒤끝의 예약 열 `id`, 열 이름 제한, 역슬래시 제한을 검사한다. 숫자/셀 비교는 문자열 표현 기준이므로 서버의 `1.0`과 로컬의 `1`도 차이로 표시할 수 있다. 행 순서는 무시하고 복합 키로 비교하며 SDK가 붙인 추가 메타데이터 열은 비교에서 제외한다. 변경 상세는 종류별 50개까지, 총 개수는 전체 기준이다.

## 단일 프로젝트의 테스트·운영 차트

새 유닛 밸런스 차트는 아래 이름을 사용한다. 기존 EndlessBalance 등 다른 차트는 변경하지 않는다.

| 파일 | 테스트 차트 | 운영 차트 |
|---|---|---|
| Units.xlsx | DEV_Units | LIVE_Units |
| BaseStats.xlsx | DEV_BaseStats | LIVE_BaseStats |
| UpgradeStats.xlsx | DEV_UpgradeStats | LIVE_UpgradeStats |

LevelBonuses.xlsx는 초안 보관용이며 이번 배포 대상에서 제외한다. 기존 exporter와 로컬 무결성 검사는 4개 파일을 유지하고, 배포 계획과 원격 묶음 비교는 3개를 사용한다.

`plan_unit_chart_deployment`에 `{"directory":"outputs/backnd-mcp/unit-xlsx-20261025"}`를 전달하면 DEV 업로드 계획을 반환한다. LIVE 계획은 같은 directory에 `"environment":"LIVE"`를 명시한다. 두 계획은 기존의 동일 파일/해시를 사용하며 업로드하지 않는다. `verify_live_chart_bundle`도 같은 인자를 받는다. 기존의 자유 입력 chartPrefix 인자는 environment로 교체했다. 도구 이름의 live는 서버 조회를 뜻하며 운영 환경 선택은 environment로 구분한다.

순서: 엑셀 내보내기 → DEV 업로드/적용 → DEV 묶음 비교 및 게임 테스트 → 동일 파일의 LIVE 반영 → LIVE 비교. 현재는 유닛 로더가 없으므로 게임 테스트·운영 배포 완료로 처리하지 않는다. 여러 차트의 혼합 버전을 방지할 서버 Release 계약도 후속 구현이다.

게임용 `UnitBalanceChartRoute`는 에디터·Development Build에서 DEV, 일반 출시 빌드에서 LIVE를 선택한다. 일반 빌드를 테스트용으로 배포하면 LIVE를 선택하므로 테스트 빌드에는 Development Build를 사용한다. 아직 실제 유닛 로더는 이 코드를 소비하지 않는다. 계정/테이블/설정은 같은 프로젝트에서 공유되므로 테스트 계정은 따로 사용한다.

서버 차트 생성·업로드·적용은 현재 자동화되지 않았다. MCP 소스를 수정한 뒤 기존 MCP 프로세스는 재연결해야 새 도구가 노출된다.

경로 코드 검증:

```powershell
dotnet run --project tools/backnd-mcp/test/RouteCompile.csproj -p:RouteSymbols=UNITY_EDITOR -- DEV
dotnet run --project tools/backnd-mcp/test/RouteCompile.csproj -p:RouteSymbols=DEVELOPMENT_BUILD -- DEV
dotnet run --project tools/backnd-mcp/test/RouteCompile.csproj -- LIVE
```

## 실서버 조회 조건

- 이 프로젝트의 Unity Editor가 열려 있고 Pipeline이 연결되어 있어야 한다.
- 게임의 기존 흐름으로 뒤끝 로그인이 완료되어 있어야 한다. MCP가 계정을 새로 만들거나 자동 로그인하지 않는다.
- MCP는 고정된 `tools/planner-studio/app/backend-chart-reader.cs`만 `run_script`로 실행한다. 도구 인자로 임의 C#이나 셸 명령을 받지 않는다.
- Editor가 비활성화되어도 콜백이 처리되도록 `set_autotick`을 활성화한다. 파일은 Assets 밖에서 메모리 컴파일되며 게임 빌드에 들어가지 않는다.
- SDK의 `GetChartListV2` → `selectedChartFileId` → `GetChartContents` → `FlattenRows` 경로를 사용한다. 현재 게임의 `BackendEndlessBalanceGateway`와 같은 읽기 방식이다.
- SDK 실패·연결 실패를 빈 차트로 간주하지 않는다. 서로 다른 차트 읽기의 원자적 릴리스 일관성은 제공하지 않는다.

**실서버 조회 검증 완료(2026-10-25)**: Unity 시작·에셋 가져오기 완료 후 실제 MCP stdio 연결에서 차트 목록 7개와 적용된 `EndlessBalance` 1행을 조회했다. 실제 CLI가 반환하는 `data.result.result` 응답을 처리하도록 수정했다. 기존 토큰 복원은 401이어서 별도 검증 헬퍼로 프로젝트의 기존 Editor 테스트 계정에 로그인한 후 확인했다. 계정 생성이나 게임정보 저장은 수행하지 않았다. MCP 도구 자체는 여전히 자동 로그인하지 않으며 Editor 재시작/도메인 재로드 시 로그인 상태를 다시 확인해야 한다.

## 검증

```powershell
npm.cmd test --prefix tools/backnd-mcp
dotnet build tools/backnd-mcp/test/BridgeCompile.csproj --nologo -v quiet
node tools/backnd-mcp/check-local.mjs
node tools/backnd-mcp/check-live.mjs
# 업로드하고 적용한 뒤 1차 대상 3개 대조 (두 번째 인자 DEV 또는 LIVE, 생략 시 DEV)
node tools/backnd-mcp/live-bundle.mjs outputs/backnd-mcp/unit-xlsx-20261025 DEV
```

C# 컴파일 검사는 .NET 9 SDK가 필요하며 Unity 안에서의 실행 검증을 대신하지 않는다. 테스트는 별도 임시 데이터로 XLSX/CSV 왕복 및 셀 타입, 누락/음수/중복/참조 오류 거부, 변경 비교, 파일 변조, 프로젝트 외부 경로, 동일 릴리스 덮어쓰기 거부 및 실제 stdio MCP handshake/tools 호출을 확인한다. 상세 결과는 [VERIFICATION.md](VERIFICATION.md).

## 근거

- [Codex MCP 설정](https://learn.chatgpt.com/docs/extend/mcp?surface=cli): 프로젝트 `.codex/config.toml`과 stdio 연결.
- [MCP TypeScript SDK](https://ts.sdk.modelcontextprotocol.io/server): McpServer/StdioServerTransport.
- [뒤끝 차트 관리](https://docs.backnd.com/guide/console-guide/backnd-base/chart/): 공통 데이터, 업로드 형식, 업로드와 적용의 구분. 공개 관리 API를 확인하지 못한 작업을 임의의 엔드포인트로 구현하지 않았다.
- `tools/planner-studio/app/content-workbook.cjs`, `app/ui/content/unit-keys.js`: 기존 키 기반 밸런스 계약.
- `Assets/WorkSpace/USW/Scripts/Endless/Runtime/BackendEndlessBalanceGateway.cs`: 실제 프로젝트 SDK 호출 경로.
