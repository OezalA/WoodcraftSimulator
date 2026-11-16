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
- [x] S2: Boards with marks recreated from the save, player pose, pause menu Save / New workshop, auto-save on quit,
  auto-continue on start (MT-016 passed, 2026-11-15)
- [x] S3: Tools and belt, assembly progress, waste, cut list, light switch (MT-017 passed, 2026-11-15; 174 tests)

## Phase 6: First Complete Crafting Job (plan approved 2026-11-15: shelf, T1/T2/M1/M2, frame stays as first job) - DONE, accepted 2026-11-15
Goal (master prompt): one woodworking project from start to finish without developer intervention.
- [x] 6a: Domain job model: `Job`, `MaterialPlan` (cut list grouped by size, stock boards incl. kerf, offcuts,
  fasteners), `CutListProgress`, `JobResult` with separate quality goals, `JobText`; sawing and fastening rules
  (`FasteningRules`, `ScrewDrive`, pilot holes / head heights / splits in
  `AssemblyBuild`); 209 tests (D-038, D-039). Code done (stroke sawing was built and reverted, D-038)
- [x] 6b: Shelf blueprint 500 × 600 × 145 (18 mm spruce, 12 × 4 × 40 mm screws), job board with two jobs (practice
  frame, small shelf), stock delivered in front of the rack, cut list note follows the job, job sheet in the HUD;
  drill bit / pilot holes, clutch and screw depth (M1, M2). Code done,
  awaiting MT-019, MT-020
- [x] 6c: Job result in the HUD (parts, screw heads, splits, material delivered / in the structure / sawdust /
  offcuts, time, quality goals), deliver to finish, order extra stock, job state saved. Code done, awaiting MT-020

## Phase 7: Inventory and Material Economy (plan approved 2026-11-15: K1-K7, D-040)
Goal (master prompt): plan and purchase materials; material use updates the inventory; final costs and waste are
calculated correctly.
- [x] 7a: Domain: `Money` (cents, €), `Ledger` (bookings, no credit), `MaterialCatalog` (boards, fastener boxes,
  prorated values, purchase), `FastenerStock` (consumable screws), `ShoppingList` (priced bill of materials),
  `JobSettlement` (70 % pay + goal bonus, timber and screws used, profit), `WorkshopInventory` (loose timber by
  species / section, cut pieces, value), save format 2 with migration; 225 tests. Domain only, nothing to play-test
- [x] D-041 (inserted): in-game texts Turkish by default (English in the pause menu), red pilot-hole points; accepted (MT-021)
- [x] D-042 (inserted): drill shows the recommended clutch for the wood and screw; 233 tests, awaiting MT-022
- [x] 7b: Scene: catalog assets and order board at the delivery area, starting funds €100 + one box of screws, job
  sheet buys the material instead of free delivery, drill uses screws from the stock, money in the HUD, inventory
  screen (Tab), result with pay / cost / profit, economy saved. Code done, awaiting batch test MT-023 (D-043)

## Phase 8: Workshop Development (scope approved 2026-11-15, D-043) - DONE, batch accepted 2026-11-16 (D-044)
- [x] 8a: Tool wall with a hook per tool (hang back with E)
- [x] 8b: Equipment upgrades bought with money (saw, drill, lamp)
- [x] 8c: Sawhorse cutting station
- [x] 8d: Placeholder sounds generated in code (saw, drill, screw, pick up / place, ambience)

## Phase 9: Advanced Woodworking (scope approved 2026-11-15, D-043) - DONE, batch accepted 2026-11-16 (D-045)
- [x] 9a: Rip cut (lengthwise)
- [x] 9b: Hammer and nails (fastener type per joint)
- [x] 9c: Sanding and surface finish (quality goal)
- [x] 9d: Wood glue and dowels (clamping, drying time)

## Phase 10: Outdoor Construction (scope approved 2026-11-15, D-043) - DONE, batch accepted 2026-11-16 (D-046..D-048)
- [x] 10a: Outdoor area next to the workshop, site preparation
- [x] 10b: Terrace 2.4 × 1.8 m blueprint: pier blocks, beams, joists with connectors, decking; stage validation
- [x] 10c: Terrace job (material, pay, deposit), batch test list

## Phase 11: Larger Structures (plan approved 2026-11-16, D-049) - 11a-11d code done, interim test MT-027 pending
First project: a garden pergola 3 × 3 m. Owner's choices: stepladder for height, a spirit level tool for plumbing
posts, stage inspections at the end of each stage (block the next stage, affect pay). Test plan: interim test after
11a-11d (ladder and spirit level are new feel), then 11e.
- [x] 11a: Domain: plumb rule (posts lean 1-3° when set, plumb = within 0.5° both ways, brace only when plumb, only
  braced posts can be fastened or carry a beam), working height (2.1 m above the feet), save data; 263 tests
- [x] 11b: Stepladder: carried like a board, Space climbs onto it / down; reach +0.95 m; saved
- [x] 11c: Spirit level tool: reads the lean of the aimed post face, wheel pushes the post upright, LMB nails a
  temporary brace (visible battens, removed once the post's joints hold)
- [x] 11d: Pergola site, blueprint (4 piers + U post bases, 4 posts 100 × 100 × 2200, 2 beams, 5 rafters, angle
  brackets), job (€850, 50 % deposit), sawhorses, catalog entries
- [x] MT-027 finding (D-050): parts could hardly be set from the ladder (aim needed a collider, ladder too short):
  ghost aim, stepladder 2.0 m with the standing step at 1.45 m, carried at the side
- [ ] 11e: Stage inspections (customer checks each stage: parts, fastenings, plumb; problems block the next stage
  until fixed; first-time passes count for pay)
