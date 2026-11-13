# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 2 (Basic Object Handling): step **2a** ✅ done. Next: step **2b** (pick up / carry / rotate / place boards).
Branch: `feature/phase-2-timber-data` (based on `feature/phase-1-pause` → `feature/phase-0-1-foundation`; none merged into `main` yet).

## Completed
- Phase 0: Git + LFS, folder structure, asmdefs, docs, baseline workshop scene. GitHub: https://github.com/OezalA/WoodcraftSimulator
- Phase 1: first-person controller, interaction system + UI Toolkit HUD, light switch, pause menu with
  mouse sensitivity (MT-001..MT-004 passed).
- Phase 2a: pure C# `WoodcraftSimulator.Domain` (`Length` 0.1 mm, `BoardDimensions`, `WoodSpecies`, `TimberPiece`),
  7 `WoodSpeciesDefinition` assets, `TimberBoard` generated from data, 9 boards in the workshop,
  first EditMode test assembly (31 tests), tools `tools/static-build.sh` + `tools/DomainTests`.
- Visual: MSAA 4x on PC quality (D-018); pause menu font sizes fixed (theme overrides).

## Latest verification
| What | Result | Date |
|---|---|---|
| MT-001..MT-004 | Passed (owner) | 2026-11-13 |
| Domain tests via `dotnet test tools/DomainTests` | 31/31 passed (Claude) | 2026-11-13 |
| Unity Test Runner EditMode | 31/31 green (owner) | 2026-11-13 |
| MT-005 Timber boards + species | Passed (owner): 9 boards (spruce stack reads as one pile), rest still, can step onto floor boards | 2026-11-13 |
| MSAA 4x + pause menu font change | Pending owner visual check (Game view at 1x) | - |

## Known issues / limitations
- Species densities are approximate, unverified placeholders (D-017).
- Boards are dynamic rigidbodies; not yet interactable (Phase 2b).
- Pause menu text was reported as not fully crisp: likely Game view zoom (1.3x) + small Game view; re-check at 1x.
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game text is English only.

## Next recommended action
1. Owner: visual re-check with Game view Scale 1x / Full HD / Play Maximized.
2. Phase 2b: pick up / carry / rotate / place boards with a validated placement preview.
3. Open PR(s) into `main` when convenient.
