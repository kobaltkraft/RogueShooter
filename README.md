# Rogue Arena — Unity 6 FPS Prototype

A small, original 3D arcade FPS foundation built with Unity primitives, the Input System, and AI Navigation. The arena is assembled at runtime to keep the prototype portable and eliminate manual reference wiring.

## Open and play

1. Install **Unity 6.0 (6000.0.58f2 or compatible Unity 6 LTS)** with Unity Hub.
2. Add this repository as a project and allow Package Manager to restore dependencies.
3. Open `Assets/_Game/Scenes/RogueArena.unity`.
4. Press **Play**. Click the Game view if the mouse is not captured.

The scene is already in Build Settings. No prefab, NavMesh, input, or inspector setup is required. The NavMesh and primitive arena are built when Play mode starts.

## Controls

- **WASD** — move
- **Mouse** — look
- **Left mouse** — automatic fire
- **Left Shift** — sprint
- **Left Ctrl** — crouch
- **Space** — jump
- **R** — reload

## Prototype systems

- CharacterController first-person locomotion, mouse look, sprint, crouch, jump, and gravity
- Event-driven player/enemy health and death
- Modular weapon definition plus hitscan rifle with magazine/reserve ammo, automatic fire, spread, recoil-ready tuning, reload, procedural placeholder shot, muzzle flash, headshots, impacts, and hit marker
- NavMeshAgent enemy with Idle, Patrol, Chase, Attack, Search, and Dead states; FOV/LOS detection and last-known-position searching
- Three waves (3, 5, and 7 enemies), safe designated spawn selection, victory, defeat, and restart
- Runtime-built multi-route arena, cover, platform, stairs, lighting, materials, HUD, crosshair, and end screens

## Extending enemies

Create a new enemy factory or prefab with a collider, `NavMeshAgent`, `Health`, and an AI component implementing the desired behavior. Subscribe death to `GameEvents.EnemyDied`, as `EnemyBrain` does. Replace or branch `ArenaFactory.CreateEnemy` from `WaveManager.SpawnEnemy` to select enemy types.

## Extending weapons

Create a `WeaponDefinition` asset via **Assets > Create > Rogue Arena > Weapon Definition** and tune damage, fire rate, magazine, reserve, spread, range, reload, and headshot multiplier. Instantiate a `HitscanWeapon` and call `Initialize` with the definition, aiming camera, input reader, and optional muzzle particles. Alternate firing behavior can be added behind a shared weapon interface without changing player movement, health, waves, or HUD.

## Project layout

Gameplay content lives under `Assets/_Game`, separated into Scenes, Scripts (Core, Player, Weapons, Combat, AI, Enemies, Waves, UI), Prefabs, Materials, Audio, Effects, and ScriptableObjects. Placeholder directories are intentionally ready for authored assets in later versions.
