using UnityEngine;
using RogueArena.Core;

namespace RogueArena.Combat
{
    /// <summary>How the damage was dealt. Drives feedback (sounds, particles, score bonuses).</summary>
    public enum DamageType
    {
        Bullet,
        Energy,
        Explosive,
        Melee,
        Fire,
        Electric,
        Environmental,
    }

    /// <summary>Immutable description of a single damage application.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly Vector3 Normal;
        public readonly DamageType Type;
        public readonly GameObject Source;
        public readonly bool Headshot;
        public readonly float Knockback;

        public DamageInfo(float amount, Vector3 point, Vector3 direction,
            DamageType type = DamageType.Bullet, GameObject source = null,
            bool headshot = false, float knockback = 0f, Vector3 normal = default)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Normal = normal == default ? -direction : normal;
            Type = type;
            Source = source;
            Headshot = headshot;
            Knockback = knockback;
        }
    }

    /// <summary>Anything that can receive damage.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo damage);
    }

    /// <summary>Anything that can be healed.</summary>
    public interface IHealable
    {
        bool IsAlive { get; }
        void Heal(float amount);
    }

    /// <summary>
    /// Everything the score, challenge and feedback systems need to know about a kill.
    /// </summary>
    public struct KillInfo
    {
        public string EnemyName;
        public EnemyKind Kind;
        public Vector3 Position;
        public bool Headshot;
        public bool WasElite;
        public bool KilledByPlayer;
        public bool KilledByBarrel;
        public bool KilledByExplosion;
        public bool PlayerWasAirborne;
        public DamageType DamageType;
        public float Distance;
        public int EliteModifierCount;
        public float TimeSinceSpawn;
    }
}
