# 플레이어 데이터 변경과 저장

원본: [PlayerDataController](../../Assets/WorkSpace/USW/Scripts/OutGame/PlayerDataController.cs), [BackendGameData](../../Assets/WorkSpace/USW/Scripts/OutGame/BackendGameData.cs), [Player](../../Assets/WorkSpace/HSD/Scripts/Data/Global/Player.cs).

PlayerDataController가 런타임 PlayerData를 소유하고 BackendGameData는 전달받은 데이터의 뒤끝 I/O를 수행한다. BackendGameData.userData라는 두 번째 공개 원본은 없다. 컨트롤러는 Player.Inject를 통해 저장 의존성을 받는다. 앱/로비 초기화 순서는 [데이터 아키텍처](server-client-data-architecture.md)에 있다.

골드/다이아/스태미나는 AddGold/SpendGold/AddDiamond/SpendDiamond/UseStamina 등 컨트롤러 API로 변경한다. SaveAndRefresh는 **Dirty 표시와 UI 갱신**을 수행하며 매 호출마다 즉시 서버 저장하는 메서드가 아니다. 실제 저장은 SaveAsync 또는 Player.UpdateDirtyDataAsync/RequestDebouncedSave 경계를 사용한다. UI는 OnUpdateUI와 현재 Data를 읽는다.

BackendGameData.GameDataGet은 콜백으로 PlayerData를 전달하고, GameDataUpdate는 저장할 PlayerData를 인자로 받는다. 스태미나 계산은 현재량·마지막 시각·최대량·현재 시각을 입력으로 받고 StaminaConfig를 사용한다. 종료/백그라운드 저장 요청은 성공 보장과 같지 않다.

**스태미나 시간은 서버 기준(2026-10-17)**: 자연 회복과 회복 타이머는 기기 시계가 아니라 `ServerClock`(뒤끝 서버 시간 + 실행 중 경과 시간)을 쓴다. 로드 직후와 앱 복귀(`ResumeAsync`) 때 다시 맞추고, 서버 시간을 모르는 동안에는 회복을 멈췄다가 30초마다 재시도한다. 그래서 휴대폰 시계를 앞으로 돌려도 회복되지 않는다. 테스트에서 시계 함수를 넘기면 그 시계를 신뢰한다. 실제 서버(에디터 testuser) 검사 22개: `outputs/stamina-20261017/server-checks.txt`(소비→회복 시작, 2주기 회복, 시간 미확인 시 정지, 복귀 재동기화, 다이아 충전, 출격 중복 요청 1회 차감, 다른 기기 저장 반영, 원래 값 복구). 남은 경계: 여러 기기 동시 저장의 서버 원자성은 없음, Android 실기기 미검증.

게임플레이의 CurrencyManager 식량과 ExpManager 레벨은 계정 재화/레벨과 별개다. 영구 강화의 IResearchSaveStore/PlayerPrefs 및 계정 전환 계획은 [강화 저장](outgame-upgrade-save-and-import.md)에 있다.

2026-10-18 계정 경험치: [계정 성장](account-progression.md)의 Excel 검증 Importer→설정 SO→보스별 최고 HP 감소 비율→승패 결과 지급을 연결했다. PlayerLevel/PlayerExp/MaxExp와 마지막 AccountExperienceReceipt·단조 AccountExperienceSequence·현재 예약 AccountExperienceRunId를 같은 PlayerData 스냅샷/계정별 저널에 보존한다. 재시도는 델타를 재적용하지 않고 이미 확정한 절대값을 저장한다. 구형 영수증 없는 행도 읽으며 Root는 로드 시 SO 곡선에 맞춰 기존 경험치/필요량을 정규화한다. 결과·로비 상단·프로필은 같은 값을 읽는다. 세부 수치와 종료 정책은 시험값이다. 격리 저장 복구와 실제 Editor 이벤트/표시 검증은 통과했으며 실제 서버 지급 E2E·Android는 미검증이다.

과거 개선 후보였던 PlayerDataView의 표시 구조와 TopPanel 초기 구독 동선은 해당 UI를 바꿀 때 실제 코드를 대조한다. 이 문서는 전면 저장 구조 변경이나 새 서버 검증의 구현 완료를 선언하지 않는다.
