# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 2 (Basic Object Handling): 2a ✅, 2b ✅. Next: **2c** (storage rack + small-item inventory prototype).
Branches: only `main` + `develop` (D-020). `main` = last owner-tested state.

## Completed
- Phase 0: Git + LFS, folder structure, asmdefs, docs, baseline workshop scene. GitHub: https://github.com/OezalA/WoodcraftSimulator
- Phase 1: first-person controller, interaction system + UI Toolkit HUD, light switch, pause menu with
  mouse sensitivity (MT-001..MT-004 passed).
- Phase 2a: pure C# `WoodcraftSimulator.Domain` (`Length` 0.1 mm, `BoardDimensions`, `WoodSpecies`, `TimberPiece`),
  7 species assets, `TimberBoard` generated from data, 9 boards, 31 EditMode tests, `tools/` (MT-005 passed).
- Phase 2b: `Carryable`, `PlayerCarrier` (hold, R/T rotation, green/red placement preview, `Physics.CheckBox`
  validation, LMB place), `IDescribable`, `PlayerInteractor.IsBlocked`, carry hint in HUD, input actions
  Place/Rotate/Tilt, kinematic resting boards (D-019). Interaction reach 2.5 m. MT-006 passed.
- Visual: MSAA 4x on PC quality (D-018), pause menu fonts fixed.

## Latest verification
| What | Result | Date |
|---|---|---|
| MT-001..MT-005 | Passed (owner) | 2026-11-13 |
| Unity Test Runner EditMode | 31/31 green (owner) | 2026-11-13 |
| MT-006 Handling | Passed (owner): floor boards reachable after 2.5 m reach, preview green/red correct, R/T work, stacking on boards works | 2026-11-13 |

## Known issues / limitations
- **Carry feel not liked yet** (owner, 2026-11-13). Ideas for later: smooth follow (lerp/slerp) instead of rigid
  attachment, hold pose depending on size/mass (long boards on the shoulder, small items in hand), slower movement
  with heavy/long items, carried object avoiding walls (sweep test), subtle sway/bob. Ask the owner what feels wrong first.
- Carried boards can visually clip into walls (no collision while carried, D-019).
- Removing a board from under another leaves the upper one floating (kinematic, D-019). No free "drop".
- Species densities are approximate, unverified placeholders (D-017).
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game text is English only.

## Next recommended action
1. Phase 2c: simple storage rack (timber rack with placement slots) + small-item inventory prototype → completes Phase 2.
2. Then Phase 3: measurement (tape measure tool, marking, tolerances).
