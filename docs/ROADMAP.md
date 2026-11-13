# Roadmap Tracker

Phase definitions and Definitions of Done: [MASTER_PROMPT.md](MASTER_PROMPT.md) §12. This file tracks status only.

Legend: ✅ done (verified) · 🟡 in progress · ⬜ not started

| Phase | Name | Status |
|---|---|---|
| 0 | Environment and Project Foundation | 🟡 |
| 1 | First-Person Workshop Prototype | 🟡 |
| 2 | Basic Object Handling | ⬜ |
| 3 | Measurement Prototype | ⬜ |
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
- [ ] Owner confirms no unexplained Console errors
- [ ] Baseline scene saved (`Workshop.unity` generated and committed)
- [ ] SampleScene `GameManager` leftover resolved (see CURRENT_STATE known issues)

## Phase 1: First-Person Workshop Prototype
- [x] Test environment: ground, walls, workbench (generator written)
- [x] First-person movement, mouse look, gravity, collision (code written)
- [ ] Manual verification of movement / look / collisions
- [ ] HUD with UI Toolkit: crosshair
- [ ] Interaction raycasting (`PlayerInteractor`) + `IInteractable`
- [ ] Interaction prompt ("[E] ...")
- [ ] One basic interactable object (e.g. light switch or inspectable board)
- [ ] Pause and cursor management (move cursor logic out of `PlayerController`)
- [ ] Mouse sensitivity setting exposed beyond the Inspector
