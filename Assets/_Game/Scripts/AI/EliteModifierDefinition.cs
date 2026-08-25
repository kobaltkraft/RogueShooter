using System;
using UnityEngine;

namespace RogueArena.AI
{
    public enum EliteModifierKind
    {
        Fast,           // moves quicker
        Armored,        // more health
        Regenerating,   // heals over time
        Explosive,      // detonates on death
        Vampiric,       // heals when it damages the player
        Shielded,       // absorbs a burst of damage first
        Teleporting,    // blinks toward the player
        Frenzied,       // faster attacks
        LongRange,      // extended attack range
        Summoner,       // spawns minions
        Berserker,      // much faster at low health
    }

    /// <summary>
    /// One reusable elite modifier. Modifiers stack in any combination - no
    /// per-combination classes exist. The brain queries behaviour flags, the
    /// visuals are tinted by the strongest modifier.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Elite Modifier", fileName = "Elite_")]
    public class EliteModifierDefinition : ScriptableObject
    {
        [Header("Identity")]
        public EliteModifierKind kind;
        public string title = "FAST";
        [TextArea] public string description = "Moves faster.";

        [Header("Stat overrides")]
        public float healthMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float attackSpeedMultiplier = 1f;
        public float attackRangeMultiplier = 1f;
        public float scoreMultiplier = 1.5f;
        public float xpMultiplier = 1.5f;

        [Header("Special behaviour")]
        public float regenPerSecond;
        public float healOnDamageDealt;
        [Tooltip("Extra shield hit points as a fraction of max health.")]
        public float shieldFraction;
        public float explodeOnDeathRadius;
        public float explodeOnDeathDamage;
        [Tooltip("Speed bonus multiplier when under lowHealthThreshold (Berserker/Frenzied).")]
        public float lowHealthSpeedBonus = 1f;
        public float lowHealthThreshold = .35f;
        public bool teleports;
        public float teleportInterval = 4.5f;
        public float teleportRange = 14f;
        public bool summons;
        public float summonInterval = 9f;
        public Core.EnemyKind summonKind = Core.EnemyKind.Grunt;
        public int summonCount = 2;

        [Header("Visuals")]
        public Color tint = new Color(.75f, .25f, 1f);
        [Range(0f, 1f)] public float tintAmount = .5f;
        public bool crown = true;
    }
}
