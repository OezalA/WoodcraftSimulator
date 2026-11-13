# Testing Workflow

Never record a test as passed unless it was executed and the result observed (master prompt §10).

## Verification levels
| Level | How | Who |
|---|---|---|
| Static inspection | Code review against installed package versions | Claude |
| Unity compilation | Focus the Unity Editor → it recompiles; check the Console, or `%LOCALAPPDATA%\Unity\Editor\Editor.log` for `error CS` | Owner triggers, Claude can read the log |
| EditMode tests (in Unity) | *Window > General > Test Runner > EditMode > Run All* | Owner |
| EditMode tests (outside Unity) | `dotnet test tools/DomainTests` (domain tests only, D-015) | Claude / owner, Editor may stay open |
| PlayMode tests | Test Runner > PlayMode | Owner |
| Manual gameplay | Procedures below | Owner |

## EditMode tests
Assembly `WoodcraftSimulator.Tests.EditMode` in `Assets/_WoodcraftSimulator/Tests/EditMode/`, references only
`WoodcraftSimulator.Domain` + NUnit. Current suites: `LengthTests`, `TimberTests`.

Running them inside Unity from the command line (Editor must be **closed**: a project can be open in only one Editor):
```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile -
```

## Static compile check (no Editor needed)
`sh tools/static-build.sh` (Git Bash): builds copies of the Unity-generated `.csproj` files with all current
`.cs` files against the real Unity DLLs. See D-010 for what this does and does not prove.

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
| Esc | Pause menu opens (see MT-004) |

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
| Esc while looking at the switch | Pause menu opens, prompt disappears; E does nothing while paused |

Failure → diagnosis:
- No crosshair at all → `HUD` object missing or its `UIDocument` has no Panel Settings / Source Asset; Console errors from `GameplayHud`.
- Crosshair visible but prompt text invisible → theme missing on `GameplayPanelSettings` (no font).
- Prompt never appears → switch has no collider, or `PlayerInteractor` missing / camera not assigned (Console error).
- Prompt appears but E does nothing → check *Project Settings > Input System Package* actions contain `Player/Interact` bound to E.

### MT-004: Pause menu, cursor and mouse sensitivity
Precondition: rebuild the scene (MT-001). Press Play, click into the Game view.
| Step | Expected |
|---|---|
| Start of Play Mode | Cursor hidden and locked, game not paused |
| Esc | Dimmed overlay with panel: "Paused", Resume, Mouse sensitivity slider (0.02–0.5, shows 0.1 by default), Quit. Cursor visible |
| While paused: move mouse, W A S D, E | Camera and player do not move, no interaction |
| Click Resume | Menu closes, cursor locked, movement works |
| Esc, then Esc again | Menu opens, second Esc closes it |
| Esc, drag slider to ~0.3, Resume, move mouse | Noticeably faster looking |
| Stop and start Play Mode again, open menu | Slider still at ~0.3 (persisted) |
| While playing, click another Editor window (e.g. Inspector) | Game pauses, menu visible |
| Esc → Quit | Play Mode stops |

Failure → diagnosis:
- `InvalidOperationException ... Player/Pause` → the Input Actions asset did not reimport; right-click `InputSystem_Actions` > Reimport.
- Menu visible but buttons don't react to clicks → UI Toolkit doesn't receive pointer input; report it (an EventSystem
  with `InputSystemUIInputModule` may be required).
- Slider label unreadable / unstyled → theme missing on `GameplayPanelSettings`.
- Game stays frozen after exiting Play Mode → report (PauseController.OnDestroy resets timeScale).

### MT-005: Timber boards and species data
Precondition: Unity has compiled (Console clean). Rebuild the scene (MT-001).
| Step | Expected |
|---|---|
| Project window: `Assets/_WoodcraftSimulator/ScriptableObjects/WoodSpecies/` | 7 assets: Beech, Douglas_fir, Larch, Oak, Pine, Spruce, Walnut; Inspector shows id, names, density, color, data notes |
| *Window > General > Test Runner > EditMode > Run All* | 31 tests, all green (`LengthTests`, `TimberTests`) |
| Scene view / Hierarchy `Environment/Workshop/Timber` | 9 boards: 3 spruce stacked + larch + Douglas fir on the floor along the left wall; pine, oak, walnut, beech on the bench; different colors per species |
| Select `Spruce_2400x95x45_1` | Transform scale (1, 1, 1); BoxCollider size (0.095, 0.045, 2.4); Rigidbody mass ≈ 4.617 |
| Play, walk to the boards | Boards rest still (no jitter, no sinking, no falling through); player cannot walk through them but can step over the floor stack |

Failure → diagnosis:
- Console `TimberBoard ... wood species is not assigned` → species assets missing; rebuild the scene.
- Pink boards → `Lumber` material missing/invalid; rebuild the scene.
- Boards visible in the Scene view but invisible/inside-out in Play → mesh winding issue in `BoxMeshBuilder`; report it.
- Test Runner shows no tests → test assembly not compiled (check Console) or *UNITY_INCLUDE_TESTS* not defined.

## Tips for visual checks in the Editor
- Game view `Scale` slider must be at **1x**; higher values zoom (upscale) the image and make 3D and UI text blurry.
- Use a fixed resolution (Game view resolution dropdown, e.g. *Full HD (1920x1080)*) or *Play Maximized*
  so UI scaling matches the 1080p reference of `GameplayPanelSettings`.
