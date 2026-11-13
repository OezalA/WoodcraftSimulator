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

## Static compile check (Claude, no Editor needed)
Copy `WoodcraftSimulator.csproj` and `WoodcraftSimulator.Editor.csproj` to a temp folder, replace the
`<Compile Include>` items with all current `.cs` files (absolute paths), make `<HintPath>Library\…` absolute,
replace `ProjectReference Unity.InputSystem.csproj` with a `Reference` to `Library/ScriptAssemblies/Unity.InputSystem.dll`,
then `dotnet build WoodcraftSimulator.Editor.csproj`. See D-010 for what this does and does not prove.

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

### MT-003: Interaction, HUD and light switch
Precondition: rebuild the scene (MT-001). Press Play, click into the Game view.
| Step | Expected |
|---|---|
| Look around | Small white dot (crosshair) in the screen center |
| Walk to the right end of the workbench, look at the small plate on the back wall | Crosshair grows and turns warm yellow; prompt `[E]  Turn off lamp` below it |
| Press E | Warm spot light over the workbench turns off; prompt changes to `Turn on lamp` |
| Press E again | Light on again, prompt `Turn off lamp` |
| Step back > 2 m or look away | Prompt disappears, crosshair back to small white dot |
| Esc while looking at the switch | Prompt disappears (no interaction while cursor released) |

Failure → diagnosis:
- No crosshair at all → `HUD` object missing or its `UIDocument` has no Panel Settings / Source Asset; Console errors from `GameplayHud`.
- Crosshair visible but prompt text invisible → theme missing on `GameplayPanelSettings` (no font).
- Prompt never appears → switch has no collider, or `PlayerInteractor` missing / camera not assigned (Console error).
- Prompt appears but E does nothing → check *Project Settings > Input System Package* actions contain `Player/Interact` bound to E.
