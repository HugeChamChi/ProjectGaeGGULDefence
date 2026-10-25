# 뒤끝 이벤트 저장·Excel 연결 설계 v1

상태: 2026-10-24 코드와 공식 문서를 확인한 **구현 기준 설계**. 이 문서 작성으로 뒤끝 콘솔 테이블/차트가 생성되거나 툴의 출력이 바뀌지는 않는다.

## 1. 현재 코드와 변경 방향

- `BackendEndlessBalanceGateway`: GetChartListV2 → selectedChartFileId → GetChartContents → FlattenRows. 이벤트 차트도 같은 비동기 게이트웨이 패턴을 사용한다.
- `BackendGameData`: 기존 `PlayerData`의 재화와 AttendanceCount/AttendanceLastDay 등을 읽고 UpdateV2로 저장한다.
- `AttendanceService`: 기존 단일 출석을 PlayerData에 저장한다. 신규 다중 출석 이벤트 상태와 바로 섞지 않는다.
- `EventDataRuntime`: 설정에 따른 미션 진행 계산은 있으나 세션 메모리만 사용한다. 저장/복원과 수령 처리는 추가해야 한다.

**결정:** 기획용 다중 시트 Excel은 유지한다. 서버 업로드는 단일 시트 차트 파일로 변환한다. 유저 진행은 Excel이 아니라 게임/서버가 만드는 별도 Private 게임정보다. 재화는 기존 PlayerData를 유지한다.

## 2. 공통 차트와 기획 시트 매핑

| 기획 데이터 | 뒤끝 차트명 / 업로드 파일 | 내용 |
|---|---|---|
| DataBindings | EventDataBindings / EventDataBindings.csv | 실행 지점 → 데이터 키, 값 선택, 고정값, 적용 여부 |
| MissionEvents | EventMissionDefinitions / EventMissionDefinitions.csv | 미션 이벤트 키, 이름, 기간, 수령 마감, 활성 여부 |
| Missions | EventMissions / EventMissions.csv | 미션 키, 이벤트 키, 데이터 키, 목표, 누적 범위, 보상 |
| AttendanceEvents | EventAttendanceDefinitions / EventAttendanceDefinitions.csv | 출석 이벤트 키, 이름, 기간, 수령 마감, 일수, 반복, 참여 대상 |
| AttendanceRewards | EventAttendanceRewards / EventAttendanceRewards.csv | 이벤트 키, 일차, 보상 |
| 툴 자동 생성 | EventRelease / EventRelease.csv | 적용할 묶음 버전과 각 차트의 행 수·내용 해시 |

EventData / ExecutionHooks 시트는 개발자가 제공한 목록을 설명하는 참고 자료다. 서버에서 임의로 실행 지점이나 코드 데이터를 추가하는 용도로 업로드하지 않는다.

기획 작업은 기존 `MissionEvents.xlsx`와 `AttendanceEventExample.xlsx` 등의 통합 문서에서 한다. 배포 버튼은 **현재 작업 프로젝트의 전체 이벤트 설정**을 검사해 5개 데이터 파일과 마지막 적용용 EventRelease 파일을 한 폴더로 출력한다. 일부 이벤트만 내보내 전체 차트를 덮는 실수를 피한다. 편집용 Excel도 같이 보관한다.

## 3. 열과 형식

공통: 출력 파일마다 모든 행에 `ReleaseKey`를 붙인다. EventKey는 출석/미션을 통틀어 유일하게 한다. 재개최는 새 EventKey를 발급하고 이전 키는 재사용하지 않는다.

- DataBindings: 기존 BindingKey, HookKey, DataKey, ValueSource, ConstantValue, Enabled 유지.
- MissionDefinitions: 기존 EventKey, Name, StartAt, EndAt, Enabled에 **ClaimEndAt 추가**. 빈칸은 EndAt. 시작 포함, 종료 제외. 수령은 ClaimEndAt 미만.
- Missions: 기존 MissionKey, EventKey, Name, DataKey, Target, Period, Rewards 유지.
- AttendanceDefinitions: 현재 열 유지. 신규유저 지원을 위해 **Audience(All/NewUsers), RegistrationStartAt, RegistrationEndAt 추가**. NewUsers는 서버 계정 생성 시각이 명시한 가입 기간 안인 유저로 정의한다. `14일 출석`은 보상 일수이며 `가입 후 14일간 참여 가능`이라는 뜻이 아니다. 가입일 상대 일정은 이번 규약에서 지원하지 않는다.
- AttendanceRewards: 현재 EventKey, Day, Rewards 유지.
- EventRelease: ReleaseKey, SchemaVersion, CatalogVersion과 각 파일의 Rows, Hash를 별도 열로 둔 1행. 예: DataBindingsRows, DataBindingsHash. 파일 내용 해시는 CSV 파일 원문이 아닌 헤더 순서/키순 정렬/형식 정규화된 데이터로 계산해 업로드 시 표현 차이를 배제한다. 실제 정규화 함수는 exporter와 parser가 공유한다.

숫자는 문화권 독립 소수점, bool은 0/1, 날짜는 UTC 오프셋을 포함한 ISO 8601. 서버 저장 시각은 Unix UTC 초로 변환한다. 초기화 시간은 이벤트 열에 넣지 않고 공통 설정을 사용한다.

**보상 한 셀:** `Gold:500;Stamina:10`. 화면에서는 지금처럼 아이콘과 수량으로 설정한다. 파서는 이를 `[{itemKey:"Gold",amount:500},{itemKey:"Stamina",amount:10}]`로 바꾼다. 허용 아이템·양의 정수·중복 아이템을 검사한다. DB에 이 문자열을 유저마다 복제하지 않는다.

CSV는 UTF-8, 정상적인 따옴표 이스케이프를 사용한다. id 예약 열은 사용하지 않고 key 이름을 유지한다. 개발용 JSON은 파싱 결과 미리보기이며 그대로 차트에 업로드하는 파일이 아니다.

## 4. 배포와 읽기

1. 기획자가 툴에서 전체 이벤트 설정을 검사하고 **뒤끝 배포용 내보내기**를 누른다(추가 구현할 버튼).
2. 운영자가 5개 차트 파일을 업로드·적용한다. 행이 0개인 데이터는 manifest의 Rows=0으로 표현하고 로더는 빈 배열을 사용한다.
3. **EventRelease를 마지막에 적용**한다.
4. 게임의 BackendEventConfigGateway가 EventRelease를 읽고 선언된 차트들을 읽는다. 모든 ReleaseKey/행 수/해시/CatalogVersion/참조를 검사한 뒤 하나의 불변 설정으로 교체한다.
5. 업로드 도중 신·구 파일이 섞이면 전체 적용을 거절한다. 검증된 이전 설정은 유지하고 재시도한다. 첫 로드에 실패했으면 이벤트만 준비 중으로 표시한다. 만료된 이전 이벤트를 유효한 것으로 연장하지 않는다.
6. 일관성 확인을 위해 읽기 끝에 EventRelease를 다시 확인한다. 이벤트 데이터 캐시는 릴리스 단위로 관리한다. 기존 Chart의 무기한 이름 캐시만으로 처리하지 않는다.

이미 진행된 이벤트의 DataKey/목표/보상/반복·자격 기준은 같은 EventKey에서 변경하지 않는다. 변경이 필요하면 명시적 이관 또는 새 EventKey를 사용한다. 전투 시작에 설정 릴리스/시각/모드/실행 키를 고정하고, 종료 요청은 그 기록으로 처리한다. 운영 로더 구현 후에는 기본 Resources 예시로 조용히 되돌아가 지급하지 않는다.

## 5. 유저 저장: EventUserState

뒤끝 게임정보 Private 테이블 **EventUserState**, 계정당 대표 행 1개로 시작한다. 실제 행 주소는 서버 inDate/owner 정보이며 앱의 EventKey가 DB 고유 제약을 자동으로 만들어 주지는 않는다. UID를 클라이언트 요청에서 신뢰하지 않고 인증된 계정을 사용한다.

| 열 | 타입/용도 |
|---|---|
| SchemaVersion | 정수, 진행 데이터 마이그레이션 버전 |
| Revision | 정수, 저장 충돌 감지용 증가값 |
| StateJson | 문자열 JSON, 활성/수령 가능 이벤트 진행 묶음 |
| ReceiptsJson | 문자열 JSON, 접수 가능한 기간의 처리 완료 실행 키 |

StateJson 예시(설명용):

```json
{
  "events": [
    {
      "eventKey": "play_3week_sample",
      "missions": [
        {"missionKey":"daily_normal_1","periodKey":"2026-11-01","progress":1,"claimedAt":0},
        {"missionKey":"total_all_30","periodKey":"Event","progress":7,"claimedAt":0}
      ]
    },
    {
      "eventKey":"attendance_newuser_14day_sample",
      "attendance":{"cycle":0,"attendedDays":2,"lastDayKey":"2026-11-02","claimedDays":[1]}
    }
  ]
}
```

- 미션 식별: EventKey + MissionKey + PeriodKey. Daily는 공통 초기화가 적용된 날짜, 누적은 Event.
- 미션은 목표까지 저장하고 완료 여부는 `progress >= target`으로 계산한다. Claimed와 ClaimedAt을 따로 중복 저장하지 않는다.
- 출석은 누적 출석 인정과 보상 수령을 분리한다. Cycle 회차별 claimedDays를 유지해야 하므로 반복 이벤트는 위 attendance 객체를 회차 목록으로 저장한다. 과거 회차 수령 허용 범위는 이벤트 수령 기한까지다.
- 출석 수령 식별: EventKey + cycle + Day. 출석 인정은 EventKey + 서버 날짜로 중복 방지한다.
- 미션 일일 데이터는 자정/초기화 때 전체 유저 DB를 일괄 수정하지 않는다. PeriodKey를 바꿔 새 진행으로 계산한다.
- 일일 미션 수령 마감은 **그 일자의 다음 공통 초기화와 ClaimEndAt 중 이른 시각**으로 한다. 지난 날짜의 미수령 보상을 다음 날 받는 기능은 포함하지 않는다.
- 종료/수령 만료 데이터는 보관 정책에 따라 정리한다. 저장 문자열 크기를 측정하고 제한 전에 정리한다. 영구히 전체 이벤트와 실행 키를 한 행에 누적하지 않는다.

ReceiptsJson은 전투 실행 키, 처리 시각, 적용 릴리스, 처리 결과를 보관한다. 권장 초기 정책은 전투 종료 후 24시간 내 재전송 허용이며, 서버가 종료 시각을 확인할 수 있는 출격 기록이 전제다. 그보다 오래된 요청은 거절한 뒤 영수증을 정리해야 한다. **기록만 잘라내고 오래된 요청을 계속 허용하면 중복 집계가 가능하다.** 24시간은 제안 정책이며 실제 오프라인 지원 범위를 확정한 후 구현한다.

## 6. 진행 처리와 수령

서버 어댑터/펑션의 논리 작업을 `GetEventState`, `SubmitBattleResult`, `ClaimEventReward`로 나눈다.

### 전투 결과

1. 인증 계정의 출격/완료 기록을 확인한다. 클라이언트가 임의로 보낸 DataKey·증가량을 그대로 신뢰하지 않는다.
2. OccurrenceKey가 이미 처리됐으면 기존 처리 결과를 반환한다.
3. 서버 설정의 실행 지점 연결로 갱신값을 만들고, 대상 이벤트의 기간·참여 자격을 검사한다.
4. 영향을 받는 미션과 실행 영수증을 하나의 EventUserState 저장으로 확정한다. 같은 데이터에 연결된 여러 미션이 있어도 같은 실행은 한 번이다.
5. 저장 성공 응답 후 클라이언트의 확정 진행도를 교체한다. 타임아웃은 동일 키로 재시도하고 다시 +1을 보내지 않는다.

서버 시각만 확인한다고 전투 플레이가 검증되는 것은 아니다. 현재 출격 예약 키는 중복 구분의 기반이며, 서버가 신뢰할 출격·종료 기록과 결과 검증 어댑터는 추가 구현 사항이다.

### 보상 수령

1. 서버에서 해당 이벤트·대상 미션/출석·수령 기한·진행도·미수령 여부를 검사한다.
2. 보상은 클라이언트 수량이 아니라 공통 차트에서 읽는다.
3. EventUserState의 수령 기록과 기존 PlayerData의 재화 변경을 **같은 트랜잭션**으로 저장한다. 재화별로 같은 PlayerData 행을 여러 번 갱신하지 않고 합산해 1작업으로 만든다.
4. 수령 키는 `EventKey/MissionKey/PeriodKey` 또는 `EventKey/cycle/Day`. 재요청이면 이미 수령한 결과를 반환한다.

**동시성 요구:** 트랜잭션의 전부 성공/실패만으로 동시 두 요청의 중복 지급이 해결되는 것은 아니다. EventUserState.Revision의 조건부 갱신 또는 서버의 계정별 직렬화가 필요하다. Where 기반 조건 갱신 후보는 공식 API에 있으나 실제 사용 SDK/펑션에서 revision 불일치가 전체 트랜잭션 실패가 되는지 통합 테스트로 확인해야 한다. 조건을 보장할 수 없는 단순 UpdateV2로 대체하지 않는다.

**기존 재화 저장도 함께 수정:** PlayerDataController가 이전 Gold/Diamond/Stamina 값을 통째로 저장하면 서버 지급을 덮어쓸 수 있다. 수령 후 최신 스냅샷 반영과 저장 큐 조정만으로 다른 기기까지 보호되는 것은 아니다. 모든 재화 변경 경로에 같은 동시성 정책(버전 조건 또는 서버 원자 연산)을 적용해야 한다. 해당 경로를 통합하기 전에는 운영 지급을 켜지 않는다.

대표 행의 최초 생성도 동시 로그인에 중복 생성되지 않도록 서버에서 생성/대표 행 선정을 직렬화한다. private 테이블은 다른 유저 읽기를 제한하는 설정이며 자기 클라이언트의 쓰기 조작까지 막는 의미는 아니다. 운영 수령·진행 확정은 서버 경로로 제한한다.

## 7. 연결할 코드와 순서

1. 툴: ClaimEndAt/신규 유저 가입 기간 추가, 배포용 CSV 분할 및 release 생성·검증 구현.
2. `BackendEventConfigGateway`: 기존 차트 API 방식으로 묶음 읽기 → 현재 EventDataConfig 및 출석 DTO 조립.
3. `EventDataRuntime`: Snapshot/Restore 추가. 설정 재조회가 유저 진행도를 초기화하지 않도록 상태와 설정을 분리.
4. `EventProgressService` + `IEventProgressStore`/뒤끝 구현: Player의 초기화·저장·계정 정리 생명주기에 등록. 기존 단순 디바운스 저장으로 수령을 처리하지 않는다.
5. 서버 처리 및 기존 PlayerData 재화 저장 경로 통합. 수령 원자성·동시성 검증.
6. 기존 AttendanceService를 이벤트별 진행으로 전환. 이전 AttendanceCount/AttendanceLastDay는 지정한 기존 7일 이벤트에만 1회 이관하고, 신규 14일 이벤트는 새 진행으로 시작. 기존 지급 경로와 새 지급 경로가 동시에 활성화되지 않게 한다.

## 8. 완료 검증 기준

- Excel → 서버용 CSV → 차트 응답 → DTO 왕복에서 key/수치/다중 보상 보존.
- 다른 ReleaseKey가 섞이면 전체 적용 거절. 배포 중 첫 로그인도 불완전 설정을 사용하지 않음.
- 앱 종료/재접속 후 진행·수령·중복 영수증 유지.
- 같은 전투 재전송, 같은 수령 두 기기 동시 요청, 성공 직후 응답 유실에서 1회 처리.
- 기존 재화 자동 저장과 이벤트 보상 수령이 겹쳐도 재화 유실/이중 지급 없음.
- 공통 초기화 직전/직후, 이벤트 종료/수령 마감, 신규 유저 가입 경계 검증.
- 계정 전환, 설정 재로드, 구버전 클라이언트, 기존 출석 이관 검증.

## 확인한 공식 자료

- [차트 관리](https://docs.backnd.com/guide/console-guide/backnd-base/chart/): 공통 조회 데이터, 단일 시트 파일, 적용 절차.
- [GetChartContents](https://docs.backnd.com/sdk-docs/backend/5.14.1/base/chart/search-chart/get-chart-contents/): 적용한 차트 데이터 조회. 프로젝트도 이 API를 사용한다.
- [TransactionWriteV2](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/write-v2/): 최대 10작업, 전체 성공/실패, 같은 행 중복 작업 제한.
- [SetUpdate / SetUpdateV2](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/crud/update/): Where 또는 행 주소로 갱신하는 인터페이스. 조건부 동시성의 실서버 검증을 대신하지 않는다.

이 설계는 현재 사용 중인 `Backend.GameData` API 기준이다. 별도 제품인 새 데이터베이스/PlayerData API로 전환하는 설계가 아니다.
