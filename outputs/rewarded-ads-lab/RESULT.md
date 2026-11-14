# 보상형 광고 격리 설계 검증

- 브랜치: `feat/rewarded-ads-lab`, 기준 로컬 Develop `2a671b20`.
- 운영 씬·재화·상점·SDK·서버를 연결하지 않고 인터페이스와 가짜 어댑터/지급 원장을 추가했다.
- 계약 검사 34개 PASS. 실패/미시청 미차감, 스태미나 일1회, 일일보상 공통3회, 런 중복, 스냅샷, 요청/광고 거래 중복, 대기 차단, 원자 커밋 실패/재시도, 메모리 재시작, 계정 격리, 고정 구매 플래그 보존, KST05 일/주 경계를 확인했다.
- 별도 씬 Play PASS. 실제 테스트 컴포넌트의 요청→대기→지급→중복 차단→메모리 재시작을 검사했다. 로드 씬이 RewardedAdsLab 하나뿐임을 확인했다.
- `play-ui.png`: GameView 전체 캡처로 버튼·잔액·상태 표시를 확인했다. Pipeline의 카메라 캡처 `play.png`는 IMGUI를 포함하지 않으므로 UI 검증에는 사용하지 않았다.
- MCP recompile 호출은 응답 대기 시간이 초과됐다. 이후 CLI editor_status에서 compiling=false를 확인하고 새 스크립트 형식을 사용하는 계약/Play 검사를 성공시켰다. Unity run_script 컴파일 진단 없음. 기존 Editor.log의 LizardBossPresentation 종료 관련 NullReferenceException은 광고 실험실 수정 범위에서 제외했다. 프로젝트 전체 오류0을 주장하지 않는다.
- 기존 미저장 씬을 보존하기 위해 테스트 씬은 additive로 생성·저장 후 닫고, Play 시작 씬만 일시 지정했다. 검사 종료 후 시작 씬 설정을 복원한다. 기존 LobbyScene/IngameScene/빌드 씬 목록은 Git 차이 없음.

## 재실행

```powershell
unity command run_script --file Tools/verification/RewardedAdsLabChecks.cs --entry RewardedAdsLabChecks.Run
# 수동 검사: RewardedAdsLab.unity를 열고 Play. 또는 아래 저작 명령으로 기존 씬을 보존하며 테스트 시작 씬 지정.
unity command run_script --file Tools/verification/RewardedAdsLabAuthoring.cs --entry RewardedAdsLabAuthoring.Create
unity command editor_play
unity command run_script --file Tools/verification/RewardedAdsLabChecks.cs --entry RewardedAdsLabChecks.Play
unity command editor_stop
unity command run_script --file Tools/verification/RewardedAdsLabAuthoring.cs --entry RewardedAdsLabAuthoring.RestoreStartScene
```

## 참조 파일과 역할 (harness describe 대조)

| 파일 | 참조 방식·역할 |
|---|---|
| `design/gdd/rewarded-ads-architecture.md` | 요청 설계, 다섯 배치 정책·운영 연결 계약 |
| `Assets/WorkSpace/USW/RewardedAdsLab/RewardedAdContracts.cs` | 신규 계약·서비스, 카드 미등록 |
| `Assets/WorkSpace/USW/RewardedAdsLab/SimulatedAdGateway.cs` | 신규 가짜 지급 원장·광고 어댑터, 카드 미등록 |
| `Assets/WorkSpace/USW/RewardedAdsLab/RewardedAdsLabScreen.cs` | 신규 테스트 UI, 카드 미등록 |
| `Assets/WorkSpace/USW/RewardedAdsLab/RewardedAdsLab.unity` | 독립 씬·Unity API 저작 |
| `Tools/verification/RewardedAdsLabAuthoring.cs` | 씬 저작 및 Play 시작 씬 복원 |
| `Tools/verification/RewardedAdsLabChecks.cs` | 계약 및 Play 검증 |
| `Assets/WorkSpace/USW/Scripts/PeriodData/PeriodModels.cs` | 기존 KST05 기간 키 직접 재사용 |
| `Assets/WorkSpace/HSD/Scripts/Data/Stamina/StaminaConfig.cs` | 실제 충전/출격 설정 확인만 |
| `Assets/WorkSpace/HSD/Scripts/Shop/DailyShopManager.cs` | 기간·구매 저장 연결 지점 확인만 |
| `Assets/WorkSpace/USW/Scripts/OutGame/PlayerDataController.cs` | 전투 정산 API 확인만 |

실제 광고·SSV·모바일·실서버 영속성·동시 기기·상품 재추첨은 미검증이다. 보상 수량은 테스트 값이다. 운영 연결 전 설계 문서 8장의 미확정 항목을 처리해야 한다.
