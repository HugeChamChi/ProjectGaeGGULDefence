# 강화 트리 차트 작업과 사용법

2026-11-14 갱신: BASIC 비용을 게임에 연결했다. 필수 비용 열은 CostItemId/기본 Gold, BaseCost/양의 정수, CostStep/0 이상의 정수이며 다음 비용은 BaseCost + CostStep × 현재 레벨이다. 최대 단계 비용은 int 범위여야 한다. 비활성 CHOICE/LANES의 공란·0 비용은 유지한다. 원본 PermanentUpgrade.xlsx에서 비용을 추출해 내장 JSON·트리 SO와 DEV/LIVE에 반영했다. 콘솔은 Excel 표시 문자열을 읽으므로 업로드 정수 서식은 쉼표 없는 `0`을 사용한다.

재화와 강화 레벨은 PlayerData의 동일 로컬 복구 저널/서버 스냅샷에 저장한다. 기존 PlayerResearchData는 최초1회 이관하고 이후 클릭마다 서버를 호출하지 않는다. 기존30초/60초 배치 저장과 출격·백그라운드 저장을 사용한다. 구버전 앱의 별도 강화 저장과 혼용하지 않는다. `outputs/research-cost-20261114/RESULT.md` 참조. 아래 10월의 상태표·기록은 당시 상태다.

2026-10-25. 기존 강화 트리를 차트 형식으로 연결했다. 서버 생성/업로드는 아직 하지 않았고 기본 데이터는 게임에 포함된 ResearchBalance.json이다.

## 작업 목록

| 작업 | 상태 |
|---|---|
| BASIC/CHOICE/LANES 추출 | 완료: 3개 트리, 61개 노드. 실제 게임 선택은 기존 BASIC 유지 |
| 단일 ResearchBalance 차트 계약 | 완료: 키, 배치, 선행 조건, 수치, 아이콘 주소 |
| Addressables 아이콘 | 완료: 스프라이트 12개, ResearchIcons 그룹 등록 |
| 기획 툴 IconAddress 필드 | 소스 완료. 기존 파일 호환 유지, 차트 배포에는 주소 필수 |
| XLSX/JSON 내보내기와 MCP | 완료: validate_research_balance / export_research_chart |
| 화면·전투 로드 연결 | 완료: 앱 수명의 동일 트리, 전투 시작 시 합계 복사 |
| 화면 전용 임시 수치 | 차트 트리는 실제 Stat/ValuePerLevel 표시. 원본 SO 시안 유지 |
| DEV/LIVE 경로 | 완료: DEV_ResearchBalance / LIVE_ResearchBalance. 서버 조회 미검증 |
| 뒤끝 차트 생성/업로드 | 미실행. 콘솔에서 수동 작업 필요 |
| 진행 서버 이관·재화 소비·새 효과 | 후순위. 현재 무료 강화와 기존 로컬 저장 유지 |

## 기획자 수정 흐름

현재 결과: `outputs/research-charts/research-initial-20261025/`

- ResearchBalance.xlsx: 단일 시트 뒤끝 업로드 파일. 직접 수정 가능.
- outgame-project.json: 기존 기획 프로젝트의 다른 표를 유지하고 강화 트리를 현재 SO·아이콘 주소로 갱신한 새 사본. 원본은 보존했다.
- ResearchBalance.json: 게임 기본 데이터와 같은 행 형식.
- manifest.json: DEV 대상 이름, 원본·엑셀 해시. 업로드 완료 증빙은 아니다.

기획 작업실에서 새 JSON 사본을 열고 영구 강화 트리를 수정한 뒤 프로젝트 JSON을 저장한다. 새 앱을 다시 실행해야 IconAddress 필드가 표시된다. 기존 저장 파일에는 주소가 없을 수 있으며, 배포할 때는 주소를 채워야 한다. IconKey는 툴 미리보기, IconAddress는 실제 게임 주소다. 서로 자동 동기화하지 않는다.

```powershell
node tools/backnd-mcp/research.mjs <수정한-프로젝트.json-또는-ResearchBalance.xlsx> research-review-002 DEV
```

MCP export_research_chart 인자:

```json
{"sourceFile":"outputs/research-charts/research-initial-20261025/outgame-project.json","releaseKey":"research-review-002","environment":"DEV"}
```

새 출력 폴더를 만들고 기존 ReleaseKey 폴더를 덮어쓰지 않는다. 엑셀은 수식 없는 단일 ResearchBalance 시트, 첫 행 영문 열 이름이다. NodeId는 문자열이다. 비율은 0.02=2%, StartFood는 식량 수량이다. Parents와 LaneNames는 JSON 문자열 배열이다. 트리 설정은 노드 행마다 반복되며 같은 TreeKey의 설정이 다르면 거부한다. 단일 차트 파일을 사용해 여러 표의 버전 혼합을 피한다. 비용은 Unity와 같은 범위로 검증한 뒤 세 비용 열에 포함한다.

TreeKey/NodeId/LegacySaveName은 저장 식별자다. 기존 키 변경은 별도 이관이 필요하다. MaxLevel을 낮춰도 저장 원값은 삭제하지 않으며 조회는 새 최대 레벨로 제한된다. 삭제한 노드의 저장 값도 기존 정책대로 보존한다.

## 게임 적용

`Assets/WorkSpace/USW/Resources/ResearchChartConfig.asset`의 UseRemote는 현재 false다. 이는 명시적인 포함 데이터 사용 모드이며 통신 실패 시 자동 대체하는 설정이 아니다.

기본 데이터 적용: Play를 멈추고 내보낸 JSON을 검증·반영한 뒤 앱을 재시작한다.

```powershell
unity command run_script --file tools/backnd-mcp/ResearchChartAuthoring.cs --entry ResearchChartAuthoring.ApplyBundled --args '["outputs/research-charts/research-review-002/ResearchBalance.json"]' --project-path D:/UnityGit/ProjectGaeGGULDefence
```

원격 적용: DEV_ResearchBalance 차트를 만들고 엑셀을 업로드하여 적용 파일로 선택한다. UseRemote를 true로 설정한 테스트 빌드에서 검증한다. 에디터/Development Build는 DEV, 일반 빌드는 LIVE를 읽는다. 검증한 **동일 파일**을 LIVE_ResearchBalance에 반영한다. 운영용 엑셀을 다시 생성할 필요는 없다.

원격 로드/검증/아이콘 실패 시 화면은 재시도를 표시하고 전투 강화 준비는 실패한다. 다른 환경이나 포함 데이터로 자동 대체하지 않는다. 성공한 정의는 앱 수명 동안 고정하고 재실행 때 새 버전을 읽는다. 진행 중인 전투는 기존 합계를 유지한다.

새 아이콘은 Addressables 등록과 카탈로그/번들 배포가 먼저 필요하다. 주소만 써도 아직 배포하지 않은 이미지가 생기지는 않는다. 이번 그룹은 로컬 빌드 경로다. APK/번들 빌드 검증은 후속이며 현재 확인은 Editor Play의 Addressables 경로다.

## 검증

- Unity 컴파일 성공. 원본 3개 트리/61개 노드와 스탯 합계 30개 일치, 수정한 차트 수치의 합계 반영 확인.
- Addressables로 불러온 스프라이트가 원본 12개와 일치. 반복 조회는 동일 트리 인스턴스.
- 중복/순환/선행 누락/미지원 스탯/NaN/혼합 버전/빈 아이콘 7개 오류 거부.
- 실제 ResearchScreen 21개 노드, 1회 강화·메모리 저장 1회, 화면과 전투 경로의 동일 저장 세션 확인. 실제 계정 강화 진행은 변경하지 않았다.
- Node 테스트 14개, 기획 툴 콘텐츠·워크북 회귀 검사 통과. XLSX 재읽기 비교 및 렌더 확인.

재검증은 Play 모드에서 ResearchChartChecks.Run / ResearchChartChecks.Screen을 실행한다. Edit 모드의 UniTask PlayerLoop에서는 아이콘 로드를 검사하지 않는다. 원격 서버·패키징된 번들·전체 전투 플레이는 이번 검증 범위 밖이다.
