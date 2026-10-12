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

## Current phase: Prototype 2 — "tools and feel"
Prototype 1 (scoop → shake → tip → carry → feed, on one chunk) is done. Prototype 2 reworks the core loop
around tools before scaling up the terrain (Phase 1 of the roadmap).

**In scope**
- A. Sprint (hold Shift, relaxed jog, no stamina) and crouch (hold Ctrl, lowers the view to reach low ash;
  not for crawling through caves); a center dot that shows whether the pile is within scoop reach
- B. Scooped embers stay hidden under the ash in the sieve and only glimmer, like in the pile; shaking
  reveals them
- C. Tools and inventory: sieve and basket are tools; max 5 tools; one active tool in hand; the basket can
  be stowed even when full, or placed on solid ground (not on the pile) and poured into; the nest accepts
  embers from the sieve or the basket; one look-to-interact system
- Then: playtest the new loop

**Later, not now:** ash settling physics (after terrain scaling), ember jostle simulation in sieve/basket and
physics for a placed basket (Phase 2 polish). Still out of scope: shop, upgrades, helpers, powers, save
system, layers, valley, audio polish, real models.

## Folder structure
```
Assets/
  _Project/
    Scenes/            Prototype_Feel.unity, later Main.unity
    Scripts/
      Core/            bootstrapping, service wiring, shared utilities
      Player/          controller, camera, interaction
      Terrain/         voxel data, marching cubes, chunk meshing, dig operations
      Sifting/         sieve, shake logic, dust FX hooks
      Embers/          ember data, spawning, tiers
      Nest/            nest, feeding, heat
      Economy/         currencies, costs (later)
      UI/              HUD and menus
      Save/            persistence (later)
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
  ThirdParty/          imported assets and packages, untouched
```

Assemblies: runtime code compiles into `Cinderbird` (`Scripts/Cinderbird.asmdef`), tests into
`Cinderbird.Tests.EditMode` (`Tests/EditMode/Cinderbird.Tests.EditMode.asmdef`).

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

## Terrain design (prototype)
- Density grid per chunk (e.g. 16×16×16 cells, cell size ~0.25 m), values > 0 = ash.
- Dig = subtract a sphere/capsule from the density field; return the **removed ash volume**.
- Embers are **data points stored per chunk** (position + tier). Embers inside the removed volume move
  into the sieve's contents. Embers are not GameObjects until they are shown in the sieve or basket.
- Marching cubes produces the mesh; collider updated for touched chunks.

## How to work with me (Art)
- I am an experienced developer (C#-like X++ background) but new to Unity. Explain Unity-specific concepts
  briefly when they first come up.
- Work in **small, testable steps**. Since Prototype 2, each step ships with an **Editor setup command**
  (`Assets/_Project/Editor/SetupStep*.cs`, menu **Cinderbird → Setup → …**) that does the scene/asset wiring
  through Unity's API: idempotent, Undo-aware, saves the scene. I run it, then test. Tell me what to click
  and what to test; only list manual Editor steps for things a script can't reasonably do (e.g. look tuning).
  **The menu only lists uncommitted steps:** when a step is committed, its `SetupStep*.cs` is deleted in that
  same commit (its result lives in the committed scene/assets); `SetupUtility.cs` stays.
- **Never hand-edit** `.unity`, `.prefab`, `.asset` YAML or `.meta` files. Setup goes through Editor scripts
  using Unity's API (SerializedObject, AssetDatabase, Undo), never by writing those files directly.
- **Ask before** adding any package or third-party asset.
- Write EditMode tests for pure logic (terrain math, economy formulas).
- When a change touches several systems, propose the plan first and wait for my OK.

## Assets and licenses
- Every third-party asset (model, texture, sound, music, font, code) gets a line in `LICENSES.md`
  with source URL and license **in the same commit** it is added.
- Prefer CC0 (Kenney, Quaternius, Poly Haven, ambientCG). CC-BY is fine with credit. No "non-commercial" licenses.

## Definition of done — Prototype 2
- [ ] I can sprint and crouch, and the center dot tells me when the pile is in scoop reach
- [ ] Embers in the sieve only glimmer until I shake the ash out
- [ ] I can switch between sieve and basket, place the basket on the ground, pour into it, pick it up
      full, and feed the nest from either tool
- [ ] The new loop feels good for 20 minutes (tested by me + 2–3 friends)
