# AI 작업 하네스 — 입구

AI(Claude·Codex)가 **코드 전체를 탐색하지 않고** 시스템 카드 한 장으로 필요한 파일에 바로 가게 하고,
여러 작업 폴더(레인)에서 동시에 작업할 때 **지금 누가 무엇을 잡고 있는지**를 공유하기 위한 문서다.
카드에 적힌 경로·클래스·메서드는 `harness.py check`가 실제 코드와 대조하므로, 카드를 근거로 써도 된다.

## AI 작업 규칙
1. **작업 전**: 아래 색인에서 해당 시스템 카드를 읽는다(`python Tools/harness/harness.py resolve "<요청>"`).
   카드의 진입 파일부터 열고, 카드에 없는 것만 추가로 탐색한다. Claude는 훅이 관련 카드를 자동으로 알려 준다.
2. **작업판 갱신**: 착수할 때 `lane set --task "무엇" --hold "수정할 경로,glob"`, 끝날 때 `lane done --note "결과 한 줄"`.
   다른 레인이 잡은 파일은 수정하지 않는다(Claude는 수정 직전에 확인 창이 뜬다). 상세 인계는 기존대로 active.md 날짜 섹션에 쓴다.
3. **보고**: Unity 명령이나 `/스킬` 명령을 실행한 요청은 끝에 참조 파일(경로·참조 방식·역할)을 보고한다.
   Claude는 훅이 자동으로 보고하고, Codex는 `harness.py describe <경로들>`의 결과를 응답에 붙인다.
4. **카드 유지**: 카드 내용과 코드가 다르면 코드가 정답이다. 작업하면서 진입 파일·규칙이 바뀌었으면 카드를 고치고
   front matter의 `verified`·`commit`을 갱신한다. 카드에 적지 않은 사실을 지어내지 않는다.

## 시스템 색인
모든 시스템에 카드가 있다. 카드의 "더 읽을 곳"이 원본 문서로 이어진다. 이 표가 프로젝트의 시스템 지도 원본이다.

| 시스템 | 한 줄 | 카드 / 원본 |
|---|---|---|
| **초기화** | RootLifetimeScope → 씬 LifetimeScope → GameInitializer, BootScene → 첫 튜토리얼 또는 완료 후 로비 | **[카드](systems/init.md)** |
| **게임 진행** | GameManager 상태/종료, WaveManager 조우/보상 순서, EndlessRunService 런·라운드·패널티, 타이머 | **[카드](systems/game-flow.md)** |
| **그리드·소환·합성** | GridManager/GridCell, UnitFactory/UnitSpawner/MergeManager, 드래그·판매 | **[카드](systems/grid.md)** |
| **공격·스킬·생산** | UnitCombatComponent/UnitStatsModifier/UnitResourceComponent, SkillData 조립, 피해 계산, 투사체 | **[카드](systems/combat.md)** |
| **드론** | 베탕·감망·델탕·젤탕 스포너와 소유 드론, 해킹 스택 | **[카드](systems/drone.md)** |
| **족장** | 카드 풀 선택과 인게임 스킬 버튼(알팡 집결). 그리드 유닛 아님 | **[카드](systems/chieftain.md)** |
| **보스·디버프** | BossData/RuntimeBossStats, BossBase 피해 경로, 패턴, DebuffController | **[카드](systems/boss.md)** |
| **EXP·선택지** | 피격 → EXP 연출/지급 → 레벨업 → 족장 풀 추첨 → 조합형 효과·명령 | **[카드](systems/levelup.md)** |
| **토템** | 보상 3택 → 인벤토리 → 배치/회전/판매, 셀 버프 | **[카드](systems/totem.md)** |
| **아웃게임** | 로그인·로비·파티·유저 데이터·영구 강화 | **[카드](systems/outgame.md)** |
| **계정 경험치** | AccountProgression.xlsx→설정 SO→보스 HP 진행→결과 지급·계정 저장·표시 | **[카드](systems/account-progression.md)** |

| 도구 | 한 줄 | 카드 |
|---|---|---|
| **Unity 명령·검증** | Pipeline 연결, run_script 검증 스크립트, 씬·공용 설정, Unity 스킬 버전 | **[카드](tools/unity.md)** |
| **Excel·문서 자료 관리** | 관리 EXE, 원본/기록 분류, 참조·중복·누락·SO 출처 해시 | **[카드](tools/data-hub.md)** |
| **Electron 기획 작업실** | 키워드 검색, 수치 편집, 키 설명, 개인 실험·전달, Excel 내보내기 | **[카드](tools/planner-studio.md)** |
| **Windows 밸런스 시뮬레이터** | NORMAL 알팡 독립 EXE, 고정 스냅샷·같은 시드 비교·실제 전투 라운드/HP 결과 | **[카드](tools/balance-simulator.md)** |

카드 형식은 [토템 카드](systems/totem.md)를 따른다: front matter(`keywords`·`verified`·`commit`) → 한 줄 요약 → 흐름 →
진입 파일 표 → 데이터 표 → 불변 규칙·함정 → 검증 표 → 더 읽을 곳. 표의 첫 열은 반드시 `` `경로` ``로 쓴다(검사·설명 조회에 쓰임).
흐름·규칙의 `` `Class.Member` ``는 실제 코드에 있어야 한다.

## 레인 (작업 폴더 × 에이전트)
작업판은 `production/session-state/active.md` 맨 위에 있다. 실험실 폴더의 같은 경로는 루트로 연결된 junction이라 한 파일을 함께 쓴다.

| 레인 | 실행 | 폴더 |
|---|---|---|
| rayumti / rayumti_lab | Claude | 루트 Develop / 실험실 work/unity-ui |
| codexgae / codexgae_lab | Codex 계정1 | 루트 / 실험실 |
| codexgae2 / codexgae2_lab | Codex 계정2 | 루트 / 실험실 |

레인 이름은 실행 별칭이 `GAE_LANE` 환경변수로 넘긴다. 레인을 새로 만들 때는 `lane set --lane 새이름`을 한 번 실행하면 행이 생긴다.

## 설치 (팀원 포함)
Python 3.9 이상과 같은 버전의 Unity 프로젝트 체크아웃이 필요하다.
**두 폴더(`docs/harness/`, `Tools/harness/`)만 복사하면 하네스 실행 코드는 전달되지만 전체 검사는 통과하지 않을 수 있다.**
카드가 참조하는 `docs/technical/`, `design/`, `Tools/verification/`, Unity 스킬 등에도 Git에서 제외된 파일이 있다.
`install`은 훅 연결과 빈 작업판 생성만 하며, 누락 문서·도구나 Unity CLI/패키지를 다운로드하지 않는다.

### 보내는 사람
프로젝트 루트에서 `python Tools/harness/bundle.py` → `Tools/harness-share.zip` 생성.
기존 ZIP이 있으면 `--output Tools/harness-share-새버전.zip`으로 새 파일을 만든다.

ZIP에는 두 하네스 폴더, 카드에 구체적으로 명시된 보조 문서·검증 도구, 두 Unity 스킬 폴더와 공용 규칙 원본이 **프로젝트 상대 경로**로 들어간다.
`harness-share-manifest.json`에 파일별 SHA-256과 체크아웃에서 받아야 할 경로가 기록된다.
`Assets/`, `Packages/`, `ProjectSettings/`의 게임 원본은 ZIP으로 배포하지 않는다.
기존 `Tools/harness.zip`은 실행 파일만 들어 있는 구형 ZIP이므로 새 공유 ZIP을 전달한다.

### 받는 사람
1. ZIP 내부 경로를 유지하여 프로젝트 루트에 합친다. 기존 공용 문서·스킬과 다른 파일은 차이를 검토한 뒤 반영한다.
   게임 체크아웃 버전은 송신자와 맞춘다. ZIP에 개인 `.claude/settings.json`, 인증 정보, `Library/`, 과거 로그·결과, 송신자의 작업판은 들어 있지 않다.
2. 프로젝트 루트에서 `python Tools/harness/harness.py install` 실행 → `.claude/settings.json`에 훅 5개 연결(기존 다른 훅 보존, 중복 설치 가능).
   `production/session-state/active.md`가 없으면 빈 작업판을 생성하고, 있으면 기존 인계·작업판을 보존한다.
   다른 작업 폴더는 `--project <폴더>`로 지정한다(그 폴더에서도 `Tools/harness`·`docs/harness`가 보여야 한다. 복사하거나 junction으로 연결).
3. 프로젝트 루트로 이동한 터미널에서 자기 레인을 지정한다(예시 이름은 팀에서 정한다):
   ```powershell
   $env:GAE_LANE = 'teammate'
   python Tools/harness/harness.py lane set --task '설치 확인' --hold ''
   python Tools/harness/harness.py lane done --note '설치 확인 완료'
   ```
   Claude 또는 Codex도 이 환경변수를 설정한 터미널에서 실행한다. PC마다 폴더 위치가 달라도 하네스는 프로젝트 기준 경로를 사용한다.
4. 확인: `python Tools/harness/harness.py check`.
   구체적인 카드 파일·심볼 누락은 수정해야 한다. 스킬 원본·설치 버전 경고는 해당 PC의 CLI·Claude 스킬 동기화·Pipeline 설치 상태를 확인한다.
   로컬 연결·Editor 로그·과거 RESULT와 이름 규칙 표의 패턴은 설치 필수 파일 검사가 아니며, 참조 보고에는 계속 사용한다.

**검증 경계**: 훅 연결·카드 검색·작업판이 정상이라는 사실과 전체 카드·환경 검사의 통과는 별개다.
송신자가 ZIP 내용을 확인했더라도 팀원 PC에서 `check`를 다시 실행해야 한다. 하네스 검사는 Unity 플레이 검증이나 Android 성능 검증을 대신하지 않는다.

Codex에는 훅이 없다. AGENTS.md의 하네스 규칙을 따라 `resolve`·`lane`·`describe`를 직접 실행한다.

## Claude 훅이 하는 일
| 시점 | 동작 |
|---|---|
| 세션 시작 | `check --brief`: 카드·스킬이 어긋났을 때만 알리고, 작업 중인 레인과 이 세션의 레인을 표시 |
| 요청 제출 | 요청 문장으로 관련 카드·스킬을 찾아 컨텍스트에 넣음(토큰 절약) |
| 파일 수정 직전 | 다른 레인이 잡은 파일이면 확인 창 |
| 도구 사용 후 | 읽은/수정한 파일, Unity 명령과 그 인자를 기록 |
| 응답 끝 | Unity 명령이나 `/스킬`이 있었던 요청만 참조 파일 보고(카드 설명 사용). 기록은 `production/session-logs/harness/` |

## 유지보수
- `python Tools/harness/harness.py check` — 카드 경로·심볼, 확인 커밋 이후 변경된 진입 파일, 스킬 버전·사본, Codex 스킬 미러, 작업판 상태.
- "확인 커밋 이후 변경" → 그 파일을 다시 읽고 카드가 여전히 맞는지 확인한 뒤 `verified`·`commit`을 갱신한다.
- `harness.py unregistered` → 보고에서 "카드 미등록"으로 쌓인 파일. 반복되는 파일은 해당 카드의 표에 추가한다.
- 새 카드는 자주 작업하는 시스템부터 만든다. 카드는 60~90줄 안팎으로 유지하고 세부 수식·필드는 원본 문서에 둔다.

### 2026-10-21 카드 대조 기준

시스템10장과 Unity/시뮬레이터 도구 카드를 코드·직렬화 자산·기존 결과와 대조했다. `verified`는 문서 대조 날짜이며 모든 기능의 재실행 날짜가 아니다. `commit: 2e76748e`는 Git 기준점이다. 현재 별도 작업의 미커밋 변경까지 카드에 설명했더라도 `check`는 그 변경을 계속 경고한다. 이를 없애려고 관련 없는 코드를 함께 커밋하거나 경고를 숨기지 않는다. 동명 GameInitializer는 인게임 USW 경로로 구분하고, 누락 과거 이미지/RESULT는 역사 참조로 보존했다. [대조·커밋 범위 결과](../../outputs/harness-refresh-20261021/RESULT.md).
