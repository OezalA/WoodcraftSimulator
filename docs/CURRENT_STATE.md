# Current State

_Last updated: 2026-11-15_

## Current milestone
**Phase 5 (Basic Assembly) done**: accepted by the owner on 2026-11-15 ("hepsi süper") after MT-013 and MT-015;
`main` fast-forwarded to `develop`.
**Save & load done** (D-036): S1–S3 accepted by the owner on 2026-11-15 ("save başarılı") after MT-016/MT-017;
`main` fast-forwarded to `develop`.
**Phase 6 (first complete job) done**: accepted by the owner on 2026-11-15 ("her şey iyi çalışıyor") after MT-020 and MT-021; `main` fast-forwarded to `develop` (86821c5). (Plan approved 2026-11-15: small shelf, frame
stays as practice job, realistic drill M1/M2, D-038, D-039). Stroke sawing (T1/T2) was reverted by the owner after the
first try: the saw works as before (D-030). In-game texts Turkish (D-041), red pilot-hole points accepted too.
**Phase 7 (material economy) started** (plan K1-K7 approved 2026-11-15, D-040): 7a domain rules done (money, ledger,
catalog, consumable screws, priced bill of materials, job settlement, inventory summary, save format 2). 7b (scene)
next. D-042 (recommended clutch on the drill, owner request) implemented, awaiting MT-022.
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
- Save & load (D-036): S1-S3, boards, player, tools/belt, assembly, waste, cut list, lamp; 174 tests.
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
| S2 save service: static compile + `dotnet test` | 0 errors, 0 warnings; 162/162 tests passed (Claude). Unity play-test pending (MT-016) | 2026-11-15 |
| S2 in Unity: compilation after Editor restart | `Tundra build success`, no errors (Editor.log, observed by Claude). Play-test pending | 2026-11-15 |
| S3 save service: static compile + `dotnet test` | 0 errors, 0 warnings; 174/174 tests passed (Claude). Unity compile + play-test pending (MT-017) | 2026-11-15 |
| MT-016 + MT-017 save & load (owner) | Passed: "save başarılı" | 2026-11-15 |
| Phase 6 (6a-6c, D-038, D-039): static compile + `dotnet test` | 0 errors, 0 warnings; 220/220 tests passed (Claude). Unity compile + play-test pending (MT-018..MT-020) | 2026-11-15 |
| Stroke sawing (owner, 1st try) | Rejected: the measurement changing while sawing made it needlessly hard → saw reverted to D-030 | 2026-11-15 |
| Saw revert: static compile + `dotnet test` | 0 errors, 0 warnings; 209/209 tests passed (Claude); Unity compile and play-test pending | 2026-11-15 |
| Saw revert in Unity: compilation | `Tundra build success`, no `error CS` (Editor.log, observed by Claude); owner rebuilt the scene and played. MT-019/MT-020 results not reported yet | 2026-11-15 |
| 7a economy domain: static compile + `dotnet test` | 0 errors, 0 warnings; 225/225 tests passed (Claude). Domain only, nothing to play-test | 2026-11-15 |
| MT-020 shelf job (owner) | Completed end to end without help: 5/5 parts within ±1 mm, 12 screws (10 flush, 2 too deep), **8 splits** (pilot-hole rule not noticed), 1 extra board; save, quit and reload kept everything. Delivery step not reported yet | 2026-11-15 |
| D-041 Turkish texts + red pilot points: static compile + `dotnet test` | 0 errors, 0 warnings; 229/229 tests passed (Claude). Unity compile + play-test pending (MT-021) | 2026-11-15 |
| Phase 6 + D-041 (owner) | Accepted: "her şey iyi çalışıyor devam edebiliriz"; `main` fast-forwarded to 86821c5 | 2026-11-15 |
| D-042 recommended clutch: static compile + `dotnet test` | 0 errors, 0 warnings; 233/233 tests passed (Claude). Unity compile + play-test pending (MT-022) | 2026-11-15 |

## Known issues / limitations
- **Carry feel not liked yet** (owner, 2026-11-13). Ideas for later: smooth follow (lerp/slerp) instead of rigid
  attachment, hold pose depending on size/mass (long boards on the shoulder), slower movement with heavy/long
  items, carried object avoiding walls (sweep test), subtle sway/bob. Ask the owner what feels wrong first.
- Carried boards can visually clip into walls (no collision while carried, D-019).
- No free "drop" of a carried object (only validated placement). Unsupported objects fall and settle (D-031).
- Species densities are approximate, unverified placeholders (D-017).
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game texts: Turkish by default, English selectable in the pause menu (D-041). Docs and code stay English.
- Tool and board visuals are placeholders until the visual pass with purchased assets (D-037).
- Screwing tuning values (clutch window, drive speed) are first guesses (D-038): expect tuning after play-testing.
- Deferred realism (D-038): squareness of hand cuts, clamping, screw angle, free screw positions (Phase 9); ask the owner first, playability comes before difficulty.
- Jobs: money, purchases and consumable screws exist in the domain only (7a); in the scene the stock is still
  delivered free and screws are unlimited until 7b.
- The € sign is outside Latin-1 (D-028 text rule); check it renders in 7b, else `Money.Symbol` becomes "EUR ".
- Prices and pay are first guesses (German DIY store level): spruce 2000 × 145 × 18 €8.50, 200 screws €6.
- Save & load (D-036): one save slot; objects only move when the game moves them (no free drop of tools yet, so tools are on the belt or where the scene put them).
- Pencil marks: no eraser, only on the board's flat faces, no squared lines / angles (D-028).
- Assist option "type the dimension, the line is drawn" (owner's option A, D-034) deferred to a settings menu.
- Recompiling scripts while in Play Mode resets non-serialized state (seen once: `NullReferenceException` in
  `PlayerToolBelt.ItemInHand`). Avoid with Unity *Preferences > General > Script Changes While Playing >
  Recompile After Finished Playing* (an editor preference, not a project setting).

## Next recommended action
1. Done: Company Name `OezalAstudio` (owner, 2026-11-15).
2. Done: MT-016, MT-017 passed.
3. Done: Phase 6 + D-041 accepted, `main` fast-forwarded. Owner: MT-022 (recommended clutch) when convenient.
4. Claude: Phase 7b (scene side of the economy, D-040), then MT-022+.
