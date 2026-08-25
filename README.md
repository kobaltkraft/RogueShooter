# Rogue Arena — Unity 6 Arcade FPS

A complete, original arcade first-person shooter built with Unity 6, URP, the Input System and AI Navigation. Everything — arenas, enemies, weapons, UI, audio, VFX — is generated at runtime from ScriptableObject definitions and procedural builders: **no prefabs, no manual wiring, no asset setup**.

## Open and play

1. Install **Unity 6.0 (6000.0.58f2 or a compatible Unity 6 LTS)**.
2. Open this repository as a project (Package Manager restores URP, Input System, AI Navigation).
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

## Debug & validation

- **F3** — runtime overlay: FPS, state, wave/enemy counts, plus cheats (1 heal, 2 ammo, 3 god mode, 4 kill all, 5 restore)
- **Tools → Rogue Arena → Validate Project** — checks build scenes, layers, URP setup, content catalog sanity and cross-references

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
