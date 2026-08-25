using UnityEngine;
using RogueArena.Core;
using RogueArena.Player;

namespace RogueArena.Waves
{
    /// <summary>
    /// A temporary per-wave gameplay modifier (procedural variation). Applied when
    /// a wave starts and removed when it ends. Balanced: only one active at a
    /// time, with exclusive rules enforced by the wave manager.
    /// </summary>
    public enum WaveModifierKind
    {
        None,
        DoubleSpeed,   // enemies move 75% faster
        Hardened,      // enemies have 50% more health
        LowGravity,    // player gravity reduced - floaty arcade fun
        RapidFire,     // player fire rate +50% (a gift)
        LimitedAmmo,   // no ammo drops this wave
        EliteInvasion, // every enemy this wave is elite
    }

    public class WaveModifier
    {
        public readonly WaveModifierKind Kind;
        public readonly string Title;
        public readonly string Description;
        public readonly Color Color;

        FirstPersonController locomotion;
        WeaponController weapons;
        Pickups.PickupManager pickups;

        WaveModifier(WaveModifierKind kind, string title, string description, Color color)
        {
            Kind = kind;
            Title = title;
            Description = description;
            Color = color;
        }

        public static WaveModifier Create(WaveModifierKind kind) => kind switch
        {
            WaveModifierKind.DoubleSpeed => new WaveModifier(kind, "SWIFT STRIKE", "Enemies move 75% faster this wave!", new Color(.3f, .85f, 1f)),
            WaveModifierKind.Hardened => new WaveModifier(kind, "HARDENED", "Enemies have 50% more health this wave!", new Color(.7f, .7f, .75f)),
            WaveModifierKind.LowGravity => new WaveModifier(kind, "LOW GRAVITY", "Gravity is failing - jump far, jump high!", new Color(.8f, .6f, 1f)),
            WaveModifierKind.RapidFire => new WaveModifier(kind, "OVERCHARGE", "Your weapons fire 50% faster this wave!", new Color(1f, .85f, .25f)),
            WaveModifierKind.LimitedAmmo => new WaveModifier(kind, "SCARCITY", "No ammo drops this wave. Make it count.", new Color(1f, .4f, .3f)),
            WaveModifierKind.EliteInvasion => new WaveModifier(kind, "ELITE INVASION", "Every enemy this wave is ELITE!", new Color(.9f, .3f, 1f)),
            _ => null,
        };

        public void Apply(FirstPersonController playerLocomotion, WeaponController playerWeapons, Pickups.PickupManager pickupManager)
        {
            locomotion = playerLocomotion;
            weapons = playerWeapons;
            pickups = pickupManager;

            switch (Kind)
            {
                case WaveModifierKind.LowGravity:
                    locomotion?.SetGravityScale(.42f);
                    break;
                case WaveModifierKind.RapidFire:
                    weapons?.SetWaveFireRateMultiplier(1.5f);
                    break;
                case WaveModifierKind.LimitedAmmo:
                    pickups?.SetAmmoDropsEnabled(false);
                    break;
            }
        }

        public void Remove()
        {
            switch (Kind)
            {
                case WaveModifierKind.LowGravity:
                    locomotion?.SetGravityScale(1f);
                    break;
                case WaveModifierKind.RapidFire:
                    weapons?.SetWaveFireRateMultiplier(1f);
                    break;
                case WaveModifierKind.LimitedAmmo:
                    pickups?.SetAmmoDropsEnabled(true);
                    break;
            }
        }
    }
}
