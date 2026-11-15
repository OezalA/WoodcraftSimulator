# Roadmap Tracker

Phase definitions and Definitions of Done: [MASTER_PROMPT.md](MASTER_PROMPT.md) §12. This file tracks status only.

Legend: ✅ done (verified) · 🟡 in progress · ⬜ not started

| Phase | Name | Status |
|---|---|---|
| 0 | Environment and Project Foundation | ✅ |
| 1 | First-Person Workshop Prototype | ✅ |
| 2 | Basic Object Handling | ✅ |
| 3 | Measurement Prototype | ✅ |
| 4 | Cutting Prototype | ✅ |
| 5 | Basic Assembly | ✅ |
| 6 | First Complete Crafting Job (vertical slice) | ⬜ |
| 7 | Inventory and Material Economy | ⬜ |
| 8 | Workshop Development | ⬜ |
| 9 | Advanced Woodworking | ⬜ |
| 10 | Outdoor Construction | ⬜ |
| 11 | Larger Structures | ⬜ |
| 12 | Progression and Career | ⬜ |
| 13 | Presentation and Polish | ⬜ |
| 14 | Testing and Release Preparation | ⬜ |

## Phase 0: Environment and Project Foundation
- [x] Inspect project, verify Unity version / URP / Input System
- [x] Git, `.gitignore`, `.gitattributes` (LFS)
- [x] Initial folder structure + assembly definitions
- [x] README, ARCHITECTURE, ROADMAP, CURRENT_STATE, DECISIONS, TESTING
- [x] Scripts compile in Unity (observed in Editor.log after restructure, 2026-11-13)
- [x] Owner confirms no unexplained Console errors (2026-11-13)
- [x] Baseline scene saved (`Workshop.unity` generated and committed)
- [x] SampleScene removed (owner approved, 2026-11-13)

## Phase 1: First-Person Workshop Prototype
- [x] Test environment: ground, walls, workbench (generator written)
- [x] First-person movement, mouse look, gravity, collision (code written)
- [x] Manual verification of movement / look / collisions (MT-001, MT-002 passed by owner, 2026-11-13)
- [x] HUD with UI Toolkit: crosshair (MT-003 passed, 2026-11-13)
- [x] Interaction raycasting (`PlayerInteractor`) + `IInteractable`
- [x] Interaction prompt ("[E] ...")
- [x] One basic interactable object: workbench lamp `LightSwitch`
- [x] Pause and cursor management: `PauseController` + UI Toolkit `PauseMenu` (MT-004 passed, 2026-11-13)
- [x] Mouse sensitivity setting in the pause menu, persisted

## Phase 2: Basic Object Handling
Split into steps: **2a** timber data + board, **2b** handling, **2c** timber rack (inventory moved to Phase 3, D-021).
- [x] 2a: `Length` (0.1 mm), `BoardDimensions`, `WoodSpecies`, `TimberPiece` + EditMode tests (31/31 green in dotnet test and Unity Test Runner)
- [x] 2a: Wood species definitions (7 species, approximate densities, D-017)
- [x] 2a: `TimberBoard` generated from data (mesh, collider, mass, color); boards in the workshop (MT-005 passed, 2026-11-13)
- [x] 2b: Pick up, carry, rotate, place a board; placement preview with collision check (MT-006 passed, 2026-11-13)
- [ ] Later: improve carry feel (owner: "taşıma hissi şimdilik çok beğenmedim"), see CURRENT_STATE
- [x] 2c: Simple storage rack: timber rack on the left wall (MT-007 passed, 2026-11-13)
- [ ] ~~2c: Small-item inventory prototype~~ → moved to Phase 3 (D-021)

## Phase 3: Measurement Prototype
- [x] 3a: Small-item inventory = tool belt (D-024, D-027): tape measure + pencil, keys 1–4
- [x] 3b: Tape measure: hooks on the nearest board end, live mm reading, sticky hook, precision look mode (D-025, D-026)
- [x] 3c: Pencil marks stored on the board (Length from the reference end), cut list target, tolerance check (default ±1 mm), tests (D-028; amended by D-034: marks on graduations, wheel fine adjustment)
- Owner accepted Phase 3 on 2026-11-14 ("tamamdır şimdi oldu devam edelim") after the cut list fix; `main` fast-forwarded.

## Phase 4: Cutting Prototype
Plan approved by the owner on 2026-11-14 ("devam et"), D-029.
- [x] 4a: Domain cut model: `Saw` (kerf), `Crosscut` (at a pencil mark, kerf on the waste side, marks handed over,
  minimum piece 5 mm), `CutWaste`, `MaterialWasteLog`; 19 new tests (108 total, `dotnet test`)
- [x] 4b: Hand saw on the belt, sawing interaction (hold LMB at a mark), two boards generated from the result (MT-011 passed, D-030)
- [x] 4c: Unsupported pieces fall and settle (also fixes floating boards), cut checked against the target, edge-case tests (112 total) (MT-012 passed, D-031)
- Owner accepted Phase 4 on 2026-11-14 ("tamamdır devam edebilirsin"); `main` fast-forwarded.

## Phase 5: Basic Assembly
Plan approved by the owner on 2026-11-14 ("tamam"), D-032. First structure: frame 600 × 300 mm (2 × 600 + 2 × 210 mm
spruce 95 × 45, butt joints, 2 screws 5 × 80 mm per corner).
- [x] 5a: Domain assembly model: `Blueprint` (slots, connections, prerequisites, accuracy ±1 / fit ±5 mm),
  `AssemblyBuild` (place / remove / fasten rules, completion), `AssemblyQualityReport`; 19 new tests (131 total)
- [x] 5b: Assembly table with a blueprint ghost; carried pieces snap into fitting slots, fit feedback (MT-013 passed, D-033)
- [x] 5c: Drill-driver on the belt, driving screws at fastening points, completion + quality report in the HUD (MT-015 passed, D-035)
- Owner accepted Phase 5 on 2026-11-15 ("hepsi süper"); `main` fast-forwarded.

## Save & Load (inserted before Phase 6, D-036)
Owner request 2026-11-15; plan approved ("tamam"). Company name for the save path: OezalAstudio.
- [x] S1: Versioned save model (`SaveGameData`), migration, validation, board round trip; 17 new tests (162 total)
- [ ] S2: Boards with marks recreated from the save, player pose, pause menu Save / New workshop, auto-save on quit,
  auto-continue on start (code done, awaiting MT-016)
- [ ] S3: Tools and belt, assembly progress, waste, cut list, light switch (code done, awaiting MT-016 + MT-017)
