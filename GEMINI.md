# GEMINI.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.


# Project Overview
**개구리 디펜스 (GaeGGUL Defence)** is a Unity-based game project. The architecture emphasizes modularity, separation of concerns, and clean data management.

## Core Technologies
- **Engine:** Unity
- **Async Framework:** UniTask
- **Animation:** DOTween
- **Backend:** TheBackend (뒤끝)

## Repository Structure
- `Assets/WorkSpace/`: Primary workspace containing individual team member folders (e.g., `HSD`, `JSY`, `KMS`, `USW`). 전부 다 읽을 필요는 없음 필요하면 물어보는 형식으로, Im HSD
- `Assets/Scenes/`: Main game scenes (Login, Lobby, Ingame).

## Development Conventions
- **Data & Logic:** Separation of data (ScriptableObjects, tables) and logic.
- **UI Architecture:** MVP (Model-View-Presenter) pattern. New panels should inherit from `UI_Base`.
- **Resource Management:** Managed via `RM.cs`. Use `RM.Instantiate` and `RM.Destroy` for object lifecycle and pooling.
- **Testing:**
  - `[Button]` attribute is available for calling methods in the Inspector.
  - Test scenes must be prefixed with `Test_`.
- **Commit Rules:** Use conventional prefixes: `Feat`, `Fix`, `Build`, `Test`, `Refactor`, `Docs`, `Release`, `Create`, `Chore`.

## Building and Running
- Open the project in Unity Editor.
- Use the defined Test Scenes for feature validation.
- TODO: Add specific build pipeline documentation if applicable.

---
*This file is generated to assist AI agent interactions.*
