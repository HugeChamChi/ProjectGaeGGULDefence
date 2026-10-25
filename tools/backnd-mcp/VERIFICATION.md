# 검증 결과 — 2026-10-25

- `npm test`: 9개 통과, 실패 0. 실제 SDK 클라이언트로 stdio 서버를 시작해 도구 8개 조회, 검증/내보내기/번들검증/비교/오류 반환을 확인했다.
- `dotnet build test/BridgeCompile.csproj`: 프로젝트에 설치된 Backend.dll, LitJSON.dll 참조로 조회 브리지 컴파일 성공, 경고 0/오류 0.
- `codex mcp get ggd_backnd --json`: 프로젝트 설정을 읽어 enabled=true, 올바른 Node 실행 경로/서버 경로/작업 디렉터리를 반환했다.
- `node check-local.mjs`: 실제 MCP 초기화 및 도구 조회 성공. Editor 연결은 실패로 정확히 보고했다.
- 실제 기획 원본 `tools/planner-studio/GGD-Planner/자료/내보내기/콘텐츠-2026-10-24T13-21-43-728Z-5ecac0/outgame-project.json` 검증 성공.
- 검토용 내보내기: `outputs/backnd-mcp/review-20261025-1792891724585/`. Units 10행, BaseStats 36행, UpgradeStats 114행, LevelBonuses 30행. 4개 CSV 왕복 및 manifest 해시 재검증 성공.
- 원격 업로드/차트 적용/테이블 생성/유저 데이터 쓰기/Unity 런타임 밸런스 변경 없음.

실서버 조회는 미검증이다. Unity 프로세스 목록은 남아 있었지만 Pipeline 연결은 응답하지 않았고 Editor.log의 마지막 기록은 종료였다. 자동 재시작이나 자동 로그인은 수행하지 않았다. 현재 대화의 도구 목록에는 새 MCP가 동적으로 추가되지 않았으므로, 다음 프로젝트 세션/MCP 재연결에서 확인해야 한다.

## 11:06 후속 연결 시도

사용자 요청으로 `unity projects open`을 실행했다. 새 Editor 프로세스가 시작됐으나 `Recovering Scene Backups` 창에서 대기하며 Pipeline은 아직 연결되지 않았다. 저장되지 않은 씬 복구 여부는 사용자에게 선택을 요청했고, 임의로 복구/폐기하지 않았다. 창 처리 후 실제 SDK 조회 검증을 재개해야 한다.

## 11:26 후속 — 실서버 조회 완료

- 사용자 복구 창 처리 후 Editor 시작과 스크립트 컴파일/에셋 가져오기가 완료됐다. Unity 6000.3.11f1, Pipeline 7800 연결 확인.
- 실제 CLI 응답에서 run_script 결과가 `data.result.result`에 들어가는 것을 확인했다. `unwrapRead`를 수정하고 실제 중첩 형식과 실패 응답 테스트를 갱신했다.
- 기존 토큰 복원은 401. 검증 헬퍼에서 기존 `BackendManager`의 Editor 테스트 계정으로 로그인만 시도했다. 로그인 콜백 대기는 15초를 넘겼지만 이후 실제 로그인 상태와 차트 조회 성공이 확인됐다. 회원가입·유저 게임정보 저장 경로는 실행하지 않았다.
- `node tools/backnd-mcp/check-live.mjs`: 실제 stdio MCP 초기화 → `backnd_status` → `list_live_charts` → `read_live_chart` 성공.
- 조회된 차트 7개. `EndlessBalance` 적용 파일 `269715`, 전체 1행/반환 1행, SchemaVersion/Mode/StartSeconds 등 24개 열 확인.
- 차트 생성·업로드·적용, 유저 게임정보 쓰기 및 게임 런타임 밸런스 변경 없음. 초기의 실서버 미검증 상태는 이 결과로 해소됐다.

## XLSX 내보내기 후속

- `export_unit_charts` 기본 출력은 XLSX로 변경, `format=csv` 선택 유지. manifest v2에 형식 기록, 기존 CSV v1 읽기 호환 유지.
- 숫자 타입, 한글/따옴표/줄바꿈, JSON, 빈 문자열, 수식처럼 시작하는 일반 텍스트를 XLSX로 왕복하고 단일 시트 구성/수식 없음을 검사한다.
- `compare_unit_chart`는 XLSX 입력을 추가 지원. `verify_live_chart_bundle` 도구와 CLI는 4개 파일 전부를 실제 차트와 대조하며 누락/조회 실패/값 불일치를 실패로 반환한다.
- 실제 기획 원본으로 `outputs/backnd-mcp/unit-xlsx-20261025/`에 XLSX 4개 생성, 10/36/114/30행과 셀 값/타입 및 manifest 해시 검증 완료. 4개 시트의 렌더링도 확인했다.
- 이번 XLSX 묶음은 아직 서버에 업로드/적용하지 않았다. 파일 생성/로컬 왕복 검증과 서버 적용 검증은 별개다.
