# Current State

_Last updated: 2026-11-14_

## Current milestone
Phase 3 (Measurement Prototype): **all three steps implemented on `develop`** (3a tool belt, 3b tape measure +
precision look, 3c pencil marks + cut list target + tolerance check). 3a/3b were play-tested twice with fixes
(D-026, D-027); the owner said "continue" after the Game view fix, but did not report the D-027 steps explicitly.
**3c not yet tested in Unity.** `main` stays at Phase 2 until the owner confirms MT-008..MT-010 together.
Branches: only `main` + `develop` (D-020). `main` = last owner-tested state.

## Completed
- Phase 0: Git + LFS, folder structure, asmdefs, docs, baseline workshop scene. GitHub: https://github.com/OezalA/WoodcraftSimulator
- Phase 1: first-person controller, interaction system + UI Toolkit HUD, light switch, pause menu with
  mouse sensitivity (MT-001..MT-004 passed).
- Phase 2a: pure C# `WoodcraftSimulator.Domain` (`Length` 0.1 mm, `BoardDimensions`, `WoodSpecies`, `TimberPiece`),
  7 species assets, `TimberBoard` generated from data, 31 EditMode tests, `tools/` (MT-005 passed).
- Phase 2b: `Carryable`, `PlayerCarrier` (hold, R/T rotation, green/red placement preview, overlap validation,
  LMB place), `IDescribable`, `PlayerInteractor.IsBlocked`, carry hint in HUD, kinematic resting boards (MT-006 passed).
- Phase 2c: timber rack (3 shelves at 30/90/150 cm) holding the long boards; aim assist for interactions;
  HUD explains refused placements (D-022); all casts from the eyes skip the player's own collider (D-023) (MT-007 passed).
- Visual: MSAA 4x on PC quality (D-018), pause menu fonts fixed.

## Latest verification
| What | Result | Date |
|---|---|---|
| MT-001..MT-006 | Passed (owner) | 2026-11-13 |
| Unity Test Runner EditMode | 31/31 green (owner) | 2026-11-13 |
| MT-007 Timber rack | Passed on 3rd try (owner), after fixes D-009 update, D-022, D-023 | 2026-11-13 |
| 3a tool belt: static compile + `dotnet test` | 0 errors; 44/44 tests passed (Claude) | 2026-11-13 |
| MT-008 Tool belt | Not yet executed | - |
| 3b tape measure: static compile + `dotnet test` | 0 errors; 68/68 tests passed (Claude) | 2026-11-13 |
| MT-008/MT-009 1st try (owner) | Tape measures; precision look works. Problems: tools not visible in hand + belt bar not visible (Game view cropped by fixed Full HD at 1x — my earlier tip), pencil hard to target, reading vanished when the aim slipped off the board | 2026-11-14 |
| Fixes: sticky hook, ≥ 5 cm grab colliders, hand poses nearer the centre (D-026); Game view tip corrected | Static compile 0 errors (Claude); re-tested in 2nd try | 2026-11-14 |
| MT-008/009 2nd try (owner) | Pencil visible in hand (no action yet: 3c). Belt bar visible once Game view Scale < 1x. Tape "vanished" when taken while holding the pencil (went onto the belt); tools gone after carrying a board → D-027 | 2026-11-14 |
| D-027 (new tool into hand, tool back after carrying) | Static compile 0 errors (Claude); re-test pending | 2026-11-14 |
| 3c 1st try (owner): cut list prompt showed the NEXT target and was read as the current one → prompt now shows the current target | 2026-11-14 |
| 3c pencil + cut list + tolerance (D-028): static compile + `dotnet test` | 0 errors, 0 warnings; 89/89 tests passed (Claude). Unity play-test pending (MT-010) | 2026-11-14 |

## Known issues / limitations
- **Carry feel not liked yet** (owner, 2026-11-13). Ideas for later: smooth follow (lerp/slerp) instead of rigid
  attachment, hold pose depending on size/mass (long boards on the shoulder), slower movement with heavy/long
  items, carried object avoiding walls (sweep test), subtle sway/bob. Ask the owner what feels wrong first.
- Carried boards can visually clip into walls (no collision while carried, D-019).
- Removing a board from under another leaves the upper one floating (kinematic, D-019). No free "drop".
- Species densities are approximate, unverified placeholders (D-017).
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game text is English only.
- Pencil marks: no eraser, not saved, only on the board's flat faces, no squared lines / angles (D-028).
- Recompiling scripts while in Play Mode resets non-serialized state (seen once: `NullReferenceException` in
  `PlayerToolBelt.ItemInHand`). Avoid with Unity *Preferences > General > Script Changes While Playing >
  Recompile After Finished Playing* (an editor preference, not a project setting).

## Next recommended action
1. Owner: stop Play, rebuild the scene, run MT-008 + MT-009 (regression, incl. D-027 steps) + MT-010
   (+ Test Runner 89 tests). Then fast-forward `main` and mark Phase 3 done.
2. Phase 4 (Cutting Prototype): plan to be agreed with the owner first (saw choice, kerf, cutting at a mark).
