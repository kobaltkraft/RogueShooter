using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.AI
{
    /// <summary>One phase of a boss fight. Phases activate at health thresholds.</summary>
    [Serializable]
    public class BossPhase
    {
        public string name = "PHASE I";
        [Tooltip("Phase begins when health falls to this fraction.")]
        [Range(.05f, 1f)] public float healthFraction = 1f;
        public float moveSpeed = 3.2f;
        public float attackInterval = 2.4f;
        public float projectileSpeed = 17f;

        [Header("Attacks available in this phase")]
        public int barrageCount = 5;
        public float barrageSpread = 28f;
        public bool groundSlam;
        public float slamTelegraph = 1.1f;
        public float slamRadius = 5f;
        public float slamDamage = 30f;
        public bool charge;
        public float chargeTelegraph = .9f;
        public float chargeSpeed = 13f;
        public float chargeDamage = 24f;
        public bool summon;
        public int summonCount = 3;
        public EnemyKind summonKind = EnemyKind.Grunt;
    }

    /// <summary>
    /// Configuration for a boss. The <see cref="BossBrain"/> drives any boss built
    /// from this definition, so additional bosses only need data.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Boss Definition", fileName = "Boss_")]
    public class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "vulcan";
        public string displayName = "VULCAN-9 OVERLORD";
        [TextArea] public string description = "Decommissioned foundry warden.";

        [Header("Vitals")]
        public float maxHealth = 2800f;
        public float moveSpeed = 3.4f;
        public float agentRadius = 1.35f;
        public float agentHeight = 4.6f;
        public float attackRange = 32f;
        public float preferredRange = 14f;

        [Header("Combat")]
        [Tooltip("Damage per shoulder-cannon projectile hit (splash deals a fraction of this).")]
        public float attackDamage = 25f;

        [Header("Phases (ordered by healthFraction, descending)")]
        public List<BossPhase> phases = new List<BossPhase>();

        [Header("Rewards")]
        public long scoreReward = 5000;
        public int xpReward = 500;
        public long currencyReward = 300;

        [Header("Presentation")]
        public float deathSequenceSeconds = 3.5f;
        public float introRoarSeconds = 2.2f;
    }
}
