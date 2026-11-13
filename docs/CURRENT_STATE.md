# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 1 ✅ done (MT-001..MT-004 passed). Next: Phase 2 (Basic Object Handling).
Branch: `feature/phase-1-pause` (based on `feature/phase-0-1-foundation`).

## Completed
- Phase 0: Git + LFS, folder structure, asmdefs, docs, baseline workshop scene. GitHub: https://github.com/OezalA/WoodcraftSimulator
- First-person controller (`PlayerController`), greybox workshop generator (`WorkshopSceneBuilder`).
- Interaction system: `IInteractable`, `PlayerInteractor`, UI Toolkit HUD (`GameplayHud`), `LightSwitch` + lamp.

## Done: pause system
- `PauseController` (Esc / gamepad Start, focus loss → pause; timeScale 0; cursor; disables player components).
- `PauseMenu` (UI Toolkit): Resume, mouse sensitivity slider, Quit. `UserPreferences` (PlayerPrefs).
- New input action `Player/Pause`. Temporary cursor code removed from `PlayerController` / `PlayerInteractor`.
- Static compile check: 0 errors, 0 warnings (2026-11-13). Unity compilation clean; MT-004 passed (owner).

## Latest verification
| What | Result | Date |
|---|---|---|
| MT-001 Build workshop scene | Passed (owner) | 2026-11-13 |
| MT-002 First-person movement | Passed (owner) | 2026-11-13 |
| MT-003 Interaction + HUD + light switch | Passed (owner) | 2026-11-13 |
| MT-004 Pause menu | Passed (owner) | 2026-11-13 |

## Known issues / limitations
- Build Settings: `Workshop` enabled; deleted `SampleScene` still listed (disabled, harmless).
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.
- In-game text is English only.

## Next recommended action
1. Open PR(s) into `main` when convenient.
2. Phase 2 (Basic Object Handling): wood board with basic material data,
   pick up / carry / rotate / place with collision-aware placement.
