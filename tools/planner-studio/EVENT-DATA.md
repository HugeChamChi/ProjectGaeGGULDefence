# 실행 지점 → 데이터 → 미션

## 기획자 작업 순서

1. **이벤트 → 데이터 연결**에서 실행 지점을 선택합니다.
2. **데이터 연결**을 누르고 제공 데이터, 사용할 값, 적용 여부를 선택합니다. 고정값은 수량을 입력합니다.
3. **미션 이벤트 → 이벤트 설정**에서 기간과 활성 여부를 설정합니다.
4. **미션·보상**에서 이벤트, 진행 데이터, 목표, 누적 범위, 아이템 보상을 선택합니다.
5. **Excel 내보내기**로 `MissionEvents.xlsx`, `MissionEvents.data.json`, `MissionEvents.schema.json`을 함께 저장합니다.
6. 자료 목록에서 Excel을 가져오면 다시 편집할 수 있습니다.

기본 자료 `MissionEvents.xlsx`는 2026-11-01 05:00부터 11-22 05:00까지의 **비활성 3주 예시**입니다. 매일 일반 모드 1판(골드 500), 누적 전체 모드 30판(다이아 100)이 들어 있습니다. 현재 미션 이벤트는 전체 유저 대상입니다.

## Excel 시트

| 시트 | 역할 |
|---|---|
| DataBindings | BindingKey, HookKey, DataKey, ValueSource, ConstantValue, Enabled |
| MissionEvents | EventKey, Name, StartAt, EndAt, Enabled |
| Missions | MissionKey, EventKey, Name, DataKey, Target, Period, Rewards |
| EventData | 코드에서 제공하는 데이터 키·이름·Add/Max·단위 (참고 전용) |
| ExecutionHooks | 코드에서 제공하는 실행 지점·사용 가능한 값 (참고 전용) |

동일 실행 지점에 활성 DataKey를 중복 연결할 수 없습니다. 데이터와 실행 지점 목록은 코드가 원본이며 Excel에서 새로운 코드를 만들어낼 수 없습니다. 제공 목록을 변조한 Excel은 거절합니다. 실행 지점/데이터/미션은 key로 연결되며 SO 경로·메모는 출력하지 않습니다.

## 개발자 제공 목록

원본: `Assets/WorkSpace/USW/Scripts/Events/EventDataCatalog.cs`.

- `BattleEnd_Normal`, `BattleEnd_Hard`: 해당 모드의 정상 승리/패배 종료. 튜토리얼, 에디터 미리보기, 오류, 중도 이탈 제외.
- 값: Constant(설정 고정값), PlaySeconds(게임에서 측정한 생존 시간), ReachedRound(마지막 도달 라운드, 클리어 라운드와 다름).
- 데이터: 일반/어려움/전체 전투 완료 횟수, 일반 플레이 시간, 일반 최고 도달 라운드.
- Add는 합산, Max는 최댓값. 의미와 단위는 코드에서 결정합니다.

`EventDataCatalog`는 수정 가능한 전역 배열 대신 읽기 전용 인스턴스 `Default`를 제공합니다. 키·실행 지점·집계 연산·입력 값 종류는 enum이며, 미션 JSON의 문자열 키는 로드 경계에서 정확히 검증한 후 내부 타입으로 변환합니다. 런타임 진행은 계정별 `EventDataRuntime`이 별도로 관리합니다.

`python Tools/planner-studio/scan-event-catalog.py`는 C# 소스를 정규식으로 읽지 않습니다. `catalog-export/CatalogExport.csproj`가 **게임과 동일한 카탈로그 소스**를 컴파일·실행해 JSON을 내보내고, 스크립트는 `app/ui/content/event-catalog.json` 및 브라우저용 `event-catalog.js`를 생성합니다. 툴 실행에는 .NET이 필요 없지만 개발자의 목록 생성/패키징에는 .NET 9 SDK가 필요합니다. 게임은 동일한 컴파일 정의를 사용하며 이 툴용 JSON을 원격 설정으로 로드하지 않습니다.

카탈로그 버전은 C#의 `EventDataCatalog.Version` 한 곳에서 정의하고 런타임 검증과 툴 출력이 공유합니다. 기존 키와 의미를 유지해 현재 버전은 1이며 기존 미션 Excel/JSON 형식도 호환됩니다. 지원 목록의 호환성을 바꾸면 버전을 올려 다시 생성·배포합니다. `python Tools/planner-studio/scan-event-catalog.py --check`로 생성 파일이 실제 정의와 같은지 검사할 수 있습니다. 생성 실패 시 기존 출력은 교체하지 않습니다.

이 생성 과정은 `package.py`에도 포함되어 있습니다. 새 데이터는 카탈로그에, 새 실행 지점은 카탈로그와 실제 게임 분기에 추가한 후 툴을 전달합니다. 이미 제공하는 지점의 데이터 연결과 이벤트 추가는 설정으로 처리합니다.

## 게임 연결과 적용

`GameManager`가 출격 예약 키를 실행 키로 보관하고, 정상 종료 결과를 확정한 뒤 `EventDataBridge.BattleCompleted`에 전달합니다. 동일 계정 소유의 실제 전투만 전달합니다.

기본 설정은 `Assets/WorkSpace/USW/Resources/EventDataConfig.json`에서 읽습니다. 이 파일은 비활성 예시입니다. 개발자가 내보낸 `MissionEvents.data.json`을 해당 파일에 적용하면 다음 설정 로드 시 파싱됩니다. **기획 툴에서 내보내기만 해서는 게임 파일을 덮어쓰지 않습니다.** Resources 변경을 배포하려면 게임 빌드가 필요합니다.

원격 설정 어댑터는 내려받은 JSON을 `EventDataBridge.Configure(accountKey, json, commonResetHourKst, serverClock)`에 전달하면 됩니다. 현재 뒤끝에서 이 JSON을 다운로드하는 어댑터는 구현되어 있지 않습니다. Configure는 새 세션 상태를 만드므로 진행 중인 계정에 반복 호출하여 진행도를 초기화하지 않도록 해야 합니다.

런타임은 `EventDataRuntime`입니다. 이벤트별 미션 진행은 시작 포함·종료 제외의 서버 시각을 사용합니다. Daily는 `DailyManager.ResetHourKst` 공통 기준으로 날짜가 나뉘며 이벤트에 초기화 시간 필드가 없습니다. Event는 해당 이벤트 기간 동안 누적합니다. 이전 계정의 실행은 거절하고 같은 실행 키는 한 번만 적용합니다.

## 현재 구현 범위

완료: 코드 카탈로그 → 툴 목록 생성 → 실행 지점별 연결 편집 → Excel/JSON 파싱 → 실제 전투 종료 전달 → 세션 내 미션 진행 계산.

미연결: 뒤끝 원격 설정 다운로드, 유저 진행도/실행 영수증 저장·재접속 복원, 서버의 전투 결과 검증, 수령 상태와 실제 보상 지급, 게임 내 미션 화면. 따라서 현재 미션은 **개발·기획 검증용**이며 운영용 지급 시스템이 아닙니다. 메모리의 중복 방지는 프로세스 종료 후 유지되지 않습니다. 기존 출석 서비스는 별도 경로를 유지합니다.

## 검증

- `node Tools/planner-studio/check-missions.cjs`: 시트 왕복, 구버전 프로젝트 이관, 미정의 키/값·중복 연결·잘못된 기간 거절, C# 입력 JSON 생성.
- `dotnet run --project Tools/planner-studio/tests/runtime/RuntimeChecks.csproj -- Tools/planner-studio/tests/runtime/config.json`: 실제 내보낸 JSON을 C#에서 읽어 모드 분기, 중복, 기간 경계, 일일 초기화, 계정 구분, 비활성, Max 계산 검증.
- `node Tools/planner-studio/check-ui.cjs`: 실제 설치본에서 실행 지점 선택/수정, 미션·보상 아이콘, Excel/JSON 내보내기와 기존 편집 기능 확인.
- Unity Pipeline에 연결할 수 없어 에디터 플레이 검증은 실행하지 않았습니다. Unity 6000.3.11f1의 C# 컴파일러와 프로젝트 참조로 현재 게임 소스 컴파일은 통과했습니다.
