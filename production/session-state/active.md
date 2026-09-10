# Session State — 2026-09-11

<!-- STATUS -->
Epic: 프로젝트 지침 / 협업 인프라
Feature: Claude×Codex 협업 구조
Task: 지침 통일 완료 — 다음 작업 대기
<!-- /STATUS -->

> 이 파일은 교대 협업의 **배턴**이다 (`docs/AI-COLLAB-PROTOCOL.md` §3).
> 세션을 끝내기 전 활성 에이전트가 갱신하고, 다음 에이전트는 이 파일을 **가장 먼저** 읽는다.

---

## 작업 브랜치
`Develop`

---

## 이번 세션에서 완성한 것 (2026-09-11)

**HSD 인수 반영 + 지침 통일 (Claude×Codex 한 마인드 구조 확립)**

### 만진 파일
| 파일 | 변경 내용 |
|------|-----------|
| `.claude/docs/directory-structure.md` | HSD "관여 금지" → "USW 인수"로 변경. 빈 JSY/KMS 참조 완전 삭제. 영역 요약표 USW 단독화 |
| `AGENTS.md` | **공용 규칙 정본화** — 정본 선언 + AI Collaboration 요약 + CancellationToken 소유 표·Allowed Libraries·Code Quality 흡수 |
| `CLAUDE.md` | 얇게 재작성 — `@AGENTS.md`로 공용 규칙 로드, Claude 전용(coordination/context/design 기준)만 유지 |
| `.claude/docs/coding-standards.md` | 슬림 — 중복 규칙 제거, 디자인문서 기준 + 검증 기준만 |
| `.claude/docs/technical-preferences.md` | 슬림 — 중복 제거, 플랫폼/테스팅/ADR/스페셜리스트 라우팅만 |
| `README.md` | 브랜치 예시 `KMS_Grid` → `USW_Grid` (죽은 팀원 참조 제거) |
| `docs/AI-COLLAB-PROTOCOL.md` | **신규** — Claude×Codex 협업 합의문 |
| `Assets/WorkSpace/JSY`, `KMS` (+`.meta`) | 빈 폴더 삭제 |

> 참고: Claude 자동 메모리 2건도 갱신(`project_hsd_handover`, `project_ai_collab_instruction_arch`)했으나 **repo 밖**이라 Codex는 못 봄 → 핵심 내용은 위 repo 파일에 이미 반영됨.

---

## 다음 할 일

- **없음 (지침 정리 완결).** 다음 기능 작업을 여기에 채우고 STATUS 블록 갱신할 것.

---

## 열린 질문 (사용자 결정 대기)

1. 빈 `Assets/WorkSpace/JSY/`, `KMS/` **폴더 자체**를 삭제할지 (현재 지침 참조만 제거, 실제 폴더+.meta는 그대로 둠).
2. 이번 지침 변경들을 `[Docs]` 커밋으로 묶을지 (현재 **미커밋**).

---

## 미완 상태 경고

- 반쯤 편집한 씬/프리팹 **없음**.
- 단, git 워킹트리에 **이번 작업과 무관한 미커밋 변경 다수** 존재 (HpBar 연출, GameDataManager DI 리팩토링, URP/셰이더, TutorialManager 등). 이는 별개 작업 흐름 — 이번 세션이 건드리지 않았음. 커밋 시 범위 분리 주의.
