# WoodcraftSimulator

First-person, realistic woodworking and construction simulator built with Unity. The player measures, marks, cuts and assembles timber by hand, starting with simple furniture and working up to wooden structures.

> Status: early prototype. See [docs/CURRENT_STATE.md](docs/CURRENT_STATE.md).

## Requirements
- Unity **6000.3.28f1** (6.3 LTS) via Unity Hub
- Windows 10/11 x64
- Git + Git LFS (`git lfs install` once per machine)
- Recommended editor: VS Code with the Unity extension and C# Dev Kit

## Getting started
1. Clone the repository and open the folder in Unity Hub (*Add project from disk*).
2. Wait for the first import (it builds `Library/`, which can take a few minutes).
3. Menu **WoodcraftSimulator > Build Test Workshop Scene** generates the greybox workshop at
   `Assets/_WoodcraftSimulator/Scenes/Workshop/Workshop.unity`.
4. Press **Play**.

## Controls (prototype)
| Action | Input |
|---|---|
| Move | W A S D / arrow keys |
| Look | Mouse |
| Sprint | Left Shift |
| Jump | Space |
| Release cursor | Esc |
| Capture cursor | Left click in Game view |

## Documentation
| Document | Contents |
|---|---|
| [docs/MASTER_PROMPT.md](docs/MASTER_PROMPT.md) | Vision, systems, roadmap, constraints (source of truth) |
| [docs/CURRENT_STATE.md](docs/CURRENT_STATE.md) | Current milestone, known issues, next step |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Phase tracker |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Folder layout, assemblies, layering |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Architecture decision log |
| [docs/TESTING.md](docs/TESTING.md) | How to verify changes |
