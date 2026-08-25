using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Weapons;

namespace RogueArena.AI
{
    /// <summary>
    /// Reusable boss controller. Phases, attacks and telegraphs are all driven by
    /// <see cref="BossDefinition"/> data, so new bosses need no new code. Handles
    /// barrage, telegraphed ground slam, charge attacks, minion summons, a death
    /// sequence with rewards, and boss-bar events for the HUD.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public class BossBrain : MonoBehaviour
    {
        public BossDefinition Definition { get; private set; }
        public int CurrentPhase { get; private set; } = 1;
        public bool IsDying { get; private set; }

        Health health;
        NavMeshAgent agent;
        EnemyVisual visual;
        Transform player;
        Transform core;
        Transform muzzle;

        BossPhase phase;
        float attackTimer;
        float summonTimer;
        bool introDone;
        bool charging;
        Vector3 chargeTarget;
        float chargeEnd;
        bool chargeDamaged;
        Coroutine slamRoutine;
        Coroutine barrageRoutine;

        void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            visual = GetComponent<EnemyVisual>();
        }

        public void Initialize(BossDefinition definition, Transform playerTransform, float statScale)
        {
            Definition = definition;
            player = playerTransform;

            health.Configure(definition.maxHealth * statScale);
            health.Damaged += OnDamaged;
            health.Died += OnDied;

            agent.radius = definition.agentRadius;
            agent.height = definition.agentHeight;
            agent.speed = definition.moveSpeed;
            agent.stoppingDistance = definition.preferredRange * .6f;

            phase = definition.phases.Count > 0 ? definition.phases[0] : new BossPhase();

            GameEvents.BossSpawned?.Invoke(definition.displayName, health.Maximum);
            Services.Audio?.Play("boss_spawn", .9f);
            Services.Audio?.Play("boss_roar", .8f);

            StartCoroutine(IntroSequence());
        }

        IEnumerator IntroSequence()
        {
            yield return new WaitForSeconds(Definition.introRoarSeconds);
            introDone = true;
            attackTimer = 1.2f;
        }

        void OnDamaged(DamageInfo info, float remaining)
        {
            GameEvents.BossHealthChanged?.Invoke(remaining, health.Maximum);

            if (info.Headshot)
                Services.Effects?.Impact(info.Point, info.Normal, new Color(1f, .9f, .3f), true);

            // Phase transitions.
            if (phase != null && remaining / health.Maximum <= phase.healthFraction
                && Definition.phases.IndexOf(phase) < Definition.phases.Count - 1)
            {
                EnterNextPhase();
            }
        }

        void EnterNextPhase()
        {
            int index = Definition.phases.IndexOf(phase) + 1;
            phase = Definition.phases[index];
            CurrentPhase = index + 1;

            agent.speed = phase.moveSpeed;
            Services.Audio?.Play("boss_phase", .9f);
            Services.Effects?.Ring(transform.position, 2f, 14f, new Color(1f, .3f, .2f, .9f), .8f);
            Services.Effects?.FlashLight(transform.position + Vector3.up * 3f, 20f, new Color(1f, .4f, .2f), .6f);
            visual?.Flash(new Color(1f, .6f, .2f), .5f);
            GameEvents.BossPhaseChanged?.Invoke(CurrentPhase, phase.name);
        }

        void Update()
        {
            if (Definition == null || IsDying) return;
            if (player == null || !introDone) return;

            var playerHealth = player.GetComponent<Health>();
            if (playerHealth != null && !playerHealth.IsAlive) { agent.ResetPath(); return; }

            float dt = Time.deltaTime;
            float distance = Vector3.Distance(transform.position, player.position);

            FacePlayer(dt);

            if (charging)
            {
                TickCharge();
                return;
            }

            // Movement: hold preferred range.
            if (distance > Definition.preferredRange * 1.2f)
                SetDestination(player.position);
            else if (agent.hasPath && distance < Definition.preferredRange * .7f)
                agent.ResetPath();

            // Summon cooldown.
            if (phase.summon)
            {
                summonTimer -= dt;
                if (summonTimer <= 0f)
                {
                    summonTimer = 12f;
                    DoSummon();
                }
            }

            // Attacks.
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = phase.attackInterval;
                ChooseAttack(distance);
            }
        }

        void ChooseAttack(float distance)
        {
            if (phase.charge && distance > 9f && distance < 30f && Random.value < .35f)
            {
                StartCoroutine(ChargeAttack());
                return;
            }
            if (phase.groundSlam && distance < 22f && Random.value < .3f)
            {
                if (slamRoutine == null) slamRoutine = StartCoroutine(GroundSlam());
                return;
            }
            if (barrageRoutine == null)
                barrageRoutine = StartCoroutine(Barrage());
        }

        // ---------------------------------------------------------------- attacks

        IEnumerator Barrage()
        {
            // Telegraph: core glows, roar cue.
            visual?.SetCharge(1f);
            Services.Audio?.Play("boss_charge", .6f);
            yield return new WaitForSeconds(.55f);
            visual?.SetCharge(0f);

            int count = phase.barrageCount;
            float spread = phase.barrageSpread;
            for (int i = 0; i < count; i++)
            {
                if (IsDying) yield break;
                FireProjectileAtPlayer(spread);
                Services.Audio?.Play("enemy_shot_heavy", .6f, 1f, muzzle != null ? muzzle.position : transform.position);
                yield return new WaitForSeconds(.16f);
            }
            barrageRoutine = null;
        }

        void FireProjectileAtPlayer(float spreadDegrees)
        {
            if (player == null) return;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 3f;
            Vector3 target = player.position + Vector3.up * .9f;
            Vector3 direction = (target - origin).normalized;

            float rad = spreadDegrees * Mathf.Deg2Rad * .5f;
            Vector2 rand = Random.insideUnitCircle * Mathf.Tan(rad);
            direction = (direction + transform.right * rand.x + Vector3.up * rand.y * .5f).normalized;

            var settings = new ProjectileSettings
            {
                speed = phase.projectileSpeed,
                gravity = 0f,
                radius = .35f,
                splashRadius = 2.6f,
                splashDamage = Definition.attackDamage * .6f,
                splashKnockback = 6f,
                selfDamageFraction = 0f,
                trail = true,
                tracerColor = new Color(1f, .45f, .15f),
                impactSfx = "explosion",
            };
            Projectile.Spawn(origin, direction, settings, Definition.attackDamage,
                Projectile.Owner.Enemy, gameObject);
        }

        IEnumerator GroundSlam()
        {
            // Telegraph a ring at the player's position, then detonate it.
            Vector3 target = player.position;
            Services.Audio?.Play("boss_charge", .7f);
            Services.Effects?.Ring(target, .5f, phase.slamRadius, new Color(1f, .25f, .15f, .85f), phase.slamTelegraph);

            float elapsed = 0f;
            while (elapsed < phase.slamTelegraph)
            {
                if (IsDying) { slamRoutine = null; yield break; }
                elapsed += Time.deltaTime;
                visual?.SetCharge(elapsed / phase.slamTelegraph);
                yield return null;
            }
            visual?.SetCharge(0f);

            Services.Effects?.Explosion(target, phase.slamRadius);
            Services.Audio?.Play("boss_slam", .9f);
            Services.Effects?.Ring(target, phase.slamRadius, phase.slamRadius * 1.4f, new Color(1f, .6f, .2f, .9f), .4f);
            ExplosionDamage.Explode(target, phase.slamRadius, phase.slamDamage, gameObject,
                DamageType.Explosive, 8f, 0f);
            slamRoutine = null;
        }

        IEnumerator ChargeAttack()
        {
            Services.Audio?.Play("boss_roar", .7f);
            visual?.SetCharge(1f);
            Services.Effects?.Tracer(transform.position + Vector3.up * 1.5f,
                player.position + Vector3.up, new Color(1f, .3f, .2f, .6f));

            yield return new WaitForSeconds(phase.chargeTelegraph);
            visual?.SetCharge(0f);

            chargeTarget = player.position;
            chargeEnd = Time.time + 1.2f;
            chargeDamaged = false;
            charging = true;
            agent.speed = phase.chargeSpeed;
            Services.Audio?.Play("boss_charge", .8f);
        }

        void TickCharge()
        {
            SetDestination(chargeTarget);
            if (!chargeDamaged && player != null
                && Vector3.Distance(transform.position, player.position) < Definition.agentRadius + 1.4f)
            {
                chargeDamaged = true;
                player.GetComponent<Health>()?.TakeDamage(new DamageInfo(
                    phase.chargeDamage, player.position, transform.forward, DamageType.Melee, gameObject, knockback: 8f));
            }

            if (Time.time >= chargeEnd || Vector3.Distance(transform.position, chargeTarget) < 2f)
            {
                charging = false;
                agent.speed = phase.moveSpeed;
                agent.ResetPath();
                Services.Effects?.Ring(transform.position, 1f, 6f, new Color(1f, .5f, .2f, .8f), .4f);
            }
        }

        void DoSummon()
        {
            if (AgentSpawner.Instance == null) return;
            int spawned = 0;
            for (int i = 0; i < phase.summonCount; i++)
            {
                Vector2 circle = Random.insideUnitCircle * 5f;
                Vector3 position = transform.position + new Vector3(circle.x, 0f, circle.y);
                if (AgentSpawner.Instance.Spawn(phase.summonKind, position, elite: false) != null) spawned++;
            }
            if (spawned > 0)
            {
                Services.Audio?.Play("enemy_alert", .6f);
                GameEvents.Notification?.Invoke($"{spawned} REINFORCEMENTS INBOUND", new Color(1f, .5f, .2f), 2.5f);
            }
        }

        // ---------------------------------------------------------------- movement helpers

        void SetDestination(Vector3 point)
        {
            if (agent.isOnNavMesh && (agent.destination - point).sqrMagnitude > 1f)
                agent.SetDestination(point);
        }

        void FacePlayer(float dt)
        {
            if (player == null) return;
            Vector3 direction = player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 120f * dt);
        }

        // ---------------------------------------------------------------- death

        void OnDied(DamageInfo info)
        {
            if (IsDying) return;
            IsDying = true;

            if (charging) { charging = false; agent.speed = phase.moveSpeed; }
            StopAllCoroutines();
            barrageRoutine = null;
            slamRoutine = null;
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

            StartCoroutine(DeathSequence(info));
        }

        IEnumerator DeathSequence(DamageInfo killerInfo)
        {
            Services.Audio?.Play("boss_die", 1f);
            Services.Time?.Set(TimeController.HitStop, .12f);
            GameEvents.Notification?.Invoke("BOSS DEFEATED", new Color(1f, .85f, .2f), 3f);

            float elapsed = 0f;
            float duration = Definition.deathSequenceSeconds;
            var rng = new System.Random();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                // Chain of explosions across the body, accelerating.
                if (Random.value < .25f + t * .5f)
                {
                    Vector3 offset = new Vector3(
                        Random.Range(-2f, 2f), Random.Range(.5f, 4f), Random.Range(-2f, 2f));
                    Vector3 point = transform.position + offset;
                    Services.Effects?.Explosion(point, 2.5f);
                    Services.Audio?.Play("explosion", .5f, Random.Range(.85f, 1.2f), point);
                }

                transform.rotation = Quaternion.Slerp(transform.rotation,
                    transform.rotation * Quaternion.Euler(1.5f, 0f, 1f), Time.unscaledDeltaTime * 2f);
                yield return null;
            }

            Services.Time?.Clear(TimeController.HitStop);
            Services.Effects?.Explosion(transform.position, 8f);
            Services.Audio?.Play("explosion_big", 1f);

            GameEvents.BossDefeated?.Invoke(Definition.displayName, false);
            PublishBossKill(killerInfo);
            Destroy(gameObject);
        }

        void PublishBossKill(DamageInfo info)
        {
            bool byPlayer = info.Source != null && info.Source.GetComponentInParent<PlayerMarker>() != null;
            var kill = new KillInfo
            {
                EnemyName = Definition.displayName,
                Kind = EnemyKind.Boss,
                Position = transform.position,
                Headshot = info.Headshot,
                KilledByPlayer = byPlayer,
                DamageType = info.Type,
                Distance = player != null ? Vector3.Distance(transform.position, player.position) : 0f,
            };
            GameEvents.EnemyKilled?.Invoke(kill);
        }

        // ---------------------------------------------------------------- gizmos

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void OnDrawGizmos()
        {
            if (!DebugSettings.ShowAiGizmos || Definition == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Definition.preferredRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Definition.attackRange);
        }
    }
}
