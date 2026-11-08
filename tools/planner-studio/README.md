# GGD 기획 작업실

2026-11-05 코드 기준. 편집·파일 생성, 서버 파일 일치, 실제 플레이 검증은 서로 다른 상태다. 기능별 현재 범위는 [적용 점검](APPLICATION-AUDIT.md), 사용 순서는 [팀 안내](TEAM-GUIDE.md), 남은 일은 [NEXT-STEPS.md](NEXT-STEPS.md)를 따른다.

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
