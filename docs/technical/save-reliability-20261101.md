# 뒤끝 저장 검증 — 2026-11-01

후속 작업에서 영구 강화도 서버에 연결했다. 아래 로컬 전용 강화 설명은 이 검증 당시 상태이며, 현재 상태는 [강화 서버 연결 결과](upgrade-server-save-result.md)를 따른다.

## 적용한 구조

- 일반 변경은 계정별 PlayerPrefs 저널에 즉시 기록한다. 서버 저장은 30초 디바운스, 연속 변경은 60초 안에 flush하며, 60초 주기로 dirty 매니저를 다시 확인한다. 출격·구매·앱 일시정지·계정 변경의 명시적 저장은 유지한다.
- PlayerData 쓰기는 SDK 콜백까지 한 요청만 유지한다. 호출자의 15초 대기 만료는 실제 요청 취소가 아니므로 잠금을 풀지 않는다. 실패 재시도는 30/60/120/240/300초로 증가한다. 변경 없는 계정에는 쓰기를 보내지 않는다.
- 저널에 마지막 확정 버전과 전송한 버전들을 함께 기록한다. 서버에는 A가 반영됐지만 ACK 전에 로컬이 B로 변하고 앱이 종료된 경우 B를 복구한다. 모르는 서버 버전이면 자동 덮어쓰기를 막고 저널을 보존한다.
- PlayerData는 마지막으로 확인한 PlayerSaveId를 Where 조건으로 갱신한다. 404일 때만 추가 조회하여 이미 적용된 동일 요청, 알려진 미확인 요청, 다른 기기의 변경을 구분한다. PlayerSaveId가 없는 구형 행은 최초 한 번 UpdateV2로 컬럼을 추가한다.
- 상점의 가격·보상·당일 구매 영수증, 가챠의 가격·전체 결과를 하나의 PlayerData 스냅샷으로 저장한다. 가챠 결과를 받기 전에는 차감하지 않는다. 구현되지 않은 Item 보상은 결제 전에 거절한다. 일일 상점 선택 목록에는 독립 DailyPeriodKey를 저장한다.
- 새 캐릭터 획득, 족장/프로필 선택, 튜토리얼 완료는 같은 PlayerData에 기록한다. 기존 캐릭터 수량과 프로필 소유권 행은 읽어서 보존한다. 기존 선택은 새 선택 필드가 -1일 때 계속 사용한다. 튜토리얼의 저장 없는 성공 로그를 제거했다.
- DailyData/WeeklyData는 기존 테이블·행을 유지한다. 동일 기간의 깨끗한 로그인은 저장하지 않는다. 로컬 저널, 실제 콜백까지의 단일 쓰기, 실패 후 재조회, 이전 PeriodKey/PayloadJson 조건의 갱신을 사용한다. 계정 전환의 미저장 확인 대상에도 포함했다.
- 손상된 기존 재화 숫자를 0으로 해석하여 저장하지 않는다. 빈/손상된 기간 payload도 오류로 처리한다.

## 검증 결과

Unity 6000.3.11f1, 설치된 BACKND SDK와 실제 프로젝트 코드를 사용했다.

| 검사 | 결과 |
|---|---|
| Unity Pipeline recompile | 성공, 컴파일 오류 0 |
| SaveReliabilityChecks.Run | 27 PASS: clean no-op, 즉시 저널, ACK 유실+새 변경+재시작, 구 세션 콜백, 실제 15초 timeout, timeScale=0, 단일 요청 유지, 실패 backoff, 구매 원자성/중복, 가챠 부분 결과 거절, 계정 격리, 충돌 보존 |
| SaveReliabilityChecks.Periods | 8 PASS: 기존 기간 no-op, 저널 복구, 이미 반영된 snapshot 처리, 서버 충돌 보존, 일/주 KST05 초기화 경계 |
| SaveReliabilityChecks.Formats | 9 PASS: 구형 로컬 데이터의 새 필드 기본값, 잘못된/음수/overflow/null 재화, 빈/null 기간 payload 거절 |
| SaveReliabilityLiveChecks.Run | 실제 로그인된 계정의 저장·재조회 PASS. 새 PlayerData 컬럼 6개, Gold/Diamond 유지, 구 버전 조건 404, 최종 버전 유지, DailyData/WeeklyData 조건부 저장 후 카운터 유지 및 각각 1행 확인 |
| git diff --check | 통과 |

격리 검사는 고유한 테스트 저널 키만 만들고 정리했다. 실제 서버 검사에서는 보상 지급이나 재화 지출을 수행하지 않았으며, 현재 snapshot의 버전과 동일한 기간 payload를 저장했다. 첫 live 호출은 로그인 준비 전이라 쓰기 없이 거절됐고, 초기화가 완료된 뒤 통과했다.

검사 진입점:

```text
unity command run_script --file Tools/verification/SaveReliabilityChecks.cs --entry SaveReliabilityChecks.Run --timeout_ms 60000
unity command run_script --file Tools/verification/SaveReliabilityChecks.cs --entry SaveReliabilityChecks.Periods
unity command run_script --file Tools/verification/SaveReliabilityChecks.cs --entry SaveReliabilityChecks.Formats
unity command run_script --file Tools/verification/SaveReliabilityLiveChecks.cs --entry SaveReliabilityLiveChecks.Run --timeout_ms 60000
```

Periods는 Edit Mode에서 실행하며 임시 clock 상태를 복원한다. Live는 기존 로그인/로드 완료 세션만 사용한다.

## 호출 비용에 미치는 영향

- 동일 기간 기존 일일·주간 행: 로그인당 강제 쓰기 2회 제거.
- 상점 구매: 별도 가격/보상/구매행 저장을 PlayerData 쓰기 1회로 통합. 가챠도 차감/캐릭터 행을 1회로 통합한다. 확률 조회는 별도다.
- 튜토리얼·선택 변경·새 캐릭터 지급은 공통 dirty 저장에 합쳐진다.
- 서버에 보낼 변화가 없으면 60초 검사도 DB 호출을 하지 않는다.
- 이는 DB 요청 구조의 감소다. 실제 청구액은 사용자 행동, 데이터 크기, 서비스 요금제에 따라 달라지며 금액을 측정한 결과가 아니다. 충돌/불확실한 저장 복구에는 추가 읽기가 필요하다.

## 보장하지 않는 범위와 운영 주의

1. **서버에 아직 전달되지 않은 저널까지 앱 삭제/기기 분실/저장장치 고장으로 지워지면 복구할 수 없다.** 중요 구매는 즉시 서버 확인을 시도하지만 오프라인 변경은 같은 기기의 재접속 복구 범위다.
2. **영구 강화는 현재 RootLifetimeScope가 PlayerPrefsResearchSaveStore를 사용한다.** 덱 강화 시안 등 로컬 전용 저장을 이번 뒤끝 저장 보강이 서버 백업으로 전환한 것은 아니다. 기기 변경 복구가 필요하면 별도 계정 저장 연결이 필요하다.
3. 두 기기의 완전히 동시인 요청, 구버전 클라이언트의 무조건 UpdateV2, 첫 행 생성/구형 컬럼 최초 추가의 경합은 실험하지 않았다. BACKND 문서는 Where 조건에 맞는 행 수정 동작을 설명하지만 DB 차원의 compare-and-swap 원자성을 여기서 입증한 것은 아니다. 실제 검증은 **이미 오래된 버전이 거절되는 것**까지다. 운영에서 구/신 버전을 동시에 쓰게 하면 기존 클라이언트가 재화나 선택을 다르게 저장할 수 있다.
4. SDK 콜백 자체가 영원히 오지 않으면 미확인 쓰기 잠금을 유지한다. 안전하게 새 요청을 겹치지 않으며 재접속/재시작 시 서버와 저널을 대조한다.
5. 기간 모델 변경은 `Mutate(data => ...)` 또는 수정 직후 `IsDirty = true`를 사용한다. `Data`의 public 필드만 바꾸고 저널 표시를 누락하면 다음 dirty 검사 전 강제 종료 구간은 보호되지 않는다. 기존 mutable API와 새 필드 기본값 계약을 유지했다.
6. DynamicShopDataManager의 오래된 별도 저장은 이번 구매 경로 통합 대상이 아니다. 소스에서 활성화/완료 호출은 Test_DynamicShop에서 확인했으며 운영 연결 시 실패 응답·복구 계약의 별도 검증이 필요하다.

공식 동작 근거: [Where 조건](https://docs.backnd.com/sdk-docs/backend/base/game-information/clause-where/basic/), [Update](https://docs.backnd.com/sdk-docs/backend/base/game-information/update/using-query/), [UpdateV2](https://docs.backnd.com/sdk-docs/backend/base/game-information/update/using-indate/).
