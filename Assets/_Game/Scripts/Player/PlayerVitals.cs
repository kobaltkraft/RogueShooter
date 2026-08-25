using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;

namespace RogueArena.Player
{
    /// <summary>
    /// Player health wrapper: armour, delayed regeneration (upgrade), damage
    /// feedback broadcasting and death handling that cleanly shuts down movement,
    /// dash and weapons.
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        public Health Health { get; private set; }

        [SerializeField] float regenDelay = 5f;
        float regenPerSecond;
        float timeSinceDamage;
        bool dead;

        public bool IsAlive => Health != null && Health.IsAlive;
        public float HealthPercent => Health != null ? Health.HealthPercent : 0f;
        public float CurrentHealth => Health != null ? Health.Current : 0f;
        public float MaxHealth => Health != null ? Health.Maximum : 1f;
        public float CurrentArmor => Health != null ? Health.Armor : 0f;
        public bool LowHealth => IsAlive && HealthPercent <= .3f;
        /// <summary>True when the player has not healed since the current boss spawned.</summary>
        public bool HealedSinceBossSpawn { get; private set; }

        public event System.Action PlayerDied;

        public void Initialize(Health health, float maxHealth, float startArmor, float regen)
        {
            Health = health;
            regenPerSecond = regen;
            health.Configure(maxHealth, startArmor);

            health.Damaged += OnDamaged;
            health.Healed += (_, _) => { HealedSinceBossSpawn = true; Services.Audio?.Play("player_heal", .6f); GameEvents.PlayerHealed?.Invoke(Health.Current, Health.Maximum); };
            health.Died += OnDied;
        }

        void OnDamaged(DamageInfo info, float remaining)
        {
            timeSinceDamage = 0f;
            Services.Audio?.Play("player_hurt", .7f, Random.Range(.9f, 1.15f));
            GameEvents.PlayerDamaged?.Invoke(info, remaining);

            // Knockback from explosions (impulse is applied by the caller through the controller).
            if (info.Knockback > 0f)
            {
                var locomotion = GetComponent<FirstPersonController>();
                locomotion?.AddImpulse(info.Direction * info.Knockback * .5f + Vector3.up * info.Knockback * .25f);
            }
        }

        void OnDied(DamageInfo info)
        {
            if (dead) return;
            dead = true;

            var locomotion = GetComponent<FirstPersonController>();
            if (locomotion != null) locomotion.InputEnabled = false;
            GetComponent<DashController>()?.CancelDash();
            GetComponent<WeaponController>()?.SetEnabled(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameEvents.PlayerDied?.Invoke();
            PlayerDied?.Invoke();
        }

        void Update()
        {
            if (!IsAlive) return;
            timeSinceDamage += Time.deltaTime;

            if (regenPerSecond > 0f && timeSinceDamage >= regenDelay && Health.Current < Health.Maximum)
                Health.Heal(regenPerSecond * Time.deltaTime);
        }

        public void Heal(float amount) => Health?.Heal(amount);

        public void AddArmor(float amount, float cap)
        {
            Health?.AddArmor(amount, cap);
            Services.Audio?.Play("player_shield", .6f);
        }

        /// <summary>Resets the "healed during boss" tracker when a boss spawns.</summary>
        public void NotifyBossSpawn() => HealedSinceBossSpawn = false;

        /// <summary>Debug / cheat helper.</summary>
        public void RestoreFull() => Health?.RestoreFull();
    }
}
