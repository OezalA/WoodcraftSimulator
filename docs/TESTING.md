# Testing Workflow

Never record a test as passed unless it was executed and the result observed (master prompt §10).

## Verification levels
| Level | How | Who |
|---|---|---|
| Static inspection | Code review against installed package versions | Claude |
| Unity compilation | Focus the Unity Editor → it recompiles; check the Console, or `%LOCALAPPDATA%\Unity\Editor\Editor.log` for `error CS` | Owner triggers, Claude can read the log |
| EditMode tests | *Window > General > Test Runner > EditMode > Run All* | Owner (or Claude via batch mode when the Editor is closed) |
| PlayMode tests | Test Runner > PlayMode | Owner |
| Manual gameplay | Procedures below | Owner |

EditMode tests start in Phase 3 (assembly `WoodcraftSimulator.Tests.EditMode` under `Assets/_WoodcraftSimulator/Tests/EditMode/`).

Running tests from the command line (Editor must be **closed**, since a project can be open in only one Editor):
```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile -
```

## Manual test procedures

### MT-001: Build the workshop scene
1. Menu **WoodcraftSimulator > Build Test Workshop Scene**.
2. **Expected:** scene `Workshop` opens; Hierarchy contains `Sun`, `Environment` (Ground, Workshop/…), `Player` (with `PlayerCamera`). Console: `Workshop scene built and saved to …`.
3. **Failure → diagnosis:** menu missing → editor assembly failed to compile (Console errors). Pink objects → URP Lit shader not found (check the active render pipeline asset).

### MT-002: First-person movement
Precondition: MT-001. Press Play, click into the Game view.
| Step | Expected |
|---|---|
| Move the mouse | Camera turns; vertical look stops at ~85° up/down |
| W A S D | Walk ~2.5 m/s relative to view direction |
| Hold Shift + W | Faster (~4.5 m/s) |
| Space | Small jump (~0.5 m), lands back |
| Walk into walls / workbench | Blocked, no passing through |
| Walk out of the door | Steps down 10 cm slab onto the ground |
| Esc | Cursor visible, look stops, movement keys still work |
| Left click in Game view | Cursor captured again |

Failure → diagnosis:
- Console `Camera Transform is not assigned` → `PlayerController.cameraTransform` missing in the Inspector; rebuild the scene.
- `InvalidOperationException` / action not found → project-wide actions not assigned (*Project Settings > Input System Package*).
- Falling through the floor → CharacterController spawned inside a collider; check `Player` Y position.
- No mouse look → cursor not locked (click into Game view) or Game view not focused.
