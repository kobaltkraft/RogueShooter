using System;
using UnityEngine;

namespace RogueArena.Combat
{
    /// <summary>
    /// Health with optional armour, damage/heal/death events and a brief post-hit
    /// invulnerability window for pulse-style attacks. Implements the shared
    /// <see cref="IDamageable"/> contract used by every weapon and hazard.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable, IHealable
    {
        [SerializeField, Min(1)] float maxHealth = 100;

        float current;
        float armor;
        float armorAbsorb = 0.6f;   // fraction of damage absorbed while armour remains
        float invulnUntil;          // Time.time-based, 0 = disabled

        /// <summary>Set > 0 to ignore all damage for N seconds after each hit.</summary>
        public float invulnerabilityWindow = 0f;

        public float Current => current;
        public float Maximum => maxHealth;
        public float Armor => armor;
        public float HealthPercent => current / Mathf.Max(1f, maxHealth);
        public bool IsAlive => current > 0f;
        public bool HasArmor => armor > 0f;

        /// <summary>current, max</summary>
        public event Action<float, float> Changed;
        /// <summary>damage info, health remaining afterwards</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action<float, float> Healed; // amount, health after
        public event Action<DamageInfo> Died;
        public event Action<float, float> ArmorChanged; // armor, max armor

        void Awake() => current = maxHealth;

        public void Configure(float max, float startingArmor = 0f)
        {
            maxHealth = Mathf.Max(1f, max);
            current = maxHealth;
            armor = Mathf.Max(0f, startingArmor);
            invulnUntil = 0f;
            Changed?.Invoke(current, maxHealth);
            ArmorChanged?.Invoke(armor, maxHealth);
        }

        /// <summary>Restores full health (used by respawning logic and debug tools).</summary>
        public void RestoreFull()
        {
            current = maxHealth;
            Changed?.Invoke(current, maxHealth);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return;
            if (invulnerabilityWindow > 0f && Time.time < invulnUntil) return;
            if (invulnerabilityWindow > 0f) invulnUntil = Time.time + invulnerabilityWindow;

            float amount = info.Amount;

            if (armor > 0f)
            {
                float absorbed = Mathf.Min(armor, amount * armorAbsorb);
                armor -= absorbed;
                amount -= absorbed;
                ArmorChanged?.Invoke(armor, maxHealth);
            }

            current = Mathf.Max(0f, current - amount);
            Changed?.Invoke(current, maxHealth);
            Damaged?.Invoke(info, current);

            if (current <= 0f) Died?.Invoke(info);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            float before = current;
            current = Mathf.Min(maxHealth, current + amount);
            if (current > before)
            {
                Changed?.Invoke(current, maxHealth);
                Healed?.Invoke(current - before, current);
            }
        }

        /// <summary>Adds armour on top of current health. Armour absorbs first.</summary>
        public void AddArmor(float amount, float cap)
        {
            armor = Mathf.Clamp(armor + amount, 0f, Mathf.Max(0f, cap));
            ArmorChanged?.Invoke(armor, maxHealth);
        }

        /// <summary>Direct stat override for elite modifiers. Keeps current ratio when shrinking.</summary>
        public void SetMaxHealth(float newMax, bool keepRatio = true)
        {
            float ratio = keepRatio ? HealthPercent : 1f;
            maxHealth = Mathf.Max(1f, newMax);
            current = Mathf.Clamp(maxHealth * ratio, keepRatio ? 0f : 1f, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
