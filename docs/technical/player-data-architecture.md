# 플레이어 데이터 변경과 저장

원본: [PlayerDataController](../../Assets/WorkSpace/USW/Scripts/OutGame/PlayerDataController.cs), [BackendGameData](../../Assets/WorkSpace/USW/Scripts/OutGame/BackendGameData.cs), [Player](../../Assets/WorkSpace/HSD/Scripts/Data/Global/Player.cs).

PlayerDataController가 런타임 PlayerData를 소유하고 BackendGameData는 전달받은 데이터의 뒤끝 I/O를 수행한다. BackendGameData.userData라는 두 번째 공개 원본은 없다. 컨트롤러는 Player.Inject를 통해 저장 의존성을 받는다. 앱/로비 초기화 순서는 [데이터 아키텍처](server-client-data-architecture.md)에 있다.

골드/다이아/스태미나는 AddGold/SpendGold/AddDiamond/SpendDiamond/UseStamina 등 컨트롤러 API로 변경한다. SaveAndRefresh는 **Dirty 표시와 UI 갱신**을 수행하며 매 호출마다 즉시 서버 저장하는 메서드가 아니다. 실제 저장은 SaveAsync 또는 Player.UpdateDirtyDataAsync/RequestDebouncedSave 경계를 사용한다. UI는 OnUpdateUI와 현재 Data를 읽는다.

BackendGameData.GameDataGet은 콜백으로 PlayerData를 전달하고, GameDataUpdate는 저장할 PlayerData를 인자로 받는다. 스태미나 계산은 현재량·마지막 시각·최대량을 입력으로 받고 StaminaConfig를 사용한다. 종료/백그라운드 저장 요청은 성공 보장과 같지 않다.

게임플레이의 CurrencyManager 식량과 ExpManager 레벨은 계정 재화/레벨과 별개다. 영구 강화의 IResearchSaveStore/PlayerPrefs 및 계정 전환 계획은 [강화 저장](outgame-upgrade-save-and-import.md)에 있다.

과거 개선 후보였던 PlayerDataView의 표시 구조와 TopPanel 초기 구독 동선은 해당 UI를 바꿀 때 실제 코드를 대조한다. 이 문서는 전면 저장 구조 변경이나 새 서버 검증의 구현 완료를 선언하지 않는다.
