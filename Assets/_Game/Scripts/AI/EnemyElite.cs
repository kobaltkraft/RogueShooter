using System.Collections.Generic;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using UnityEngine.AI;

namespace RogueArena.AI
{
    /// <summary>
    /// Applies a stack of elite modifiers to an enemy and runs their ongoing
    /// behaviours (regeneration, teleporting, summoning, berserk speed). One
    /// reusable component - no per-combination classes.
    /// </summary>
    public class EnemyElite : MonoBehaviour
    {
        readonly List<EliteModifierDefinition> modifiers = new List<EliteModifierDefinition>(3);

        EnemyBrain brain;
        Health health;
        NavMeshAgent agent;
        EnemyVisual visual;
        EnemyCombat combat;

        float regenPerSecond;
        float healOnDamage;
        float lowHealthSpeedBonus = 1f;
        float lowHealthThreshold = .35f;
        bool teleports;
        float teleportTimer;
        float teleportInterval = 5f;
        float teleportRange = 14f;
        bool summons;
        float summonTimer;
        float summonInterval = 9f;
        EnemyKind summonKind;
        int summonCount;

        public int ModifierCount => modifiers.Count;
        public IReadOnlyList<EliteModifierDefinition> Modifiers => modifiers;
        public float RangeMultiplier { get; private set; } = 1f;

        /// <summary>Wired by the factory. Must run before <see cref="Apply"/>.</summary>
        public void Wire(EnemyBrain brainRef, Health healthRef, NavMeshAgent agentRef, EnemyVisual visualRef, EnemyCombat combatRef)
        {
            brain = brainRef;
            health = healthRef;
            agent = agentRef;
            visual = visualRef;
            combat = combatRef;
            if (combat != null) combat.DamageDealtToPlayer += OnDealtDamage;
        }

        void OnDealtDamage(float amount)
        {
            if (healOnDamage > 0f && health != null && health.IsAlive)
                health.Heal(healOnDamage);
        }

        public void Apply(IEnumerable<EliteModifierDefinition> list, RngService rng)
        {
            float healthMult = 1f;
            float speedMult = 1f;
            float attackSpeedMult = 1f;

            foreach (EliteModifierDefinition mod in list)
            {
                if (mod == null) continue;
                modifiers.Add(mod);

                healthMult *= mod.healthMultiplier;
                speedMult *= mod.speedMultiplier;
                attackSpeedMult *= mod.attackSpeedMultiplier;
                RangeMultiplier *= mod.attackRangeMultiplier;
                regenPerSecond += mod.regenPerSecond;
                healOnDamage += mod.healOnDamageDealt;
                if (mod.lowHealthSpeedBonus > 1f)
                {
                    lowHealthSpeedBonus = Mathf.Max(lowHealthSpeedBonus, mod.lowHealthSpeedBonus);
                    lowHealthThreshold = mod.lowHealthThreshold;
                }
                if (mod.teleports) { teleports = true; teleportInterval = mod.teleportInterval; teleportRange = mod.teleportRange; }
                if (mod.summons) { summons = true; summonInterval = mod.summonInterval; summonKind = mod.summonKind; summonCount = mod.summonCount; }

                // Shield: absorb burst equal to a fraction of max health.
                if (mod.shieldFraction > 0f && health != null)
                    health.AddArmor(mod.shieldFraction * health.Maximum, health.Maximum * 3f);
            }

            if (health != null && healthMult != 1f) health.SetMaxHealth(health.Maximum * healthMult, keepRatio: false);

            if (agent != null && speedMult != 1f)
            {
                agent.speed *= speedMult;
                baseSpeed = agent.speed;
            }

            if (brain != null && brain.Combat != null)
                brain.Combat.AttackSpeedMultiplier = attackSpeedMult;

            // Visual identity: tint + crown in the strongest modifier's colour.
            if (visual != null && modifiers.Count > 0)
            {
                EliteModifierDefinition primary = modifiers[0];
                visual.ApplyEliteTint(primary.tint, primary.tintAmount);
                var crown = BuildCrown(primary.tint);
                visual.AttachCrown(crown);
            }

            if (teleports) teleportTimer = teleportInterval * .6f;
            if (summons) summonTimer = summonInterval * .7f;
        }

        float baseSpeed;

        void Update()
        {
            if (brain == null || health == null || !health.IsAlive) return;
            float dt = Time.deltaTime;

            if (regenPerSecond > 0f && health.HealthPercent < 1f)
                health.Heal(regenPerSecond * dt);

            // Berserker: extra speed when wounded.
            if (lowHealthSpeedBonus > 1f && agent != null && agent.enabled)
            {
                bool enraged = health.HealthPercent <= lowHealthThreshold;
                float target = enraged ? baseSpeed * lowHealthSpeedBonus : baseSpeed;
                agent.speed = Mathf.MoveTowards(agent.speed, target, 8f * dt);
            }

            if (teleports)
            {
                teleportTimer -= dt;
                if (teleportTimer <= 0f && brain.Player != null)
                {
                    teleportTimer = teleportInterval * Random.Range(.8f, 1.25f);
                    TryTeleport();
                }
            }

            if (summons)
            {
                summonTimer -= dt;
                if (summonTimer <= 0f)
                {
                    summonTimer = summonInterval * Random.Range(.9f, 1.2f);
                    TrySummon();
                }
            }
        }

        void TryTeleport()
        {
            if (brain.Player == null) return;
            Vector3 toPlayer = brain.Player.position - transform.position;
            if (toPlayer.magnitude < teleportRange * .4f) return; // already close

            Vector3 destination = brain.Player.position - toPlayer.normalized * (teleportRange * .45f);
            if (agent != null && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    Services.Effects?.Poof(transform.position + Vector3.up, new Color(.8f, .4f, 1f, .8f));
                    agent.Warp(hit.position);
                    Services.Effects?.Poof(transform.position + Vector3.up, new Color(.8f, .4f, 1f, .8f));
                    Services.Audio?.Play("teleport", .5f, 1f, transform.position);
                }
            }
        }

        void TrySummon()
        {
            if (brain.Player == null || AgentSpawner.Instance == null) return;
            for (int i = 0; i < summonCount; i++)
            {
                Vector2 circle = Random.insideUnitCircle * 3f;
                Vector3 position = transform.position + new Vector3(circle.x, 0f, circle.y);
                AgentSpawner.Instance.Spawn(summonKind, position, elite: false);
            }
            Services.Effects?.Ring(transform.position, 1f, 5f, new Color(1f, .6f, 1f, .7f), .6f);
        }

        Transform BuildCrown(Color color)
        {
            var crownGo = new GameObject("EliteCrown");
            crownGo.transform.SetParent(transform, false);
            crownGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var crown = crownGo.AddComponent<EliteCrown>();
            crown.SetColor(color);
            return crownGo.transform;
        }

        /// <summary>Explosive elite: detonate on death (chaining kill credit).</summary>
        public void OnDeath(DamageInfo killerInfo)
        {
            foreach (EliteModifierDefinition mod in modifiers)
            {
                if (mod.explodeOnDeathRadius > 0f)
                {
                    Services.Effects?.Explosion(transform.position, mod.explodeOnDeathRadius);
                    Services.Audio?.Play("explosion", .8f, 1f, transform.position);
                    ExplosionDamage.Explode(transform.position, mod.explodeOnDeathRadius,
                        mod.explodeOnDeathDamage, killerInfo.Source, DamageType.Explosive, 5f, 0f);
                }
            }
        }

        void OnDestroy()
        {
            if (combat != null) combat.DamageDealtToPlayer -= OnDealtDamage;
        }
    }

    /// <summary>Floating rotating marker above elite enemies.</summary>
    public class EliteCrown : MonoBehaviour
    {
        public void SetColor(Color color)
        {
            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "Crown";
            Destroy(crown.GetComponent<Collider>());
            crown.transform.SetParent(transform, false);
            crown.transform.localScale = new Vector3(.5f, .1f, .1f);
            crown.GetComponent<Renderer>().sharedMaterial = RogueArena.Visual.MaterialLibrary.Emissive(
                $"EliteCrownMat_{ColorUtility.ToHtmlStringRGB(color)}", color * .5f, color, 2.5f);

            var crown2 = Instantiate(crown, transform, false);
            crown2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        void Update()
        {
            transform.Rotate(0f, 80f * Time.deltaTime, 0f);
            transform.localPosition = new Vector3(0f, 2.3f + Mathf.Sin(Time.time * 2.4f) * .12f, 0f);
        }
    }
}
