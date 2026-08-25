using System;
using UnityEngine;

namespace RogueArena.Pickups
{
    public enum PowerupKind { DamageBoost, RapidFire, SpeedBoost, InfiniteAmmo, Shield }

    /// <summary>
    /// A timed arcade powerup. The <see cref="Player.PlayerPowerups"/> component
    /// applies the effect; this definition only carries data, so adding a new
    /// powerup needs no new scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Powerup Definition", fileName = "Powerup_")]
    public class PowerupDefinition : ScriptableObject
    {
        public string id = "damage";
        public string displayName = "DAMAGE BOOST";
        [TextArea] public string description = "Double weapon damage.";
        public PowerupKind kind = PowerupKind.DamageBoost;
        [Min(1f)] public float duration = 12f;
        [Tooltip("Multiplier (or shield amount for Shield).")]
        public float magnitude = 2f;
        public Color color = new Color(1f, .4f, .15f);
        [Tooltip("Single glyph shown on the pickup and HUD.")]
        public string glyph = "D";
        public float respawnSeconds = 32f;
        public string pickupSfx = "pickup_powerup";
    }

    /// <summary>Instant pickups share the pickup system but apply immediately.</summary>
    public enum InstantPickupKind { Health, Armor, Ammo }
}
