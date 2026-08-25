using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Weapons;
using RogueArena.Visual;

namespace RogueArena.AI
{
    /// <summary>
    /// Executes attacks for every enemy archetype: melee, hitscan (with tracers
    /// and wind-up telegraphs), lobbed/straight projectiles and the medic's
    /// healing aura. All timing comes from the definition - no per-type code.
    /// </summary>
    public class EnemyCombat : MonoBehaviour
    {
        EnemyBrain brain;
        EnemyDefinition def;
        Health playerHealth;
        Transform player;
        Transform muzzle;
        EnemyVisual visual;

        // modifiers
        public float AttackSpeedMultiplier { get; set; } = 1f;
        public float RangeMultiplier { get; set; } = 1f;
        public float DamageScale { get; set; } = 1f;

        // state
        float nextAttackTime;
        bool winding;
        float windupEnd;
        Vector3 windupAimPoint;
        GameObject healTarget;
        float nextHealBeam;

        /// <summary>Raised when this enemy damages the player (vampiric elites).</summary>
        public event System.Action<float> DamageDealtToPlayer;

        static readonly Collider[] allyBuffer = new Collider[24];

        public void Initialize(EnemyBrain owner, EnemyDefinition definition, Transform muzzlePoint, EnemyVisual enemyVisual)
        {
            brain = owner;
            def = definition;
            muzzle = muzzlePoint;
            visual = enemyVisual;
            player = owner.Player;
            playerHealth = owner.PlayerHealth;
            nextAttackTime = Time.time + Random.Range(.4f, 1.2f);
        }

        public float EffectiveRange => def.attackRange * RangeMultiplier;
        public bool Winding => winding;

        /// <summary>Called by the brain while the enemy is in attack position.</summary>
        public void TickCombat(bool inRange, bool hasSight)
        {
            if (def == null || player == null) return;

            switch (def.attackStyle)
            {
                case AttackStyle.Healer:
                    TickHealer();
                    return;
                case AttackStyle.None:
                    return;
            }

            if (winding)
            {
                TickWindup();
                return;
            }

            if (!inRange || !hasSight) return;
            if (Time.time < nextAttackTime) return;

            StartWindup();
        }

        // ---------------------------------------------------------------- windup

        void StartWindup()
        {
            winding = true;
            float interval = def.attackInterval / Mathf.Max(.2f, AttackSpeedMultiplier);
            nextAttackTime = Time.time + interval;
            windupEnd = Time.time + def.attackWindup;
            windupAimPoint = player.position + Vector3.up * 1.1f;

            if (def.attackStyle == AttackStyle.Hitscan && def.attackWindup > .8f)
                Services.Audio?.Play("enemy_alert", .4f); // sniper telegraph cue
        }

        void TickWindup()
        {
            // Track the player during windup (last third locks in - dodgeable).
            if (Time.time < windupEnd - def.attackWindup * .3f)
                windupAimPoint = player.position + Vector3.up * 1.1f;

            float progress = 1f - Mathf.Max(0f, windupEnd - Time.time) / Mathf.Max(.01f, def.attackWindup);
            visual?.SetCharge(progress);

            // Sniper laser telegraph.
            if (def.attackStyle == AttackStyle.Hitscan && def.attackWindup > .8f && visual != null)
                Services.Effects?.Tracer(muzzle.position, windupAimPoint, new Color(1f, .2f, .2f, .5f));

            if (Time.time >= windupEnd)
            {
                winding = false;
                visual?.SetCharge(0f);
                ExecuteAttack();
            }
        }

        void ExecuteAttack()
        {
            switch (def.attackStyle)
            {
                case AttackStyle.Melee: ExecuteMelee(); break;
                case AttackStyle.Hitscan: ExecuteHitscan(); break;
                case AttackStyle.Projectile: ExecuteProjectile(); break;
            }
        }

        // ---------------------------------------------------------------- attacks

        void ExecuteMelee()
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > def.attackRange * 1.35f) return; // player escaped

            // Melee cannot reach through walls: require a clear line to the player.
            Vector3 eye = transform.position + Vector3.up * (def.agentHeight * .6f);
            Vector3 to = player.position + Vector3.up * 1.0f - eye;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit block, to.magnitude,
                    Layers.OccluderMask, QueryTriggerInteraction.Ignore))
                return; // something solid is in the way

            Services.Audio?.Play(def.meleeSfx, .6f, 1f, transform.position);
            Services.Effects?.Impact(player.position + Vector3.down * .6f, -transform.forward, new Color(1f, .4f, .2f));

            bool playerAlive = playerHealth != null && playerHealth.IsAlive;
            playerHealth?.TakeDamage(new DamageInfo(
                def.attackDamage * DamageScale,
                player.position,
                (player.position - transform.position).normalized,
                DamageType.Melee,
                gameObject));
            if (playerAlive) DamageDealtToPlayer?.Invoke(def.attackDamage * DamageScale);
        }

        void ExecuteHitscan()
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.3f;
            Services.Audio?.Play(def.shotSfx, .7f, 1f, origin);

            int shots = Mathf.Max(1, def.projectileCount);
            for (int i = 0; i < shots; i++)
            {
                Vector3 direction = (windupAimPoint - origin).normalized;
                float spreadRad = def.attackSpread * Mathf.Deg2Rad;
                if (spreadRad > 0f)
                {
                    Vector2 rand = Random.insideUnitCircle * Mathf.Tan(spreadRad);
                    direction = (direction + transform.right * rand.x + Vector3.up * rand.y).normalized;
                }

                if (Physics.Raycast(origin, direction, out RaycastHit hit, def.attackRange * RangeMultiplier,
                        Layers.VisionMask, QueryTriggerInteraction.Ignore))
                {
                    bool hitPlayer = hit.collider.gameObject.layer == Layers.PlayerBody;
                    Services.Effects?.Tracer(origin, hit.point, def.tracerColor);

                    if (hitPlayer && playerHealth != null && playerHealth.IsAlive)
                    {
                        playerHealth.TakeDamage(new DamageInfo(
                            def.attackDamage * DamageScale, hit.point, direction, DamageType.Bullet, gameObject));
                        DamageDealtToPlayer?.Invoke(def.attackDamage * DamageScale);
                    }
                    else
                    {
                        Services.Effects?.Impact(hit.point, hit.normal, new Color(.7f, .7f, .6f));
                    }
                }
                else
                {
                    Services.Effects?.Tracer(origin, origin + direction * 30f, def.tracerColor);
                }
            }
        }

        void ExecuteProjectile()
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.3f;
            Vector3 target = player.position + Vector3.up * .8f;

            float speed = def.projectileSpeed;
            Vector3 toTarget = target - origin;

            // Solve a simple ballistic lead + arc for lobbed projectiles.
            Vector3 direction;
            if (def.projectileArc > 0f)
            {
                float flightTime = Mathf.Clamp(toTarget.magnitude / speed, .3f, 3f);
                direction = (toTarget + Vector3.up * (.5f * def.projectileArc * flightTime)) / (speed * flightTime);
            }
            else
            {
                // Lead the target using the player's current velocity.
                var locomotion = player.GetComponent<FirstPersonController>();
                Vector3 predicted = target;
                if (locomotion != null)
                {
                    float flightTime = toTarget.magnitude / Mathf.Max(1f, speed);
                    predicted += locomotion.HorizontalVelocityVector * flightTime * .7f;
                }
                direction = (predicted - origin).normalized;
            }

            var settings = new ProjectileSettings
            {
                speed = speed,
                gravity = def.projectileArc,
                radius = .25f,
                splashRadius = def.kind == Core.EnemyKind.Explosive ? 3.5f : 0f,
                splashDamage = def.kind == Core.EnemyKind.Explosive ? def.attackDamage * .8f * DamageScale : 0f,
                splashKnockback = 4f,
                selfDamageFraction = 0f,
                trail = true,
                tracerColor = def.tracerColor,
                impactSfx = "explosion",
            };

            Projectile.Spawn(origin, direction.normalized, settings, def.attackDamage * DamageScale,
                Projectile.Owner.Enemy, gameObject);
            Services.Audio?.Play(def.shotSfx, .6f, 1f, origin);
        }

        // ---------------------------------------------------------------- healer

        void TickHealer()
        {
            if (Time.time < nextHealBeam) return;
            nextHealBeam = Time.time + .4f;

            healTarget = FindWoundedAlly();
            if (healTarget == null) return;

            var ally = healTarget.GetComponent<Health>();
            if (ally == null || !ally.IsAlive) { healTarget = null; return; }

            ally.Heal(def.healPerSecond * .4f);
            Services.Effects?.Tracer(
                transform.position + Vector3.up * 1.2f,
                healTarget.transform.position + Vector3.up * 1.1f,
                new Color(.25f, 1f, .4f, .7f));

            if (Random.value < .15f) Services.Audio?.Play("heal_beam", .25f, 1f, transform.position);
        }

        GameObject FindWoundedAlly()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, def.healRange, allyBuffer,
                Layers.EnemyMask, QueryTriggerInteraction.Ignore);

            GameObject best = null;
            float lowestHealth = 1f;
            for (int i = 0; i < count; i++)
            {
                Collider collider = allyBuffer[i];
                if (collider == null) continue;
                var health = collider.GetComponentInParent<Health>();
                if (health == null || !health.IsAlive) continue;
                if (health.gameObject == gameObject) continue;
                if (health.HealthPercent >= .99f) continue;
                if (health.HealthPercent < lowestHealth)
                {
                    lowestHealth = health.HealthPercent;
                    best = health.gameObject;
                }
            }
            return best;
        }

        /// <summary>Interrupts an attack (knockback, stagger, death).</summary>
        public void CancelAttack()
        {
            winding = false;
            visual?.SetCharge(0f);
        }
    }
}
