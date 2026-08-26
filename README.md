# Rogue Arena — Unity 6 Arcade FPS

A complete, original arcade first-person shooter built with Unity 6, URP, the Input System and AI Navigation. Everything — arenas, enemies, weapons, UI, audio, VFX — is generated at runtime from ScriptableObject definitions and procedural builders: **no prefabs, no manual wiring, no asset setup**.

## Open and play

1. Install one of the supported Unity editors:

   | Unity editor | Changeset |
   |---|---|
   | **6000.0.58f2** | `92dee566b325` |
   | **6000.3.22f1** | `1c726e1fb402` |
   | **6000.5.9f1** | `b57deb96f08d` |

2. Open this repository as a project (Package Manager restores the shared, pinned URP, Input System, AI Navigation and uGUI versions).
3. Open **any** scene — `Assets/_Game/Scenes/MainMenu.unity` is the intended entry — and press **Play**.
   - The persistent `SystemRoot` bootstraps itself automatically (even in play-in-scene).
   - Opening an `Arena_*` scene directly auto-starts a Survival run in that arena.

All four scenes are in Build Settings. NavMesh, arena geometry, lighting, UI and audio are built when a run starts.

## Game modes

| Mode | Goal |
|---|---|
| **Survival** | 12 scripted waves ending in a VULCAN-9 boss fight |
| **Time Attack** | Reach a 40-kill quota as fast as possible |
| **Boss Rush** | All bosses back-to-back with scaling stats |
| **Endless** | Procedurally generated waves that scale forever |

## Arenas

- **Industrial Complex** — containers, catwalks, pillars, hazard floors, a sliding blast door and two auto-turrets
- **Desert Excavation** — central dune plateau, rock cover clusters, ruined arches, a hazard pit crossed by a moving platform
- **Neon City** — buildings with interiors and rooftop access, cars as street cover, an elevator, switch-locked door and a rooftop turret

Every arena has AI cover points, spawn points, weapon/powerup pads, explosive barrels and breakable crates.

## Controls

- **WASD** move · **Mouse** look · **LMB** fire · **RMB/Shift** sprint · **Ctrl** crouch (slide while sprinting)
- **Space** jump (buffered, coyote time) · **Q** dash (2 charges) · **R** reload · **1/2/3 + wheel** weapons
- **E** interact (switches) · **Esc** pause · **F3** debug overlay

## Systems

- **Weapons** — 8 data-driven weapons (rifle, SMG, burst, marksman, shotgun, plasma, grenade launcher, rocket) with recoil, dynamic crosshair spread, view-model sway/bob, muzzle flashes, reload and switch animations, projectile pooling and splash damage
- **Enemies** — 11 archetypes with distinct silhouettes and utility-based AI: sight/vision cones, hearing, last-known-position search, strafing, cover use, preferred engagement distances, flanking and retreat. Flying drones use steering flight rather than NavMesh
- **Elites** — stackable modifier system (armored, vampiric, teleporting, summoning, exploding, berserk, regenerating, shielding…) with no per-combination scripts
- **Boss** — reusable 3-phase boss with telegraphed barrages, ground slams, charges and summons; accelerating multi-explosion death sequence
- **Waves** — authored waves plus balanced procedural generation (budget-based, per-type caps, pacing), wave modifiers (double speed, hardened, low gravity, rapid fire, limited ammo, elite invasion)
- **Progression** — XP/levels, scrap currency, weapon unlocks and per-weapon upgrade tracks, 6 player upgrades, 12 challenges with rewards, persistent records
- **Environment** — explosive barrels with chain reactions, breakables, sliding doors, moving platforms/elevator, cycling hazard floors, switches, auto-turrets
- **Reliability** — strict game-state machine, versioned atomic saves with backup fallback, null-safe service locator, pooled projectiles/VFX, static teardown on every scene change, `Tools → Rogue Arena → Validate Project` editor validation
- **Audio** — fully procedural synthesis (~55 clips): weapons, impacts, explosions, enemy calls, UI, stingers and layered music, with category volumes and voice limiting
- **Visuals** — consistent material library, emissive accent lighting, subtle bloom + vignette, distinct silhouette language, hit flashes and death bursts

## Unity version compatibility

`6000.0.58f2` is intentionally the serialized project baseline. Both newer supported editors can import that baseline, while a project saved only in a newer serialization format cannot reliably be reopened in the oldest editor. The shared package pins in `Packages/manifest.json` are also intentionally conservative; do not upgrade packages from just one editor and commit the result without rerunning the full matrix. uGUI is a direct dependency because Unity 6.5 no longer supplies it transitively through the Render Pipeline Core package.

The manifest also pins the built-in modules the code depends on (`com.unity.modules.ai`, `.audio`, `.jsonserialize`, `.particlesystem`, `.physics`, all at `1.0.0`). Built-in modules can be toggled off per-project in the Package Manager, and a disabled module turns into hard compile errors (`CS1069 … forwarded to assembly UnityEngine.<Module>Module`); pinning them in the manifest keeps the project compiling regardless of local module toggles. Do not remove a pin while its APIs are still in use.

When switching editor versions, close Unity first. If Unity reports stale imports, delete the generated `Library` directory and reopen the project; do not copy a `Library` directory between editor versions. Any automatic changes made by a newer editor should be reviewed before committing so the `6000.0.58f2` baseline is preserved.

The compatibility contract lives in `Tools/UnityCompatibility.json`. To check its project marker and package pins without Unity:

```bash
python3 Tools/CheckUnityCompatibility.py
```

To compile and run the project validator in isolated copies under all three installed editors:

```bash
python3 Tools/RunUnityCompatibilityMatrix.py \
  --editor 6000.0.58f2=/path/to/6000.0.58f2/Editor/Unity \
  --editor 6000.3.22f1=/path/to/6000.3.22f1/Editor/Unity \
  --editor 6000.5.9f1=/path/to/6000.5.9f1/Editor/Unity
```

The runner also discovers standard Unity Hub install locations. Matrix logs are written to `Artifacts/unity-compatibility/` and ignored by Git.

## Debug & validation

- **F3** — runtime overlay: FPS, state, wave/enemy counts, plus cheats (1 heal, 2 ammo, 3 god mode, 4 kill all, 5 restore)
- **Tools → Rogue Arena → Unity Compatibility** — checks the current editor, project marker and shared package pins
- **Tools → Rogue Arena → Validate Project** — runs compatibility checks plus build scenes, layers, URP setup, content catalog sanity and cross-references

## Project layout

```
Assets/_Game/
  Scenes/          MainMenu, Arena_Industrial, Arena_Desert, Arena_City (empty by design)
  Scripts/
    Core/          services, state machine, game flow, scene director, system root
    Player/        locomotion, camera rig, dash, vitals, powerups, player factory
    Weapons/       weapon definitions, controller, view models, projectiles, factory
    AI/            brains, combat, elites, flight, spawner, enemy/boss factories, cover
    Waves/         wave definitions, procedural generation, modifiers
    Environment/   arena builders, interactive props, primitive kit
    Pickups/       pickups, powerups, pickup manager
    Progression/   XP, upgrades, challenges
    UI/            UI kit, HUD, menus, screens, diorama
    Audio/         procedural SFX synthesis + director
    VFX/           pooled effect director
    Content/       the whole game catalog as code-built ScriptableObjects
    Persistence/   versioned save + settings services
```

Save data: `persistentDataPath/roguearena_save.json` (atomic writes + rolling `.bak`).

## Design notes

- No ML: all AI is deterministic, inspectable utility scoring — debuggable in the editor with gizmos (see `Core/DebugSettings`).
- No authored art: a procedural material/silhouette library keeps the visual identity consistent and the repo tiny.
- Everything data-driven: new weapons/enemies/waves/arenas are ScriptableObject additions, not code.
