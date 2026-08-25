using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;
using RogueArena.Pickups;

namespace RogueArena.Player
{
    /// <summary>
    /// Manages active timed powerups and exposes their combined multipliers.
    /// Weapons and movement read the multipliers every frame, so stacking and
    /// expiry are automatically respected everywhere.
    /// </summary>
    public class PlayerPowerups : MonoBehaviour
    {
        /// <summary>One active timed powerup (HUD reads this).</summary>
        public struct Active
        {
            public PowerupDefinition Definition;
            public float Remaining;
        }

        readonly List<Active> active = new List<Active>();

        public float DamageMultiplier { get; private set; } = 1f;
        public float FireRateMultiplier { get; private set; } = 1f;
        public float SpeedMultiplier { get; private set; } = 1f;
        public bool InfiniteAmmo { get; private set; }

        public event Action EffectsChanged;
        public int ActiveCount => active.Count;

        /// <summary>Read-only view for the HUD.</summary>
        public IReadOnlyList<Active> Active => active;

        public void Apply(PowerupDefinition definition)
        {
            if (definition == null) return;

            if (definition.kind == PowerupKind.Shield)
            {
                GetComponent<PlayerVitals>()?.AddArmor(definition.magnitude, 100f);
                GameEvents.PowerupStarted?.Invoke(definition.id, definition.displayName, 0f, definition.color);
                return;
            }

            // Refresh duration if already active.
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Definition.id == definition.id)
                {
                    active[i] = new Active { Definition = definition, Remaining = definition.duration };
                    GameEvents.PowerupStarted?.Invoke(definition.id, definition.displayName, definition.duration, definition.color);
                    return;
                }
            }

            active.Add(new Active { Definition = definition, Remaining = definition.duration });
            Recompute();
            GameEvents.PowerupStarted?.Invoke(definition.id, definition.displayName, definition.duration, definition.color);
        }

        void Update()
        {
            if (active.Count == 0) return;

            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Active a = active[i];
                a.Remaining -= Time.deltaTime;
                if (a.Remaining <= 0f)
                {
                    active.RemoveAt(i);
                    GameEvents.PowerupEnded?.Invoke(a.Definition.id);
                    changed = true;
                }
                else active[i] = a;
            }
            if (changed) Recompute();
        }

        void Recompute()
        {
            float damage = 1f, fireRate = 1f, speed = 1f;
            bool infinite = false;
            foreach (Active a in active)
            {
                switch (a.Definition.kind)
                {
                    case PowerupKind.DamageBoost: damage *= a.Definition.magnitude; break;
                    case PowerupKind.RapidFire: fireRate *= a.Definition.magnitude; break;
                    case PowerupKind.SpeedBoost: speed *= a.Definition.magnitude; break;
                    case PowerupKind.InfiniteAmmo: infinite = true; break;
                }
            }
            DamageMultiplier = damage;
            FireRateMultiplier = fireRate;
            SpeedMultiplier = speed;
            InfiniteAmmo = infinite;
            EffectsChanged?.Invoke();
        }

        public float RemainingFor(string id)
        {
            foreach (Active a in active)
                if (a.Definition.id == id) return a.Remaining;
            return 0f;
        }

        public void ClearAll()
        {
            for (int i = 0; i < active.Count; i++)
                GameEvents.PowerupEnded?.Invoke(active[i].Definition.id);
            active.Clear();
            Recompute();
        }
    }
}
