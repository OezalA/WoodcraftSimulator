# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 2 (Basic Object Handling): 2a ✅, 2b ✅, **2c** timber rack implemented, awaiting owner test MT-007.
Small-item inventory moved to Phase 3 (D-021). After MT-007 passes, Phase 2 is complete.
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
| Static compile (2c rack) | 0 errors, 0 warnings (Claude) | 2026-11-13 |
| MT-007 Timber rack (1st try) | Failed (owner): lowest shelf boards hard to target; boards could not be placed on the floor | 2026-11-13 |
| Fixes: shelf spacing 0.6 m, aim assist (D-009 update), blocker feedback in HUD (D-022) | Static compile 0 errors (Claude) | 2026-11-13 |
| MT-007 2nd try + diagnostic log | Floor still refused. Log: casts hit the player's own CharacterController (`surface='Player' 0.09 m`) | 2026-11-13 |
| Fix: `PhysicsQueries` skip the player (D-023) | Static compile 0 errors (Claude); MT-007 re-run pending | 2026-11-13 |

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
1. Owner: rebuild the scene, run MT-007; then commit `develop`, fast-forward `main` → Phase 2 complete.
2. Phase 3: small-item inventory (first items: tape measure, pencil) + measurement (tape measure tool, marking, tolerances).
