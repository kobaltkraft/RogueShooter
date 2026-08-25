using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Persistence;

namespace RogueArena.Progression
{
    /// <summary>
    /// Tracks challenge progress from gameplay events and stores it in the save
    /// file. Run-scoped context (mode, time, heals) is pushed in by GameFlow.
    /// </summary>
    public sealed class ChallengeService : IDisposable
    {
        readonly SaveService save;
        readonly List<ChallengeDefinition> catalog;
        ChallengeData Data => save?.Data?.challenges;

        // Run context.
        public string RunModeId;
        public float RunSeconds;
        bool damagedThisWave;
        bool healedDuringBoss;
        bool bossActive;
        int wavesClearedNoDamage;

        public ChallengeService(SaveService saveService, List<ChallengeDefinition> challengeCatalog)
        {
            save = saveService;
            catalog = challengeCatalog ?? new List<ChallengeDefinition>();

            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.PlayerHealed += OnPlayerHealed;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.BossSpawned += OnBossSpawned;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.Victory += OnVictory;
        }

        public void BeginRun(string modeId)
        {
            RunModeId = modeId;
            RunSeconds = 0f;
            damagedThisWave = false;
            healedDuringBoss = false;
            bossActive = false;
            wavesClearedNoDamage = 0;
        }

        public bool IsCompleted(ChallengeDefinition challenge)
        {
            return Data != null && Data.completed.Contains(challenge.id);
        }

        public float Progress(ChallengeDefinition challenge)
        {
            if (challenge == null) return 0f;
            if (IsCompleted(challenge)) return 1f;
            return Mathf.Clamp01(ReadProgress(challenge.id) / Mathf.Max(1f, challenge.goal));
        }

        long ReadProgress(string id)
        {
            if (Data == null) return 0;
            var entry = Data.progress.Find(p => p.key == id);
            return entry?.value ?? 0;
        }

        void WriteProgress(string id, long value)
        {
            if (Data == null) return;
            var entry = Data.progress.Find(p => p.key == id);
            if (entry == null)
            {
                entry = new StringLongEntry { key = id, value = value };
                Data.progress.Add(entry);
            }
            else entry.value = value;
            save?.MarkDirty();
        }

        // ---------------------------------------------------------------- events

        void OnEnemyKilled(KillInfo kill)
        {
            if (!kill.KilledByPlayer) return;

            Bump("first_blood");
            if (kill.Headshot) Bump("headhunter");
            if (kill.KilledByBarrel) Bump("demolitionist");
            if (kill.WasElite) Bump("elite_hunter");
            if (kill.KilledByExplosion) Bump("boom_operator");

            // Weapon-specific: attribute to the player's current weapon.
            if (Services.Session?.Weapons != null)
            {
                string weaponId = Services.Session.Weapons.CurrentWeaponId;
                if (!string.IsNullOrEmpty(weaponId) && weaponId == "marksman")
                    Bump("longshot_hero");
            }
        }

        void Bump(string challengeId)
        {
            ChallengeDefinition challenge = catalog.Find(c => c.id == challengeId);
            if (challenge == null || IsCompleted(challenge)) return;

            long value = ReadProgress(challengeId) + 1;
            WriteProgress(challengeId, value);
            if (value >= challenge.goal) Complete(challenge);
        }

        void OnPlayerDamaged(DamageInfo info, float remaining)
        {
            damagedThisWave = true;
        }

        void OnPlayerHealed(float amount, float after)
        {
            if (bossActive) healedDuringBoss = true;
        }

        void OnComboChanged(float multiplier, float seconds)
        {
            ChallengeDefinition challenge = catalog.Find(c => c.id == "combo_artist");
            if (challenge == null || IsCompleted(challenge)) return;

            long value = (long)(multiplier * 10);
            if (value > ReadProgress("combo_artist"))
            {
                WriteProgress("combo_artist", value);
                if (value >= challenge.goal * 10) Complete(challenge);
            }
        }

        void OnWaveStarted(int wave, int totalEnemies) => damagedThisWave = false;

        void OnWaveCleared(int wave)
        {
            if (!damagedThisWave)
            {
                wavesClearedNoDamage++;
                Bump("untouchable", _ => 1);
            }
        }

        void OnBossSpawned(string bossName, float maxHealth)
        {
            bossActive = true;
            healedDuringBoss = false;
        }

        void OnBossDefeated(string bossName, bool wasFinal)
        {
            bossActive = false;
            Bump("boss_slayer", _ => 1);

            if (!healedDuringBoss)
            {
                ChallengeDefinition challenge = catalog.Find(c => c.id == "cold_hands");
                if (challenge != null && !IsCompleted(challenge))
                {
                    WriteProgress("cold_hands", 1);
                    Complete(challenge);
                }
            }
        }

        void OnVictory()
        {
            // Time Attack under N seconds.
            ChallengeDefinition speed = catalog.Find(c => c.id == "speed_demon");
            if (speed != null && !IsCompleted(speed) && RunModeId == "timeattack" && RunSeconds > 0f
                && RunSeconds <= speed.goal)
            {
                WriteProgress("speed_demon", (long)RunSeconds);
                Complete(speed);
            }
        }

        /// <summary>Endless depth tracking; called by the flow on run end.</summary>
        public void NotifyBestWave(string modeId, int wave)
        {
            if (modeId != "endless") return;
            ChallengeDefinition challenge = catalog.Find(c => c.id == "deep_run");
            if (challenge == null || IsCompleted(challenge)) return;
            if (wave >= challenge.goal)
            {
                WriteProgress("deep_run", wave);
                Complete(challenge);
            }
        }

        public void NotifyRunTime(float seconds) => RunSeconds = seconds;

        void Complete(ChallengeDefinition challenge)
        {
            if (Data == null || Data.completed.Contains(challenge.id)) return;

            Data.completed.Add(challenge.id);
            if (!string.IsNullOrEmpty(challenge.rewardCosmetic) && save?.Data?.progress != null
                && !save.Data.progress.cosmetics.Contains(challenge.rewardCosmetic))
            {
                save.Data.progress.cosmetics.Add(challenge.rewardCosmetic);
            }
            save?.MarkDirty();

            Services.Progression?.AddXp(challenge.rewardXp);
            Services.Progression?.AddCurrency(challenge.rewardCurrency);

            GameEvents.ChallengeCompleted?.Invoke(challenge.id, challenge.title);
            Services.Audio?.Play("challenge", .9f);
            GameEvents.Notification?.Invoke($"CHALLENGE: {challenge.title}",
                new Color(1f, .85f, .3f), 3f);
        }

        public void Dispose()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.PlayerHealed -= OnPlayerHealed;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.BossSpawned -= OnBossSpawned;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.Victory -= OnVictory;
        }
    }
}
