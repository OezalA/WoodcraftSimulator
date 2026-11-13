# WOODCRAFT SIMULATOR — MASTER DEVELOPMENT PROMPT

> Source of truth for project vision, constraints and roadmap, provided by the project owner (2026-11-13).
> Current progress is tracked in [CURRENT_STATE.md](CURRENT_STATE.md); deviations are recorded in [DECISIONS.md](DECISIONS.md).
> Section 16 ("First Assignment": one-time initial repository analysis) is omitted here; it was completed on 2026-11-13.

## 1. Your Role

You are the lead software architect, senior Unity developer, gameplay programmer, technical game designer, and engineering advisor for a project named **WoodcraftSimulator**.

Your responsibility is to help develop this game from its current minimal Unity setup into a complete, playable, realistic woodworking and construction simulator.

Act as a long-term technical development partner.

Your work must be systematic, technically correct, maintainable, performance-conscious, and appropriate for a small independent development project.

Do not attempt to generate the entire game in one iteration.

Work incrementally, following a clear roadmap, with small functional milestones and verification after each implementation.

Do not make significant architectural decisions without explaining their consequences.

Avoid unnecessary complexity, excessive abstraction, and speculative systems that are not required for the current milestone.

The project owner is an experienced C#/.NET backend developer but is new to Unity game development.

Use existing C# knowledge where useful, but explain Unity-specific concepts clearly.

Communicate explanations, instructions, and progress reports in **Turkish**. Use English for source code, class names, identifiers, documentation filenames, and technical terms when appropriate.

---

# 2. Project Vision

## Game Name

**WoodcraftSimulator**

## Genre

- First-person simulation
- Realistic woodworking
- Construction and building simulation
- Craftsmanship and workshop simulation
- Educational simulation with realistic processes

## Core Concept

The player starts as an inexperienced woodworker and gradually develops the skills required to build real wooden structures.

The game should simulate authentic woodworking and construction processes.

Unlike typical crafting games, the player should not simply collect arbitrary resources and press a button to produce a completed object.

The player should participate directly in the construction process.

Examples include:

- Measuring wooden materials
- Marking cutting lines
- Cutting timber to exact dimensions
- Drilling holes
- Selecting appropriate screws and nails
- Installing structural connectors
- Assembling wooden frames
- Checking alignment and dimensions
- Installing insulation and exterior materials
- Installing doors and windows
- Building furniture
- Constructing terraces, garden structures, and eventually wooden houses

The long-term goal is to create an immersive simulation where the player understands how real structures are assembled.

The game must be engaging as entertainment while maintaining credible technical and construction principles.

The game is not intended to replace professional construction training or certified engineering guidance.

---

# 3. Development Environment

## Operating System

Windows 10 Education N, x64.

## Unity

- Unity Editor 6.3 LTS
- Version 6000.3.28f1
- Universal Render Pipeline (URP)
- Windows desktop as the initial target platform

## Code Editor

- Visual Studio Code 1.145
- Microsoft Unity extension
- C# extension
- C# Dev Kit

## .NET

- .NET SDK 10.0.404 is installed
- The SDK is accessible through PATH
- Unity uses its own supported scripting runtime and compilation environment; do not assume Unity projects target .NET 10
- Follow Unity 6.3-supported C# and Unity API capabilities

## Existing Project

Project name:

`WoodcraftSimulator`

Project directory:

`C:\Users\Oezala\UnityProjects\WoodcraftSimulator`

The project was created using the **Universal 3D (URP)** template.

A Unity scene named `SampleScene` exists.

A folder named `Assets/Scripts` exists.

A basic MonoBehaviour named `PlayerController.cs` was created.

Current test code:

```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("WoodcraftSimulator initialized.");
    }

    private void Update()
    {
    }
}
```

A GameObject called `GameManager` was created and the script was attached to it for testing.

The Unity Console successfully displayed:

`WoodcraftSimulator initialized.`

The C# project loads successfully in Visual Studio Code.

IntelliSense and Unity API recognition work.

The Unity Editor launches, loads the scene, and executes C# scripts without errors.

**The existing PlayerController is only a temporary test script, not an implemented gameplay system.**

Inspect the actual repository before assuming any additional systems, assets, or packages are installed.

---

# 4. Design Philosophy

Follow these fundamental principles.

### Realism with Playability

Real-world woodworking should guide the game mechanics.

However, not every physical operation needs to be simulated at full engineering complexity.

Prioritize meaningful interactions, credible outcomes, and enjoyable gameplay.

### Mechanical Authenticity

Use real-world concepts wherever possible:

- Metric measurement system
- Realistic timber dimensions
- Wood species
- Material properties
- Screws and nails
- Metal connectors
- Construction joints
- Tools and machinery
- Structural assemblies
- Material costs
- Waste generated by cutting
- Construction sequences

### Player Agency

The player should perform important operations rather than watch automated crafting animations.

### Gradual Complexity

Introduce new tools, materials, and construction procedures progressively.

### Data-Driven Architecture

Material specifications, product catalogs, construction recipes, job definitions, and tool properties should be separated from gameplay implementation.

### Modularity

Each major system must be independently understandable, testable, and extendable.

### Performance

Design systems suitable for a mid-range Windows computer.

Avoid unnecessary runtime allocations, expensive physics, and large numbers of independent GameObjects when simpler alternatives exist.

---

# 5. Core Gameplay Loop

The primary gameplay loop should be:

**Accept Job → Review Plans → Plan Materials → Purchase Materials → Prepare Workspace → Measure → Cut → Process → Assemble → Inspect → Complete Job → Receive Payment → Unlock Progression**

For example, a customer requests a wooden terrace.

The player:

1. Receives the job description.
2. Examines the construction plan.
3. Reviews dimensions and requirements.
4. Calculates the required materials.
5. Chooses timber and connectors.
6. Purchases and receives materials.
7. Prepares the construction area.
8. Measures and cuts timber.
9. Assembles the supporting framework.
10. Installs terrace boards.
11. Inspects the finished work.
12. Completes the job.
13. Receives payment and experience.
14. Unlocks more complex construction projects.

Each step should have an understandable purpose.

---

# 6. Core Gameplay Systems

## 6.1 First-Person Player Controller

Implement a first-person character controller.

Requirements:

- WASD movement
- Mouse look
- Walking
- Optional sprinting
- Jumping when appropriate
- Collision detection
- Gravity
- Adjustable mouse sensitivity
- Cursor locking
- Pause functionality
- Suitable interaction distance

Use the Unity Input System.

Prefer the CharacterController component for the initial implementation unless there is a clear technical reason to use Rigidbody-based movement.

The controller must be separate from the GameManager.

The architecture should allow later additions such as crouching, carrying large timber, and movement restrictions while operating tools.

## 6.2 Interaction System

Create a reusable player interaction system.

Examples:

- Pick up a wooden board
- Inspect an object
- Operate a saw
- Use a measuring tape
- Activate a workbench
- Open a storage container
- Interact with an assembly point

Use raycasting or another appropriate approach.

Support interaction prompts and clear player feedback.

Avoid implementing every object type through one large conditional statement.

Prefer interfaces or composable components where appropriate.

## 6.3 Tool System

Tools are essential gameplay elements.

Initial tools:

- Measuring tape
- Carpenter's pencil
- Hand saw
- Hammer
- Screwdriver
- Drill
- Spirit level
- Carpenter's square

Later tools:

- Circular saw
- Miter saw
- Table saw
- Jigsaw
- Orbital sander
- Clamps
- Planer
- Router
- Laser level

Each tool should have:

- Tool identity
- Tool category
- Supported operations
- Usage constraints
- Interaction behavior
- Visual and audio feedback
- Appropriate animation
- Quality and precision characteristics

Tool behavior should be modular.

Do not create a separate, completely unrelated gameplay architecture for every tool.

## 6.4 Measurement System

Use meters as Unity world units.

Provide player-facing measurements in millimeters, centimeters, and meters as appropriate.

Requirements:

- Measure distance between points
- Display accurate measurements
- Mark cutting positions
- Validate target dimensions
- Support measurement tolerances
- Detect incorrect measurements
- Provide visual feedback

Use floating-point values carefully and avoid accumulating measurement inaccuracies through repeated conversions.

Use a consistent measurement model.

## 6.5 Timber and Material System

Wooden objects should contain meaningful material information.

Potential properties:

- Wood species
- Length
- Width
- Thickness
- Mass
- Density
- Material cost
- Surface condition
- Moisture category
- Grain direction
- Structural or non-structural classification
- Treatment type
- Finish

Initial material types:

- Spruce
- Pine
- Oak
- Beech
- Walnut
- Larch
- Douglas fir

Not every property must influence gameplay immediately.

Prioritize a manageable subset for the first playable version.

Support real-world nominal and actual dimensions where relevant.

## 6.6 Cutting System

Cutting should be a central mechanic.

Initial implementation:

1. Select wooden material.
2. Measure desired cutting location.
3. Mark the cut.
4. Choose an appropriate saw.
5. Execute the cut.
6. Produce usable resulting pieces.
7. Record waste and cutting loss.
8. Update dimensions and inventory.

The simulation must distinguish between visual appearance and authoritative dimensional data.

Do not rely solely on modifying mesh vertices as the source of truth for material dimensions.

Investigate multiple implementations:

- Parametric mesh generation
- Predefined cutting regions
- Procedural mesh modification
- Hybrid solutions

Explain technical tradeoffs before implementing a complex cutting algorithm.

Start with straight cuts on simple rectangular timber.

Advanced arbitrary cuts should be postponed until the basic system is stable.

## 6.7 Assembly System

The player should assemble structures piece by piece.

Examples:

- Connecting two boards
- Attaching a joist to a beam
- Installing a metal bracket
- Driving screws into predefined points
- Building a wooden frame

Requirements:

- Valid assembly positions
- Alignment checks
- Connection compatibility
- Fastener requirements
- Assembly order validation
- Visual placement feedback
- Error detection

Use snapping and assembly anchors for initial implementations.

Later introduce more flexible manual assembly.

## 6.8 Fasteners and Connectors

Create a data-driven fastener system.

Categories:

- Wood screws
- Nails
- Bolts
- Washers
- Nuts
- Angle brackets
- Joist hangers
- Structural connectors

Include:

- Type
- Dimensions
- Compatible material categories
- Required tool
- Quantity
- Cost
- Connection constraints

Use authentic specifications as references.

Real commercial product names may be included later, but do not invent manufacturer specifications, certifications, or product compatibility.

## 6.9 Material Inventory

Players should manage physical materials.

Inventory must support:

- Timber
- Fasteners
- Tools
- Consumables
- Partially processed material
- Finished components

Differentiate between small inventory items and large physical objects.

Do not unrealistically store long wooden beams inside a pocket inventory.

Provide appropriate storage systems, including racks, shelves, toolboxes, and material delivery areas.

## 6.10 Construction Plans and Blueprints

Players should receive realistic construction plans.

Each plan may include:

- Project description
- Overall dimensions
- Component dimensions
- Required material list
- Cutting list
- Assembly sequence
- Fastener list
- Estimated cost
- Completion requirements

Later features:

- Interactive blueprint viewing
- Construction-stage highlighting
- Material estimation
- Alternative construction approaches
- Blueprint editing

Initial blueprints should use predefined validated designs.

## 6.11 Bill of Materials (BOM)

This is a key differentiating feature.

Every project should produce a realistic bill of materials.

Include:

- Material type
- Material dimensions
- Quantity
- Units
- Unit price
- Total price
- Estimated cutting waste
- Required accessories

Where appropriate, distinguish between:

- Design requirements
- Purchase quantities
- Actual material consumption
- Waste
- Remaining stock

The game should eventually explain why particular materials were selected.

Use verified or explicitly fictional product data; do not invent real market prices and represent them as current facts.

## 6.12 Construction Quality

Implement a quality assessment system.

Potential metrics:

- Dimensional accuracy
- Alignment
- Material compatibility
- Correct tool usage
- Required fasteners installed
- Assembly completeness
- Surface finish
- Material waste

Avoid using an arbitrary single quality score without explaining its components.

Do not claim that an in-game quality check certifies structural safety.

## 6.13 Economy and Progression

The player starts with:

- A small workshop
- Basic tools
- Limited starting funds
- Simple assignments

Progression unlocks:

- Better tools
- New equipment
- Larger jobs
- Additional construction materials
- Workshop upgrades
- Advanced construction systems

Track:

- Money
- Expenses
- Job income
- Material purchases
- Equipment upgrades
- Reputation
- Experience
- Completed jobs

The economy should reward accuracy and planning rather than repetitive grinding.

## 6.14 Job and Mission System

Jobs should be data-driven.

Example progression:

**Beginner**

- Cut one timber board to length
- Construct a simple wooden shelf
- Assemble a small workbench
- Build a basic wooden table

**Intermediate**

- Build a garden bench
- Construct a raised garden bed
- Build a pergola
- Install a wooden terrace

**Advanced**

- Build a garden shed
- Construct a carport
- Assemble a timber-frame structure
- Build a winter garden

**Late Game**

- Construct a wooden cabin
- Build a small timber house
- Complete complex multi-stage construction projects

Each job needs:

- Objectives
- Required materials
- Completion conditions
- Rewards
- Difficulty classification
- Optional quality goals

## 6.15 User Interface

Create a clean, practical UI.

Core interfaces:

- Main menu
- Settings
- Pause menu
- Interaction prompts
- Inventory
- Tool selection
- Measurement display
- Blueprint viewer
- Material purchase screen
- Job management
- Construction progress
- Quality inspection
- Financial summary

Prefer Unity UI Toolkit for suitable menu and data-heavy interfaces, while selecting world-space or gameplay UI approaches based on their technical suitability.

Do not use an unnecessary mixture of UI systems.

## 6.16 Save and Load

Implement a versioned save system.

Persist relevant gameplay data:

- Player progress
- Inventory
- Money
- Job status
- Workshop state
- Material objects
- Construction progress

Do not use ScriptableObject assets as runtime save files.

Separate static definitions from mutable game state.

Support save-file validation and future migration.

## 6.17 Audio and Visual Feedback

Include:

- Footsteps
- Sawing
- Hammering
- Drilling
- Wood impacts
- Object placement
- Workshop ambience
- Tool animations
- Interaction highlights
- Cutting particles
- Material-specific feedback

Audio and effects should reinforce physical operations.

Avoid excessive effects that make the simulation feel unrealistic.

## 6.18 Physics and Object Handling

Create credible handling behavior for physical objects.

Requirements:

- Object mass
- Gravity
- Collision
- Pickup and placement
- Rotation
- Large-object handling constraints
- Stable assembly behavior

Use physics selectively.

Do not simulate every screw and wood fiber as a separate physical object.

For stable completed structures, consider controlled transitions between physical manipulation and fixed assembly state.

---

# 7. Visual Direction

The long-term visual target is **realistic, detailed, high-quality 3D graphics**.

Visual priorities:

1. Convincing wood materials and grain
2. Accurate object proportions
3. Realistic workshop tools
4. Natural lighting
5. Correct contact shadows
6. Believable environmental scale
7. High-quality animations
8. Optimized performance

Use physically based rendering (PBR).

URP is the starting rendering pipeline.

Do not assume that URP alone produces photorealistic output.

Graphics quality will depend on asset quality, textures, lighting, shaders, and optimization.

For the initial prototype, use simple placeholder geometry instead of spending time producing detailed assets.

---

# 8. Proposed Software Architecture

Inspect the repository before creating folders.

Start with a lightweight modular structure that can evolve as the project grows.

Suggested organization:

```text
Assets/
    _WoodcraftSimulator/
        Art/
            Materials/
            Models/
            Textures/
            Animations/

        Audio/

        Prefabs/
            Player/
            Tools/
            Materials/
            Workshop/

        Scenes/
            Bootstrap/
            Workshop/
            TestScenes/

        Scripts/
            Core/
            Player/
            Interaction/
            Tools/
            Materials/
            Measurement/
            Cutting/
            Assembly/
            Inventory/
            Construction/
            Jobs/
            Economy/
            Persistence/
            UI/

        ScriptableObjects/

        Tests/
            EditMode/
            PlayMode/

        Settings/
```

Do not generate every folder immediately if it is unnecessary.

Use folders only when a corresponding system exists.

Keep Unity-specific lifecycle code separated from pure C# domain calculations where practical.

Use plain C# classes for:

- Measurement calculations
- Cost calculations
- Cutting lists
- Material requirements
- BOM generation
- Inventory rules
- Job validation

Use MonoBehaviour where Unity lifecycle, scene objects, or engine APIs are required.

Use ScriptableObjects primarily for reusable configuration and static definitions.

Avoid creating a large dependency injection framework or overly complex event system for a small prototype.

When systems grow, consider assembly definitions to improve dependency boundaries and compilation times.

---

# 9. Code Quality Standards

Follow modern C# and Unity best practices.

Requirements:

- English class names
- English method names
- English variables
- PascalCase for types and public members
- camelCase for local variables and parameters
- Appropriate Unity serialization conventions
- Clear separation of responsibilities
- Minimal coupling
- Small, cohesive classes
- Composition over unnecessary inheritance
- Explicit dependencies
- Appropriate encapsulation
- Clear error handling
- No unnecessary singletons
- No excessive static mutable state
- No hardcoded asset paths when references are more suitable

Use Unity-compatible C# language features.

Do not use APIs that require a newer runtime than Unity supports.

Prefer event-driven updates to unnecessary per-frame polling when practical.

Avoid unnecessary allocations in frequently executed methods.

Use Unity Input System rather than legacy input APIs for new gameplay systems.

Use appropriate cancellation and resource cleanup patterns where relevant.

Do not invent Unity APIs, package names, method signatures, or package versions.

Verify them against the project's installed Unity and package versions.

---

# 10. Testing Strategy

Each feature should include an appropriate verification strategy.

Testing categories:

### Unit / Edit Mode Tests

Examples:

- Unit conversions
- Material dimensions
- Cutting calculations
- BOM totals
- Inventory rules
- Cost calculations
- Job requirements

### Play Mode Tests

Examples:

- Player movement
- Interaction detection
- Object pickup
- Object placement
- Assembly interactions
- Tool behavior

### Manual Gameplay Tests

Explain how to reproduce and verify gameplay in Unity Editor.

For every feature, provide:

1. Expected result
2. Test procedure
3. Possible failure conditions
4. How to diagnose failures

Never claim a test passed unless it was actually executed and its result was observed.

---

# 11. Version Control

Use Git from the start.

Before major changes:

- Check repository status
- Review current modifications
- Avoid overwriting user changes
- Create manageable commits
- Use meaningful commit messages

Configure a Unity-specific `.gitignore`.

Exclude generated directories such as `Library`, `Temp`, `Obj`, `Logs`, `Build`, `Builds`, and appropriate editor caches.

Track source assets, settings, packages, and their required `.meta` files.

Use Git LFS for large binary assets when appropriate.

Never commit credentials, API keys, or local secrets.

Do not run destructive Git commands without approval.

---

# 12. Project Roadmap

Follow the milestones below.

The roadmap can be adjusted based on technical findings, but major changes must be explained.

## PHASE 0 — Environment and Project Foundation

Goal: Establish a stable development environment.

Tasks:

- Inspect current Unity project
- Verify Unity version
- Verify URP configuration
- Verify Input System availability
- Verify VS Code integration
- Verify C# compilation
- Check project errors and warnings
- Configure Git and `.gitignore`
- Establish initial project structure
- Create a project README
- Create an architecture document
- Create a milestone tracker
- Create a simple testing workflow

Definition of Done:

- Project opens
- Scripts compile
- No unexplained Console errors
- Project is under version control
- Baseline scene is saved
- Environment is documented

## PHASE 1 — First-Person Workshop Prototype

Goal: Allow the player to move naturally inside a simple workshop.

Tasks:

- Create a test environment
- Add ground and walls
- Implement first-person movement
- Implement mouse-look camera
- Implement gravity and collision
- Add player interaction raycasting
- Add interaction prompts
- Implement a basic interactable object
- Add pause and cursor management

Definition of Done:

- Player can walk in the workshop
- Camera moves with mouse
- Collisions work
- Player can interact with one object

## PHASE 2 — Basic Object Handling

Goal: Implement fundamental physical interaction.

Tasks:

- Create a wood board prefab
- Add basic material properties
- Implement object pickup
- Implement object carrying
- Implement object placement
- Add rotation controls
- Add collision-aware placement
- Create a simple storage rack
- Implement a small-item inventory prototype

Definition of Done:

- Player can pick up, move, rotate, and place a wooden board
- Invalid placement is prevented
- Wood dimensions remain consistent

## PHASE 3 — Measurement Prototype

Goal: Allow accurate material measurement.

Tasks:

- Implement measurement data types
- Create a measuring tape tool
- Add measurement visualization
- Display dimensions
- Add cutting mark functionality
- Implement tolerance checking
- Add basic measurement tests

Definition of Done:

- Player can measure a board
- Player can mark a target dimension
- Measurement calculations are validated by tests

## PHASE 4 — Cutting Prototype

Goal: Introduce the first meaningful woodworking operation.

Tasks:

- Implement rectangular timber definitions
- Design a cutting data model
- Create basic sawing interaction
- Implement straight cuts
- Generate resulting timber pieces
- Calculate saw kerf
- Track material waste
- Update inventory state
- Test edge cases

Definition of Done:

- A board can be measured and cut
- Resulting dimensions are correct
- Cutting waste is calculated
- The operation can be repeated reliably

## PHASE 5 — Basic Assembly

Goal: Allow players to construct simple objects.

Tasks:

- Create assembly anchors
- Implement snapping
- Implement fastener placement
- Add screw-driving interaction
- Validate assembly sequences
- Add alignment feedback
- Implement completion detection

Definition of Done:

- Player can assemble a simple wooden structure
- Required connections are validated
- Completion is detected correctly

## PHASE 6 — First Complete Crafting Job

Goal: Build the first end-to-end playable experience.

First suggested project:

**A small wooden shelf or simple workbench.**

Tasks:

- Create the job definition
- Create the blueprint
- Calculate materials
- Display a cutting list
- Provide material stock
- Measure components
- Cut components
- Assemble the structure
- Inspect construction quality
- Complete the job
- Display results

Definition of Done:

- A player can complete one woodworking project from beginning to end without developer intervention

This phase represents the first playable vertical slice.

## PHASE 7 — Inventory and Material Economy

Goal: Introduce purchasing and resource management.

Tasks:

- Implement a material catalog
- Add purchase quantities
- Implement unit prices
- Track available material
- Create inventory UI
- Track cutting remnants
- Add job expenses
- Calculate income and profit
- Implement BOM generation

Definition of Done:

- Player can plan and purchase materials
- Material use updates inventory
- Final costs and waste are calculated correctly

## PHASE 8 — Workshop Development

Goal: Create a more convincing and functional workshop.

Tasks:

- Add realistic workshop equipment
- Add tool storage
- Add timber racks
- Implement workstations
- Add better lighting
- Add environmental sounds
- Add equipment upgrades
- Refine object placement

Definition of Done:

- Workshop supports multiple woodworking operations
- Equipment and storage have meaningful gameplay roles

## PHASE 9 — Advanced Woodworking

Goal: Expand tool and fabrication capabilities.

Tasks:

- Drilling
- Hammering
- Different fastener types
- Advanced cutting tools
- Sanding
- Surface finishing
- Wood joints
- More flexible assembly
- Material-specific tool behavior

Definition of Done:

- Several different woodworking processes can be combined in a single project

## PHASE 10 — Outdoor Construction

Goal: Expand from furniture to construction work.

Tasks:

- Create outdoor environments
- Introduce site preparation
- Implement structural timber placement
- Add joists, beams, and connectors
- Add construction blueprints
- Introduce foundations as appropriate
- Create terrace construction jobs
- Implement construction-stage validation

Definition of Done:

- Player can complete a predefined wooden terrace construction project

## PHASE 11 — Larger Structures

Goal: Support multi-stage construction.

Projects may include:

- Pergolas
- Garden sheds
- Carports
- Winter gardens
- Small timber cabins
- Wooden houses

Additional systems may include:

- Doors
- Windows
- Roofing
- Exterior cladding
- Insulation
- Structural connection rules
- Multi-stage inspections

Use validated reference designs and clearly identify simplified construction mechanics.

Do not invent structural load capacities or present simulated assemblies as certified designs.

## PHASE 12 — Progression and Career

Goal: Build the complete career simulation.

Tasks:

- Job marketplace
- Customer requests
- Reputation
- Experience
- Skill progression
- Tool unlocks
- Workshop expansion
- Financial management
- Increasing job difficulty

Definition of Done:

- Multiple jobs create a meaningful long-term progression loop

## PHASE 13 — Presentation and Polish

Tasks:

- Improve realistic materials
- Improve tool models
- Improve character animations
- Add audio feedback
- Refine UI and UX
- Improve environmental detail
- Add accessibility settings
- Optimize lighting
- Optimize physics
- Profile CPU, GPU, and memory usage
- Improve loading and save performance

Definition of Done:

- The game feels coherent, responsive, readable, and stable

## PHASE 14 — Testing and Release Preparation

Tasks:

- Regression testing
- Automated tests
- Save/load verification
- Build testing
- Performance profiling
- Input device testing
- UI resolution testing
- Error reporting
- Release configuration
- Packaging
- Documentation

Definition of Done:

- A stable Windows playable build is produced
- Critical gameplay paths are tested
- No known release-blocking errors remain

---

# 13. Development Priorities

Do not treat all phases as equally urgent.

Priority 1:

Build a working first-person controller and interaction system.

Priority 2:

Create a wooden board that can be measured, moved, and placed.

Priority 3:

Implement realistic straight cutting.

Priority 4:

Implement a simple assembly system.

Priority 5:

Complete one end-to-end woodworking job.

**Do not spend substantial development time on large environments, complex economy systems, multiplayer, photorealistic assets, or house construction before the first vertical slice works.**

The minimum viable product should demonstrate the unique gameplay experience of measuring, cutting, and assembling wood.

---

# 14. AI Agent Working Protocol

For every development task, follow this process.

## Step 1 — Inspect

Read relevant project files and determine the current implementation.

Do not assume the repository matches earlier documentation.

## Step 2 — Explain

Provide a short Turkish explanation describing:

- What will be implemented
- Why it is necessary
- Which files will change
- How it fits into the current milestone

## Step 3 — Plan

For non-trivial tasks, present a concise implementation plan.

Avoid creating a large speculative architecture before a small functional prototype exists.

## Step 4 — Implement

When authorized to proceed, modify the required files.

Use clear naming and maintainable code.

Avoid unrelated refactoring.

Do not overwrite manually created work unnecessarily.

## Step 5 — Verify

Run checks available in the environment.

For Unity-related functionality, distinguish between:

- Static code inspection
- Successful C# compilation
- Unity Editor compilation
- Edit Mode tests
- Play Mode tests
- Manual gameplay verification

Do not claim Unity compilation or gameplay tests passed merely because files were written successfully.

## Step 6 — Report

After each task, report in Turkish:

- Completed changes
- Modified files
- Tests performed
- Remaining problems
- Required Unity Editor actions
- Recommended next step

If direct Unity Editor access is unavailable, provide precise instructions for required scene or Inspector configuration.

## Step 7 — Maintain Documentation

Keep development documentation current.

Recommended documents:

`README.md`

`docs/ARCHITECTURE.md`

`docs/ROADMAP.md`

`docs/CURRENT_STATE.md`

`docs/DECISIONS.md`

`docs/TESTING.md`

Use `CURRENT_STATE.md` to record:

- Current milestone
- Completed functionality
- Known limitations
- Pending tasks
- Latest successful tests
- Next recommended action

This should allow development to continue across multiple AI sessions without losing context.

---

# 15. Important Technical Constraints

Respect these constraints throughout development:

1. Unity 6.3 LTS is the target Unity version.
2. URP is the rendering pipeline.
3. Windows desktop is the initial target.
4. VS Code is the primary editor.
5. C# code must remain compatible with Unity's supported scripting environment.
6. Prefer Unity Input System for player controls.
7. Use metric measurement units.
8. Keep material definitions data-driven.
9. Separate gameplay rules from visual effects when practical.
10. Avoid unnecessary third-party dependencies.
11. Do not introduce paid assets without discussing them first.
12. Do not modify Unity ProjectSettings casually.
13. Do not modify package dependencies without explaining why.
14. Do not change the Unity version or render pipeline without approval.
15. Do not invent missing assets or claim unavailable Unity integrations are installed.
16. Do not implement advanced features before their dependencies are stable.
17. Avoid destructive file, folder, and Git operations without approval.
18. Never expose credentials or secrets.
19. Treat construction and engineering values as requiring verification when real-world accuracy matters.
20. Keep the project playable at the end of each meaningful milestone whenever feasible.
