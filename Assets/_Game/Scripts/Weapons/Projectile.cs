using System.Collections.Generic;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Visual;

namespace RogueArena.Weapons
{
    /// <summary>
    /// Pooled projectile used by player and enemy weapons alike. Movement is a
    /// swept raycast (no rigidbody physics) which keeps large projectile counts
    /// cheap and fully deterministic.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public enum Owner { Player, Enemy }

        Vector3 velocity;
        float gravity;
        float damage;
        float splashRadius;
        float splashDamage;
        float splashKnockback;
        float selfDamageFraction;
        float lifetime;
        float radius;
        Owner owner;
        GameObject source;
        bool alive;
        Color tracerColor;
        string impactSfx;
        TrailRenderer trail;
        MeshRenderer body;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[4];

        static readonly Queue<Projectile> pool = new Queue<Projectile>();
        static Transform poolRoot;

        public static Projectile Spawn(Vector3 position, Vector3 direction, ProjectileSettings settings,
            float damage, Owner owner, GameObject source)
        {
            Projectile p = null;
            // Skip stale pooled entries left over from a destroyed scene.
            while (pool.Count > 0 && p == null) p = pool.Dequeue();
            if (p == null) p = Create();
            p.gameObject.SetActive(true);
            p.transform.position = position;
            p.transform.rotation = Quaternion.LookRotation(direction);
            p.velocity = direction * settings.speed;
            p.gravity = settings.gravity;
            p.damage = damage;
            p.splashRadius = settings.splashRadius;
            p.splashDamage = settings.splashDamage;
            p.splashKnockback = settings.splashKnockback;
            p.selfDamageFraction = settings.selfDamageFraction;
            p.radius = Mathf.Max(.08f, settings.radius);
            p.lifetime = 9f;
            p.owner = owner;
            p.source = source;
            p.alive = true;
            p.tracerColor = settings.tracerColor;

            p.SetupVisual(settings);
            return p;
        }

        static Projectile Create()
        {
            // The pool root lives in the current scene so everything is torn down
            // cleanly on scene changes; the static queue tolerates stale entries.
            if (poolRoot == null) poolRoot = new GameObject("~ProjectilePool").transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;
            var p = go.AddComponent<Projectile>();
            go.transform.SetParent(poolRoot, false);
            go.SetActive(false);
            return p;
        }

        /// <summary>Called by the scene teardown so no projectile survives a reload.</summary>
        public static void DisposePool()
        {
            pool.Clear();
            if (poolRoot != null)
            {
                Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        void SetupVisual(ProjectileSettings settings)
        {
            if (body == null)
            {
                body = GetComponent<MeshRenderer>();
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = .18f;
                trail.startWidth = .09f;
                trail.endWidth = 0f;
                trail.material = MaterialLibrary.SpriteMaterial(tracerColor);
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Color bodyColor = new Color(tracerColor.r * .5f + .5f, tracerColor.g * .5f + .5f, tracerColor.b * .5f + .5f);
            body.sharedMaterial = MaterialLibrary.SpriteMaterial(bodyColor);
            body.transform.localScale = Vector3.one * radius * 2.2f;
            trail.material = MaterialLibrary.SpriteMaterial(tracerColor);
            trail.Clear();
            trail.enabled = settings.trail;
            body.enabled = true;

            int layer = owner == Owner.Player ? Layers.PlayerProjectiles : Layers.EnemyProjectiles;
            gameObject.layer = layer;
        }

        void Update()
        {
            if (!alive) return;

            float dt = Time.deltaTime;
            lifetime -= dt;
            if (lifetime <= 0f) { Despawn(); return; }

            if (gravity != 0f) velocity.y -= gravity * dt;

            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance <= 0f) return;

            Vector3 direction = step / distance;
            int mask = owner == Owner.Player
                ? Layers.BulletHitMask
                : Layers.EnemyProjectileHitMask;

            int hits = Physics.SphereCastNonAlloc(transform.position, radius, direction, hitBuffer, distance + radius * .5f, mask, QueryTriggerInteraction.Ignore);

            // NonAlloc results are not guaranteed distance-sorted: always take
            // the closest valid hit so projectiles never detonate on geometry
            // behind the thing they actually struck.
            int bestHit = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (hit.collider == null) continue;

                // Ignore our own shooter.
                if (source != null && hit.collider.transform.IsChildOf(source.transform)) continue;

                // Player projectiles must not hit the player.
                if (owner == Owner.Player && hit.collider.gameObject.layer == Layers.PlayerBody) continue;

                // Enemy projectiles must not hit other enemies.
                if (owner == Owner.Enemy && hit.collider.gameObject.layer == Layers.Enemies) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = i;
                }
            }

            if (bestHit >= 0)
            {
                OnHit(hitBuffer[bestHit]);
                return;
            }

            transform.position += step;
            if (gravity != 0f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        void OnHit(RaycastHit hit)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (splashRadius > 0f)
            {
                // Explosion handles everything in the radius (including the direct target).
                var effects = Services.Effects;
                if (effects != null) effects.Explosion(hit.point, splashRadius);
                Services.Audio?.Play(impactSfx, .9f, 1f, hit.point);
                GameEvents.NoiseEmitted?.Invoke(hit.point, 30f);
                ExplosionDamage.Explode(hit.point, splashRadius, splashDamage, source,
                    DamageType.Explosive, splashKnockback, selfDamageFraction);
                // Direct hit bonus on the struck target.
                if (damageable != null && damageable.IsAlive && damageable is Health h && h.gameObject != source)
                    damageable.TakeDamage(new DamageInfo(damage, hit.point, velocity.normalized, DamageType.Explosive, source, knockback: splashKnockback * .5f));
            }
            else
            {
                bool head = HitZone.IsHead(hit.collider);
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(new DamageInfo(damage, hit.point, velocity.normalized,
                        DamageType.Energy, source, headshot: head));
                }
                Services.Effects?.Impact(hit.point, hit.normal, tracerColor, energy: true);
                Services.Audio?.Play("impact_energy", .5f, 1f, hit.point);
            }

            Despawn();
        }

        void Despawn()
        {
            alive = false;
            gameObject.SetActive(false);
            if (pool.Count < 96) pool.Enqueue(this);
            else Destroy(gameObject);
        }
    }
}
