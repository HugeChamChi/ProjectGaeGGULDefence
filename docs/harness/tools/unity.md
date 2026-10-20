---
system: unity-tools
title: Unity 명령·검증 도구
keywords: unity command, run_script, recompile, editor_status, pipeline, 유니티 명령, 리컴파일, 컴파일, compile, 콘솔 오류, play 검증, 플레이모드, batchmode, unity-cli, 에디터 연결, 하네스, harness, 팀원 공유, 공유 zip
verified: 2026-10-21
commit: 2e76748e
---
# Unity 도구 카드

**한 줄 요약**: Unity Editor는 Pipeline 패키지(Editor HTTP 서버)로 제어한다. 샌드박스 안에서는 `Tools/verification/unity-pipeline.ps1`을 쓰고, 일회성 검증·저작은 `Tools/verification/*.cs`를 `run_script`로 실행한다.

## 자주 쓰는 명령
```powershell
& Tools/verification/unity-pipeline.ps1 -Command editor_status
& Tools/verification/unity-pipeline.ps1 -Command set_autotick -ExtraArgs @('--enable','true')
& Tools/verification/unity-pipeline.ps1 -Command recompile -ExtraArgs @('--focus','false')
& Tools/verification/unity-pipeline.ps1 -Command recompile_status
& Tools/verification/unity-pipeline.ps1 -Command run_script -File Tools/verification/Foo.cs -Entry Foo.Run
unity command <name> --project-path <작업 폴더>   # 호스트 실행 경로에서만. 폴더 두 개라 --project-path 필수
```
Editor 포트는 7800~7849에서 배정된다. 루트/실험실 모두 각 프로젝트 Library/Pipeline 연결 파일의 projectPath·포트를 확인하며 고정7800/7801을 가정하지 않는다. 샌드박스에서 CLI가 Editor를 못 찾아도 "꺼졌다"고 판단하지 않는다(AGENTS.md Unity Editor automation).

## 연결·실행 파일
| 파일 | 역할 |
|---|---|
| `Tools/verification/unity-pipeline.ps1` | 샌드박스용 Pipeline 호출 래퍼. 연결 파일의 포트·토큰으로 `/api/exec`를 직접 호출하고, 프로세스 정리나 에디터 실행은 하지 않음 |
| `Tools/invoke_unity_hidden.ps1` | `unity` CLI를 창 없이 실행하고 출력을 받아옴 |
| `Assets/Settings/Pipeline/EditorPipelineManager.asset` | Pipeline 서버 AutoStart·Watchdog 설정 |
| `Assets/WorkSpace/USW/Scripts/Editor/PipelineDescriptorRecovery.cs` | 연결 파일이 사라지면 5초마다 재기록하는 복구 코드 |
| `Assets/WorkSpace/USW/Scripts/Editor/DomainReloadDiagnostics.cs` | recompile(도메인 리로드) 지연·실패 진단 |
| `Tools/verification/PipelineConnectionChecks.cs` | run_script 연결 확인(프리뷰 씬에 임시 오브젝트를 만들고 지움) |
| `Packages/manifest.json` | 설치 패키지와 버전(Pipeline, URP, VContainer 등) |
| `Packages/packages-lock.json` | 패키지 해석 결과·git 커밋 고정값 |
| `ProjectSettings/ProjectSettings.asset` | 플레이어/빌드 설정. 반쯤 수정한 채 인계 금지 |
| `ProjectSettings/EditorBuildSettings.asset` | 빌드 씬 목록. 실험실은 skip-worktree로 다른 값 유지 |

## 로컬 생성 파일 (공유·존재 검사 제외)
각 PC에서 생성되는 연결·로그·검증 결과다. 공유 ZIP에 넣지 않으며, 없다는 이유만으로 설치 실패로 보지 않는다. 참조 보고에서는 계속 설명한다.

| 파일 | 역할 |
|---|---|
| `Library/Pipeline/.unity-pipeline-port` | 실행 중 Editor의 연결 파일(포트·인증 토큰). 내용 출력·커밋 금지. 샌드박스 CLI 탐색이 지울 수 있음 |
| `Library/Pipeline/eval-usage.jsonl` | Pipeline eval/run_script 사용 기록(패키지가 씀) |
| `%LOCALAPPDATA%/Unity/Editor/Editor.log` | 실행 중 Editor 로그(컴파일 오류·예외) |
| `%LOCALAPPDATA%/Unity/Editor/Editor-prev.log` | 직전 Editor 세션 로그 |
| `outputs/*/RESULT.md` | 작업별 검증 결과 원문(설치 필수 파일 아님) |

## run_script 대상 스크립트 (이름 규칙)
아래 패턴은 파일 역할을 설명하기 위한 이름 예시로, 모든 확장자·접미사의 파일이 있어야 한다는 설치 조건은 아니다. 시스템 카드의 구체적인 검증 스크립트 경로는 계속 검사한다.

| 파일 | 역할 |
|---|---|
| `Tools/verification/*.cs` | 일회성 검증·저작 스크립트. Assets 밖이라 recompile 없음 |
| `Tools/verification/*Checks.cs` | 검증: 상태를 읽고 PASS/FAIL 반환(원칙적으로 수정 없음) |
| `Tools/verification/*LiveChecks.cs` | 플레이 모드에서 실제 흐름을 돌리는 검증 |
| `Tools/verification/*PlayChecks.cs` | 플레이 모드 진입 후 동작 검증 |
| `Tools/verification/*Authoring.cs` | 저작: 씬·프리팹·SO를 실제로 수정함. 실행 전 대상과 작업판 확인 |
| `Tools/verification/*Setup.cs` | 기능 초기 저작·배치(에셋 생성·수정) |
| `Tools/verification/*Probe.cs` | 상태 관찰(읽기 위주, 로그·캡처) |
| `Tools/verification/*Build.cs` | APK 빌드. 사용자 요청 없이 실행 금지 |
| `Tools/verification/*.py` | 검증 보조(캡처·로그·에셋 텍스트 분석) |
| `Tools/verification/*.cjs` | 문서·에셋 텍스트 일괄 편집 |
| `Tools/verification/*.mjs` | 마이그레이션·밸런스 감사 |
| `Assets/WorkSpace/USW/Scripts/Editor/*.cs` | 상시 에디터 코드(메뉴·윈도우·검증기). Assets 안이라 수정하면 recompile |
| `Assets/WorkSpace/USW/Tutorial/Editor/*.cs` | 튜토리얼 씬 동기화·검증 |

## 씬·공용 설정
| 파일 | 역할 |
|---|---|
| `Assets/Scenes/BootScene.unity` | 앱 시작(RootLifetimeScope 초기화·로딩) |
| `Assets/Scenes/LobbyScene.unity` | 로비(강화·출격·족장 선택) |
| `Assets/Scenes/IngameScene.unity` | 본게임(GameInitializer·WaveManager·그리드) |
| `Assets/WorkSpace/USW/Tutorial/Scenes/TutorialScene.unity` | 인게임 튜토리얼 |
| `Assets/**/*.unity` | 씬. 두 레인이 동시에 수정 금지 → 작업판에 잡고 작업 |
| `Assets/**/*.prefab` | 프리팹. 반쯤 수정한 채 인계 금지 |
| `Assets/WorkSpace/HSD/Resources/Data/GameConfig.asset` | 게임 전역 설정 SO(G06) |

## 하네스·규칙
| 파일 | 역할 |
|---|---|
| `AGENTS.md` | Claude·Codex 공용 규칙 정본 |
| `production/session-state/active.md` | 인계 + 실시간 작업판(맨 위) |
| `docs/harness/README.md` | 하네스 입구·시스템 색인 |
| `docs/harness/systems/*.md` | 시스템 카드 |
| `docs/harness/tools/*.md` | 도구 카드 |
| `Tools/harness/harness.py` | 하네스 CLI·Claude 훅 |
| `Tools/harness/bundle.py` | 카드가 요구하는 보조 문서·검증 도구·Unity 스킬을 포함한 팀원 공유 ZIP 생성 |
| `docs/technical/unity-pipeline-connection.md` | Pipeline 연결·샌드박스 복구 운영 원본 |
| `.agents/skills/unity-cli/SKILL.md` | Codex용 unity CLI 스킬 사본 |
| `.agents/skills/unity-pipeline/SKILL.md` | Codex용 Pipeline 스킬 사본 |

## 암묵 참조
명령 정규식과, 그 명령이 인자로 받지 않아도 읽는 파일. 정규식 안의 `|`는 `\|`로 쓴다.

| 명령 정규식 | 읽는 파일 |
|---|---|
| `unity-pipeline\.ps1` | `Library/Pipeline/.unity-pipeline-port` |
| `\bunity(\.exe)?\s+(command\|status\|pipeline)\b` | `Library/Pipeline/.unity-pipeline-port` |

## 스킬 버전 (check가 읽음)
`installed`는 설치 버전, `aligned`는 스킬이 맞춘 버전, `upstream`은 내용이 같아야 할 원본, `recorded`는 이 카드에 적어 둔 버전이다. 어긋나면 `sync_hint`대로 처리한다. Claude 동기화본은 로컬에서 고치지 않는다.

```json harness-skills
[
  {"name": "unity-cli (Claude 동기화본)",
   "path": "~/.claude/skills/synced/*/unity-cli/SKILL.md",
   "installed": ["cmd", "unity --version"],
   "aligned": ["regex", "~/.claude/skills/synced/*/unity-cli/CHANGELOG.md", "^## CLI `([^`]+)`"],
   "sync_hint": "Claude 앱 동기화 대기"},
  {"name": "unity-cli (Codex 사본)",
   "path": ".agents/skills/unity-cli/SKILL.md",
   "upstream": "~/.claude/skills/synced/*/unity-cli/SKILL.md",
   "installed": ["cmd", "unity --version"],
   "aligned": ["regex", ".agents/skills/unity-cli/CHANGELOG.md", "^## CLI `([^`]+)`"],
   "sync_hint": "동기화본 unity-cli 폴더 전체를 .agents/skills/unity-cli/로 복사"},
  {"name": "unity-pipeline (Codex 사본)",
   "path": ".agents/skills/unity-pipeline/SKILL.md",
   "upstream": "Library/PackageCache/com.unity.pipeline@*/.claude/skills/unity-pipeline/SKILL.md",
   "installed": ["manifest", "com.unity.pipeline"],
   "recorded": "0.6.0-exp.1",
   "sync_hint": "패키지 원본 SKILL.md를 복사하고 recorded 갱신"},
  {"name": "Pipeline 운영 문서",
   "path": "docs/technical/unity-pipeline-connection.md",
   "installed": ["manifest", "com.unity.pipeline"],
   "aligned": ["regex", "docs/technical/unity-pipeline-connection.md", "Pipeline ([0-9][0-9A-Za-z.\\-]*[0-9A-Za-z])"],
   "sync_hint": "PipelineDescriptorRecovery를 재검증한 뒤 문서 버전 수정"},
  {"name": "Unity 버전",
   "path": "docs/engine-reference/unity/VERSION.md",
   "installed": ["regex", "ProjectSettings/ProjectVersion.txt", "m_EditorVersion: (\\S+)"],
   "recorded": "6000.3.11f1",
   "sync_hint": "VERSION.md와 AGENTS.md 엔진 버전 갱신"}
]
```
