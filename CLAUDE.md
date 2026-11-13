# WoodcraftSimulator: Claude Guide

**Read first, every session:**
1. [docs/CURRENT_STATE.md](docs/CURRENT_STATE.md): where we are, what's next.
2. [docs/MASTER_PROMPT.md](docs/MASTER_PROMPT.md): role, vision, systems, roadmap, working protocol, constraints (source of truth).
3. [docs/DECISIONS.md](docs/DECISIONS.md): decisions and deviations from the master prompt.

## Non-negotiables (summary of the master prompt)
- **Talk to the owner in Turkish.** Code, identifiers, doc filenames and technical terms in English.
- Owner is an experienced C#/.NET developer, new to Unity: explain Unity-specific concepts.
- Follow the working protocol (master prompt §14): Inspect → Explain → Plan → Implement → Verify → Report → update docs (`CURRENT_STATE.md` at minimum).
- Small increments; no speculative systems; explain architectural consequences before deciding.
- Never claim Unity compilation / tests / gameplay passed unless observed. Distinguish static inspection, Unity compilation, EditMode, PlayMode and manual tests.
- Don't touch ProjectSettings or packages casually; no destructive file/Git ops without approval.

## Environment facts (verified 2026-11-13)
- Unity 6000.3.28f1, URP 17.3.0, Input System 1.20.1, Test Framework 1.6.0, uGUI 2.0.0.
- `activeInputHandler: 1` → **new Input System only**; legacy `UnityEngine.Input` will not work.
- Project-wide actions = `Assets/InputSystem_Actions.inputactions` → `InputSystem.actions.FindAction("Player/Move")`.
- The Unity Editor is usually open during sessions and recompiles on focus. Check compilation results in
  `%LOCALAPPDATA%\Unity\Editor\Editor.log` (search `error CS`, `WoodcraftSimulator.dll`).
- Verify without the Editor: `sh tools/static-build.sh` (compile vs Unity DLLs) and
  `dotnet test tools/DomainTests` (runs EditMode domain tests). Report them as such, not as Unity results.
- Domain code (`Scripts/Domain`, assembly `WoodcraftSimulator.Domain`) must not use UnityEngine; C# 9 only (no records/init).
- Git LFS enabled for binary assets. Remote `origin` = https://github.com/OezalA/WoodcraftSimulator (**public**).
- Branch workflow: work on `feature/...` branches, push, merge into `main` via PR. `origin/main` started at the
  template commit; `feature/phase-0-1-foundation` holds Phase 0 + Phase 1 interaction work.
  Ask before pushing; never force-push or rewrite pushed history without approval.
