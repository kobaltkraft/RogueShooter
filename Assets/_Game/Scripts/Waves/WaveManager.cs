using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.AI;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Weapons;

namespace RogueArena.Waves
{
    /// <summary>
    /// Drives entire runs. Pulls authored waves from the mode definition, then
    /// generates balanced procedural waves for endless play, handles pacing,
    /// preparation beats, wave modifiers, elite rolls, boss waves and mode-specific
    /// victory conditions (Time Attack quota, Boss Rush sequence, Survival finale).
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        GameModeDefinition mode;
        AgentSpawner spawner;
        RngService rng;
        List<Transform> spawnPoints = new List<Transform>();
        Transform player;

        FirstPersonController playerLocomotion;
        WeaponController playerWeapons;
        Pickups.PickupManager pickups;

        public int WaveNumber { get; private set; }
        public int EnemiesRemaining { get; private set; }
        public int KillsThisRun { get; private set; }
        public bool RunOver { get; private set; }

        int scriptIndex;
        int bossRushIndex;
        int totalRunKills;
        WaveModifier activeModifier;
        Coroutine runRoutine;
        bool lowAmmoAppliedThisRun;

        public void Initialize(GameModeDefinition modeDefinition, AgentSpawner agentSpawner,
            IReadOnlyList<Transform> points, Transform playerTransform, RngService random)
        {
            mode = modeDefinition;
            spawner = agentSpawner;
            player = playerTransform;
            rng = random;
            spawnPoints.AddRange(points);

            playerLocomotion = playerTransform.GetComponent<FirstPersonController>();
            playerWeapons = playerTransform.GetComponent<WeaponController>();
            pickups = Object.FindAnyObjectByType<Pickups.PickupManager>();

            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDestroy()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            activeModifier?.Remove();
        }

        public void BeginRun()
        {
            if (runRoutine != null) StopCoroutine(runRoutine);
            runRoutine = StartCoroutine(RunLoop());
        }

        public void StopRun()
        {
            RunOver = true;
            if (runRoutine != null) StopCoroutine(runRoutine);
            runRoutine = null;
            activeModifier?.Remove();
            activeModifier = null;
            StopAllCoroutines();
        }

        IEnumerator RunLoop()
        {
            // Small breathing room before the first wave.
            yield return new WaitForSeconds(1.5f);

            while (!RunOver)
            {
                WaveDefinition wave = GetNextWave();
                if (wave == null) { FinishVictory(); yield break; }

                yield return StartCoroutine(PlayWave(wave));

                if (RunOver) yield break;

                // Mode-specific completion.
                if (mode.kind == GameModeKind.TimeAttack && totalRunKills >= mode.killQuota)
                {
                    FinishVictory();
                    yield break;
                }
            }
        }

        IEnumerator PlayWave(WaveDefinition wave)
        {
            WaveNumber = wave.waveNumber;

            // --- preparation beat ---
            Services.Flow?.State.Set(GameState.WaveStart, "wave prep");
            GameEvents.WavePrepared?.Invoke(WaveNumber, wave.prepTime);
            Services.Audio?.Play("wave_start", .7f);
            yield return new WaitForSeconds(wave.prepTime);
            if (RunOver) yield break;

            // --- wave modifier ---
            activeModifier?.Remove();
            activeModifier = MaybeRollModifier(wave);
            if (activeModifier != null)
            {
                activeModifier.Apply(playerLocomotion, playerWeapons, pickups);
                GameEvents.WaveModifierApplied?.Invoke(activeModifier.Title, activeModifier.Description);
            }

            // --- boss waves ---
            if (wave.isBossWave)
            {
                yield return StartCoroutine(PlayBossWave(wave));
                activeModifier?.Remove();
                activeModifier = null;
                yield break;
            }

            // --- spawn queue ---
            var queue = new List<EnemyKind>();
            foreach (WaveEntry entry in wave.entries)
                for (int i = 0; i < entry.count; i++)
                    queue.Add(entry.kind);

            rng.Shuffle(queue);
            EnemiesRemaining = queue.Count;
            totalWaveEnemies = queue.Count;
            GameEvents.WaveStarted?.Invoke(WaveNumber, queue.Count);
            GameEvents.WaveChanged?.Invoke(WaveNumber, EnemiesRemaining);
            Services.Flow?.State.Set(GameState.Combat, "wave start");

            float difficulty = 1f + (WaveNumber - 1) * mode.difficultyScalePerWave;
            float speedMult = activeModifier?.Kind == WaveModifierKind.DoubleSpeed ? 1.75f : 1f;
            float healthMult = activeModifier?.Kind == WaveModifierKind.Hardened ? 1.5f : 1f;

            int spawned = 0;
            foreach (EnemyKind kind in queue)
            {
                if (RunOver) yield break;

                // Respect alive caps: hold the queue when the field is crowded.
                float wait = 0f;
                while (spawner.AliveCount >= wave.maxAlive && wait < 8f)
                {
                    wait += .25f;
                    yield return new WaitForSeconds(.25f);
                    if (RunOver) yield break;
                }

                var definition = Content.GameContent.EnemyOf(kind);
                if (definition != null && spawner.CanSpawn(definition))
                {
                    int eliteCount = RollEliteCount(wave);
                    if (activeModifier?.Kind == WaveModifierKind.EliteInvasion) eliteCount = Mathf.Max(1, eliteCount + 1);
                    spawner.SpawnScaled(definition, PickSpawnPoint().position, difficulty * healthMult, eliteCount, speedMult);
                }
                else
                {
                    // Capped type: still count it so the wave can end.
                    EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
                    GameEvents.WaveChanged?.Invoke(WaveNumber, EnemiesRemaining);
                }

                spawned++;
                yield return new WaitForSeconds(wave.spawnInterval);
            }

            // --- wait for the wave to clear ---
            // Watchdog: if nothing is alive but the remaining count disagrees
            // (lost kills, capped spawns, edge-of-arena deaths), self-heal so a
            // wave can never soft-lock the run.
            float clearWatchdog = 60f;
            while (EnemiesRemaining > 0 && !RunOver)
            {
                yield return new WaitForSeconds(.25f);
                clearWatchdog -= .25f;
                if (clearWatchdog <= 0f || (spawner != null && spawner.AliveCount == 0))
                    EnemiesRemaining = 0;
            }

            if (RunOver) yield break;

            GameEvents.WaveCleared?.Invoke(WaveNumber);
            Services.Audio?.Play("wave_clear", .8f);
            Services.Flow?.State.Set(GameState.Playing, "wave cleared");
            if (pickups != null) pickups.NotifyWaveCleared();
        }

        int totalWaveEnemies;

        IEnumerator PlayBossWave(WaveDefinition wave)
        {
            Services.Flow?.State.Set(GameState.Boss, "boss wave");
            var bossDefinition = Content.GameContent.Boss(wave.bossId);
            if (bossDefinition == null)
            {
                Debug.LogWarning($"[Waves] Boss '{wave.bossId}' not found, skipping wave.");
                yield break;
            }

            // Spawn escort first so they pathfind while the intro plays.
            var difficulty = 1f + (WaveNumber - 1) * mode.difficultyScalePerWave;
            foreach (WaveEntry entry in wave.entries)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    var definition = Content.GameContent.EnemyOf(entry.kind);
                    if (definition != null && spawner.CanSpawn(definition))
                        spawner.SpawnScaled(definition, PickSpawnPoint().position, difficulty, RollEliteCount(wave));
                    yield return new WaitForSeconds(.3f);
                }
            }

            EnemiesRemaining = wave.TotalEnemies + 1; // + boss
            GameEvents.WaveStarted?.Invoke(WaveNumber, EnemiesRemaining);

            BossBrain boss = BossFactory.Spawn(bossDefinition,
                PickSpawnPoint(farthest: true).position + Vector3.back * 2f, player, wave.bossStatScale);
            if (boss == null)
            {
                // Spawn failed (no navmesh?): drop the phantom boss from the count
                // so the wave can still complete once escorts are dealt with.
                EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
            }

            while (boss != null && !RunOver)
            {
                yield return new WaitForSeconds(.3f);
            }

            if (RunOver) yield break;

            // Boss defeated - clear escorts gracefully (they surrender/explode).
            float escortWatchdog = 45f;
            while (EnemiesRemaining > 0 && !RunOver)
            {
                yield return new WaitForSeconds(.3f);
                escortWatchdog -= .3f;
                if (escortWatchdog <= 0f || spawner == null || spawner.AliveCount == 0)
                {
                    // Nothing left alive but the count disagrees - self-heal.
                    EnemiesRemaining = 0;
                }
            }

            GameEvents.WaveCleared?.Invoke(WaveNumber);
            Services.Flow?.State.Set(GameState.Playing, "boss cleared");
        }

        // ---------------------------------------------------------------- wave sourcing

        WaveDefinition GetNextWave()
        {
            switch (mode.kind)
            {
                case GameModeKind.Survival:
                {
                    if (scriptIndex < mode.scriptedWaves.Count)
                        return mode.scriptedWaves[scriptIndex++];
                    return null; // scripted campaign complete
                }

                case GameModeKind.TimeAttack:
                    return GenerateWave();

                case GameModeKind.BossRush:
                {
                    if (bossRushIndex >= mode.bossOrder.Count) return null; // rush complete

                    var wave = ScriptableObject.CreateInstance<WaveDefinition>();
                    wave.waveNumber = bossRushIndex + 1;
                    wave.prepTime = bossRushIndex == 0 ? 5f : 7f;
                    wave.isBossWave = true;
                    wave.bossId = mode.bossOrder[bossRushIndex];
                    wave.bossStatScale = 1f + bossRushIndex * .35f;
                    if (bossRushIndex > 0)
                        wave.entries.Add(new WaveEntry { kind = EnemyKind.Grunt, count = 2 + bossRushIndex });
                    bossRushIndex++;
                    return wave;
                }

                case GameModeKind.Endless:
                    return GenerateWave();

                default:
                    return null;
            }
        }

        /// <summary>
        /// Procedural, budget-based wave generation with strict balance rules:
        /// enemy variety unlocks by wave, expensive types are capped, medics are
        /// rare and the total stays proportional to the wave number.
        /// </summary>
        WaveDefinition GenerateWave()
        {
            int waveNumber = (mode.kind == GameModeKind.Endless ? scriptIndex : WaveNumber) + 1;
            scriptIndex++;

            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            wave.waveNumber = waveNumber;
            wave.prepTime = Mathf.Lerp(5f, 3f, Mathf.Clamp01(waveNumber / 20f));
            wave.spawnInterval = Mathf.Lerp(.45f, .25f, Mathf.Clamp01(waveNumber / 20f));
            wave.maxAlive = Mathf.Clamp(10 + waveNumber, 10, 24);
            wave.eliteChance = Mathf.Clamp(.05f + waveNumber * .02f, 0f, .45f);
            wave.eliteMaxModifiers = waveNumber >= 10 ? 2 : 1;

            float budget = 4f + waveNumber * 1.6f * Mathf.Max(.6f, mode.varietyRamp * .6f);
            if (mode.kind == GameModeKind.Endless) budget *= 1.1f;

            var pool = new List<EnemyDefinition>();
            foreach (EnemyDefinition candidate in Content.GameContent.Enemies)
            {
                if (candidate.minWave <= waveNumber && candidate.budgetCost <= budget)
                    pool.Add(candidate);
            }
            if (pool.Count == 0) pool.Add(Content.GameContent.Grunt);

            float spent = 0f;
            int safety = 200;
            while (spent < budget && safety-- > 0)
            {
                EnemyDefinition pick = rng.Pick(pool, e => e.budgetCost <= budget - spent + .5f);
                if (pick == null) break;

                // Respect per-type concurrency caps when composing.
                if (pick.maxConcurrent > 0 && spawner != null && spawner.CountOfKind(pick.kind) + CountInWave(wave, pick.kind) >= pick.maxConcurrent)
                {
                    pool.Remove(pick);
                    if (pool.Count == 0) break;
                    continue;
                }

                AddToWave(wave, pick.kind);
                spent += pick.budgetCost;
            }

            // Guarantee at least something.
            if (wave.TotalEnemies == 0)
                AddToWave(wave, EnemyKind.Grunt);

            return wave;
        }

        static void AddToWave(WaveDefinition wave, EnemyKind kind)
        {
            foreach (WaveEntry entry in wave.entries)
            {
                if (entry.kind == kind) { entry.count++; return; }
            }
            wave.entries.Add(new WaveEntry { kind = kind, count = 1 });
        }

        static int CountInWave(WaveDefinition wave, EnemyKind kind)
        {
            foreach (WaveEntry entry in wave.entries)
                if (entry.kind == kind) return entry.count;
            return 0;
        }

        // ---------------------------------------------------------------- modifiers

        WaveModifier MaybeRollModifier(WaveDefinition wave)
        {
            if (!wave.allowWaveModifier || WaveNumber < 4) return null;
            if (mode.kind == GameModeKind.BossRush) return null;
            if (rng.Chance(.3f) == false) return null;

            var candidates = new List<WaveModifierKind>
            {
                WaveModifierKind.DoubleSpeed,
                WaveModifierKind.Hardened,
                WaveModifierKind.LowGravity,
                WaveModifierKind.RapidFire,
            };
            if (WaveNumber >= 6) candidates.Add(WaveModifierKind.LimitedAmmo);
            if (WaveNumber >= 6 && mode.kind == GameModeKind.Endless) candidates.Add(WaveModifierKind.EliteInvasion);

            WaveModifierKind kind = rng.Pick(candidates);

            // Balance rules: no hardening on boss waves, no repeat ammo drought.
            if (wave.isBossWave && (kind == WaveModifierKind.Hardened || kind == WaveModifierKind.DoubleSpeed))
                kind = WaveModifierKind.RapidFire;
            if (kind == WaveModifierKind.LimitedAmmo && lowAmmoAppliedThisRun)
                kind = WaveModifierKind.RapidFire;
            if (kind == WaveModifierKind.LimitedAmmo) lowAmmoAppliedThisRun = true;
            if (kind == WaveModifierKind.EliteInvasion && wave.isBossWave)
                kind = WaveModifierKind.DoubleSpeed;

            return WaveModifier.Create(kind);
        }

        int RollEliteCount(WaveDefinition wave)
        {
            if (wave.eliteChance <= 0f) return 0;
            if (!rng.Chance(wave.eliteChance)) return 0;
            return rng.Range(wave.eliteMinModifiers, wave.eliteMaxModifiers + 1);
        }

        // ---------------------------------------------------------------- events

        void OnEnemyKilled(Combat.KillInfo kill)
        {
            if (RunOver) return;
            totalRunKills++;
            KillsThisRun = totalRunKills;

            if (EnemiesRemaining > 0)
            {
                EnemiesRemaining--;
                GameEvents.WaveChanged?.Invoke(WaveNumber, EnemiesRemaining);
            }
        }

        Transform PickSpawnPoint(bool farthest = false)
        {
            if (spawnPoints.Count == 0) return player;

            Transform best = spawnPoints[0];
            float bestScore = float.MinValue;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null) continue;
                float distance = Vector3.Distance(point.position, player.position);
                // Score prefers points 12-25m from the player: not on top of them,
                // not across the map either.
                float score = -Mathf.Abs(distance - 18f);
                if (distance < 8f) score -= 50f;
                if (farthest) score = distance;
                if (score > bestScore) { bestScore = score; best = point; }
            }
            return best;
        }

        void FinishVictory()
        {
            if (RunOver) return;
            RunOver = true;
            GameEvents.Victory?.Invoke();
        }
    }
}
