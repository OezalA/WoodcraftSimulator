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

### MT-010: Pencil marks and tolerance (Phase 3c)
Precondition: stop Play, rebuild the scene (MT-001). Press Play. Game view: 16:9 Aspect, Scale 1x.
| Step | Expected |
|---|---|
| Look at the white sheet at the front-left of the bench | `[E]  Change target   (now: 900 mm)` |
| Press E a few times | "now" cycles 1200 → 300 → 450 → 600 → 900 mm; the pencil readout shows the same target |
| Take only the pencil (no tape yet), aim at a board | Readout `Take a tape measure to measure where to mark`; LMB does nothing |
| Take the tape measure, then press the pencil's slot key | Pencil in hand |
| Aim at the pine board on the bench | Tape blade from the near end; readout `412 mm   target 900 mm` |
| Hold RMB, bring the reading to 900, click LMB | Dark line across the board at the crosshair; second line e.g. `Marked at 899.7 mm  (-0.3 mm)  OK ±1 mm` |
| Mark clearly off target (e.g. at 905) | `... (+5.x mm)  OUT OF TOLERANCE ±1 mm` |
| Aim slightly past the board's end (sticky hook) and click | `Aim at the board to draw a line`, no mark |
| Pick the marked board up, carry it, put it down | Lines move with the board |
| Esc → Resume with the pencil in hand (click Resume) | No line is drawn by the Resume click |
| Switch to the tape (its slot key) | Tape works as before (MT-009) |
| EditMode tests | 89 tests, all green (new: `ToleranceTests`, `BoardMarksTests`, `ToolBelt.Contains`) |

Failure → diagnosis:
- `InvalidOperationException ... Player/UseTool` → Input Actions not reimported (focus Unity once, wait for import).
- No line visible but the result text appears → board lacks the `PencilMark` material reference; rebuild the scene.
- Nothing happens with the pencil → the pencil lacks `PencilTool` (rebuild the scene).

### MT-011: Hand saw crosscut (Phase 4b)
Precondition: stop Play, rebuild the scene (MT-001). Press Play. Take the tape measure, pencil and hand saw (grey
blade at the front of the bench; 3 belt slots).
| Step | Expected |
|---|---|
| With the pencil, mark 300 mm on the pine board (1000 mm) on the bench (MT-010) | Line + `Marked at ...` |
| Take the saw in hand, aim at the board away from any line | `No pencil line here: mark the cut first` |
| Aim near the line (within ~3 cm) | Orange cut line across the board right next to the pencil line (on the waste side); `Hold LMB to saw at 300 mm` (or the exact mark) |
| Hold LMB | Saw moves back and forth; `Sawing  NN%` counts up (~1.5 s for 200 × 18 mm) |
| Release halfway, then hold again | Continues from the same percentage |
| Finish | The board becomes two boards with a 2 mm gap; readout `Cut: 300 mm + 698 mm   (kerf 2 mm, sawdust 7.2 cm³)` (lengths depend on your mark) |
| Look at each piece | Description shows its new length, e.g. `Pine 300 × 200 × 18 mm (...)` |
| Pick up and place both pieces | Carry/place works; other pencil lines on them moved along |
| Cut one of the pieces again | Works; second line `Total sawdust ... cm³ in 2 cuts` |
| Mark 3 mm from an end and try to saw there | `Too close to the end: pieces must be at least 5 mm`; nothing happens |
| Esc → Resume (click) with the saw in hand while aiming at a line | No sawing starts by the click |
| EditMode tests | 108 tests, all green (new: `CrosscutTests`) |

Failure → diagnosis:
- `InvalidOperationException ... Player/UseTool` → Input Actions not reimported.
- Saw has no readout → saw lacks `HandSawTool` / `SawDefinition` (rebuild the scene); see Console for the error.
- Pieces overlap or jump → report the board name and where it was lying; the piece positions come from `KerfStart/EndFromStart`.

### MT-012: Falling offcuts, unsupported boards, cut vs target (Phase 4c)
Precondition: stop Play, rebuild the scene (MT-001). Press Play. Take tape measure, pencil and saw.
| Step | Expected |
|---|---|
| Pick up the pine board (1000 mm), press R so it points away from the bench, place it with the crosshair ~20 cm behind the bench's front edge | About half of the board sticks out over the front edge; it stays (its center is on the bench) |
| Mark ~250 mm from the overhanging end and saw there | The overhanging offcut drops to the floor (may tip); the other piece stays on the bench |
| Pick up the offcut from the floor | Works as usual |
| Place a small board (walnut) on top of another board (oak), then pick up the oak board | The walnut board drops onto the bench instead of floating |
| Set the cut list to 600 mm, mark 600 mm and saw | `Cut: 600 mm (target 600 mm, OK ±1 mm) + ...` |
| Mark clearly off target (e.g. 610 mm) and saw | `Cut: 610 mm (target 600 mm, OUT OF TOLERANCE ±1 mm) + ...` |
| Saw a board lying fully on the bench | Both pieces stay exactly where they were (no jump) |
| EditMode tests | 112 tests, all green |

Failure → diagnosis:
- A piece keeps jittering or sliding → note where it lies; settling ends after 0.3 s at rest (6 s at most).
- A piece falls through the floor or bench → report it with the board size.

### MT-013: Assembly table, snapping parts into the frame (Phase 5b)
Precondition: stop Play, rebuild the scene (MT-001). Press Play.
Material: cut from the spruce boards (rack, bottom shelf, 2400 × 95 × 45): 2 × 600 mm and 2 × 210 mm (MT-011), plus
one wrong piece, e.g. 225 mm.
| Step | Expected |
|---|---|
| Look at the new table on the right side of the room | Light-blue translucent frame 600 × 300 mm: two long rails, two short sides, boards on edge |
| Carry a 600 mm piece and aim at the table close to the front rail ghost | Green preview snaps exactly into the ghost; HUD `Front rail: fits` |
| Click LMB | The piece sits in the frame, its ghost disappears |
| Carry the 225 mm piece to a side ghost | Red preview in the slot; `Doesn't fit Left side: needs 210 × 95 × 45 mm, this one 225 × 95 × 45 mm`; LMB does nothing |
| A piece 1–5 mm off (e.g. 212 mm) | Green, `Left side: fits, but 2 mm too long (inaccurate)` |
| Aim at the table away from the ghosts | Normal free placement on the table works as before |
| Look at an installed piece | `[E]  Take Spruce 600 × 95 × 45 mm ... out of the Frame 600 × 300 mm` → E picks it up, the ghost reappears |
| Fill all four slots | Frame complete visually (screws come in 5c) |
| With the saw, aim at a pencil line on an installed piece | `Take it out of the Frame 600 × 300 mm to cut it` |
| EditMode tests | 131 tests, all green (new: `AssemblyBuildTests`) |

Failure → diagnosis:
- No ghosts → the table lacks `AssemblyStation` / blueprint asset (rebuild the scene); Console error names it.
- Snap never happens → aim closer to the ghost (within 8 cm of its box); report where you aimed.

### MT-014: Fine marking with the mouse wheel (D-034)
Precondition: stop Play, rebuild the scene (MT-001). Take tape measure and pencil, pencil in hand.
| Step | Expected |
|---|---|
| Aim roughly at ~600 mm on a spruce board | Readout e.g. `597 mm   target 900 mm` + `Wheel: ±1 mm   Shift + wheel: ±10 mm`; blue preview line across the board at the reading, tape blade up to it |
| Scroll the wheel up / down one notch | Reading +1 / −1 mm, `(wheel)` appears; the blue line moves 1 mm; small mouse movements don't change the reading |
| Hold Shift and scroll | ±10 mm per notch |
| Bring it to exactly 600 and click LMB | Dark line exactly where the blue preview was; `Marked at 600 mm  (0 mm)  OK ±1 mm` (with target 600) |
| Move the aim clearly along the board (> 2 cm) | `(wheel)` disappears, the mouse sets the reading again |
| Scroll at the far end of the board | Stops at the board's length; scroll down stops at 0 |
| Without scrolling, click | Line on the graduation shown (whole mm) |
| EditMode tests | 143 tests, all green (new: `TapeMeasure.PositionFromStart` / `Nudge`) |

Failure → diagnosis:
- `InvalidOperationException ... Player/AdjustTool` → Input Actions not reimported.
- Wheel does nothing → Console errors? Report whether `(wheel)` appears.

### MT-015: Screwing and completing the frame (Phase 5c)
Precondition: stop Play, rebuild the scene (MT-001). Press Play. Parts as in MT-013 (2 × 600, 2 × 210 mm; optionally
one side 1–5 mm off to see an "inaccurate" line). Belt: tape, pencil, saw, drill-driver (teal box on the assembly
table) = 4 slots.
| Step | Expected |
|---|---|
| Install the front rail and the left side | Two small orange squares appear on the front rail's outer face at the left corner |
| Take the drill-driver in hand, aim at the table | `Frame 600 × 300 mm: parts 2/4, screws 0/8` + `Aim at an orange screw point` |
| Aim at an orange square | `Hold LMB to drive a Wood screw 5 × 80 mm` |
| Hold LMB | Driver pushes forward, `Driving NN%`; after ~1 s a grey screw head replaces the orange square; `screws 1/8` |
| Look at the front rail / left side (E prompt) | `Screwed into the Frame 600 × 300 mm`; E does nothing |
| Look at the other installed parts | Still `Take ... out of ...` while no screw is in them |
| Install all parts and drive all 8 screws | Report on the right for ~20 s: `Frame 600 × 300 mm: COMPLETE`, parts 4/4 with each length and OK / inaccurate, `Fasteners 8/8`, sawdust so far, safety note; drill readout `Frame ... complete: see the report` |
| Aim at the finished frame with the drill | `Frame 600 × 300 mm is complete` |
| EditMode tests | 145 tests, all green (new: `ReportText_*`) |

Failure → diagnosis:
- No orange squares → both parts of that corner must be installed; else the station lacks the marker material (rebuild).
- Driving does nothing → the drill lacks `DrillDriverTool` / fastener asset (rebuild); Console error names it.

### MT-016: Save and continue (S2: boards and player)
Precondition: stop Play, rebuild the scene (MT-001). Press Play. Save file: `%USERPROFILE%\AppData\LocalLow\<Company Name>\WoodcraftSimulator\workshop.json`.
| Step | Expected |
|---|---|
| Esc | Pause menu shows `Save`, a status line `New workshop (no save yet)`, `New workshop`, `Quit` |
| Cut a board into two pieces, draw a few pencil lines, carry one piece somewhere else, walk to another spot | - |
| Esc → Save | Status `Saved HH:MM`; the JSON file exists (readable text) |
| Stop Play, press Play again | Status `Loaded save from ...`; you stand where you saved, looking the same way; the cut pieces with their lines are where you left them; the original uncut board is gone |
| Quit via Esc → Quit (or stop Play) after changing something, then Play | The change is there (auto-save on quit) |
| Save while carrying a board, stop, Play | The board is where it was in front of you and drops to the floor |
| Esc → New workshop | Button asks `Really start over? Click again`; closing the menu cancels |
| Click it again | The scene restarts as freshly built; status `New workshop (no save yet)` |
| Corrupt the JSON file (e.g. delete half of it) while Play is stopped, then Play | Fresh workshop, status `Save could not be loaded (kept aside, see Console)`; Console warning lists the problems; the file was renamed to `workshop.invalid-<time>.json` |
| EditMode tests | 174 tests, all green |

### MT-017: Save and continue (S3: tools, belt, assembly, waste, cut list, lamp)
Precondition: MT-016 works; rebuild the scene once more after S3 (the tools, table, switch and cut list need their ids).
Esc → New workshop for a clean start.
| Step | Expected |
|---|---|
| Take tape, pencil and saw (belt slots 1–3), select the pencil (key 2) | Pencil in hand |
| E on the cut list until `now: 450 mm`; turn the lamp off at the switch | - |
| Make one cut (note the sawdust value in the saw's result line) | - |
| Install two or three frame parts on the assembly table, drive at least one screw | Screw head visible |
| Esc → Save, stop Play, press Play | Belt shows the same three tools in the same slots, **pencil in hand**; the drill is still on the table; cut list `now: 450 mm`; lamp off |
| Look at the assembly table | The installed parts are in their slots (no ghost there), the screw head is there, the orange squares only where still open; a screwed part can't be taken out, an unscrewed one can |
| Finish the frame, check the report | The sawdust total includes the cut made before saving |
| Save with the finished frame, stop, Play | Frame complete on the table; the report does **not** pop up again |
| Carry a board with the saw in hand, Esc → Save, stop, Play | Board drops in front of you; **saw in hand** |
| Console | No warnings from `SaveGameService` / `AssemblyStation` |
| EditMode tests | 174 tests, all green |

### MT-018: Free saw placement and stroke sawing (D-038 T1, T2) - OBSOLETE: reverted, the saw works as in MT-011 again
Precondition: stop Play, rebuild the scene (new job board, drill bits, saw settings). Esc → New workshop.
| Step | Expected |
|---|---|
| Mark a line on a spruce board, take the saw, aim near the line | Kerf line follows the aim, not snapped; HUD `Piece ... (line ..., ...)` says long/short and which side |
| Mouse wheel up/down | Blade moves 0.5 mm per notch (Shift: 5 mm); moving the mouse more than 3 mm gives control back |
| Hold LMB without moving the mouse | Nothing cuts; the view is locked (no turning, no walking) |
| Hold LMB, move the mouse slowly forward/back | `Sawing NN%`, first `Starting: ...`, later `In the kerf ...`; the saw moves with the mouse |
| While starting, move the mouse sideways / make very fast strokes | `blade wandered X mm` / `The saw jumped!`; the kerf line moves |
| Release LMB mid-cut, look around, aim back at the board | `Cut NN%: hold ... to continue`; continues in the same kerf |
| Finish the cut | Two pieces; result line with the measured length vs target / line |
| EditMode tests | 209 tests, all green |

### MT-019: Pilot holes, clutch and screw depth (D-038 M1, M2)
Precondition: stop Play, rebuild the scene, Esc → New workshop. Parts installed on the assembly table (free play: the frame).
| Step | Expected |
|---|---|
| Drill in hand | `Driver bit, clutch 12   [Wheel] clutch   [Q] Drill bit 3 mm` |
| Wheel | Clutch changes 1..20, then `drill (no clutch)` |
| Q | `Changing to the drill bit...` for about 1 s |
| Drill bit: hold LMB on an orange point | `Drilling NN%`, then `Pilot hole drilled`; a small dark hole appears |
| Driver bit, clutch 1-3: hold LMB long on a point | Stops part-way: `Clutch slipping: head NN mm out`; release: `Screw in, but head ... out` |
| Hold LMB on that screw again with a higher clutch | It is driven further |
| Frame (5 × 80): clutch about 9-14; shelf (4 × 40): about 5-7; hold until it stops | `Screw in: head flush` |
| Clutch 20 or drill mode, keep holding | The head sinks: `Screw driven too deep` |
| Screw near a board end **without** a pilot hole | `The board split!`; a dark crack line on the board |
| Release very early (less than 3 mm in) | `Screw not set` |

### MT-020: First complete job: the small shelf (Phase 6)
Precondition: MT-019. Esc → New workshop (empty assembly table).
| Step | Expected |
|---|---|
| Look at the right sheet on the job board (back wall, left of the bench) | Top left: offer `Small shelf`, `Material: 2 x Spruce board 2000 × 145 × 18 mm`, cut plan per board, 12 screws |
| E | 2 boards appear on the floor in front of the rack; the table shows the shelf ghosts; top left: job sheet with timer and cut list `2 x 600 mm ...`, `3 x 464 mm ...` |
| E on the bench cut list note | Cycles `600 mm, 2 x Left side, Right side` / `464 mm, 3 x ... shelf` |
| Measure, mark, cut 2 × 600 and 3 × 464 | The job sheet lines count `ready` pieces |
| Install the left side, then the shelves (a shelf first: `place left first`), then the right side | Lines count `in` |
| Pre-drill the top/bottom joints, drive all 12 screws | After the last screw: result on the right (parts, heads, splits, material, time, 4 goals) |
| Save, stop, Play | Job, time and result are still there |
| E on the shelf sheet | `Deliver the Small shelf`: the shelf disappears, the job sheet is gone, the cut list note is back to practice targets |
| Accept the practice frame | Works the same with 95 × 45 stock; its corner screws need pilot holes |
| Ruin a part, E on the active sheet | One more board is delivered; the result shows `1 ordered extra` |

### MT-021: Turkish texts and red pilot-hole points (D-041)
Precondition: stop Play, let Unity compile, rebuild the scene (this also fills the Turkish names of the assets),
Esc → New workshop.
| Step | Expected |
|---|---|
| Play | All texts in Turkish: prompts (`[E]  Metre al`), tool readouts, job board, job sheet, pause menu. Turkish letters (ç, ğ, ı, İ, ö, ş, ü) render correctly, no empty boxes |
| Esc | Menu: `Duraklatıldı`, `Devam et`, `Kaydet`, `Fare hassasiyeti`, `Language: English`, `Yeni atölye`, `Çıkış` |
| Accept the shelf job, install the left side, the bottom and the middle shelf | Bottom joint: both points **red** (9 mm from the side's end); middle joint: orange |
| Drill (driver bit) aimed at a red point | Extra line: `Kırmızı nokta: tahta ucuna yakın, önce pilot delik aç ([Q] matkap ucu), yoksa tahta çatlar` |
| Q, drill the red point | `Pilot delik açıldı: vida ucuna geç [Q]`; the point turns orange |
| Esc → `Language: English` | The workshop is saved and reloaded in English (job, boards and screws unchanged); the button now reads `Dil: Türkçe` |
| `Dil: Türkçe` | Back to Turkish |

### MT-022: Recommended clutch (D-042)
Precondition: let Unity compile (no scene rebuild needed). Shelf job accepted, a side and a shelf installed.
| Step | Expected |
|---|---|
| Driver bit on an orange point | Extra line `Önerilen tork 5-7 (Ladin, Ahşap vidası 4 × 40 mm): şu an 12, fazla, baş gömülür` |
| Wheel down to 6 | The line changes to `şu an 6, uygun`; holding LMB stops the screw flush |
| Wheel down to 3 | `şu an 3, az, vida yarıda kalır` |
| Red point | Only the pilot-hole warning; after drilling, the clutch advice appears |
| Practice frame (5 × 80 mm) | Recommended `9-14` |

## Batch test up to Phase 10 (D-043)
One session for everything built after the owner's last test (86821c5). Before starting: Unity *Preferences >
General > Script Changes While Playing > Recompile After Finished Playing*; stop Play, let Unity compile (no red
Console errors), **Build Test Workshop Scene**, Play, Esc → `Yeni atölye`. Report per test: OK, or what you saw.

### MT-023: Money, buying, consumable screws, inventory (Phase 7b, D-040)
| Step | Expected |
|---|---|
| Play | Top right: `Para €100.00   [Tab] envanter` |
| Tab | Inventory panel: money, timber grouped by species and section (uncut boards / cut pieces, value), `200 x Ahşap vidası 4 × 40 mm`, `50 x Ahşap vidası 5 × 80 mm`; Tab closes it |
| Order board (back wall, right of the bench), aim at a sheet | `Satın al: Ladin tahta 2000 × 145 × 18 mm   €8.50   (para €100.00)` |
| E | Money €91.50; a board appears in the delivery area (floor in front of the rack); a second purchase lies next to it, not on top |
| Job board: aim at the shelf sheet | Offer shows `Atölyede / alınacak:` lines; prompt `İşi kabul et: Küçük raf   (ücret €60.00; alınacak malzeme €8.50, ...)` (one board already lies there) |
| E | One more board is bought and delivered; the job starts |
| Build the shelf; each screw | The screw count in the inventory goes down by one per screw (`Vida tutmadı` = none used) |
| Complete the shelf | Result ends with `Ücret: ... / €60.00 (teslim €42.00 + hedefler ...)`, `Kullanılan malzeme: ahşap ..., vida ...`, `Kâr: ...` |
| E on the shelf sheet | `Teslim edildi: +€... (kâr €...)`; money goes up by the pay |
| Buy screws until money is short / drive with 0 screws (optional) | `Para yetmiyor` / `... kalmadı: sipariş panosundan bir kutu al` |
| Save, quit Play, Play | Money, screws and bought boards are the same |

### MT-024: Tool wall, upgrades, sawhorses, sounds (Phase 8, D-044)
| Step | Expected |
|---|---|
| Look at the wall above the workbench | Board with four light outlines; aiming at one: `Metre yeri` etc. |
| Take the tape measure, aim at its outline | `[E]  Metre duvara as`; E: it hangs upright on the wall, the belt slot is empty; E on it takes it again |
| Order board, lower row | `Yükseltme: Japon çekme testere, %70 daha hızlı keser   €45.00 ...`; buying it: money down, sheet now `Sende var: ...` |
| Saw a board with the pull saw | Noticeably quicker than before |
| Buy the LED lamp | The bench light is brighter; the switch still turns it on/off |
| Brushless drill | Pilot holes and screws go in quicker; the recommended clutch is unchanged |
| A board lying on the floor, saw on a pencil line | `Yerde kesilmez: tahtayı tezgaha ya da sehpalara koy` |
| Lay a long board across the two sawhorses (left of the bench), saw it | It cuts; the pieces rest on the horses or fall off the outer ends |
| Sounds | Saw strokes while sawing, drill motor while drilling/driving, a click when a screw seats, a knock when picking up/putting down, tones on buying (falling) and on pay (rising), a faint room tone |
| Save, quit Play, Play | Upgrades still owned (sheets say `Sende var`), hung tools still on the wall |

### MT-025: Hammer, glue and dowels, sanding and oil, rip cut, tool tote (Phase 9, D-045)
| Step | Expected |
|---|---|
| Tool wall | Four more tools hang there: `Çekiç`, `Ahşap tutkalı`, `Zımpara takozu`, `Ahşap yağı ve fırça` |
| Job board: third sheet `Alet sandığı` | Offer: `Ücret €90.00`, 3 boards, boards 2 and 3 `boyuna ... şerit, sonra ...` |
| Accept; saw in hand, Q | `Boyuna kesim: genişlik 100 mm ...`; wheel changes the width; a guide line runs along the board |
| Board on the sawhorses or bench, hold LMB | Takes a few seconds; result `Boyuna kesildi: 100 mm şerit + 43 mm kalan`; two boards side by side |
| Rip a 45 mm strip, crosscut all parts (Q back to crosscut) | The job sheet shows `genişlik 100 mm` / `45 mm` lines with ready counts |
| Install base, ends; drill (driver bit) on an end-base point | `Bu bağlantı kavelalı: delikleri aç ...` |
| Drill bit on the dowel points, then glue bottle, hold LMB | `Kavela tutkallanıp takıldı`; after the 2nd: `Bağlantı işkencede: tutkal kuruyor`; aiming at the table with the glue shows `Tutkal kuruyor: NN sn` |
| Sides: hammer on red points without pre-drilling (optional) | `Tahta çatladı!` (same rule as screws); pre-drilled: `Çivi çakıldı, hizada` |
| Handle: drill-driver (red points: pre-drill) | Screws as before |
| Last part done, glue still drying | No result yet; it appears when the glue is dry |
| Sanding block on a part, hold LMB | `Zımparalandı`, the part gets lighter; oil brush on an unsanded part: `Önce zımparala ...`; on a sanded part: darker, `Yağlandı` |
| Result | Five goals incl. `Her parça zımparalı ve yağlı (n/6 parça)`; pay split over five goals |
| Save while glue dries, quit, Play | Drying continues where it stopped; surfaces kept |

### MT-026: Outdoor terrace (Phase 10, D-046)
| Step | Expected |
|---|---|
| Go out of the door | A grey site floor 2.8 × 2.2 m, six small dark spots with red stakes, a corner stake, a delivery area to the right, sawhorses to the left |
| Job board: fourth sheet `Küçük teras` | Offer lists four stocks (larch decking, beams, joists, concrete blocks); prompt shows `ücret €650.00, avans €325.00 hemen` |
| E | Money: +€325 deposit, then the material is paid; boards and blocks appear at the outdoor delivery area; message `dışarıdaki şantiyeye bırakıldı` |
| Job sheet | First line `Aşama 1/5: zemin hazırlığı (0/6 yer, önce işaretle)` |
| Carry a block to a spot before preparing | Red preview: `önce alanı işaretle ...` |
| E on the corner stake | A string line appears around the outline |
| E on each spot | The stake goes, a gravel bed appears; stage counts up |
| Set the six blocks, then the beams, joists (cut 1800 mm on the outdoor sawhorses), deck boards | Each layer only after the one below; the stage line follows (`Aşama 3/5: kirişler ...`) |
| Try to saw a concrete block | `Beton testereyle kesilmez` |
| Set a pier block (D-048) | A steel U beam bracket appears on top of it; the beam sits between its flanges |
| Set a joist (D-048) | A steel angle bracket appears at each beam: foot on the beam top, upright leg on the joist's inner side |
| Drill-driver on a U bracket flange / angle bracket point | Works like a screw (clutch advice shown), never splits |
| Deck screws at the board ends | Red points: pre-drill |
| Complete | Result with pay, timber cost of all four stocks, profit; delivering pays the rest (pay minus the €325 deposit) |
| Save during the build, quit, Play | Site preparation, deposit and progress kept |

### MT-027: Pergola interim test: stepladder, spirit level, braces, working height (Phase 11a-11d, D-049)
Rebuild the scene first (*WoodcraftSimulator > Build Test Workshop Scene*).
| Step | Expected |
|---|---|
| Tool wall | A yellow `Su terazisi` hangs right of the oil brush; E takes it |
| Go out, right of the terrace | Pergola site 3.4 × 3.4 m with four spots, a corner stake, sawhorses at its front left, a yellow stepladder |
| Look at the stepladder | `[E] Merdiven al` and below `[Space] Merdivene çık` |
| Space | You stand on the ladder (feet 1.45 m higher, D-050: you look down onto the post tops), can look around but not walk; prompt line `[Space] Merdivenden in` |
| Space again | Back on the ground where you stood |
| E on the ladder, carry it, place it, R rotates | Stays upright, carried low at your right side (view stays free); placed like a board |
| Job board: fifth sheet `Bahçe pergolası` | Offer: 5 rafters, 2 beams, 4 posts 2400 × 100 × 100, 4 concrete blocks; `ücret €850.00, avans €425.00` |
| Prepare the site, set the 4 blocks | Each block shows a steel U post base on top |
| Cut a post to 2200 mm on the pergola sawhorses, set it on a block | The post goes into the U base and stands visibly a little crooked |
| Drill-driver on the post base point | `Direk n henüz şakülünde sabitlenmedi ...` (no screw) |
| Spirit level in hand, aim at a post face | `Direk n: tepe sola/sağa x.x° yatık (metrede y mm)`; wheel changes it (Shift ×10); at ≤ 0.5° `bu yüzde kabarcık ortada ... Yan yüzü de kontrol et` |
| LMB when only one face is plumb | `Henüz şakülünde değil: diğer yüzü kontrol et` |
| Plumb on both faces, LMB | Two wooden braces from the post to the ground; `Destek çakıldı ...`; the wheel no longer moves it |
| Carry a beam to two posts, one not braced yet | Red: `Ön kiriş: önce direkleri şakülüne getir ve geçici destek çak` |
| Braced posts, beam from the ground | Red: `Buradan yerleştiremezsin (ayağından 2.4 m yukarıda): merdiven kullan` |
| Climb the ladder with the beam in hand, aim at the beam's ghost (in the air above the posts, D-050), place it | Green as soon as the crosshair is on the ghost; it sits on the post tops; steel angle brackets appear on the post tops |
| Rafters: aim at a rafter ghost from the ladder | Green on the ghost itself, no need to hit the beam top |
| Bench / terrace: aim at a ghost as before, and lay a board on the ground next to a red ghost | Snapping works as before; a red ghost does not stop putting the board on the ground behind it |
| Drill-driver on a high point from the ground | `Uzanamazsın ...: merdiven kullan`; from the ladder it works |
| Fasten all joints of a post | Its braces disappear |
| Rafters (from the ladder), all brackets | Stage line: site, foundation, posts, beams, rafters; at the end the result with pay; delivery pays the rest |
| Save while on the ladder / with crooked and braced posts, quit, Play | You stand on the ground beside the ladder; ladder pose, post leans and braces kept |

### MT-028: Job card (D-051)
| Step | Expected |
|---|---|
| Accept the shelf job (bench) | Top left a dark card: `AKTİF İŞ` with the time on the right, the job name large, one grey line below; sections `KESİM LİSTESİ`, `MALZEME`, `İPUÇLARI`; no `AŞAMA` section |
| Look at the cut list | One row per size: dot, `2 ×`, `600 × 145 × 18 mm`, part names, `0/2 takılı`; heading shows the total, e.g. `0/7 takılı` |
| Cut a part, then install it | The row gets `1 hazır` and an amber dot; installed: `1/2 takılı`; all in: green dot, `tamam` |
| Accept the pergola job | `AŞAMA 1/5`, `Zemin hazırlığı`, `0/4`, empty bar, amber note `önce alanı işaretle (köşe kazığı)` |
| Set out, prepare spots, set blocks | The bar fills, then the next stage starts with an empty bar |
| `MALZEME` of the pergola | Four stock lines with their own counts (5 × Ladin 3000 × 120 × 45 mm, 2 × ... 145 ..., 4 × ... 100 × 100, 4 × Beton 200 × 200 × 200 mm), then the fasteners |
| Long part names | Cut off with `...` in the row, the card does not get wider |
| Look at a job sheet on the board with no job active | The offer text as before |

## Game view setup (corrects the earlier tip)
A fixed resolution such as *Full HD (1920x1080)* at `Scale 1x` **crops** the image when the Game view window is
smaller than 1920×1080: the bottom (tool belt bar) and the lower right (tool in hand) are cut off.
Use instead: resolution dropdown → **16:9 Aspect** (the view always fits the window), Scale slider at its leftmost
value; or, outside Play Mode, set **Play Maximized** in the Play Focused dropdown (or press Shift+Space over the Game view).
