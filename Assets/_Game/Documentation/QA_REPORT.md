# RogueArena — Pre-Alpha QA Report

**Scope:** Full audit & repair pass over all 78 runtime scripts, 4 scenes, project settings, and the runtime-generated content catalog. Static review + targeted repairs; verified with a 165-check automated static test suite (`Tools/QaStaticTests.py`, results in `QA_TEST_RESULTS.txt`).

**Environment:** Unity 6.0 (6000.0.58f2), URP 17.0.4, Input System 1.14.2, AI Navigation 2.0.9.
**Constraint:** No .NET compiler was available in the QA sandbox — all verification is by static analysis, grep-based invariant checks, and math replication tests. A compile pass in the Unity Editor is still required (see *Manual verification*).

---

## Verdict

| Area | Verdict |
|---|---|
| Compile risk (structure, syntax balance, APIs used) | **Clean** — verified statically; editor compile still required |
| Scene transitions & duplicate managers | **Clean** — singleton SystemRoot, per-scene bootstrap, UI rebuilt per scene |
| Event / memory leaks | **Clean** — all scene subscribers unsubscribe; 3 real leaks fixed this pass |
| Damage / death / rewards | **Clean** — death-once, damage-after-death blocked, dedup'd explosions |
| Weapons & ammo | **Clean** — negative ammo impossible, reload clamped, heat model guarded |
| Waves / boss / victory | **Clean** — watchdogs for stuck waves, boss death-once, no post-victory spawns |
| Save / load hardening | **Clean** — atomic write, backup, sanitize clamps, corrupt-file fallback |
| Performance (20–40 enemies) | **Good** — pooling everywhere, per-frame allocations removed, caps enforced |
| Known minor issues | 5 LOW items (below) — none block play |

**Overall: playable pre-alpha.** No known CRITICAL or HIGH issues remain in code.

---

## Fixed this pass

### CRITICAL (would have broken gameplay)

1. **Death drops never spawned** — `PickupManager.NotifyEnemyDied` was implemented but called by nobody, so enemy kills never dropped ammo/health/currency. `PickupManager` now subscribes to `GameEvents.EnemyKilled` in `Initialize` (unsubscribes in `OnDestroy`) and routes every kill through the drop rolls. *(Pickups/PickupManager.cs)*
2. **Melee attacked through walls** — `EnemyCombat` melee resolved on range/timer alone. Now requires a line-of-sight raycast (`Layers.OccluderMask`, eye at `agentHeight * 0.6`) before damage lands. *(AI/EnemyCombat.cs)*
3. **Material leak on every ring VFX** — `EffectDirector.Ring()` assigned `renderer.material = ...` per play; each assignment orphans the previous instance on a pooled renderer (rings never die). Rings now recolour the renderer's own instance created once in `BuildRing()`. *(VFX/EffectDirector.cs)*
4. **Material leak on every pickup spawn** — `MaterialLibrary.Lit/Emissive` built a new `Material` per call; ammo drops alone would create hundreds of materials per run. Both are now cached by name (all dynamic names are colour-hashed, so the cache is small and bounded). *(Visual/MaterialLibrary.cs)* The elite crown name was made colour-dependent to match (`EliteCrownMat_{hex}`), fixing a latent colour collision introduced by the cache. *(AI/EnemyElite.cs)*
5. **Boss could spawn off the NavMesh** — `WaveManager` offsets the boss spawn by `Vector3.back * 2`, which can push the point off-mesh. `BossFactory.Spawn` now snaps via `NavMesh.SamplePosition(6f)` before placing. *(AI/BossFactory.cs)*
6. **Legacy saves could grant free upgrade tiers** — `WeaponUpgradeTier`/`PlayerUpgradeTier` returned raw list counts; a hand-edited or pre-balance save would count as already-maxed tiers. Both now clamp to the track's `MaxTier`. *(Progression/ProgressionService.cs)*

### HIGH (visible bugs / leaks)

7. **One-shot pickups lingered forever** — `Pickup.Collect` on one-shot drops disabled visuals but left the GameObject + collider alive for the whole run (stacking hidden colliders). Now disables the collider and destroys the object after 0.8 s. Unused `Init()` removed. *(Pickups/Pickup.cs)*
8. **Powerups accumulated without bound** — live powerup drops had no cap; a long run could flood the arena. `PickupManager` now caps live powerups at 6 (oldest destroyed first, null entries pruned). *(Pickups/PickupManager.cs)*
9. **Drones flew through walls** — `EnemyFlight` had no collision at all. Added axis-separated wall sliding (full step → horizontal-only → vertical-only, `OverlapSphereNonAlloc` against `OccluderMask`) so drones bank along geometry instead of ghosting through it. Also guarded `LookRotation` against a zero horizontal vector (player directly above/below). *(AI/EnemyFlight.cs)*
10. **Damage dealt to a dead player** — `EnemyCombat`'s `DamageDealtToPlayer` fired regardless of player state; melee and hitscan damage are now gated on `playerHealth.IsAlive`. *(AI/EnemyCombat.cs)*
11. **Per-frame `GetComponent` in interactive systems** — switches, turrets, moving platforms and hazard zones called `GetComponent`/`GetComponentInParent` every frame. All cached (lazy, re-resolved when the player reference changes); the hazard now damages the session player directly instead of walking collider parents. *(Environment/Interactive.cs)*
12. **Per-frame string allocation in HUD** — `Hud.UpdateWeapon` rebuilt the ammo string every frame; now only writes when magazine/reserve/reload state changes. *(UI/Hud.cs)*

### MEDIUM

13. Dead code removed: unused `hitKill` field + duplicate combo-line in `Hud`, unused `fallbackY` parameter in `OverlayScreens.AddSliderRow`.
14. Dead `managed` list removed from `PickupManager` (was write-only).
15. Wave-health/ammo drops made one-shot so they can't be farmed forever.

---

## Audited clean (no changes needed)

- **Health**: death fires exactly once (`Died` invoked from a single path, `IsAlive` gate blocks re-entry), non-positive damage/heal blocked, armour absorbs 60 % and drains before HP.
- **EnemyBrain**: decision cadence 0.18 s, stuck detection, full-stop stagger when the player dies, kill publish → `EnemyKilled`, `Pacify` for wave-clear teardown.
- **BossBrain** (404 lines): death-once, `StopAllCoroutines`, colliders off on death, per-hit phase catch-up (can't skip phases), summons don't corrupt wave counts, `SetDestination` guarded by `isOnNavMesh`, escorts self-heal the wave counter (45 s watchdog).
- **EnemyElite**: modifier stacking without per-combination scripts, navmesh-guarded teleport, OnDestroy unsubscribes, death-explosion chains credit the original killer.
- **FirstPersonController**: no infinite jump (coyote + buffer, ground required), jump blocked while crouched/dashing, bhop retention `Range(.5, 1)` cannot add speed, air control can't exceed the movement budget, slide has cooldown + min-speed, `ResetState` for restarts.
- **DashController** (charges bounded, `CancelDash` on death), **PlayerVitals** (death-once, heal-once-per-boss tracking), **AgentSpawner.FindSafePosition** (navmesh snap + 3 m player-distance nudge).
- **Weapon** fire/reload state machine: heat/magazine guards, auto-reload, dry-fire, absolute-time reload (pause-safe), `FinishReload = min(need, Reserve)`, `AddAmmo` caps reserve.
- **Projectile**: 96-instance pool, owner-layer skip (no self-shots), closest-hit resolution, splash through dedup'd `ExplosionDamage`, 9 s lifetime.
- **GameFlow / GameState machine**: strict transitions, `TryPause` refuses outside gameplay, restart guarded, no double victory/defeat (`runEnded` latch).
- **TimeController**: reason-dictated with min-wins semantics; pause reasons can't strand each other.
- **SaveService**: v1 JSON, atomic tmp→swap with `.bak`, corrupt/missing/outdated fallback to defaults, `Sanitize` clamps every persisted counter non-negative and level ≥ 1.
- **Event lifecycle**: persistent services subscribe once on `SystemRoot` (torn down by `GameEvents.ResetAll()` only at root destruction); every scene-local subscriber (Hud, OverlayScreens, MenuScreen, PickupManager, …) unsubscribes in `OnDestroy`. Verified: `ResetAll` is called from exactly one place.
- **Layers/masks**: bullets can't hit the shooter's own projectiles, enemy projectiles can't hit enemies, occluders can't hide the player from enemy vision, hazards occlude properly.
- **UI**: event-driven; `UiRoot.OnDestroy` destroys all child screens, so restart/menu cycles can't duplicate HUD or overlays.
- **DebugOverlay**: per-frame `StringBuilder` accepted (debug tool, hidden by default).

---

## Performance summary

- **Pooling**: projectiles (96), particles, tracers, rings, lights — all `ComponentPool` with `maxInstances` saturation behaviour (skip politely, never grow unbounded).
- **Caps**: `AgentSpawner.GlobalEnemyCap = 34`, wave `maxAlive` clamped 10–24, live powerups ≤ 6, material caches bounded by unique names/colours.
- **Zero per-frame allocations** in gameplay `Update` loops (checked mechanically across all scripts; the two debug-only exceptions are whitelisted in the test suite).
- **No `Find*`/`Camera.main` in hot paths** — the five `FindAnyObjectByType` calls are one-shot init/boot paths.
- **Physics**: all overlap/raycast calls use `*NonAlloc` variants with preallocated buffers.

---

## Architecture notes (documented, not refactored)

- `GameContent` is a single 500-line static factory — intentional: every balance number lives in one file and the editor validator can export the SOs. Lookups are linear `List.Find`; acceptable at call frequency (menu/purchase/spawn), not a hot path.
- `MaterialLibrary` is a code-authored material library (no .mat assets); now name-cached and idempotent across scene loads.
- `SystemRoot` (`[DefaultExecutionOrder(-1000)]`, `AutoCreate`, `DontDestroyOnLoad`) is the composition root; everything else is created by `BootstrapArena` in strict order (arena → player → spawner → pickups → waves → session → UI → flow).

---

## Not fixed (known LOW issues, safe to ship)

| # | Issue | Why left |
|---|---|---|
| 1 | Enemy alert SFX can retrigger on LOS flicker | Cosmetic audio spam; needs a per-enemy cooldown timer — low value vs. risk |
| 2 | Mid-load weapon-switch input can play the old selection once | One-frame edge; weapon switch is blocked the same frame it resolves |
| 3 | Vampiric elites don't leech from projectile-splash kills (event only fires melee/hitscan) | Requires threading damage-source metadata through `ExplosionDamage`; documented behaviour instead |
| 4 | `BossDefeated`'s `wasFinal` flag is always false | Harmless: victory is driven by the wave/boss completion path, not the flag |
| 5 | Boss death-sequence uses `unscaledDeltaTime`, so the corpse explodes during pause | Accepted: keeps the death beat readable if the player pauses at the kill moment |

---

## Automated tests

`Tools/QaStaticTests.py` — **165 checks, 0 failures** (run `python3 Tools/QaStaticTests.py` from the repo root; full log in `Assets/_Game/Documentation/QA_TEST_RESULTS.txt`). Covers:

- structure & compile-risk (file inventory, brace balance, banned APIs)
- hot-path hygiene (allocations / `GetComponent` / `.material` in `Update`)
- event-subscription balance and teardown for every script
- **damage math**: armour absorb, lethal hits, damage-after-death, multi-hit frames, explosion falloff/dedup (replicated in Python from extracted constants)
- **headshot**: per-weapon multipliers ≥ 1, applied in the damage path
- **ammo**: per-weapon data sanity (incl. the heat model for the plasma rifle), reserve ≥ 2 magazines, reload clamps
- **upgrade stacking**: tier multipliers (×1.15/×1.5/×0.8), max tiers, cost escalation, legacy-save clamping
- **score/combo**: window, +10 % steps, ×3 cap, long accumulation
- **wave progression**: budget `4 + wave·1.6`, alive cap 10–24, elite chance clamp 0.45, authored survival waves 1–10 + boss 11 + finale 12, watchdogs present
- **difficulty scaling**: per-mode `difficultyScalePerWave`, boss stat scale, enemy definition sanity (11 kinds, budget/minWave/score)
- **save serialization**: JSON round-trip of the full `SaveData` shape, tampered-negative clamping, atomic write/backup
- **layers/masks**: self-shot, friendly-fire, vision and occlusion masks
- **pooling & static-state leaks**

---

## Manual verification required (Unity Editor)

The sandbox has no Unity/.NET runtime. Before calling this pass done, run in the Editor:

1. **Compile** — open the project, confirm zero console errors/warnings (a syntax-level mistake is the main residual risk of static-only QA).
2. **Full cycle** — boot → menu → start Survival/Industrial → die → restart → die → quit to menu → start Time Attack/Desert → win or die → City/Endless. Watch for duplicate HUD, audio, players, or "missing" systems after each transition.
3. **Pause rules** — pause during reload/dash/boss charge/upgrade screen; confirm time freezes (including reload timers and boss coroutines) and no input leaks into menus.
4. **Death drops** — kill 20+ enemies; verify ammo/health/scrap drops now appear (fix #1) and one-shot pickups vanish.
5. **Melee through walls** — let a melee enemy chase you around a corner; confirm no damage while a wall is between you.
6. **Drone navigation** — Desert/City arenas: drones should slide along walls, never clip through.
7. **Boss fight** — Survival wave 11 (VULCAN-9): boss snaps onto the navmesh, phases advance per HP threshold, death fires once, victory screen exactly once, escorts clear.
8. **Save tampering** — hand-edit the save (negative currency, level 0, tier 999 upgrade) and relaunch; values must clamp.
9. **Frame profile** — wave 12 with 24+ alive: confirm no GC spikes (Profiling → Memory), 60 fps target on mid hardware.
10. **Editor validator** — run the `ProjectValidator` menu command; it should report no missing scene refs or layer mismatches.

---

*Report generated by the pre-alpha QA pass. All code changes are on the working branch; see `git log` for the fix commits.*
