using System;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.AI
{
    /// <summary>How an enemy tries to hurt the player.</summary>
    public enum AttackStyle { None, Melee, Hitscan, Projectile, Healer }

    /// <summary>
    /// Complete, designer-tunable configuration for one enemy archetype.
    /// Every enemy type shares <see cref="EnemyBrain"/> and only differs by data,
    /// which keeps the AI architecture uniform and debuggable.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "grunt";
        public string displayName = "Sentry Grunt";
        public EnemyKind kind = EnemyKind.Grunt;

        [Header("Vitals")]
        public float maxHealth = 70f;
        [Tooltip("0-1: fraction of incoming knockback ignored.")]
        [Range(0f, 1f)] public float knockbackResistance;

        [Header("Movement")]
        public float moveSpeed = 3.6f;
        public float acceleration = 16f;
        public float agentRadius = .45f;
        public float agentHeight = 2f;
        public bool flying;
        [Tooltip("Hover height for flying units.")]
        public float flyHeight = 3f;
        [Tooltip("Scale applied to the visual body.")]
        public float bodyScale = 1f;

        [Header("Perception")]
        public float sightRange = 26f;
        public float fieldOfView = 120f;
        public float hearingRange = 26f;
        [Tooltip("Seconds between perception updates (staggered across enemies).")]
        public float perceptionInterval = .2f;

        [Header("Engagement")]
        [Tooltip("Distance the enemy wants to fight from.")]
        public float preferredRange = 2.2f;
        [Tooltip("Minimum distance before it backs off.")]
        public float minimumRange = 0f;
        public float strafeSpeed = 2f;
        public float strafeInterval = 1.4f;
        [Tooltip("0-1 chance of attempting a flank approach instead of a direct one.")]
        [Range(0f, 1f)] public float flankChance;
        [Tooltip("0-1 chance of using cover points when available.")]
        [Range(0f, 1f)] public float coverPreference;
        [Tooltip("Health fraction below which the enemy retreats. 0 = never.")]
        public float retreatHealthFraction;
        [Tooltip("Seconds spent searching the last known player position.")]
        public float searchDuration = 5f;
        [Tooltip("Seconds of repositioning after each attack (snipers).")]
        public float repositionAfterAttack;
        [Tooltip("Sniper/height seeking units pick elevated spots.")]
        public bool usesElevatedPositions;

        [Header("Attack")]
        public AttackStyle attackStyle = AttackStyle.Melee;
        public float attackRange = 2.2f;
        public float attackDamage = 10f;
        public float attackInterval = 1.1f;
        [Tooltip("Telegraph time before the damage lands.")]
        public float attackWindup = .35f;
        [Tooltip("Projectiles/pellets per attack.")]
        public int projectileCount = 1;
        public float projectileSpeed = 24f;
        [Tooltip("Downward acceleration for lobbed projectiles.")]
        public float projectileArc;
        [Tooltip("Cone half-angle in degrees for multi-projectile attacks.")]
        public float attackSpread = 4f;
        [Tooltip("Hitscan attacks show a tracer in this colour.")]
        public Color tracerColor = new Color(1f, .35f, .2f);
        [Tooltip("Healer: health restored per second to allies.")]
        public float healPerSecond = 14f;
        [Tooltip("Healer: range of the healing aura.")]
        public float healRange = 8f;

        [Header("Rewards")]
        public int xpValue = 20;
        public long scoreValue = 100;
        [Tooltip("Chance to drop a small currency bonus pickup.")]
        [Range(0f, 1f)] public float currencyDropChance = .08f;

        [Header("Wave generation")]
        public int minWave = 1;
        [Tooltip("Composition budget cost in generated waves.")]
        public float budgetCost = 1f;
        [Tooltip("Maximum concurrently alive of this type (0 = unlimited).")]
        public int maxConcurrent = 0;

        [Header("Audio")]
        public string shotSfx = "enemy_shot";
        public string alertSfx = "enemy_alert";
        public string deathSfx = "enemy_die";
        public string meleeSfx = "enemy_melee";
    }
}
