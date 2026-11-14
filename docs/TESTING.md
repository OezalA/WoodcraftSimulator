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
| Step back > 2.5 m or look away | Prompt disappears, crosshair back to small white dot |
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
- Game view `Scale` above 1x zooms (upscales) the image and blurs 3D and UI text; see "Game view setup" below.
- Prefer **16:9 Aspect** or *Play Maximized*; a fixed resolution larger than the window gets cropped (see below).


### MT-006: Pick up, carry, rotate and place boards
Precondition: rebuild the scene (MT-001). Press Play.
| Step | Expected |
|---|---|
| Look at the top spruce board on the floor stack (≤ 2.5 m from the eyes, ~1.9 m horizontally) | Prompt `[E]  Pick up Spruce 2400 × 95 × 45 mm (4.6 kg)` |
| Press E | Board held in front of you, level; prompt changes to `[LMB] Place  [R] Rotate  [T] Tilt` |
| Look at the floor 1–3 m away | Translucent **green** preview of the board lying on the floor |
| Look at a spot where the preview overlaps a wall, the bench or the other boards | Preview turns **red**; left click does nothing |
| Look at the floor right at your feet | Red (would overlap the player) |
| Look at a wall or the sky | No preview; left click does nothing |
| R / T | Preview and held board turn 90° (R: around vertical; T: board flat ↔ on edge) |
| Left click on a green spot | Board placed exactly at the preview pose; prompt returns to normal |
| Pick the board up again and place it on the workbench or on top of another board | Works when green; placed board stays still (no sliding) |
| Look at the light switch while carrying | No `[E]` prompt (hands full) |
| Esc while carrying, then Resume | Preview hidden while paused; clicking Resume does not place the board |
| Inspector of a placed board | Same dimensions and mass as before (dimensions never change) |

Failure → diagnosis:
- `InvalidOperationException ... Player/Place` → Input Actions not reimported (right-click `InputSystem_Actions` > Reimport).
- Preview pink / opaque → `PlacementPreview` material not transparent; select it once in the Inspector or rebuild the scene.
- Always red on open floor → something overlaps the test box (check with Scene view gizmos); report it.
- Prompt missing on boards → board lacks `Carryable` or collider; rebuild the scene.

### MT-007: Timber rack
Precondition: rebuild the scene (MT-001). Press Play.
| Step | Expected |
|---|---|
| Look at the left wall | Steel-grey rack with 3 wooden shelves (30 / 90 / 150 cm); 3 spruce boards on the lowest shelf, larch + Douglas fir on the middle one, top shelf empty |
| Pick up a spruce board from the lowest shelf (look under the middle shelf; aiming next to a board is enough thanks to aim assist) | Prompt and pick-up work |
| Aim at the empty top shelf | Green preview lying on the shelf |
| Aim so the preview would hit an upright or another board | Red, cannot place; HUD: `Blocked by Upright_…` / `Blocked by <board name>` |
| Hold a 2.4 m board lengthwise (R) and aim at the floor just in front of you | Red, HUD `Blocked: too close to you`; aim further away or press R → green |
| Aim at a wall / the sky while carrying | No preview, HUD `Aim at a free, level surface within reach` |
| Place it on the top shelf, then on the floor, then back on the rack | All work; boards stay still |
| Workshop floor | Free of boards (they now live on the rack) |

Failure → diagnosis:
- Lowest shelf boards unreachable → report the position you stood at (reach is 2.5 m from the eyes).
- Preview always red on a shelf → the shelf above may be too close for the board's orientation (try T); otherwise report.

### MT-008: Tool belt (tape measure + pencil)
Precondition: rebuild the scene (MT-001). Press Play.
| Step | Expected |
|---|---|
| Start | Belt bar at the bottom: `1 —  2 —  3 —  4 —` (all empty, dimmed) |
| Look at the yellow box at the front-left of the workbench | `[E]  Take Tape measure` |
| Press E | Tape measure disappears from the bench and appears in your hand (lower right); slot 1 shows "Tape measure", highlighted |
| Take the red pencil | Goes to slot 2 and into your hand; the tape goes onto the belt (D-027) |
| Press 1 / 2 / 2 | Tape in hand / pencil in hand / hands empty (slot 2 pressed again holsters) |
| Press 3 (empty slot) while holding something | Hands empty |
| With a tool in hand, pick up a board (E) | Tool disappears (holstered), board in hands |
| Put the board down (LMB) | The same tool comes back into your hand (D-027) |
| While carrying, press 1 / 2 | Nothing happens (hands full) |
| Esc while holding a tool | Pause works; after Resume the tool is still in hand |
| EditMode tests | 44 tests, all green (new: `ToolBeltTests`) |

Failure → diagnosis:
- `InvalidOperationException ... Player/ToolSlot1` → Input Actions not reimported (right-click `InputSystem_Actions` > Reimport).
- No belt bar → HUD lacks the `toolBelt` reference or `belt` element; rebuild the scene; check Console.
- Pencil hard to target → its grab area is ≥ 5 cm (D-026); rebuild the scene if it is not.

### MT-009: Tape measure + precision look
Precondition: rebuild the scene (MT-001), take the tape measure (MT-008) and hold it (key 1).
| Step | Expected |
|---|---|
| Hold RMB anywhere | View zooms in smoothly (~25° FOV); mouse look slower; release → back to normal |
| Aim at a spruce board on the rack | Yellow tape blade drawn on the board's top face from the end nearest to you to the crosshair; readout above the crosshair, e.g. `623 mm   (board 2400 mm)` |
| Move the aim along the board | Reading changes in 1 mm steps; blade follows |
| Aim beyond the board's far end (up to ~60 cm past it) | Reading stays at the full length (2400), blade to the end |
| Let the aim slip slightly beside the board (≤ ~25 cm) | Reading continues (sticky hook, D-026) |
| Aim at the wall / floor | Readout and blade disappear |
| Walk to the other end of a board and aim at it again (look away first) | Hook moves to the end now nearest to you |
| Hold RMB while measuring | Exact millimetres are easy to hit (e.g. 900 mm) |
| Press 1 (holster) / Esc | Readout and blade disappear |
| EditMode tests | 68 tests, all green (new: `TapeMeasureTests`, `Length.RoundTo/Clamp`) |

Failure → diagnosis:
- `InvalidOperationException ... Player/PrecisionLook` → Input Actions not reimported.
- No readout, no blade → tape lacks `TapeMeasureTool`/`TapeBlade` (rebuild scene) or the board has no `TimberBoard`.
- Blade pink → `TapeBlade` material missing; rebuild the scene.

## Game view setup (corrects the earlier tip)
A fixed resolution such as *Full HD (1920x1080)* at `Scale 1x` **crops** the image when the Game view window is
smaller than 1920×1080: the bottom (tool belt bar) and the lower right (tool in hand) are cut off.
Use instead: resolution dropdown → **16:9 Aspect** (the view always fits the window), Scale slider at its leftmost
value; or, outside Play Mode, set **Play Maximized** in the Play Focused dropdown (or press Shift+Space over the Game view).
