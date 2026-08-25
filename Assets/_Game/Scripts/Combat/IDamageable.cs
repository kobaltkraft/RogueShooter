using UnityEngine;
namespace RogueArena.Combat
{
    public readonly struct DamageInfo
    {
        public readonly float Amount; public readonly Vector3 Point; public readonly Vector3 Direction; public readonly bool Headshot;
        public DamageInfo(float amount, Vector3 point, Vector3 direction, bool headshot=false) { Amount=amount; Point=point; Direction=direction; Headshot=headshot; }
    }
    public interface IDamageable { bool IsAlive { get; } void TakeDamage(DamageInfo damage); }
}
