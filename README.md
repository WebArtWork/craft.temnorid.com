# Temnorid Craft

**Gather. Carry. Store.**

Gathering, crafting and production. A [Temnorid](https://temnorid.com) game for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Version 0.2.0.** Windows, Android and iOS. Website: https://craft.temnorid.com · Download: [Temnorid-Craft-Setup.exe](https://github.com/WebArtWork/craft.temnorid.com/releases/latest/download/Temnorid-Craft-Setup.exe)

## What it is

Build up your farmstead, grow food, gather wood, stone and ore, and make tools, weapons and armour. Supply the settlements in Lords with resources.

## The first playable

The first playable is the heart of Craft: a worker in a small settlement gathers wood, stone and iron, carries them in a backpack and stores them in the warehouse.

### How a session goes

1. Walk around the settlement in third person.
2. Stand next to a tree, a rock or an iron vein and hold **E** to gather. A progress bar fills and the resource goes into your backpack.
3. The backpack has 3 slots of 8 items (24 in all), and each slot holds one kind of resource. When there is no room you cannot gather more, so one trip is never enough for the goal.
4. Walk to the warehouse and press **E** to store everything you carry.
5. Watch the warehouse totals for wood, stone and iron grow. Exhausted resources grow back after a while.
6. Your progress is saved automatically, so the warehouse is still full when you come back.

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| E (hold) | Gather a resource |
| E (press) | Store your load when you stand at the warehouse |
| V | Switch between third and first person |
| Esc | Pause menu (resume, save, main menu) |

### Win and lose

- **Win:** Store 10 wood, 10 stone and 10 iron in the warehouse. A message tells you the goal is reached, and you can keep playing.
- **Lose:** Nothing can go wrong in Craft: it is a calm game about gathering and building up.

### The scene

One flat settlement: a forest, a rock field, an iron field and a small warehouse. All shapes are simple blocks and capsules; every object has a stable asset id so real models can replace them later.

## Playing with others

Choose **Play with others** in the main menu (the shell's lobby): up to **4 players** share one settlement. The host owns the world: the resource nodes and the warehouse. A guest's gather and store are requests that the host checks (the node has something left, the guest stands near it and is not faster than gathering allows) and applies; each player's backpack stays with that player, and everybody sees the same trees, rocks, ore and warehouse totals. Other workers are shown from their poses. A guest does not read or write their single-player save (the host saves the world it hosts); if the host leaves, the game ends for everybody and returns to the main menu.

## Updates

The Windows game updates itself from the newest GitHub release at start (the installer runs silently and starts the game again). **Profile** can turn this off.

## Phones

Android (minimum API 26) and iOS builds come from Unity Build Profiles (`ShellSceneKit` sets the platform settings). Touch controls: a move stick, a look area (drag anywhere else), **Use** (hold to gather, tap to store), Jump, Run, View and **Bag** (opens the Stock place). The shell puts the places in a bottom bar and respects the safe area.

## How it connects to Lords

Resources gathered in Craft will supply the settlements of Lords. Later, tools, weapons and armour made here will be used by heroes in the other games.

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

## Scope of this version

- Resource nodes, gathering with progress, backpack, warehouse, local save.
- No farming, crafting recipes or building yet: those come after the first playable.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (keyboard and mouse), Android and iOS (touch). One map, local saves, optional multiplayer (see Playing with others).
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, `3d-constructor-character-view-camera`, `3d-constructor-character-view-controls`), `3d-unity-core`, `3d-unity-input` (all input, through `EngineInput`), `3d-unity-inventory` (backpack and warehouse, new in this workspace, with `3d-inventory-schema`) and `3d-unity-save` (with `3d-save-schema`). The gathering, the warehouse, the goal, the screens and the settlement are written in this game (`Assets/Scripts`).
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-core`, `../3d-unity-input`, `../3d-unity-character`, `../3d-constructor-character-view-camera`, `../3d-constructor-character-view-controls`, `../3d-unity-inventory`, `../3d-unity-save`) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Level building:** menu **Temnorid > Craft > Build Scene** (or `-executeMethod Temnorid.Craft.EditorTools.CraftSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` from scratch: ground, forest, rock field, iron field, warehouse, worker, camera and light, all from primitives. Do not edit that scene by hand. **Temnorid > Craft > Capture Preview Pictures** renders a few pictures to `Builds/Preview` (needs a graphics device).
- **Code map (`Assets/Scripts`):**
  - `CraftSession`: the rules with no Unity in them (gather into the backpack, store in the warehouse, the goal) and `CraftSaveStore` (the save).
  - `CraftGame`: the scene's state, messages, pause and saving. `PlayerWorker`: hold or press E near things. `ResourceNode` and `Warehouse`: the things to interact with. `CraftHud`: all screens, plain uGUI. `AssetIdTag`: stable ids like `resources/wood-node` for future models.
- **Shell and online:** `CraftShell` sets the `ShellConfig` (GameId `craft`, Repo `WebArtWork/craft.temnorid.com`, 4 players, places World and Stock). Online code is `CraftNet` (host authority: `NetSession.IsAuthority` is true offline and on the host; nodes and warehouse are published as entities, ids 1 to 3 the warehouse and 100 and up the nodes sorted by name; guests send `cr.gather` and `cr.dep` events), `RemoteWorker` (a guest's worker from poses). Scenes: Boot, Menu, Main (`CraftSceneBuilder` builds all three and the build order). Test port 7804 (`OnlineCraftTests`).
- **Tests:** `Assets/Tests/EditMode` covers the rules and the save file (and `3d-unity-inventory` has its own tests, run through `testables` in the manifest); `Assets/Tests/PlayMode` loads the scene, checks it has everything and gathers from every node until the goal is reached. Run them from Window > General > Test Runner, or with `Unity.exe -batchmode -nographics -runTests -testPlatform EditMode` (or `PlayMode`).
- **Save file:** `<persistent data path>/craft/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Craft\craft\default.json`): backpack, warehouse and whether the goal was reached.
- **Windows build:** **Temnorid > Craft > Build Windows Player** (or `-executeMethod Temnorid.Craft.EditorTools.CraftReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridCraft` and a portable zip. `installer/Craft.iss` turns the folder into `Builds/Installer/Temnorid-Craft-Setup.exe` with Inno Setup.
- **Do not add:** Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://craft.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/craft.temnorid.com/releases/latest/download/Temnorid-Craft-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-Craft-Setup.exe`.
3. Create a GitHub Release (tag `v0.2.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
