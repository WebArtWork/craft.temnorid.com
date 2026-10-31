# Temnorid Craft

**Gather. Carry. Store.**

Gathering, crafting and production. A [Temnorid](https://temnorid.com) game for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Version 0.4.0.** Windows, Android and iOS. Website: https://craft.temnorid.com · Download: [Temnorid-Craft-Setup.exe](https://github.com/WebArtWork/craft.temnorid.com/releases/latest/download/Temnorid-Craft-Setup.exe)

## What it is

Build up your farmstead, grow food, gather wood, stone and ore, and make tools, weapons and armour. Supply the settlements in Lords with resources.

## The first playable

The first playable is the heart of Craft: a worker in the player's **Lords village and its farm** gathers the six resources of Lords, carries them in a backpack and stores them in the village's warehouse, where they join the **same stock** the player has in Lords.

### How a session goes

1. Walk around the settlement in third person.
2. Stand on a farm plot (a forest, a quarry, a mine, a pond, a grain field or a clay pit: one resource per plot, named and levelled by the label above it) and hold **E** to gather. A progress bar fills and the resource goes into your backpack. A higher plot gives a bigger harvest per gather (see below).
3. The backpack has 6 slots of 8 items (48 in all), and each slot holds one kind of resource, so you can carry all six kinds at once. When there is no room you cannot gather more, and the goal needs 60 items, so one trip is never enough.
4. Walk to the village's warehouse and press **E** to store everything you carry. The stores have a capacity (Lords' formula, raised by the warehouse, granary and well): what does not fit stays in your backpack.
5. Watch the stores (the right panel, and the Stock place) for the six resources grow. Exhausted plots grow back after a while.
6. Your progress is saved automatically: the backpack, and for the standard village the stock too. A Lords player's stock is saved in Lords.

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

- **Win:** Store 10 of each of the six resources (wood, stone, iron, water, grain and clay) in one visit. The stock itself starts at 750 of each, so the goal counts what you stored since the game started, not what was there. A message tells you the goal is reached, and you can keep playing. (Saves from version 0.3.0 and earlier still load; their old warehouse numbers and goal are not carried over.)
- **Lose:** Nothing can go wrong in Craft: it is a calm game about gathering and building up.

### The scene

The player's village and its farm, drawn from primitives (see "The village and the shared stock"). Every farm plot is a patch of its resource's Lords colour with level markers, a name and a level; every building is a box that grows with its level with a floating label (buildings under construction show a scaffold); a wall rings the village when the lord has built one.

## The village and the shared stock

Craft's world is the player's [Lords](https://lords.temnorid.com) village together with its farm, and the stock is the **same stock** in both games, not a copy: gathering in Craft raises the Lords stock, spending lowers it. All the village code is in the shared package `temnorid-village` (see its README).

- **Who sees what.** Signed in with **Google** (the shell's account) and a Lords save exists: *your* village and farm (places, buildings with their levels now, plots with their levels, the wall) and the shared stock. A **guest** (anonymous account), signed out, offline, or a Google player with no Lords save yet: a standard default village (12 places, the lord's hall in the centre, warehouse, granary, well and a few more buildings, a farm of 12 plots, stock 750 of each), with the reason shown in the Stock place. The stock of the standard village is kept in Craft's own save and never goes anywhere.
- **Loading.** The village is loaded after the shell menu (the shell restores the account in the background). "Loading your village…" shows for a moment; it waits at most about 10 seconds and then falls back to the standard village. It never blocks the game.
- **Gathering.** Each farm plot is a gather spot for one resource (the ordinary gather loop: hold E, the load goes into the backpack, store it at the warehouse). A plot's harvest per gather grows with its level: 2, plus one for every two levels, at most 8 (level 1 gives 2, level 2 gives 3, level 4 gives 4). Lords' hourly production per plot is unchanged; Craft only scales the size of one harvest. Plots grow back 25 seconds after being used up.
- **The warehouse** is the lord's stores: the six stocks and their capacity are shown live (production keeps running while Craft is open, as in Lords). Storing adds to the stock, clamped by the capacity of the warehouse (wood, stone, iron, clay), the granary (grain) and the well (water); the excess stays in the backpack.
- **The goal** counts what was stored in Craft this visit (10 of each resource), because the stock starts at 750.
- **Craft builds nothing.** It does not build or upgrade anything: Lords stays authoritative for construction, farm levels and everything else. Craft only changes the stock.
- **What is written to Lords: only `stock`.** At most one write every 5 seconds, and when you leave the game, pause or quit. A write reads your save fresh, works out the stock Lords has now (you may have spent resources in the browser), applies Craft's net change, and patches only the field `stock` with the document's `updateTime` as precondition (on a conflict it reads again and retries). It never creates the document, never writes another field, never writes for a guest, and writes nothing when the document is missing or the stock has an unexpected shape. The Firestore rules already let the owner read and write `users/{uid}/progress/lords`. A hard quit can lose the last few seconds of Craft's changes.
- **Online.** The host's village is shown to everyone: a guest sends `host.cr.hello` and the host answers with one `cr.village` event holding the village as JSON. Guests never load a village of their own. The host's account owns the stock and only the host writes to Lords; a guest's gather and store go through the request events to the host.
- **Scale.** Lords' village map becomes metres at 0.04 m a unit and its farm map at 0.1 m a unit (they are two different maps). Documented approximations are in the package README.

## Playing with others

Choose **Play with others** in the main menu (the shell's lobby): up to **4 players** share one village. The host owns the world: the village shown (its own Lords village or the standard one), the farm plots and the lord's stock. A guest's gather and store are requests that the host checks (the plot has something left, the guest stands near it and is not faster than gathering allows) and applies; each player's backpack stays with that player, and everybody sees the same plots and stock. Other workers are shown from their poses. A guest does not read or write their single-player save, does not load a village and never writes to Lords; if the host leaves, the game ends for everybody and returns to the main menu.

## Updates

The Windows game updates itself from the newest GitHub release at start (the installer runs silently and starts the game again). **Profile** can turn this off.

## Phones

Android (minimum API 26) and iOS builds come from Unity Build Profiles (`ShellSceneKit` sets the platform settings). Touch controls: a move stick, a look area (drag anywhere else), **Use** (hold to gather, tap to store), Jump, Run, View and **Bag** (opens the Stock place). The shell puts the places in a bottom bar and respects the safe area.

## Resources (the same as Lords)

Craft uses exactly the six resources of [Lords](https://lords.temnorid.com), with the same ids, and since 0.4.0 the same stock. The ids are stable: never rename them.

| Id | Name | Colour | On the farm |
| --- | --- | --- | --- |
| `wood` | Wood | `#3f6b2f` | forest plots |
| `stone` | Stone | `#7a746b` | quarry plots |
| `iron` | Iron | `#8a4b33` | mine plots |
| `water` | Water | `#2f78a0` | pond plots |
| `grain` | Grain | `#b8892a` | grain field plots |
| `clay` | Clay | `#a0613a` | clay pit plots |

Each plot has three harvests and then grows back, like the old nodes did. The standard village has two plots of each kind; a Lords farm has the plots the lord has.

## How it connects to Lords

Since 0.4.0 Craft talks to Lords' data: it shows the player's Lords village and farm and shares the stock (see "The village and the shared stock"). Lords stays the central strategy game; Craft only gathers and stores. Later, tools, weapons and armour made here will be used by heroes in the other games.

## Scope of this version

- The Lords village and farm (or the standard one), gather plots, gathering with progress, backpack, the shared stock, local save for the standard village.
- No farming, crafting recipes or building yet: those come after the first playable.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (keyboard and mouse), Android and iOS (touch). One map, local saves, optional multiplayer (see Playing with others).
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, `3d-constructor-character-view-camera`, `3d-constructor-character-view-controls`), `3d-unity-core`, `3d-unity-input` (all input, through `EngineInput`), `3d-unity-inventory` (the backpack), `3d-unity-save` (with `3d-save-schema`), `3d-unity-account` (the player's account and the Firestore client) and the Temnorid package `temnorid-village` (the Lords village, farm and shared stock). The gathering, the goal, the screens and the online wiring are written in this game (`Assets/Scripts`).
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-core`, `../3d-unity-input`, `../3d-unity-character`, `../3d-constructor-character-view-camera`, `../3d-constructor-character-view-controls`, `../3d-unity-inventory`, `../3d-unity-save`) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Level building:** menu **Temnorid > Craft > Build Scene** (or `-executeMethod Temnorid.Craft.EditorTools.CraftSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` from scratch: a safety ground, the `VillageView` (with its two materials, so the shaders are in the build), the worker, camera and light. The village and farm themselves are drawn at run time from the save. Do not edit that scene by hand. **Temnorid > Craft > Capture Preview Pictures** renders a few pictures to `Builds/Preview` (needs a graphics device; it shows the old world until updated).
- **Code map (`Assets/Scripts`):**
  - `CraftSession`: the rules with no Unity in them (gather into the backpack, store in the lord's stock, the goal of the visit) and `CraftSaveStore` (the save).
  - `CraftGame`: the scene's state (the village, loading, messages, pause, saving) and the link to `temnorid-village` (`VillageSource`, `VillageView`, `StockSyncer`). `PlayerWorker`: hold or press E near things. `ResourceNode` (a farm plot) and `Warehouse` (the village's warehouse): the things to interact with. `CraftHud`: all screens, plain uGUI.
- **Shell and online:** `CraftShell` sets the `ShellConfig` (GameId `craft`, Repo `WebArtWork/craft.temnorid.com`, 4 players, places World and Stock). Online code is `CraftNet` (host authority: `NetSession.IsAuthority` is true offline and on the host; the plots and the stock are published as entities, ids 1 to 6 the stock (one per resource, in `Items.All` order, State the whole units) and 100 and up the plots sorted by name; guests send `cr.gather` and `cr.dep` events, and `host.cr.hello` to get the village, which the host answers with `cr.village`), `RemoteWorker` (a guest's worker from poses). Scenes: Boot, Menu, Main (`CraftSceneBuilder` builds all three and the build order). Test ports 7804, 7805 and 7806 (`OnlineCraftTests`).
- **Tests:** `Assets/Tests/EditMode` covers the rules and the save file; `Assets/Tests/PlayMode` loads the scene with the standard village, with a fake Lords save in memory (the stock is shared, only `stock` is written, a guest account reads and writes nothing, offline falls back), and online (host, and a guest of a detached host). The packages `temnorid-village` and `3d-unity-account` have their own edit-mode tests; to run them from Craft add `"testables": ["com.itkamianets.temnorid-village", "com.itkamianets.3d-unity-account"]` to the manifest temporarily. No test reaches the real Firestore. Run them from Window > General > Test Runner, or with `Unity.exe -batchmode -nographics -runTests -testPlatform EditMode` (or `PlayMode`).
- **Save file:** `<persistent data path>/craft/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Craft\craft\default.json`): the backpack and, for the standard village, the stock. A Lords village keeps its stock in Lords. Old saves (0.2.0 and 0.3.0) load; their `warehouse` and `goalReached` are read and ignored.
- **Windows build:** **Temnorid > Craft > Build Windows Player** (or `-executeMethod Temnorid.Craft.EditorTools.CraftReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridCraft` and a portable zip. `installer/Craft.iss` turns the folder into `Builds/Installer/Temnorid-Craft-Setup.exe` with Inno Setup.
- **Do not add:** building or upgrading in Craft, or any write to Lords other than the `stock` field.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://craft.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/craft.temnorid.com/releases/latest/download/Temnorid-Craft-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-Craft-Setup.exe`.
3. Create a GitHub Release (tag `v0.4.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
