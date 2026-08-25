using System.Collections.Generic;
using UnityEngine;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Weapons;

namespace RogueArena.Pickups
{
    /// <summary>
    /// Owns the arena pickup economy: weapon pads at authored points, a capped
    /// rotation of powerups, post-wave bonus health, and one-shot death drops
    /// (currency / ammo). All drops are single-use and self-destruct so long
    /// Endless runs cannot accumulate hidden objects.
    /// </summary>
    public class PickupManager : MonoBehaviour
    {
        const int MaxLivePowerups = 6;

        readonly List<WeaponDefinition> weaponPool = new List<WeaponDefinition>(8);
        readonly List<Pickup> livePowerups = new List<Pickup>(MaxLivePowerups);

        RngService rng;
        bool ammoDropsEnabled = true;
        float powerupTimer;
        const float PowerupInterval = 22f;

        // Powerup points rotate through these, seeded for balance.
        List<Transform> powerupPoints;

        public void Initialize(RngService random, List<Transform> weaponPoints, List<Transform> powerupPoints,
            List<WeaponDefinition> arenaWeaponPool)
        {
            rng = random;
            this.powerupPoints = powerupPoints ?? new List<Transform>();

            weaponPool.Clear();
            if (arenaWeaponPool != null) weaponPool.AddRange(arenaWeaponPool);

            // Weapon pickups at authored points (respawning pads).
            if (weaponPoints != null)
            {
                for (int i = 0; i < weaponPoints.Count && weaponPool.Count > 0; i++)
                {
                    WeaponDefinition weapon = rng.Pick(weaponPool);
                    PickupFactory.CreateWeapon(weapon, weaponPoints[i].position);
                }
            }

            // First powerup appears after a short delay.
            powerupTimer = 14f;

            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDestroy()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
        }

        void OnEnemyKilled(KillInfo kill)
        {
            if (!kill.KilledByPlayer) return;
            NotifyEnemyDied(null, kill.Position, Services.Rng ?? rng);
        }

        void Update()
        {
            powerupTimer -= Time.deltaTime;
            if (powerupTimer <= 0f)
            {
                powerupTimer = PowerupInterval;
                SpawnRandomPowerup();
            }
        }

        void SpawnRandomPowerup()
        {
            if (powerupPoints == null || powerupPoints.Count == 0) return;
            if (Content.GameContent.Powerups.Count == 0) return;

            // Cap live powerups: retire the oldest before spawning another so
            // long runs cannot accumulate idle pickups.
            PrunePowerups();
            if (livePowerups.Count >= MaxLivePowerups)
            {
                Pickup oldest = livePowerups[0];
                livePowerups.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            // Avoid stacking the same powerup twice on the field.
            PowerupDefinition def = rng.Pick(Content.GameContent.Powerups,
                p => p.kind != PowerupKind.Shield || rng.Chance(.3f));
            def ??= Content.GameContent.Powerups[0];

            Transform point = rng.Pick(powerupPoints);
            if (point == null) return;
            livePowerups.Add(PickupFactory.CreatePowerup(def, point.position));
        }

        void PrunePowerups()
        {
            for (int i = livePowerups.Count - 1; i >= 0; i--)
                if (livePowerups[i] == null) livePowerups.RemoveAt(i);
        }

        /// <summary>Called by the wave manager when a wave clears (bonus drops).</summary>
        public void NotifyWaveCleared()
        {
            // Small chance of a bonus health pickup after a wave (single use).
            if (rng != null && rng.Chance(.35f) && powerupPoints != null && powerupPoints.Count > 0)
            {
                Transform point = rng.Pick(powerupPoints);
                if (point != null)
                {
                    var health = PickupFactory.CreateHealth(point.position, 30f);
                    health.OneShot = true;
                }
            }
        }

        /// <summary>Spawns one-shot death drops (currency / ammo) at a kill position.</summary>
        public void NotifyEnemyDied(AI.EnemyDefinition definition, Vector3 position, RngService random)
        {
            if (random == null) return;

            float currencyChance = definition != null ? definition.currencyDropChance : .08f;
            int scoreValue = definition != null ? definition.scoreValue : 100;

            if (currencyChance > 0f && random.Chance(currencyChance))
            {
                int amount = Mathf.RoundToInt(10 + scoreValue * .04f);
                PickupFactory.CreateCurrency(position, amount); // one-shot by design
            }

            // Ammo drought support: enemies normally sometimes drop ammo.
            if (ammoDropsEnabled && random.Chance(.12f))
            {
                var ammo = PickupFactory.CreateAmmo(position + Vector3.up * .5f);
                ammo.OneShot = true;
            }
        }

        public void SetAmmoDropsEnabled(bool value) => ammoDropsEnabled = value;
    }
}
