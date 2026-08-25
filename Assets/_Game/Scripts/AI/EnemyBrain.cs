using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Environment;
using RogueArena.Player;

namespace RogueArena.AI
{
    public enum EnemyState { Idle, Patrol, Investigate, Chase, Engage, TakeCover, Search, Retreat, Dead }

    /// <summary>
    /// The shared brain for every enemy archetype. A utility-flavoured FSM driven
    /// entirely by <see cref="EnemyDefinition"/> data plus live tactical inputs:
    /// distance, health, allies, cover, recent damage and last-known player
    /// position. Perception runs on a staggered tick - no per-frame raycasts.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        public EnemyDefinition Definition { get; private set; }
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsElite => elite != null && elite.ModifierCount > 0;

        public Transform Player { get; private set; }
        public Health PlayerHealth { get; private set; }
        public Vector3 LastKnownPlayerPos { get; private set; }
        public bool SeesPlayer { get; private set; }
        public float TimeSinceSeen { get; private set; }
        public CoverPoint CoverTarget { get; private set; }
        public EnemyCombat Combat => combat;

        NavMeshAgent agent;
        Health health;
        EnemyCombat combat;
        EnemyElite elite;
        EnemyVisual visual;
        EnemyFlight flight;
        RngService rng;

        /// <summary>Raised when the enemy object is destroyed (spawner bookkeeping).</summary>
        public event System.Action OnDestroyed;

        // perception
        float perceptionTimer;
        Vector3 eyeOffset = Vector3.up * 1.35f;

        // decisions
        float decisionTimer;
        Vector3 destination;
        Vector3 home;
        Vector3 strafeDirection = Vector3.right;
        float strafeTimer;
        float repositionTimer;
        float searchTimer;
        Vector3 investigatePoint;
        float investigateTimer;
        bool alertPlayed;

        // damage reaction
        float staggerTimer;
        float spawnTime;
        float damageScale = 1f;

        // movement bookkeeping
        Vector3 lastDestinationSet;
        float destinationCooldown;

        // stuck detection (unreachable destinations should never stall a wave)
        Vector3 lastStuckCheckPosition;
        float stuckTimer;

        public void Initialize(EnemyDefinition definition, Transform playerTransform, RngService random, float difficultyScale = 1f)
        {
            Definition = definition;
            Player = playerTransform;
            rng = random;
            damageScale = difficultyScale;
            PlayerHealth = playerTransform != null ? playerTransform.GetComponent<Health>() : null;
            home = transform.position;
            spawnTime = Time.time;
            perceptionTimer = rng?.Range(0f, .2f) ?? Random.value * .2f;
            eyeOffset = Vector3.up * (definition.agentHeight * .7f);

            if (combat != null)
            {
                combat.DamageScale = damageScale;
                combat.Initialize(this, definition, muzzlePoint, visual);
            }
            if (flight != null) flight.Initialize(this, definition);

            health.Died += Die;
        }

        public void SetPlayer(Transform playerTransform)
        {
            Player = playerTransform;
            PlayerHealth = playerTransform != null ? playerTransform.GetComponent<Health>() : null;
        }

        // ---------------------------------------------------------------- wiring (called by factory before Initialize)

        public void Wire(Health healthRef, NavMeshAgent agentRef, EnemyCombat combatRef, EnemyElite eliteRef,
            EnemyVisual visualRef, EnemyFlight flightRef, Transform muzzle)
        {
            health = healthRef;
            agent = agentRef;
            combat = combatRef;
            elite = eliteRef;
            visual = visualRef;
            flight = flightRef;
            muzzlePoint = muzzle;
        }

        Transform muzzlePoint;

        // ---------------------------------------------------------------- lifecycle

        void Update()
        {
            if (Definition == null || State == EnemyState.Dead) return;

            float dt = Time.deltaTime;
            if (staggerTimer > 0f) { staggerTimer -= dt; return; }

            bool playerAlive = PlayerHealth != null && PlayerHealth.IsAlive;
            if (!playerAlive) { SetState(EnemyState.Idle); return; }

            perceptionTimer -= dt;
            if (perceptionTimer <= 0f)
            {
                perceptionTimer = Definition.perceptionInterval;
                UpdatePerception();
            }

            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                decisionTimer = .18f;
                MakeDecision();
            }

            UpdateStuckDetection(dt);
            Act(dt);
        }

        // ---------------------------------------------------------------- perception

        void UpdatePerception()
        {
            if (Player == null) return;

            bool canSee = CanSeePlayer();
            if (canSee)
            {
                SeesPlayer = true;
                TimeSinceSeen = 0f;
                LastKnownPlayerPos = Player.position;
                if (!alertPlayed && State is EnemyState.Idle or EnemyState.Patrol)
                {
                    alertPlayed = true;
                    Services.Audio?.Play(Definition.alertSfx, .5f, 1f, transform.position);
                }
            }
            else
            {
                SeesPlayer = false;
                TimeSinceSeen += Definition.perceptionInterval;
                alertPlayed = false;
            }
        }

        bool CanSeePlayer()
        {
            if (Player == null || flight == null && agent != null && !agent.isOnNavMesh) return false;

            Vector3 eye = transform.position + eyeOffset;
            Vector3 target = Player.position + Vector3.up * 1.0f;
            Vector3 to = target - eye;
            float sqrRange = Definition.sightRange * Definition.sightRange;

            if (to.sqrMagnitude > sqrRange) return false;

            // Flying and melee units have generous FOV; strict for precise shooters.
            float fov = Definition.fieldOfView;
            if (State == EnemyState.Engage || State == EnemyState.Chase) fov = 360f; // already alerted
            if (Vector3.Angle(transform.forward, to) > fov * .5f) return false;

            float distance = to.magnitude;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, distance + .5f,
                    Layers.VisionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.gameObject.layer == Layers.PlayerBody;
            }
            return false;
        }

        /// <summary>Noise hearing hook (gunfire, footsteps, jumps).</summary>
        public void HearNoise(Vector3 position, float radius)
        {
            if (State == EnemyState.Dead || Player == null) return;
            float distance = Vector3.Distance(transform.position, position);
            if (distance > Mathf.Min(radius, Definition.hearingRange)) return;

            investigatePoint = position;
            investigateTimer = 8f;
            if (State is EnemyState.Idle or EnemyState.Patrol or EnemyState.Search)
                SetState(EnemyState.Investigate);
        }

        // ---------------------------------------------------------------- decisions

        void MakeDecision()
        {
            if (Player == null) return;

            float distance = Vector3.Distance(transform.position, Player.position);
            float healthFraction = health.HealthPercent;
            float attackRange = Definition.attackRange * (elite != null ? elite.RangeMultiplier : 1f);
            if (combat != null) attackRange = combat.EffectiveRange;

            switch (State)
            {
                case EnemyState.Idle:
                case EnemyState.Patrol:
                    if (SeesPlayer) SetState(EnemyState.Chase);
                    break;

                case EnemyState.Investigate:
                    if (SeesPlayer) SetState(EnemyState.Chase);
                    else if (investigateTimer <= 0f) SetState(EnemyState.Patrol);
                    break;

                case EnemyState.Chase:
                {
                    if (!SeesPlayer && TimeSinceSeen > 2.5f) { SetState(EnemyState.Search); break; }
                    if (SeesPlayer && distance <= attackRange) SetState(EnemyState.Engage);
                    break;
                }

                case EnemyState.Engage:
                {
                    if (!SeesPlayer) { SetState(EnemyState.Search); break; }

                    // Retreat when badly wounded.
                    if (Definition.retreatHealthFraction > 0f && healthFraction < Definition.retreatHealthFraction)
                    {
                        SetState(EnemyState.Retreat);
                        break;
                    }

                    // Seek cover between attacks sometimes.
                    if (CoverTarget == null && Definition.coverPreference > 0f
                        && (rng?.Chance(Definition.coverPreference * .4f) ?? false)
                        && distance > attackRange * .5f)
                    {
                        CoverPoint candidate = CoverRegistry.FindCover(transform.position,
                            Player.position + Vector3.up * 1.6f, 18f);
                        if (candidate != null)
                        {
                            CoverTarget = candidate;
                            candidate.Occupant = this;
                            SetState(EnemyState.TakeCover);
                            break;
                        }
                    }

                    // Out of range - chase back in.
                    if (distance > attackRange * 1.15f) { SetState(EnemyState.Chase); break; }

                    // Back off if the player is crowding us.
                    if (Definition.minimumRange > 0f && distance < Definition.minimumRange)
                    {
                        SetState(EnemyState.Retreat);
                        break;
                    }
                    break;
                }

                case EnemyState.TakeCover:
                {
                    if (CoverTarget == null) { SetState(EnemyState.Engage); break; }

                    bool atCover = Vector3.Distance(transform.position, CoverTarget.transform.position) < 1.2f;
                    if (atCover)
                    {
                        // Cover is only useful while it still blocks sight; peek to fire.
                        bool blocks = CoverTarget.BlocksSight(Player.position + Vector3.up * 1.6f);
                        if (!blocks || healthFraction < .15f)
                        {
                            ReleaseCover();
                            SetState(EnemyState.Engage);
                        }
                        // Otherwise stay and peek-attack (Act handles firing from cover edge).
                    }
                    else if (TimeSinceSeen > 1.5f || distance > attackRange * 1.4f)
                    {
                        ReleaseCover();
                        SetState(EnemyState.Engage);
                    }
                    break;
                }

                case EnemyState.Search:
                    if (SeesPlayer) { SetState(EnemyState.Chase); break; }
                    if (searchTimer <= 0f) { SetState(EnemyState.Patrol); break; }
                    break;

                case EnemyState.Retreat:
                {
                    bool safe = distance > attackRange * 1.5f;
                    bool healthy = healthFraction > Definition.retreatHealthFraction + .2f;
                    if (safe && (healthy || Definition.retreatHealthFraction <= 0f))
                        SetState(SeesPlayer ? EnemyState.Engage : EnemyState.Search);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- actions

        void Act(float dt)
        {
            if (Player == null) return;

            investigateTimer -= dt;
            searchTimer -= dt;
            strafeTimer -= dt;
            repositionTimer -= dt;
            destinationCooldown -= dt;

            float distance = Vector3.Distance(transform.position, Player.position);
            float attackRange = combat != null ? combat.EffectiveRange : Definition.attackRange;

            switch (State)
            {
                case EnemyState.Idle:
                    StopMoving();
                    break;

                case EnemyState.Patrol:
                    PatrolTick();
                    break;

                case EnemyState.Investigate:
                    MoveTo(investigatePoint, Definition.moveSpeed * .8f);
                    if (Arrived(investigatePoint, 1.5f))
                    {
                        investigateTimer -= dt * 3f; // look around, then leave
                    }
                    break;

                case EnemyState.Chase:
                {
                    // Flankers aim for an offset point beside the player.
                    Vector3 target = Player.position;
                    if (Definition.flankChance > 0f && (rng?.Chance(Definition.flankChance) ?? false))
                    {
                        Vector3 side = Random.value < .5f ? Vector3.right : Vector3.left;
                        target += Player.TransformDirection(side) * 5f;
                    }
                    MoveTo(target, Definition.moveSpeed);
                    FaceTowards(Player.position, dt);
                    TryAttack(distance <= attackRange * .95f, SeesPlayer);
                    break;
                }

                case EnemyState.Engage:
                    EngageTick(dt, distance, attackRange);
                    break;

                case EnemyState.TakeCover:
                {
                    if (CoverTarget != null)
                    {
                        MoveTo(CoverTarget.transform.position, Definition.moveSpeed);
                        FaceTowards(Player.position, dt);
                        bool atCover = Vector3.Distance(transform.position, CoverTarget.transform.position) < 1.4f;
                        if (atCover && SeesPlayer && distance <= attackRange)
                        {
                            // Peek: fire while in cover.
                            TryAttack(true, true);
                        }
                    }
                    break;
                }

                case EnemyState.Search:
                {
                    MoveTo(LastKnownPlayerPos, Definition.moveSpeed * .75f);
                    if (Arrived(LastKnownPlayerPos, 2f))
                    {
                        // Wander near the last known position.
                        Vector3 wander = LastKnownPlayerPos + new Vector3(
                            Mathf.PerlinNoise(Time.time * .3f, 0f) * 8f - 4f, 0f,
                            Mathf.PerlinNoise(0f, Time.time * .3f) * 8f - 4f);
                        MoveTo(wander, Definition.moveSpeed * .6f);
                    }
                    break;
                }

                case EnemyState.Retreat:
                {
                    Vector3 away = (transform.position - Player.position).normalized;
                    Vector3 retreatPoint = transform.position + away * 8f;
                    MoveTo(retreatPoint, Definition.moveSpeed * 1.05f);
                    FaceTowards(Player.position, dt);
                    // Medic keeps healing while retreating.
                    if (combat != null && Definition.attackStyle == AttackStyle.Healer)
                        combat.TickCombat(false, false);
                    break;
                }
            }
        }

        void EngageTick(float dt, float distance, float attackRange)
        {
            // Strafe around the preferred range.
            if (strafeTimer <= 0f)
            {
                strafeTimer = Definition.strafeInterval * Random.Range(.7f, 1.3f);
                strafeDirection = Random.value < .5f ? Vector3.right : Vector3.left;
            }

            Vector3 toPlayer = (Player.position - transform.position).normalized;
            Vector3 strafe = Player.TransformDirection(strafeDirection);
            float preferred = Definition.preferredRange;

            Vector3 desired;
            if (distance > preferred * 1.25f) desired = Player.position - toPlayer * preferred * .8f;
            else if (distance < preferred * .7f) desired = transform.position - toPlayer * 3f;
            else desired = transform.position + strafe * 3f;

            // Keep a minimum distance from the player (no face-hugging).
            if (distance < 2.2f) desired -= toPlayer * 2f;

            if (Definition.strafeSpeed > 0f || distance > preferred * 1.25f)
                MoveTo(desired, Definition.moveSpeed);
            else StopMoving();

            FaceTowards(Player.position, dt);
            TryAttack(distance <= attackRange, SeesPlayer);

            // Snipers reposition after firing.
            if (Definition.repositionAfterAttack > 0f && combat != null && !combat.Winding
                && repositionTimer <= 0f && distance < attackRange * .9f)
            {
                repositionTimer = Definition.repositionAfterAttack;
                Vector3 repositionPoint = transform.position + new Vector3(
                    Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                MoveTo(repositionPoint, Definition.moveSpeed);
                if (Definition.usesElevatedPositions)
                {
                    CoverPoint spot = CoverRegistry.FindCover(transform.position,
                        Player.position + Vector3.up * 1.6f, 22f);
                    if (spot != null) MoveTo(spot.transform.position, Definition.moveSpeed);
                }
            }
        }

        void PatrolTick()
        {
            if (Arrived(destination, 1.5f) || destination.sqrMagnitude < .1f)
            {
                Vector2 circle = Random.insideUnitCircle * 8f;
                destination = home + new Vector3(circle.x, 0f, circle.y);
            }
            MoveTo(destination, Definition.moveSpeed * .45f);
        }

        /// <summary>
        /// Unsticks the agent: if it has been effectively motionless for several
        /// seconds while trying to move, it wanders to a nearby point instead so
        /// a single unreachable destination can never stall a wave.
        /// </summary>
        void UpdateStuckDetection(float dt)
        {
            if (State is EnemyState.Idle or EnemyState.Dead)
            {
                stuckTimer = 0f;
                lastStuckCheckPosition = transform.position;
                return;
            }

            if (Vector3.SqrMagnitude(transform.position - lastStuckCheckPosition) > .04f)
            {
                // Genuinely moving.
                stuckTimer = 0f;
                lastStuckCheckPosition = transform.position;
                return;
            }

            stuckTimer += dt;
            if (stuckTimer >= 3.5f)
            {
                stuckTimer = 0f;
                lastStuckCheckPosition = transform.position;
                ReleaseCover();
                Vector2 circle = Random.insideUnitCircle * 6f;
                MoveTo(transform.position + new Vector3(circle.x, 0f, circle.y), Definition.moveSpeed);
                if (State is EnemyState.Engage or EnemyState.TakeCover or EnemyState.Retreat)
                    SetState(EnemyState.Chase);
            }
        }

        /// <summary>Run-end hook: stops attacking and idles until teardown.</summary>
        public void Pacify()
        {
            if (State == EnemyState.Dead) return;
            ReleaseCover();
            combat?.CancelAttack();
            SetState(EnemyState.Idle);
            StopMoving();
        }

        void TryAttack(bool inRange, bool hasSight)
        {
            combat?.TickCombat(inRange, hasSight);
        }

        // ---------------------------------------------------------------- movement helpers

        void MoveTo(Vector3 point, float speed)
        {
            if (flight != null) { flight.MoveTo(point, speed); return; }
            if (agent == null || !agent.isOnNavMesh) return;

            if (destinationCooldown > 0f && (point - lastDestinationSet).sqrMagnitude < 2.25f) return;
            lastDestinationSet = point;
            destinationCooldown = .2f;

            agent.speed = speed;
            agent.SetDestination(point);
        }

        void StopMoving()
        {
            if (flight != null) { flight.Stop(); return; }
            if (agent != null && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.ResetPath();
            }
        }

        bool Arrived(Vector3 point, float threshold)
        {
            float planar = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(point.x, 0f, point.z));
            return planar <= threshold;
        }

        void FaceTowards(Vector3 point, float dt)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) return;
            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 360f * dt);
        }

        void SetState(EnemyState next)
        {
            if (State == next) return;

            EnemyState previous = State;
            State = next;

            switch (next)
            {
                case EnemyState.Search:
                    searchTimer = Definition.searchDuration > 0 ? Definition.searchDuration : 5f;
                    break;
                case EnemyState.Retreat:
                    ReleaseCover();
                    break;
                case EnemyState.Engage:
                case EnemyState.Chase:
                    // resume pathing
                    break;
            }

            if (previous == EnemyState.TakeCover) ReleaseCover();
        }

        void ReleaseCover()
        {
            if (CoverTarget != null) CoverTarget.Occupant = null;
            CoverTarget = null;
        }

        // ---------------------------------------------------------------- reactions

        /// <summary>Applies knockback with resistance; staggers briefly.</summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            if (State == EnemyState.Dead) return;
            float scale = 1f - Definition.knockbackResistance;
            if (scale <= 0f) return;

            if (flight != null) { flight.AddImpulse(impulse * scale); return; }
            if (agent != null && agent.isOnNavMesh)
                agent.velocity += impulse * scale;
            staggerTimer = Mathf.Max(staggerTimer, .18f);
            combat?.CancelAttack();
        }

        // ---------------------------------------------------------------- death

        void Die(DamageInfo info)
        {
            if (State == EnemyState.Dead) return;
            State = EnemyState.Dead;
            ReleaseCover();

            if (elite != null) elite.OnDeath(info);

            if (agent != null) agent.enabled = false;
            combat?.CancelAttack();
            if (flight != null) flight.enabled = false;

            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

            // Death presentation: tilt over and sink.
            transform.Rotate(70f, 0f, Random.Range(-25f, 25f));

            Services.Audio?.Play(Definition.deathSfx, .7f, Random.Range(.9f, 1.15f), transform.position);
            Services.Effects?.DeathBurst(transform.position + Vector3.up * 1f,
                new Color(.9f, .3f, .2f, .95f), Definition.bodyScale);

            PublishKill(info);
            Destroy(gameObject, 1.4f);
        }

        void PublishKill(DamageInfo info)
        {
            bool byPlayer = info.Source != null &&
                (info.Source.GetComponentInParent<PlayerMarker>() != null ||
                 info.Source.GetComponent<Environment.ExplosiveBarrel>() != null);
            bool byBarrel = info.Source != null && info.Source.GetComponent<Environment.ExplosiveBarrel>() != null;

            var kill = new KillInfo
            {
                EnemyName = Definition.displayName,
                Kind = Definition.kind,
                Position = transform.position,
                Headshot = info.Headshot,
                WasElite = IsElite,
                KilledByPlayer = byPlayer || byBarrel,
                KilledByBarrel = byBarrel,
                KilledByExplosion = info.Type == DamageType.Explosive,
                PlayerWasAirborne = Player != null && Player.GetComponent<FirstPersonController>() is { IsGrounded: false },
                DamageType = info.Type,
                Distance = Player != null ? Vector3.Distance(transform.position, Player.position) : 0f,
                EliteModifierCount = elite != null ? elite.ModifierCount : 0,
                TimeSinceSpawn = Time.time - spawnTime,
            };
            GameEvents.EnemyKilled?.Invoke(kill);
        }

        void OnDestroy()
        {
            OnDestroyed?.Invoke();
        }

        // ---------------------------------------------------------------- gizmos

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void OnDrawGizmos()
        {
            if (!DebugSettings.ShowAiGizmos || Definition == null) return;

            Gizmos.color = State switch
            {
                EnemyState.Chase => Color.yellow,
                EnemyState.Engage => Color.red,
                EnemyState.TakeCover => Color.cyan,
                EnemyState.Search => new Color(1f, .5f, 0f),
                EnemyState.Retreat => Color.magenta,
                _ => Color.gray,
            };
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .2f, .6f);

            if (Application.isPlaying && agent != null && agent.hasPath)
            {
                Gizmos.color = new Color(0f, 1f, 1f, .5f);
                Vector3 previous = transform.position;
                foreach (Vector3 corner in agent.path.corners)
                {
                    Gizmos.DrawLine(previous, corner);
                    previous = corner;
                }
            }

            if (Application.isPlaying && Player != null)
            {
                Gizmos.color = SeesPlayer ? Color.green : new Color(.5f, .5f, .5f, .3f);
                Gizmos.DrawLine(transform.position + eyeOffset, Player.position + Vector3.up);
            }

            if (Application.isPlaying && CoverTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, CoverTarget.transform.position + Vector3.up * .5f);
            }
        }
    }
}
