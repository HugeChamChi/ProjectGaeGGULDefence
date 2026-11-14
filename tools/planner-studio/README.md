## 2026-11-12 백엔드 출력 기준

현재 Excel 내보내기는 표별 단일 시트 `.backend.xlsx`를 생성합니다. 작성용 다중 시트 Excel, `.data.json`, `.schema.json`, CSV/전체 ZIP 중복 출력은 제거했습니다. 프로젝트 JSON 백업·불러오기는 유지하며, 배포 준비에는 복원용 `source-project.json`과 이력이 포함됩니다. 기존 작성용 Excel은 이전 자료 가져오기용으로 읽을 수 있습니다.

유닛 강화 비용은 UnitBaseStats의 UpgradeCost 행으로 출력하며 게임 구매에 연결됩니다. Gold·Diamond·UpgradeTokenA 중 한 재화와 양의 정수 수량을 사용합니다. 게임에서 소비하지 않는 미리보기 강화 배율·전체 보너스 편집은 제거했습니다. 과거 JSON의 값은 보존하지만 게임 출력 대상은 아닙니다. 실제 레벨 보너스는 보너스 편집 → UnitLevelBonuses 차트로 관리합니다. 게임의 성장 공식을 새로 변경한 작업은 아닙니다.

아래의 작성용 Excel 구조·왕복 설명은 기존 파일을 가져오는 호환 규약입니다. 현재 출력 기준은 이 절을 따릅니다.

﻿# GGD 기획 작업실

2026-11-05 코드 기준. 편집·파일 생성, 서버 파일 일치, 실제 플레이 검증은 서로 다른 상태다. 기능별 현재 범위는 [적용 점검](APPLICATION-AUDIT.md), 사용 순서는 [팀 안내](TEAM-GUIDE.md), 남은 일은 [NEXT-STEPS.md](NEXT-STEPS.md)를 따른다.

## 2026-11-14 뒤끝 차트 업로드

왼쪽 **뒤끝 차트 업로드**에서 파일·내용 이름·DEV/LIVE를 선택하고 **내용 확인 후 업로드…**를 누릅니다. **주의!** 창에서 전체 차트명과 파일을 확인한 다음에만 업로드·적용합니다. LIVE는 출시 영향 안내와 별도의 확인 인자로 실행합니다. 성공 시 적용 파일 ID·이전 파일 ID·행 수와 이력을 표시합니다.

콘텐츠 편집 화면의 **차트 업로드…**는 현재 값을 검사·내보내고 파일을 연결합니다. 유닛 수정 중에는 먼저 수정 저장 또는 취소해야 합니다. 기존 DEV 적용 준비의 **준비한 차트 업로드…**도 사용할 수 있습니다. 묶음은 파일별로 하나씩 확인해 적용합니다.

게임 프로젝트의 `Tools/backnd-mcp/chart-upload.mjs`와 해당 도구의 의존성·Chrome이 필요합니다. 경로를 찾지 못하면 프로젝트를 선택하세요. Node 실행은 동봉 Electron을 사용합니다. 로그인 필요 안내가 나오면 **업로드용 로그인**을 누릅니다. 앱의 수동 콘솔 창과 업로드 명령의 로그인은 별개입니다. 업로드·로그인·상태 조회는 앱 안에서 한 번에 하나만 실행하며 외부 CLI/MCP에서도 동시에 명령을 실행하지 마세요.

게임 규칙 검사는 별도로 먼저 진행합니다. 실패·시간 초과·적용 확인 불명확 시 자동 재시도하지 않습니다. 콘솔의 적용 파일을 확인한 뒤 판단하세요. 확률표와 COMMON_ 차트 업로드는 지원하지 않습니다.

## 2026-11-14 체력 연동 경제

강화 밸런스 → **체력 연동 경제**에서 전투 보상·유닛 강화석·BASIC 영구강화 골드의 공식 계수를 편집합니다. 새 프로젝트는 HP 연동 기획안의 168,230강화석 / 1,684,450골드를 사용합니다. 기존 저장본은 비용을 보존하므로 문서 기준값 불러오기 → 일반 유닛·BASIC 비용표 생성으로 반영하세요.

백엔드 Excel 저장은 단일 시트 차트 3개와 뒤끝 Function 호출용 C# 계산 모듈을 출력합니다. 공식과 가격표가 다르면 출력을 막습니다. 실제 서버 지급·차감 연결과 Function 배포는 별도입니다. [출력 규약과 서버 연결 안내](app/Function/README.md)를 따릅니다.

## 실행과 개발

프로젝트에서는 `기획작업실 실행.cmd` 또는 `GGD-Planner/GGD-Planner.exe`를 실행한다. 전달할 때는 EXE 하나가 아니라 GGD-Planner 폴더 전체를 전달한다. 사용에는 Node/Python/Unity/Excel 설치가 필요 없다.

개발 소스는 `app/`, 동봉 자료는 `app/seeds/`다. 새 PC에서는 `npm.cmd ci --prefix tools/planner-studio/app --ignore-scripts`로 의존성을 설치한다. 패키징에는 기존 Electron 런타임이 필요하며 카탈로그 생성에는 .NET 9 SDK도 사용한다. `integrate.py`는 초기 추출 소스 복원용이므로 일반 빌드에서 실행하지 않는다.

## 편집 원본

| 대상 | 관리 위치 | 출력·적용 경로 |
|---|---|---|
| 무한모드 | 밸런스 편집 | EndlessBalance.xlsx → 차트 |
| 계정 경험치·전투 EXP | 밸런스 편집 → 계정 성장 | AccountProgression.xlsx → 기존 Unity Importer |
| 유닛 기본값·강화 시안 | 강화 밸런스 → 유닛 | UnitBalance.xlsx. 강화 시안 수치와 실제 구매 정책은 구분 |
| 레벨 보너스 | 레벨 보너스 설계 | BonusDefinition / BonusEffect → UnitLevelBonuses 차트 |
| 영구강화 | 강화 밸런스 → 영구강화 | ResearchBalance 변환 경로 |
| 미션 | 이벤트 | 편집용 MissionEvents.xlsx → Events / Missions 두 업로드 파일 |
| 상점 | 상점 | 편집용 목록 → 목록 차트·ID 확률표 분리 출력 |
| 재화·스태미나·파티·프로필·튜토리얼 | 기본 설정 | 기능별 편집용 Excel. 출력만으로 게임에 적용되지 않음 |

계정 성장의 중복 `AccountLevel` / `BattleReward` 표는 기본 설정 메뉴와 콘텐츠 Excel/ZIP 출력에서 제외했다. 기존 콘텐츠 JSON에는 사용자 수정값을 보존한다. 이 값은 AccountProgression으로 자동 합치거나 덮어쓰지 않는다. 서로 다른 과거 작업값은 JSON을 보관하고 원본 계정 성장에서 검토 후 반영한다. AccountSettings.xlsx는 Economy만 출력한다. 이전 혼합 Excel을 가져오면 Economy만 반영하고 과거 계정 표는 가져오지 않는다.

유닛 보너스의 UnitMilestone은 BonusDefinition/BonusEffect에서 생성하는 요약이다. 독립 원본으로 편집하지 않는다. 초기 계정 상태 등 기본 설정에 남은 에셋 스냅샷은 AccountProgression과 자동 동기화되지 않는다.

## 저장과 내보내기

콘텐츠 작업은 사용자 폴더의 `content-project-v2.json`에 자동 저장하며 전달용 JSON으로도 보관한다. 기존 밸런스 실험 저장과 콘텐츠 프로젝트는 서로 다른 자료다. JSON 불러오기 전 현재 작업을 저장한다.

Excel 가져오기는 포함된 지원 표만 교체한다. 사용자 작업 파일은 패키징 과정에서 임의로 덮어쓰지 않는다. 데이터 오류가 있으면 Excel 출력을 막고 초안은 JSON으로 보관한다. 규약은 [EXCEL-CONTRACT.md](EXCEL-CONTRACT.md), 보너스는 [Value 규약](../../docs/technical/unit-bonus-authoring-v2.md), 상점은 [상점 규약](../../docs/technical/shop-catalog.md)을 따른다. 고정된 지원 표 개수 대신 현재 메뉴와 생성 규약을 기준으로 확인한다.

## 검증과 패키징

```powershell
node tools/planner-studio/check-composition.cjs
node tools/planner-studio/check-content.cjs
node tools/planner-studio/check-workbook.cjs
node tools/planner-studio/check-excel-contract.cjs
python tools/planner-studio/package.py
node tools/planner-studio/check-ui.cjs
```

실행본 갱신 전 앱을 종료한다. `package.py --runtime <런타임폴더> --output <새폴더>`로 별도 검증본을 만들 수 있다. `release-team.py --output <새ZIP경로>`는 개인 자료와 임시 백업을 제외한 전달본을 만든다. 공통 통신·배포 모듈 책임은 [MAINTENANCE.md](MAINTENANCE.md)를 참조한다.
