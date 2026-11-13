# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 0 (finishing) → Phase 1 (started).

## Completed
- Git repository (`main`) with Unity `.gitignore`, `.gitattributes` and Git LFS.
- Template tutorial content removed (`Assets/TutorialInfo`, `Assets/Readme.asset`).
- Folder structure `Assets/_WoodcraftSimulator/` with runtime and editor assembly definitions.
- `PlayerController` rewritten as a first-person controller (CharacterController + Input System).
- `WorkshopSceneBuilder` editor menu generating the greybox workshop.
- Documentation set: README, MASTER_PROMPT, ARCHITECTURE, ROADMAP, DECISIONS, TESTING, CURRENT_STATE.

## Latest verification
| What | Result | Date |
|---|---|---|
| Unity compilation of `WoodcraftSimulator` + `WoodcraftSimulator.Editor` after restructure | Succeeded, no `error CS` (Editor.log); owner to confirm Console is clean | 2026-11-13 |
| MT-001 Build workshop scene | Not yet executed | - |
| MT-002 First-person movement | Not yet executed | - |

## Known issues / limitations
- `Assets/Scenes/SampleScene.unity` still contains the test `GameManager` object with `PlayerController` attached.
  The controller now requires a CharacterController and a camera reference, so playing SampleScene logs
  `Camera Transform is not assigned`. Proposed fix: delete SampleScene (owner approval needed).
- No crosshair yet (uGUI draft removed in favor of UI Toolkit, see D-008).
- Cursor lock lives temporarily in `PlayerController`.
- Mouse look tuned for mouse only; gamepad right stick would be far too slow (no deltaTime scaling).
- Build Settings still list only `SampleScene`; add `Workshop.unity` manually when building.

## Next recommended action
1. Owner: focus Unity, confirm no Console errors, run MT-001 and MT-002 (docs/TESTING.md).
2. Commit the generated `Workshop.unity` + greybox materials as the Phase 0 baseline.
3. Phase 1 next coding task: interaction system: `IInteractable` + `PlayerInteractor` (raycast from camera,
   ~2 m reach) + UI Toolkit HUD (crosshair + "[E] …" prompt) + one test interactable.
