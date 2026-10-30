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
