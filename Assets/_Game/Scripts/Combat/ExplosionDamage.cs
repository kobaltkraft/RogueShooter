using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;
using RogueArena.AI;

namespace RogueArena.Combat
{
    /// <summary>Static helpers for area damage and knockback.</summary>
    public static class ExplosionDamage
    {
        static readonly Collider[] overlapBuffer = new Collider[64];
        // Dedupes multi-collider bodies so one explosion damages each target once.
        static readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();

        /// <summary>
        /// Applies radial damage with linear falloff to enemies, the player and
        /// hazards (barrels). Arcade rules: no line-of-sight reduction, full damage
        /// inside 40% of the radius, linear falloff to the edge.
        /// </summary>
        public static void Explode(Vector3 center, float radius, float damage, GameObject source,
            DamageType type = DamageType.Explosive, float knockback = 0f, float selfDamageFraction = 0f)
        {
            int mask = Layers.EnemyMask | Layers.PlayerMask | Layers.HazardMask;
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, mask, QueryTriggerInteraction.Collide);

            hitSet.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null) continue;

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (!hitSet.Add(damageable)) continue; // already damaged by this explosion

                Vector3 closest = hit.ClosestPoint(center);
                float distance = Vector3.Distance(center, closest);
                float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(.01f, radius));
                float scaled = Mathf.Lerp(damage * .4f, damage, falloff);

                // Self damage (rocket jumping) is reduced by the weapon's fraction.
                bool isSource = source != null && damageable is Health h && h.gameObject == source;
                if (isSource) scaled *= selfDamageFraction;

                var info = new DamageInfo(
                    scaled,
                    closest,
                    (closest - center).normalized,
                    type,
                    source,
                    knockback: knockback * falloff,
                    normal: (closest - center).normalized);

                damageable.TakeDamage(info);
            }
        }

        /// <summary>Pushes enemies away from a point. Resistant enemies move less.</summary>
        public static void Knockback(Vector3 center, float radius, float force)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, Layers.EnemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null) continue;
                var brain = hit.GetComponentInParent<EnemyBrain>();
                if (brain == null) continue;
                Vector3 dir = (brain.transform.position - center).normalized + Vector3.up * .35f;
                brain.ApplyKnockback(dir * force);
            }
        }
    }
}
