# Current State

_Last updated: 2026-11-13_

## Current milestone
Phase 0 ✅ done. Phase 1 (First-Person Workshop Prototype) in progress.

## Completed
- Git repository (`main`) with Unity `.gitignore`, `.gitattributes` and Git LFS.
- Template tutorial content and `SampleScene` removed.
- Folder structure `Assets/_WoodcraftSimulator/` with runtime and editor assembly definitions.
- `PlayerController`: first-person controller (CharacterController + Input System).
- `WorkshopSceneBuilder` editor menu; generated baseline scene `Scenes/Workshop/Workshop.unity` committed.
- Documentation set: README, MASTER_PROMPT, ARCHITECTURE, ROADMAP, DECISIONS, TESTING, CURRENT_STATE.

## Latest verification
| What | Result | Date |
|---|---|---|
| Unity compilation (both assemblies) | Succeeded, Console clean (Editor.log + owner) | 2026-11-13 |
| MT-001 Build workshop scene | Passed (owner) | 2026-11-13 |
| MT-002 First-person movement | Passed (owner): look, WASD, collisions, exit through door work; feel "fena değil" | 2026-11-13 |

## Known issues / limitations
- Build Settings scene list still points to the deleted `SampleScene`; owner to replace it with `Workshop.unity`
  via *File > Build Profiles > Scene List* (Editor-side change, not edited on disk while the Editor is open).
- No crosshair yet (UI Toolkit HUD planned, see D-008).
- Cursor lock lives temporarily in `PlayerController`.
- Mouse look tuned for mouse only; gamepad right stick would be far too slow.

## Next recommended action
Phase 1 interaction system: `IInteractable` + `PlayerInteractor` (camera raycast) + UI Toolkit HUD
(crosshair + prompt) + one test interactable.
