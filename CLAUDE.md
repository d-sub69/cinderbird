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

## Current phase: Prototype 1 — "the feel"
Goal: prove that digging and sifting ash feels satisfying for 20 minutes **with no upgrades and no real art**.

**In scope**
1. First-person controller (walk, look, jump; no sprint needed)
2. A small diggable ash pile (voxel terrain, marching cubes)
3. Scoop: aim at the pile, click to scoop; ash volume is removed from the terrain and goes into the sieve
4. Shake: hold a key / wiggle the mouse; dust particles fall from the sieve, ash amount drops to zero,
   embers that were inside the scooped volume remain visible in the sieve
5. Tip the sieve into a carried basket
6. Walk to the nest and pour the basket in; the nest fire flares; a Glow and Heat counter goes up
7. Placeholder grey materials, simple debug UI

**Out of scope for now:** shop, upgrades, helpers, powers, save system, layers, valley, audio polish,
real models. Do not build these until Prototype 1 is approved.

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
- Work in **small, testable steps**. After each step, tell me exactly what to do in the Unity Editor
  (create object, add component, assign references) as a short numbered list.
- **Never hand-edit** `.unity`, `.prefab`, `.asset` YAML or `.meta` files. Create scripts; I do the
  Editor setup from your instructions.
- **Ask before** adding any package or third-party asset.
- Write EditMode tests for pure logic (terrain math, economy formulas).
- When a change touches several systems, propose the plan first and wait for my OK.

## Assets and licenses
- Every third-party asset (model, texture, sound, music, font, code) gets a line in `LICENSES.md`
  with source URL and license **in the same commit** it is added.
- Prefer CC0 (Kenney, Quaternius, Poly Haven, ambientCG). CC-BY is fine with credit. No "non-commercial" licenses.

## Definition of done — Prototype 1
- [ ] I can walk up to the pile, scoop, shake, and see embers left in the sieve
- [ ] The pile visibly shrinks where I dig, with no frame drops on my machine
- [ ] Basket → nest → counters go up, the fire reacts
- [ ] It feels good to do for 20 minutes (tested by me + 2–3 friends)
