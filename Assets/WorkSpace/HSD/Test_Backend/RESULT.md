# Backend test — 2026-10-30

- BootScene.unity: copy moved from Assets/Tests/BootSceneCopy; original preserved.
- LobbyScene.unity: copy of Assets/Scenes/LobbyScene.unity.
- Boot automatic guest login: FAIL, HTTP 401 bad customId.
- Existing Editor test account login: PASS.
- Authenticated initialization: remote units (9), mission configuration and research preparation completed, but PlayerData.IsReady=false and HasSaveConflict=true. Lobby transition blocked. Pending local journal was not deleted or overwritten.
- Research screen using remote configuration: PASS, 21 nodes, one upgrade and one isolated memory save. No real account research progression changed by this check.
- MCP Node suite: 16/16 PASS.
- Full Boot-to-Lobby and real gameplay validation remain incomplete. Do not interpret loader tests as full account/payment validation.

Automation: outputs/BackendSuite.cs (Setup/Run), tools/backnd-mcp/test/RestoreExistingSession.cs, tools/backnd-mcp/ResearchChartChecks.cs.

Detailed read-only probe: server snapshot dated 2026-10-24, local pending LastResetDate 2026-10-18; stamina server=30/local=25, account battle sequence server=6/local=1. PlayerSaveId exists. Both pending SaveId and BaseSaveId differ from the server. Journal parses correctly. This is an obsolete/conflicting local snapshot, not a missing PlayerSaveId column. Exact writer history cannot be reconstructed from these snapshots alone.
Repro scripts: tools/backnd-mcp/test/BackendSuite.cs and AccountStructureProbe.cs. Raw account diagnostics stay in ignored outputs/.

Recovery 2026-10-30 18:08 KST: user authorized keeping server state. Pending journal and loaded server state backed up to ignored outputs/account-recovery/20261030-090846-653. AcceptServerAfterConflict completed. BackendSuite.Run PASS: authenticated Player initialization, remote unit/mission/research preparation, copied LobbyScene load and DI readiness. This explicitly uses an already authenticated account; automatic guest login 401 remains unresolved. Editor left running in copied LobbyScene Play mode.

Excel regression 2026-10-30 18:24 KST: DEV_UnitBaseStats and DEV_MissionEvents exactly match outputs/dev-chart-upload-1793181571206 upload XLSX (version/hash/bytes). ResearchChartChecks.Run: 3 trees/61 nodes, 30 bundled stat comparisons, 7 invalid inputs rejected, remote icons matched. ResearchChartChecks.Screen: remote definition, 21 nodes, upgrade plus isolated memory save PASS. UnitBaseStatsEditorChecks: isolated UI/combat attack777, 2 attacks/sec=0.5sec, food12/sec and original SO preservation PASS (synthetic values, not uploaded production values). EventChartEditorChecks local payload/parser DEV route PASS. Node MCP16/16, content and composition checks PASS. Full battle, reward persistence and Research XLSX-to-server full-row equality not verified by these checks.

Unit bonus table 2026-10-30: UnitBonusChart reads DEV/LIVE_UnitLevelBonuses, validates EffectType enum and maps to StatModifierBonus/EveryNthCycleBonus. Player initialization now requires the bonus chart in remote mode. Account PlayerData.UnitLevels roundtrips through BackendGameData; absent entries are level1. Factory captures account levels per run. UI descriptions use remote definitions. UnitBonusTableChecks passed 9 units/45 milestones, level2 locked, Betan second-cycle 1.4 damage/0.7 charge, combat attack105/interval1 over2.1, food7.8, cooldown9.5, drone bonus2 and unknown-effect rejection. Unity compilation and MCP17 tests passed. Tests inject levels and do not purchase upgrades or mutate real account levels. Live bonus download and full boot still require new chart upload/apply; preview purchase remains separate.

LIVE DEV table check 2026-10-30 19:35 KST: UnitBaseStats file272579/36 rows, MissionEvents file272577/11 rows, ResearchBalance file272576/127 rows, UnitLevelBonuses file272581/45 rows. All219 rows matched every Excel column, ignoring row order. Boot copy auto-login reached LobbyScene with PlayerData.IsReady=true, UnitBalanceRuntime.IsLoaded=true and UnitBonusChart.IsLoaded=true. Server-downloaded bonus rows passed UnitBonusTableChecks (isolated level2/15, attack/speed/food/cooldown/cycle calculations). Remote Research screen21 nodes and isolated upgrade/save PASS. BackendSuite authenticated initialization and copied LobbyScene DI PASS. Editor left in copied LobbyScene Play. Full battle replay, real upgrade purchases/rewards and account level mutation were not tested.

Bonus description live check 2026-10-30 20:35 KST: DEV_UnitLevelBonuses applied file272684, 50rows (45 effects+5 chief descriptions), every Excel cell matches server. Existing test account login and authenticated copied-Lobby initialization PASS. UnitBonusChart loaded actual server descriptions; isolated DeckUpgradeBonusView rendered chief text, orange numeric markup, glossary link markup and TMP effect link; invoking the effect link handler created the tooltip. BonusDescriptionChecks validated actual downloaded rows and rejected arbitrary rich text. Physical pointer input, main-lobby visual placement and actual chief combat abilities were not tested. Editor left in copied LobbyScene Play.

Drone-only live check 2026-10-30 21:00 KST: DEV_UnitBaseStats file272692 has20rows; DEV_UnitLevelBonuses file272691 has35rows (25effects+10descriptions). Every column matches prepared Excel; no UD_Frog entries. Boot copy auto-login -> Lobby with PlayerData ready and unit/bonus loaders ready. Actual downloaded rows pass 5-drone/25-milestone checks, Betan branch parity228 comparisons, live chief description/link/tooltip isolated UI checks. Authenticated copied Lobby initialization PASS. No real upgrade purchase, account level grant or complete battle replay performed. Editor kept in copied LobbyScene Play.

## 2026-10-30 HTML 최신 보너스 정렬

기준: outputs/drone-efficiency/current-model.js의 최신 합의안. 베탕/델탕/감망/디시그망/젤탕/알팡 각 5개, 총 30개 보너스를 실제 전투 소비 지점과 연결했다. 효과 39행, 설명 30행. 가정용 디시그망 선택지는 제외했다.

- Unity 컴파일: 오류 0.
- UnitBonusTableChecks.Run: 99개 통과. 등급별 생산/소모, 비중첩 공유 용량, 예약 유지, 실제 시간 회복·다음 사격, 투사체 적중 문맥과 스킬 분리, 실제 식량 지급·5번째 2.5배 교체·보스 보상 카운터 제외, 알팡 초기 충전/쿨타임/속도 만료/집결 후 받피증·기존 디버프 공존.
- BetanBranchParityChecks.Run: 브랜치와 동일한 SO 대비 12개 레벨 경계 540개 비교 통과.
- 기존 HackingChecks.Run: 568개 통과. 이 검사는 로컬 SO 전용이어서 현재 원격 필수 설정으로 직접 실행하면 초기화 미완료 오류가 난다. 검사 중에만 remoteRequired=false를 주입한 격리 래퍼로 실행 후 이전 값 복원. 운영 설정 파일 변경 없음.
- BonusDescriptionChecks + LiveBonusDescriptionChecks.RunPrepared: 준비한 로컬 표에서 실제 TMP 설명 강조/용어/효과 링크, 동적 10초·20% 툴팁 생성 통과. 물리 포인터 클릭 검사는 아님.
- 기획툴 Electron UI 22개 통과. 효과/설명 편집, 배포 표 69행, 통합 유닛 Excel 6시트, 구버전 기본값 이관 및 사용자 수정 보존, 재가져오기 통과.
- 실행 씬: Assets/WorkSpace/HSD/Test_Backend/LobbyScene.unity, Play 상태 유지. 격리 오브젝트는 검사 후 제거. 원본 씬 저장/계정 데이터 쓰기 없음.

새 차트 파일은 outputs/html-bonuses-ready-20261030/UnitLevelBonuses_2026-10-30.xlsx. DEV_UnitLevelBonuses에 등록 후 적용해야 한다. 새 파일의 서버 전체 행 비교와 타이틀→로비→전투 다운로드 재검증은 업로드 후 진행할 항목이다. 현재 서버에 반영 완료했다고 판정하지 않는다. 기획툴 및 실제 코드 근거는 docs/technical/drone-html-level-bonuses.md 참조.

## 2026-10-30 서버 적용 후 최종 검증

- 뒤끝 DEV_UnitLevelBonuses 적용 파일 ID `272706`: 업로드용 UnitLevelBonuses_2026-10-30.xlsx의 69개 행/모든 열이 서버와 일치. Part 39행, Description 30행. 행 순서와 SDK 부가 열만 비교에서 제외.
- 디시그망은 `UnitData_Disigman` 키로 존재한다. 업로드 Excel 효과 24~28행, 설명 56~60행. 한글 유닛명 열이 없어 한글 이름으로 찾으면 보이지 않을 수 있다.
- DEV_UnitBaseStats 적용 파일 ID `272692`: 전체 20행. 디시그망 normal/rare/epic/legend 4행, 공격력 291/945/2164/14545, 공격 빈도 0.33 확인.
- LiveHtmlBonusChecks.Boot: 복사 BootScene을 새로 Play하여 자동 로그인→서버 차트 다운로드→로비 전환 확인. 6종 30보너스·30설명 로드, 복사 LobbyScene의 DI 준비 통과.
- UnitBonusTableChecks.RunRemote: 로컬 정의를 쓰는 대신 SDK로 서버 데이터를 직접 내려받아 동일 전투 검사 99개 통과.
- LiveBonusDescriptionChecks.Run: 실제 로드된 원격 데이터로 5종 유닛(디시그망 포함)의 레벨별 설명 25개, 알팡 설명/용어 링크/숫자 강조/10초·20% 툴팁 통과. 물리 포인터 클릭 검사는 아님.
- LiveHtmlBonusChecks.Factory: Unity AssetDatabase로 IngameScene을 Test_Backend에 복사. 실제 UnitFactory를 통해 5종×4등급=20개 실제 프리팹 생성, 원격 보너스→UnitData→UnitBase.Init→15레벨 스냅샷의 모든 스탯 전달 확인. Editor Preview 보호를 확인하고 소모·보상 없는 테스트로 실행. 생성물 제거, 계정 스냅샷/파티 복원, 복사 로비로 복귀.
- Unity 콘솔 Error/Exception/Assert 0건 확인. 에디터는 복사 로비에서 Play 유지.

중요한 경계: 현재 로그인 계정의 UnitLevels 저장 목록은 0건이므로 일반 생성 시 레벨 1이다. 15레벨 해금 효과의 전투 검사는 계정 레벨 저장값을 바꾸지 않고 테스트용 UnitLevelSnapshot으로 진행했다. 차트에 설계가 존재하는 것과 계정이 해당 레벨을 해금한 것은 별개다. 실계정 강화 구매·저장은 이 검증에서 구현하거나 우회하지 않았다.

재현: BootScene 복사본 Play → LiveHtmlBonusChecks.Boot → UnitBonusTableChecks.RunRemote → LiveBonusDescriptionChecks.Run → LiveHtmlBonusChecks.Factory. 서버 전체 행 비교 산출물은 outputs/html-bonus-live-20261030/comparison.json과 server.json.
