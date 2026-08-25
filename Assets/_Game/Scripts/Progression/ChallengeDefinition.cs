using UnityEngine;

namespace RogueArena.Progression
{
    public enum ChallengeMetric
    {
        TotalKills,          // kill N enemies (cumulative)
        HeadshotKills,       // kill N enemies with headshots (cumulative)
        ReachCombo,          // reach combo multiplier N in one run
        WaveNoDamage,        // clear N waves without taking damage (cumulative)
        BarrelKills,         // kill N enemies with explosive barrels (cumulative)
        TimeAttackUnder,     // finish a Time Attack run under N seconds (best time)
        BossNoHeal,          // defeat a boss without healing (count)
        ReachWave,           // reach wave N in any endless mode (best wave)
        WeaponKills,         // kill N enemies with a specific weapon (cumulative, weaponId in payload)
        ExplosiveKills,      // kill N enemies with explosives (cumulative)
        EliteKills,          // kill N elite enemies (cumulative)
        BossKills,           // defeat N bosses (cumulative)
    }

    /// <summary>
    /// One achievement-style challenge. Progress is tracked by
    /// <see cref="ChallengeService"/> from gameplay events and stored in the save.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Challenge", fileName = "Challenge_")]
    public class ChallengeDefinition : ScriptableObject
    {
        public string id;
        public string title = "FIRST BLOOD";
        [TextArea] public string description = "Defeat 10 enemies.";
        public ChallengeMetric metric = ChallengeMetric.TotalKills;
        public float goal = 10f;
        [Tooltip("Cumulative challenges persist progress between runs.")]
        public bool cumulative = true;
        [Tooltip("Optional payload (e.g. weapon id for WeaponKills).")]
        public string payload;

        [Header("Rewards")]
        public int rewardXp = 100;
        public long rewardCurrency = 100;
        [Tooltip("Cosmetic crosshair id granted on completion (optional).")]
        public string rewardCosmetic;
    }
}
