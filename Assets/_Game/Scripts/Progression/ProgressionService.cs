using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;
using RogueArena.Combat;
using RogueArena.Persistence;

namespace RogueArena.Progression
{
    /// <summary>
    /// Persistent meta-progression: XP, levels, currency, weapon unlocks and
    /// upgrade tiers. All state lives in the save file; XP flows in from kill
    /// events during runs.
    /// </summary>
    public sealed class ProgressionService : IDisposable
    {
        public int Level => Data.level;
        public long TotalXp => Data.xp;
        public long Currency => Data.currency;
        public long XpIntoLevel { get; private set; }
        public long XpForNextLevel { get; private set; }

        readonly SaveService save;
        ProgressData Data => save?.Data?.progress;

        public ProgressionService(SaveService saveService)
        {
            save = saveService;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.Victory += OnVictory;
            RecalculateLevelProgress();
        }

        // ------------------------------------------------------------------ xp

        public static long XpRequiredForLevel(int level) => (long)(80f * Mathf.Pow(level, 1.35f));

        void RecalculateLevelProgress()
        {
            if (Data == null) { XpIntoLevel = 0; XpForNextLevel = XpRequiredForLevel(1); return; }

            long consumed = 0;
            int level = 1;
            while (level < 999 && Data.xp >= consumed + XpRequiredForLevel(level))
            {
                consumed += XpRequiredForLevel(level);
                level++;
            }
            XpIntoLevel = Data.xp - consumed;
            XpForNextLevel = XpRequiredForLevel(level);

            if (Data.level != level)
            {
                Data.level = level;
                save?.MarkDirty();
            }
        }

        public void AddXp(long amount)
        {
            if (amount <= 0 || Data == null) return;

            int before = Data.level;
            Data.xp += amount;
            RecalculateLevelProgress();

            GameEvents.XpGained?.Invoke((int)Math.Min(int.MaxValue, amount), Data.xp);
            if (Data.level > before)
            {
                GameEvents.LevelUp?.Invoke(Data.level);
                Services.Audio?.Play("level_up", .9f);
                GameEvents.Notification?.Invoke($"LEVEL {Data.level}", new Color(.35f, .9f, 1f), 2.5f);
                Data.currency += Data.level * 25; // level-up grant
            }
            save?.MarkDirty();
        }

        public void AddCurrency(long amount)
        {
            if (amount <= 0 || Data == null) return;
            Data.currency += amount;
            GameEvents.CurrencyChanged?.Invoke(Data.currency);
            save?.MarkDirty();
        }

        // ------------------------------------------------------------------ kills -> xp

        void OnEnemyKilled(KillInfo kill)
        {
            if (!kill.KilledByPlayer || Data == null) return;

            int baseXp = 8 + kill.EliteModifierCount * 10;
            if (kill.Headshot) baseXp = (int)(baseXp * 1.3f);
            AddXp(baseXp);

            var records = save?.Data?.records;
            if (records != null)
            {
                records.totalKills++;
                if (kill.Headshot) records.headshotKills++;
                if (kill.WasElite) records.eliteKills++;
                if (kill.KilledByBarrel) records.barrelKills++;
                if (kill.KilledByExplosion) records.explosiveKills++;
            }

            float currencyRoll = .06f + kill.EliteModifierCount * .1f;
            if (Services.Rng != null && Services.Rng.Chance(currencyRoll))
                AddCurrency(10 + kill.EliteModifierCount * 15);
        }

        void OnBossDefeated(string bossName, bool wasFinal)
        {
            if (Data == null) return;
            var records = save?.Data?.records;
            if (records != null) records.bossKills++;
            AddXp(wasFinal ? 400 : 150);
        }

        void OnVictory()
        {
            if (Data == null) return;
            var records = save?.Data?.records;
            if (records != null) records.runsWon++;
            AddXp(300);
        }

        public void NotifyRunStarted()
        {
            if (Data == null) return;
            var records = save?.Data?.records;
            if (records != null) records.runsPlayed++;
            save?.MarkDirty();
        }

        public void NotifyRunSeconds(double seconds)
        {
            if (Data == null) return;
            var records = save?.Data?.records;
            if (records == null) return;
            records.totalPlaySeconds += seconds;
            save?.MarkDirty();
        }

        // ------------------------------------------------------------------ unlocks

        public bool IsWeaponUnlocked(string weaponId)
        {
            if (Data == null) return false;
            return Data.unlockedWeapons.Contains(weaponId);
        }

        /// <summary>Unlocks a weapon if not owned and affordable. Returns success.</summary>
        public bool TryUnlockWeapon(string weaponId, long cost)
        {
            if (Data == null || IsWeaponUnlocked(weaponId) || Data.currency < cost) return false;
            Data.currency -= cost;
            Data.unlockedWeapons.Add(weaponId);
            GameEvents.CurrencyChanged?.Invoke(Data.currency);
            Services.Audio?.Play("unlock", .9f);
            save?.MarkDirty();
            return true;
        }

        public IReadOnlyList<string> UnlockedWeapons => Data?.unlockedWeapons;

        // ------------------------------------------------------------------ weapon upgrades

        /// <summary>Stored as "weaponId|track". Tier count per weapon+track (clamped to the track max).</summary>
        public int WeaponUpgradeTier(string weaponId, string track)
        {
            if (Data == null) return 0;
            string key = weaponId + "|" + track;
            int count = 0;
            foreach (string entry in Data.weaponUpgrades)
                if (entry == key || entry.StartsWith(key + "#", StringComparison.Ordinal)) count++;
            return Mathf.Clamp(count, 0, WeaponUpgradeTracks.MaxTier(track));
        }

        public bool TryPurchaseWeaponUpgrade(string weaponId, string track, long cost, int maxTier)
        {
            if (Data == null) return false;
            int tier = WeaponUpgradeTier(weaponId, track);
            if (tier >= maxTier || Data.currency < cost) return false;

            Data.currency -= cost;
            Data.weaponUpgrades.Add(weaponId + "|" + track + "#" + tier);
            GameEvents.CurrencyChanged?.Invoke(Data.currency);
            Services.Audio?.Play("unlock", .85f);
            save?.MarkDirty();
            return true;
        }

        // ------------------------------------------------------------------ player upgrades

        public int PlayerUpgradeTier(PlayerUpgradeDefinition upgrade)
        {
            if (upgrade == null || Data == null) return 0;
            int count = 0;
            foreach (string entry in Data.playerUpgrades)
                if (entry == upgrade.id || entry.StartsWith(upgrade.id + "#", StringComparison.Ordinal)) count++;
            return Mathf.Clamp(count, 0, upgrade.maxTier);
        }

        public bool TryPurchasePlayerUpgrade(PlayerUpgradeDefinition upgrade)
        {
            if (upgrade == null || Data == null) return false;
            int tier = PlayerUpgradeTier(upgrade);
            if (tier >= upgrade.maxTier || Data.currency < upgrade.CostAtTier(tier)) return false;

            Data.currency -= upgrade.CostAtTier(tier);
            Data.playerUpgrades.Add(upgrade.id + "#" + tier);
            GameEvents.CurrencyChanged?.Invoke(Data.currency);
            Services.Audio?.Play("unlock", .85f);
            save?.MarkDirty();
            return true;
        }

        /// <summary>Total value of a player upgrade kind across all purchased tiers.</summary>
        public float PlayerUpgradeValue(PlayerUpgradeKind kind, List<PlayerUpgradeDefinition> catalog)
        {
            if (catalog == null) return 0f;
            float total = 0f;
            foreach (PlayerUpgradeDefinition upgrade in catalog)
            {
                if (upgrade == null || upgrade.kind != kind) continue;
                total += upgrade.valuePerTier * PlayerUpgradeTier(upgrade);
            }
            return total;
        }

        public void Dispose()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.Victory -= OnVictory;
        }
    }
}
