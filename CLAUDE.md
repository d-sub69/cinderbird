# Cinderbird — Project Guide for Claude

## What this game is
**Cinderbird: Sift the Ashes of a Phoenix** (short name: Cinderbird) is a **first-person 3D cozy incremental game** for Steam (premium, $4.99, 3–5 hours).
The last phoenix burned out on a mountaintop and left a mountain of ash. The player (Wick, a small fire
salamander) scoops ash with a sieve, shakes out the dust, carries the glowing embers to the nest, and
upgrades tools until every ember is found. Final ember = phoenix reborn = game ends.

- **Full design doc:** https://claude.ai/code/artifact/235f11c2-09a8-4379-b24a-dfd44ba54a88
- **Core loop:** Scoop → Shake → Carry → Feed the nest → Earn Glow (currency) + Heat (progress) → Upgrade.
- **Look:** stylized low-poly, night scene, warm ember glow, bloom.

## Tech stack
- Unity 6 LTS, Universal Render Pipeline (URP), C#
- New Input System (not the legacy Input Manager)
- TextMeshPro for UI text
- Git + Git LFS for binary assets

## Current phase: Phase 3 — the whole game, start to credits (placeholder content)
Done and playtested: Prototype 1 (core loop), Prototype 2 (tools, inventory, placeable basket, hidden embers,
ash pour), Phase 1 (multi-chunk Burst terrain, ash layers, save system, game shell with pause/options,
performance budget), ash settling (scoops drain while walls slide in, flying embers) and Phase 2, the vertical
slice: Heat levels 1–3 opening layers by the design rule, the shop stall (3 sieves, the mesh, power upgrades, tabs),
5 offerings and the shrine shelf, Sally the helper (walks, scoops, shakes, feeds; A* on a walk grid; sent to a spot
with a green circle), the Warm Wind power (hold F 0.8 s), the phoenix silhouette in the fire and the valley's
first thaw. Run `Docs/PerformanceTest.md` with the F7 scenario key.

Phase 3 builds the full 3–5 hour curve on cheap placeholders so the economy can be balanced before content and art
(Phase 4: offerings, feathers' story, models, audio; Phase 5: demo and release). One committed step each:
- 3A. Heat levels 1–10 and layers 3–6 by the design rule; ember tiers placed per layer as in the design doc
- 3B. The rest of the upgrades: sieves up to the enchanted winnow, bigger baskets, the ember chute
- 3C. Obstacles, never punishments: cinder glass, obsidian, wet ash; the pick and gloves
- 3D. The remaining powers: Heat Sight, Kiln Breath, Ember Call, Spirit Birds, Rising Sun
- 3E. The remaining helpers: ash moths, fire beetle, stone tortoise
- 3F. Feathers (system, placeholder memories), phoenix stages and valley thaw for every Heat level
- 3G. The finale: the Heart Ember, rebirth, spring, credits, free play
- 3H. Balance pass (a tested pacing simulator, a full playthrough) and the minimum-spec performance run

**Still out of scope:** real offering/feather content, real models, audio beyond placeholders, the demo build.

## Folder structure
```
Assets/
  _Project/
    Scenes/            Main.unity (the game; start-up runs through Core/GameBootstrap)
    Scripts/
      Core/            bootstrapping, service wiring, shared utilities
      Player/          controller, camera, interaction
      Terrain/         voxel data, marching cubes, chunk meshing, dig operations
      Sifting/         sieve, shake logic, dust FX hooks
      Embers/          ember data, spawning, tiers
      Nest/            nest, feeding, heat
      Offerings/       offerings found in the ash, the shrine shelf, set bonuses
      Helpers/         Sally the helper: work rules, walk grid and A*, mover, animation, sending her to a spot
      Powers/          the player's powers (Warm Wind) and their cooldowns
      Valley/          the valley below the summit: its placeholder landform and its thaw by Heat level
      Economy/         currencies, costs (later)
      UI/              HUD and menus
      Save/            persistence: save file, autosave, terrain snapshots
      Options/         player options (options.json), applied to camera, input, quality, display, volume
    Data/              ScriptableObject assets (ember tiers, upgrade definitions, balance values)
    Prefabs/
    Materials/
    Models/
    Textures/
    Audio/
    Settings/          input actions and other project-level config assets
    Editor/            editor-only setup tooling (menu Cinderbird → Setup → …), namespace Cinderbird.EditorTools
    Tests/
      EditMode/        unit tests for pure C# logic (must be under Assets/ for Unity to compile them)
      PlayMode/        smoke tests on the real Main scene (start-up, dig chain); saves go to a temp folder
  ThirdParty/          imported assets and packages, untouched
```

Assemblies: runtime code compiles into `Cinderbird` (`Scripts/Cinderbird.asmdef`), tests into
`Cinderbird.Tests.EditMode` (`Tests/EditMode/Cinderbird.Tests.EditMode.asmdef`) and `Cinderbird.Tests.PlayMode`
(`Tests/PlayMode/Cinderbird.Tests.PlayMode.asmdef`).

## Architecture rules
- **Namespaces:** `Cinderbird.<Folder>` (e.g. `Cinderbird.Terrain`, `Cinderbird.Sifting`).
- **Keep logic in plain C# classes where possible** (no MonoBehaviour), so it can be unit tested in EditMode.
  MonoBehaviours are thin adapters: input, rendering, wiring.
- **Data-driven:** ember tiers, values, upgrade costs and balance numbers live in ScriptableObjects under
  `Assets/_Project/Data/`, never hard-coded.
- **Communication:** C# events or ScriptableObject event channels. No `FindObjectOfType` / `GameObject.Find`
  at runtime; references are wired in the Inspector or passed in.
- **No singletons** except a single, explicit bootstrap if truly needed.
- **One class per file**, file name = class name.

## C# conventions
- `PascalCase` for types, methods, properties, events; `_camelCase` for private fields; `camelCase` for locals/parameters.
- `[SerializeField] private` instead of public fields.
- Always explicit access modifiers.
- Prefer `readonly` and small methods; early returns over deep nesting.
- Comments explain *why*, not *what*.

## Performance rules
- No allocations in `Update`/`FixedUpdate` hot paths (no LINQ, no `new` lists per frame).
- Terrain is chunked; only re-mesh chunks touched by a dig.
- Plan for the Jobs System + Burst for meshing once the prototype works; keep the meshing code pure and
  data-oriented so it can be moved there.
- Embers: emissive material + bloom for all; real point lights only on the nearest few (cap configurable).
- **Minimum spec:** GTX 1050, i5-6500, 8 GB RAM, 60 fps at 1080p Medium. **Dev PC:** Ryzen 7 5700X, RTX 3070 Ti, 32 GB.
- **Budget** (dev PC, 1080p Medium, VSync off; `Data/PerformanceBudget.asset`, red in the F3 overlay): main thread
  ≤ 6 ms, GPU ≤ 4 ms, 0 B garbage per frame, dig incl. re-mesh ≤ 4 ms, autosave main-thread part ≤ 2 ms,
  start-up ≤ 3 s. Check with `Docs/PerformanceTest.md` (builds via Cinderbird → Build; the game logs `[Perf]` lines).

## Terrain design
- A `VoxelWorld` of chunks, each a density grid of 32×32×32 cells (cell size 0.25 m), values > 0 = ash.
  Neighboring chunks share their border samples (writes update every copy) so meshes are seamless; only the
  world's outer border is forced to air.
- Dig = subtract a sphere/capsule from the density field; return the **removed ash volume**.
- Embers are **data points stored per chunk** (position + tier). Embers inside the removed volume move
  into the sieve's contents. Embers are not GameObjects until they are shown in the sieve or basket.
- Marching cubes produces the mesh; collider updated for touched chunks. Colliders bake in the background from their own
  copy of the shape and are never waited for; sliding ash re-meshes a few chunks per frame and re-bakes colliders at
  most 4× a second, so a slide of any size costs each frame about the same (`ChunkMeshUpdater`).
- Settling (`AshSettler` / `AshSettling`): loose ash around a dig slides to the repose angle (50°, the generated
  mountain's own slopes count as stable), overhangs collapse, locked layers act as rock, mass is conserved exactly.
  A scoop drains out of the pile over the pour time while the walls sink in, as one simulation on a worker thread.

## Your role: technical lead, not just an implementer
- Approach every request as a top-tier game developer with an analytical mind: analyze it, propose the best
  technical solution, then build it. Guide me toward that solution rather than only executing my wording.
- Solutions follow solid OOP design (clear responsibilities, encapsulation, composition over inheritance, small
  testable units) and current, proven game-development practice (data-driven design, jobs/Burst where it pays,
  allocation-free hot paths, event-driven communication).
- If a request is clearly wrong, or would work against how games in general or this game in particular should
  work (feel, performance, maintainability, player experience), say so plainly with the reason and a better
  option before building anything. Don't build something you believe is a mistake without flagging it first.

## How to work with me (Art)
- I am an experienced developer (C#-like X++ background) but new to Unity. Explain Unity-specific concepts
  briefly when they first come up.
- Work in **small, testable steps**. Since Prototype 2, each step ships with an **Editor setup command**
  (`Assets/_Project/Editor/SetupStep*.cs`, menu **Cinderbird → Setup → …**) that does the scene/asset wiring
  through Unity's API: idempotent, Undo-aware, saves the scene. I run it, then test. Tell me what to click
  and what to test; only list manual Editor steps for things a script can't reasonably do (e.g. look tuning).
  **The menu only lists uncommitted steps:** when a step is committed, its `SetupStep*.cs` is deleted in that
  same commit (its result lives in the committed scene/assets); `SetupUtility.cs`, `SetupUi.cs` (menu-building helpers) and `BuildMenu.cs` stay.
- **Never hand-edit** `.unity`, `.prefab`, `.asset` YAML or `.meta` files. Setup goes through Editor scripts
  using Unity's API (SerializedObject, AssetDatabase, Undo), never by writing those files directly.
- **Ask before** adding any package or third-party asset.
- Write EditMode tests for pure logic (terrain math, economy formulas).
- Testing aids (debug keys, the F3 overlay, Test Mode) check `Core/DevTools.Enabled`: on in the Editor, development
  builds and the Performance Test build, off in the Release build players get.
- When a change touches several systems, propose the plan first and wait for my OK.

## Assets and licenses
- Every third-party asset (model, texture, sound, music, font, code) gets a line in `LICENSES.md`
  with source URL and license **in the same commit** it is added.
- Prefer CC0 (Kenney, Quaternius, Poly Haven, ambientCG). CC-BY is fine with credit. No "non-commercial" licenses.

## Definition of done — Phase 3
- [ ] I can play from the first spark to the credits: Heat 1–10, all six layers, every ember tier
- [ ] Every upgrade, power, helper and obstacle from the design doc works (placeholder looks)
- [ ] Feathers can be found; the phoenix and the valley change at every Heat level
- [ ] Placing the Heart Ember ends the game with the rebirth, then free play
- [ ] A full playthrough takes 3–5 hours and the upgrade pacing hits the design doc's targets
- [ ] Carried over from Phase 1: the frame-rate target holds on real minimum-spec hardware
