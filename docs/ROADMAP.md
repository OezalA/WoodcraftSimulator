# Roadmap Tracker

Phase definitions and Definitions of Done: [MASTER_PROMPT.md](MASTER_PROMPT.md) §12. This file tracks status only.

Legend: ✅ done (verified) · 🟡 in progress · ⬜ not started

| Phase | Name | Status |
|---|---|---|
| 0 | Environment and Project Foundation | ✅ |
| 1 | First-Person Workshop Prototype | ✅ |
| 2 | Basic Object Handling | ✅ |
| 3 | Measurement Prototype | 🟡 |
| 4 | Cutting Prototype | ⬜ |
| 5 | Basic Assembly | ⬜ |
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
- [ ] 3a: Small-item inventory = tool belt (D-024): tape measure + pencil, keys 1–4 (code done, awaiting MT-008)
- [ ] 3b: Tape measure: hooks on the nearest board end, live mm reading, precision look mode (code done, awaiting MT-009)
- [ ] 3c: Pencil marks stored on the board (Length from the reference end), tolerance check (default ±1 mm), tests
