---
system: balance-simulator
title: 독립 Windows 밸런스 시뮬레이터
keywords: 시뮬레이터, 밸런스 시뮬레이션, balance simulator, 반복 계산, Windows EXE, 시드 비교
verified: 2026-10-21
commit: 2e76748e
---
# 독립 Windows 밸런스 시뮬레이터

NORMAL/알팡 드론/영구 강화0의 Windows 오프라인 수치 모델. 0.3.2는 SnapshotRemoteContract 게임/Excel24열 캡처를 유지하고 설명 가능한 균형형 봇과 결과별 선택/4행6열 배치 이력을 제공한다. 0.3.2는 토템14종/드론 유닛5종 이미지를 내장하고 기본 창1360×980/로그 영역 조절을 지원한다. 라이브 서버 조회/운영 적용 없음. 어려움은 악어만, 쉬움8종. 배치 토템5개·보관함5칸 별도. 실행/중지·실제 마지막 전투 라운드/HP진행·요약·CSV/JSON·같은 시드 A/B를 지원한다. 과거 캡처/증거는 보존한다.

2026-10-19 소스 후속: 미획득9108 프리뷰 롤의 델탕 생산 누수를 수정했다. **배포0.3.2 EXE는 미반영**이다. 회귀/정책1176검사 PASS, 전체 검사는 과거 Unity 참조 CSV 누락으로 미완료. [수정과 경계](../../../outputs/simulator-roll-fix-20261019/RESULT.md).

## 진입 파일

| 파일 | 역할 |
|---|---|
| `Tools/BalanceSimulator/README.md` | 사용·빌드·지원 범위·근사 한계 유지 원본 |
| `Tools/BalanceSimulator/App/MainForm.cs` | Windows Forms GUI·작업 스레드·중지·결과·요약·내보내기 |
| `Tools/BalanceSimulator/App/ScenarioPanel.cs` | 최신 유일 프로필 및 후보 수치 설정 |
| `Tools/BalanceSimulator/App/RunInspector.cs` | 결과 행 클릭·사건 목록·4행6열 보드·토템 범위/후보 점수/이유 |
| `Tools/BalanceSimulator/App/PresentationAssets.cs`, `BoardCellButton.cs`, `Presentation/manifest.json` | 고정 SO GUID에 맞춘 토템14종/유닛5종 이미지·파일별 해시·가변 칸 렌더링 |
| `Tools/BalanceSimulator/Core/BalancedPolicy.cs` | 공개 현재 상태만 보는 균형형 휴리스틱, 카드/합성/토템/배치 판단 |
| `Tools/BalanceSimulator/Core/DecisionFrame.cs` | 사건별 후보·이유·불변 보드 기록 |
| `Tools/BalanceSimulator/Core/DecisionOption.cs` | 제시/합법 후보의 비교 점수·근거 |
| `Tools/BalanceSimulator/Core/BoardView.cs` | 유닛/토템/보관함/봉인 보드 스냅샷 |
| `Tools/BalanceSimulator/Core/UnitView.cs` | 유닛 등급·드론·점유 기록 |
| `Tools/BalanceSimulator/Core/TotemView.cs` | 토템 위치·방향·성장·효과/공격금지 범위·설명 |
| `Tools/BalanceSimulator/Checks/PolicyChecks.cs` | 회전/성장초기화/상한/점수/RNG무접근/기록량 무영향 검증 |
| `Tools/BalanceSimulator/Core/SimulationEngine.cs` | 전투/진행·실제 조우 기록·종료·HP 적용 |
| `Tools/BalanceSimulator/Core/CombatModel.cs` | 드론/식량/스킬/해킹/족장·지진 수치 모델 |
| `Tools/BalanceSimulator/Core/CardEffects.cs` | 카드 획득·프리뷰 롤 캐시·획득한 롤만 실제 생산에 기여 |
| `Tools/BalanceSimulator/Checks/SelectionRollChecks.cs` | 9108 미획득/재제시/후속 획득의 실제 생산 및 난수 회귀 |
| `Tools/BalanceSimulator/Core/TotemModel.cs` | 토템14종·배치·셀 효과·화상/아머·보관함 |
| `Tools/BalanceSimulator/Core/BossSchedule.cs` | 역할별 셔플과 캡처된 실제 보스 계산기 연결 |
| `Tools/BalanceSimulator/Core/GameSnapshot.cs` | 내장 입력과 GUID/관리 참조·SHA-256 |
| `Tools/BalanceSimulator/Core/SimulationSettings.cs` | 실행 설정·자동 정책·후보값 유효성 검사 |
| `Tools/BalanceSimulator/Core/RunResult.cs` | 실행별 결과·빌드/입력 식별자·한계 |
| `Tools/BalanceSimulator/Core/EncounterResult.cs` | 실제 전투한 보스의 최대/남은 HP와 진행률 |
| `Tools/BalanceSimulator/Core/ResultExport.cs` | 전체 CSV/JSON·메타데이터·요약 분모 |
| `Tools/BalanceSimulator/Core/StableRandom.cs` | SplitMix64 독립 난수 스트림 |
| `Tools/BalanceSimulator/Core/SeedBatchGenerator.cs` | 실행 버튼마다 OS 난수 기반 새 시드 묶음, 같은 세션 자동 묶음 중복 방지 |
| `Tools/BalanceSimulator/freeze_snapshot.py` | 게임 입력 읽기 전용 캡처·원본/해시 보존 |
| `Tools/BalanceSimulator/extract_data.py` | 캡처 YAML→내장 데이터, GUID/관리 참조/프리팹 상속 |
| `Tools/BalanceSimulator/verify_snapshots.py` | 파일 해시·payload/manifest 결합·현 작업 폴더 차이 |
| `Tools/BalanceSimulator/Snapshot/manifest.json` | 리팩터링 전3907파일 캡처 |
| `Tools/BalanceSimulator/SnapshotBossRefactor/manifest.json` | 리팩터링 후3918파일 캡처 |
| `Tools/BalanceSimulator/SnapshotRemoteContract/manifest.json` | 최신 코드·Excel·LitJSON 포함3926파일 캡처, 유일한 배포 프로필 |
| `Tools/BalanceSimulator/Core/UnityHashBoundary.cs` | 진단 전용 SHA-256, Unity Hash128과 구분 |
| `Tools/BalanceSimulator/build.ps1` | 입력 재캡처 없이 단일 EXE 빌드·배포 해시 |
| `Tools/BalanceSimulator/Core/Core.csproj` | 캡처된 게임 계산 원문을 직접 컴파일에 연결 |
| `Tools/BalanceSimulator/Checks/Program.cs` | Unity 수치 대조·재현·결과 경계·내보내기 검사 |
| `Tools/BalanceSimulator/UiChecks/Program.cs` | 보이지 않는 자체 Windows 화면·실행/중지 검사·레이아웃 캡처 |
| `Tools/BalanceSimulator/UnityReferenceChecks.cs` | Unity 임시 PreviewScene 수치/생산/단순 대결 참조 fixture 생성 |
| `outputs/balance-simulator-portraits-20261019/RESULT.md` | 0.3.2 사진/이름/유닛 등급·토템 등급 없음·버프 방향·독립 EXE |
| `outputs/boss-backend-required-20261019/SIMULATOR-HANDOFF.md` | 최신 Excel/뒤끝 계약·악어 단독 Hard 정정 인계 |
| `outputs/endless-boss-refactor-20261019/SIMULATOR-HANDOFF.md` | 반영된 실제 보스 계약 인계 원본 |

## 불변 규칙
- 0.3 사용자가 지정한 배치 토템5개와 보관함5칸은 별개다. 게임 파일은 바꾸지 않는다. 균형형은 설명 가능한 비교식이며 실측 DPS/전역 최적 해답이 아니다. 실제 제시 후보·현재 공개 정보만 보고 미래 난수를 열람하지 않는다.
- 기록은 과거 보드의 불변 복사다. 실제 후보/점수/이유·선택 카드 전체·최종 보드도 내보낸다. GUI 이력 예산8만 프레임으로 실행당 한도를 자동 제한하며 중간 생략을 명시한다. 기록량이 실제 전투/정책 RNG를 바꾸면 안 된다.
- UI 기본값은 매번 새 시드. 실행 버튼마다 새 기본 시드, 회차별 base+i, A/B 같은 회차는 같은 시드. 고정 재현 옵션을 유지하고 실제 시드·생성 정책을 내보낸다. 64비트 시드/기본 시드는 JSON 문자열이다.
- 새 스냅샷과 이전 캡처를 섞지 않는다. 최신 EXE에는 이전 분류/프로필을 내장/노출하지 않는다. 과거 원본 캡처와 증거는 보존한다.
- 성장률13%는 Excel 시험 입력이며 `GrowthRateFinalized=false`. 후보를 최종 게임 수치로 확정하지 않는다. 라이브 원격값 미포함/APK 동등성 미검증. BalanceVersion·입력 출처·Excel/기본/후보 행 해시를 기록한다.
- NORMAL HP/방어/체력줄/시작·처치 시간/분류/전투EXP는 Excel24열의 명시적 계약 입력. 최신 게임 파서·계산 원문을 컴파일하며 SO 대체는 없다. SO 풀은 콘텐츠 등록표다. 전투EXP는 BossKey 초기 지급률/호환 곡선이고 계정EXP와 구분한다.
- 예약된 다음 라운드를 마지막 전투로 쓰지 않는다. 보스 교체 대기는 직전 조우, 첫 전투 전 종료는 대상 없음.
- HP 진행은 종료 시 실제 HP 차감으로0~100%, 과잉 피해 제한. 계정 최고 진행과는 별개다.
- 미지원은 사유를 남겨 종료. 중지/오류/미지원은 기본 요약 분모에서 제외하고 계산 한계 종료는 중도 절단으로 표시한다.
- 이 도구는 Unity 전체 자동 플레이/Android 성능 동등성의 증거가 아니다. 모든 근사/정책을 실행별로 기록한다.

## 명령
`python Tools/BalanceSimulator/verify_snapshots.py`, `Tools/BalanceSimulator/build.ps1`.
Unity 참조 검사는 루트 `Tools/verification/unity-pipeline.ps1 -Command run_script -File Tools/BalanceSimulator/UnityReferenceChecks.cs -Entry UnityReferenceChecks.Run`. 기존 Editor가 정지한 상태에서 임시 PreviewScene만 사용하고 원래 씬/dirty/RNG를 보존한다. Editor를 새로 띄우거나 포커스를 옮기지 않는다.

## 로컬 생성 파일·과거 증거
다음 과거 RESULT는 현재 체크아웃에 없다. 기존 카드의 참조 이름을 보존하되 재검증 근거 또는 설치 필수 파일로 주장하지 않는다. 현재 지원 범위는 Tools/BalanceSimulator/README.md와 존재하는 portraits/roll-fix 결과를 확인한다.
- `outputs/balance-simulator-20261019/RESULT.md` (현재 없음)
- `outputs/balance-simulator-seeds-20261019/RESULT.md` (현재 없음)
- `outputs/balance-simulator-contract-20261019/RESULT.md` (현재 없음)
- `outputs/balance-simulator-icons-20261019/RESULT.md` (현재 없음)
- `outputs/balance-simulator-policy-20261019/RESULT.md` (현재 없음)
