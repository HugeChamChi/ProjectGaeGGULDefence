# 🚀 Rclone G-Drive Sync System 기술 명세서 (V1.2)

> **상태**: 🟢 사용 중  
> **최종 수정**: 2026-10-01 — Imports meta는 Git으로 관리, 양방향 전송에서 meta 제외 및 PC 전환 순서 추가  
> **핵심 가치**: 팀원 에셋 받기, 데이터 삭제 사고 방지

---

## 📑 1. 시스템 구성 요소

| 구분 | 명칭 | 경로 | 주요 역할 |
| :--- | :--- | :--- | :--- |
| **Tool** | `RcloneSyncTool` | `Assets/Editor/RcloneSyncTool.cs` | 유니티 에디터 내 업로드/다운로드 UI 및 실행 로직 |
| **Engine** | `Rclone` | PATH 또는 툴의 "Rclone 경로" 칸 | 실제 전송 및 구글 드라이브 API 통신 |
| **Remote** | `gdrive_ggd` | 각자 PC의 rclone 설정 | 공유 `Imports` 폴더를 `root_folder_id`로 가리키는 드라이브 리모트 |

> ⚠️ 이전 버전 문서에 있던 `Setup_Rclone_GGD.bat`은 저장소에 **존재하지 않는다**. 리모트는 아래 §3 절차로 각자 직접 설정한다.

---

## 🔄 2. 동작 방식 (Sync Logic)

| 버튼 | 실행 명령 | 동작 |
| :--- | :--- | :--- |
| 📥 **Download** | `rclone copy gdrive_ggd: <로컬> --ignore-existing --exclude "*.meta"` | **로컬에 없는 원본 파일만** 받는다. 이미 있는 파일은 덮어쓰지 않고, 아무것도 삭제하지 않는다. |
| 🚀 **Upload** | `rclone copy <로컬> gdrive_ggd: --exclude "*.meta"` | 새 원본/변경된 원본을 올린다. 드라이브의 파일은 삭제하지 않는다. |

공통 옵션: `--transfers`, `--checkers` (툴 슬라이더), `--drive-chunk-size 64M`, `--buffer-size 32M`, `--fast-list`, `--progress`

### 알아둘 점
*   **Download는 수정된 파일을 받지 않는다.** 드라이브에서 내용이 바뀐 기존 파일은 로컬에 이미 같은 이름이 있으면 건너뛴다. 갱신이 필요하면 해당 파일을 로컬에서 지우고 다시 Download 하거나 수동으로 받는다.
*   **파일 삭제는 전파되지 않는다.** 한쪽에서 지운 파일은 다른 쪽에 그대로 남는다. 드라이브 정리는 웹에서 직접 한다.
*   **`--size-only`는 쓰지 않는다.** 크기가 같고 내용만 바뀐 파일(텍스처 미세 수정, 수치 변경 등)이 업로드에서 누락되기 때문이다. 대신 수정 시간 + 해시로 비교하므로 첫 업로드 비교는 조금 더 걸릴 수 있다.
*   **Download 후 전송 0건은 정상일 수 있다.** 로컬이 이미 드라이브와 같으면 목록만 훑고 끝난다 (`Checks: N / N`, `Transferred: 0 B`).
*   성공 시 cmd 창은 자동으로 닫히고, 실패 시에만 `pause`로 창이 유지된다.

---

## 🛠️ 3. 팀원 셋업 가이드

### [Step 1] 전용 Google Cloud Client ID 만들기 (필수)
rclone 기본 공용 Client ID(프로젝트 번호 `202264815644`)는 전 세계 rclone 사용자가 할당량을 공유해서 **403 `rateLimitExceeded`** 오류가 자주 난다. 각자 전용 ID를 만든다.

1.  [Google Cloud Console](https://console.cloud.google.com)에서 새 프로젝트 생성 (상위 리소스: **조직 없음**).
2.  **API 및 서비스 → 라이브러리** → `Google Drive API` → **사용**.
3.  **OAuth 동의 화면(Google 인증 플랫폼)** → 앱 이름·지원 이메일 입력, 대상 **외부**.
    *   로고/도메인/데이터 액세스(범위)는 **비워둔다**.
    *   **게시하지 않고 "테스트" 상태로 둔다.** 프로덕션 게시에는 홈페이지·개인정보처리방침 URL이 필요하다.
    *   **대상 → 테스트 사용자**에 rclone으로 연결할 본인 Gmail을 추가한다.
4.  **클라이언트 → 클라이언트 만들기** → 유형 **데스크톱 앱** → Client ID / Secret 복사. (Secret은 저장소·채팅에 올리지 않는다.)

### [Step 2] rclone 리모트 설정
리모트가 없으면 `rclone config`로 `gdrive_ggd`(drive, scope=`drive`)를 만들고, **`root_folder_id`에 공유 `Imports` 폴더 ID**를 넣는다. 폴더 ID는 드라이브 폴더 URL의 `folders/` 뒤 문자열이다.

이미 리모트가 있으면 Client ID만 교체한다:
```
rclone config update gdrive_ggd client_id "<ID>" client_secret "<SECRET>"
rclone config reconnect gdrive_ggd:
rclone about gdrive_ggd:        # 용량이 나오면 성공
```

### [Step 3] 유니티 에디터에서 사용
1.  `Tools > Rclone Sync Manager` 메뉴 진입.
2.  **로컬 경로**: 본인의 `.../Assets/Imports`
3.  **구글 드라이브 경로**: `gdrive_ggd:` — `root_folder_id`가 이미 `Imports` 폴더를 가리키므로 폴더 이름을 붙이지 않는다. (`gdrive_ggd:Imports`로 쓰면 그 안의 하위 `Imports`를 찾아 오류가 난다.)
4.  **Download**: 팀원이 올린 새 에셋 받기 / **Upload**: 내 작업물 공유.

### 드라이브 공유 권한
*   폴더 일반 액세스는 **"제한됨"**으로 둬도 된다. rclone은 링크가 아니라 로그인한 계정 권한으로 접근한다.
*   팀원 계정을 폴더에 직접 추가: Download만 → **뷰어**, Upload까지 → **편집자**.

---

## 4. Imports meta 운영 순서

`Assets/Imports`의 `.meta`와 루트 폴더 `Assets/Imports.meta`는 Git으로 관리하고, 원본 파일은 Rclone으로 공유한다. 루트 Develop PC의 meta/GUID가 정답이다. 공개 저장소에 유료 에셋과 GGD_ArtWork 원본을 추가하지 않는다. Drive의 기존 meta는 삭제하지 않으며 전송에서 제외한다.

### 받을 때

Unity를 닫은 상태에서 **git pull → Rclone Download → Unity 열기** 순서로 진행한다. 원본 없이 Unity를 열면 짝 없는 meta가 삭제될 수 있다. 이 삭제를 커밋하면 다른 PC의 참조도 끊긴다. 실수로 삭제됐다면 Unity를 닫고 `git checkout -- Assets/Imports`로 meta를 복구한 뒤 원본을 Download하고 Unity를 연다.

### 새 에셋을 넣을 때

Unity 임포트 → 생성된 `.meta`는 Git 커밋 → 원본은 Rclone Upload. meta와 원본 중 하나라도 전달되지 않으면 다른 PC의 참조가 끊긴다. 원본은 동일한 상대 경로에 유지한다. 현재 전환 작업에서는 git add까지만 하며 커밋·푸시는 사용자 지시 후 수행한다.

### 다른 PC 최초 전환 (사용자 직접)

1. 그 PC에서만 만든 미커밋 씬·프리팹이 있으면 사용자에게 먼저 알리고 보존 방법을 결정한다.
2. Unity를 닫는다.
3. 그 PC의 `Assets/Imports/**/*.meta`만 삭제한다. 원본 파일과 폴더는 유지한다.
4. `git pull`로 정답 meta를 받는다. 기존 untracked meta를 남기면 `untracked working tree files would be overwritten` 충돌로 멈출 수 있다.
5. Rclone Download로 누락 원본을 받는다.
6. Unity를 열고 보스 HP바 Fill/Background, 설정 아이콘, `InGame_Setting_Panel`/`Vol_Setting`/`Boss_Encounter`/`TotemSelectCardPrefab_*`의 Missing 참조를 확인한다.

### 실험실 워크트리

`outputs/workspaces/unity-ui`는 루트 Imports를 복사해 만들어 GUID가 같다. 병합 때 untracked meta 충돌이 날 수 있으므로 Unity 닫기 → 기존 Imports meta만 삭제 → 병합 → Rclone Download → Unity 열기 순서로 처리한다. 미커밋 씬·프리팹이 있으면 먼저 보존 방법을 결정한다. Codex는 이번 작업에서 실험실 워크트리를 수정하지 않는다.

---

## 🧯 5. 트러블슈팅

| 증상 | 원인 | 해결 |
| :--- | :--- | :--- |
| `Error 403 ... rateLimitExceeded` (consumer `202264815644`) | 공용 Client ID 할당량 초과 | §3 Step 1로 전용 Client ID 설정. 급하면 체크/전송 슬라이더를 16/8 정도로 낮춤 |
| `invalid_grant`, 토큰 만료 | OAuth 앱이 "테스트" 상태라 7일마다 토큰 만료 | `rclone config reconnect gdrive_ggd:` |
| `unauthorized_client` | Client ID 변경 후 토큰 미갱신 | `rclone config reconnect gdrive_ggd:` |
| `액세스 차단됨: 앱이 인증 절차를 완료하지 않음` | 테스트 사용자에 계정 미추가 | OAuth 동의 화면 → 대상 → 테스트 사용자에 추가 |
| `directory not found` | 드라이브 경로에 폴더 이름을 중복 기입 | 경로를 `gdrive_ggd:`로 |
| `Listed` 수가 실제 파일 수보다 비정상적으로 큼 (수십만), 끝나지 않음 | 폴더 안에 **자기 자신을 가리키는 바로가기**가 있어 무한 재귀 | 드라이브 웹에서 화살표(↗) 아이콘이 붙은 해당 바로가기만 삭제. 공유 폴더 안에 그 폴더의 바로가기를 만들지 말 것 |
| Download 해도 받는 파일이 없음 | 로컬이 이미 최신 (정상) | `rclone check gdrive_ggd: <로컬> --one-way --size-only`로 누락 0건인지 확인 |

---

> 📌 **Note**: 수만 개의 작은 파일 처리 기준 설정이다. 파일당 수백 MB 이상인 대용량 위주로 바뀌면 `--drive-chunk-size`를 128M 이상으로 올린다.
