# Current State

_Last updated: 2026-11-15_

## Current milestone
**Phase 5 (Basic Assembly) done**: accepted by the owner on 2026-11-15 ("hepsi süper") after MT-013 and MT-015;
`main` fast-forwarded to `develop`.
**Save & load in progress** (owner request 2026-11-15, plan approved, D-036): **S1 (domain save model) done**;
next S2. Owner sets Company Name = OezalAstudio in Player Settings.
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
- Phase 3: tool belt with tape measure + pencil (D-024, D-027), tape measure with sticky hook and precision look
  (D-025, D-026), pencil marks + cut list target + ±1 mm tolerance check (D-028); 89 EditMode tests.
- Phase 4: domain crosscut at a pencil mark with kerf on the waste side and waste log (D-029), hand saw splitting
  boards in the scene (D-030), unsupported pieces fall and settle, cut checked against the target (D-031); 112 tests.
- Phase 5: domain assembly model (D-032), assembly table with snapping slots (D-033), pencil on graduations with
  wheel fine adjustment (D-034), drill-driver + screws + completion report (D-035); 145 tests.
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
| 3c 1st try (owner) | Cut list prompt showed the NEXT target and was read as the current one → now shows the current target; static compile 0 errors (Claude), re-test pending | 2026-11-14 |
| 3c pencil + cut list + tolerance (D-028): static compile + `dotnet test` | 0 errors, 0 warnings; 89/89 tests passed (Claude) | 2026-11-14 |
| Phase 3 as a whole (owner, after the cut list fix) | Accepted: "tamamdır şimdi oldu" | 2026-11-14 |
| 4a cut model: static compile + `dotnet test` | 0 errors, 0 warnings; 108/108 tests passed (Claude). Nothing to play-test yet (domain only) | 2026-11-14 |
| 4b hand saw: static compile + `dotnet test` | 0 errors, 0 warnings; 108/108 tests passed (Claude). Unity play-test pending (MT-011) | 2026-11-14 |
| MT-011 Hand saw (owner) | Passed: "gayet iyi çalıştı" | 2026-11-14 |
| 4c: static compile + `dotnet test` | 0 errors, 0 warnings; 112/112 tests passed (Claude). Unity play-test pending (MT-012) | 2026-11-14 |
| MT-012 (owner) | Passed: offcut falls, cut OK; a 315 mm line instead of 300 was cut where marked (correct) | 2026-11-14 |
| Phase 4 as a whole (owner) | Accepted: "tamamdır devam edebilirsin" | 2026-11-14 |
| 5a assembly model: static compile + `dotnet test` | 0 errors, 0 warnings; 130/130 tests passed (Claude). Domain only, nothing to play-test | 2026-11-14 |
| 5b assembly table + snapping: static compile + `dotnet test` | 0 errors, 0 warnings; 131/131 tests passed (Claude). Unity play-test pending (MT-013) | 2026-11-14 |
| D-034 pencil on graduations + wheel fine adjustment: static compile + `dotnet test` | 0 errors, 0 warnings; 143/143 tests passed (Claude). Unity play-test pending (MT-014) | 2026-11-14 |
| MT-014 fine marking (owner) | Passed: "gayet düzgün çalışıyor" | 2026-11-15 |
| 5c drill-driver + completion: static compile + `dotnet test` | 0 errors, 0 warnings; 145/145 tests passed (Claude). Unity play-test pending (MT-015) | 2026-11-15 |
| MT-013 + MT-015 (owner) | Passed: "hepsi süper" | 2026-11-15 |
| S1 save model: static compile + `dotnet test` | 0 errors, 0 warnings; 162/162 tests passed (Claude). Domain only | 2026-11-15 |

## Known issues / limitations
- **Carry feel not liked yet** (owner, 2026-11-13). Ideas for later: smooth follow (lerp/slerp) instead of rigid
  attachment, hold pose depending on size/mass (long boards on the shoulder), slower movement with heavy/long
  items, carried object avoiding walls (sweep test), subtle sway/bob. Ask the owner what feels wrong first.
- Carried boards can visually clip into walls (no collision while carried, D-019).
- No free "drop" of a carried object (only validated placement). Unsupported objects fall and settle (D-031).
- Species densities are approximate, unverified placeholders (D-017).
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game text is English only.
- **No save / load yet**: every start begins with the generated workshop (owner wants to continue where they left off).
- Pencil marks: no eraser, not saved, only on the board's flat faces, no squared lines / angles (D-028).
- Assist option "type the dimension, the line is drawn" (owner's option A, D-034) deferred to a settings menu.
- Recompiling scripts while in Play Mode resets non-serialized state (seen once: `NullReferenceException` in
  `PlayerToolBelt.ItemInHand`). Avoid with Unity *Preferences > General > Script Changes While Playing >
  Recompile After Finished Playing* (an editor preference, not a project setting).

## Next recommended action
1. Owner: Edit > Project Settings > Player > Company Name = `OezalAstudio` (D-036). Note: this also moves the saved
   mouse sensitivity (PlayerPrefs) to a new location, so it resets once.
2. S2: runtime board factory + save ids, `SaveGameService` (JsonUtility, persistentDataPath, migrate + validate),
   pause menu Save / New workshop (with confirmation), auto-save on quit, auto-continue on start.
3. S3, then Phase 6.
