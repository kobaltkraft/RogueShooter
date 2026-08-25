using System;
using UnityEngine;

namespace RogueArena.Weapons
{
    public enum FireMode { Auto, Semi, Burst, Pump }
    public enum WeaponRarity { Standard, Advanced, Experimental, Heavy }

    /// <summary>Projectile behaviour for rocket/grenade/plasma style weapons.</summary>
    [Serializable]
    public class ProjectileSettings
    {
        [Tooltip("Metres per second.")]
        public float speed = 40f;
        [Tooltip("Downward acceleration (grenade launcher arcs).")]
        public float gravity = 0f;
        [Tooltip("Collision radius for the projectile sweep.")]
        public float radius = .25f;
        [Tooltip("Explosion radius. 0 = direct-hit only.")]
        public float splashRadius = 0f;
        [Tooltip("Damage dealt by the explosion (full at centre).")]
        public float splashDamage = 0f;
        [Tooltip("Knockback applied by the explosion.")]
        public float splashKnockback = 0f;
        [Tooltip("Fraction of explosion damage the shooter takes (rocket jumping!).")]
        public float selfDamageFraction = .35f;
        public bool trail = true;
        public Color tracerColor = new Color(1f, .6f, .15f);
        public string impactSfx = "explosion";
    }

    /// <summary>
    /// Complete, designer-tunable configuration for one weapon. Instances are
    /// provided by <see cref="Content.GameContent"/> and can also be authored as
    /// assets (Create > Rogue Arena > Weapon Definition).
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Weapon Definition", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "rifle";
        public string displayName = "AR-1 Vanguard";
        [TextArea] public string description = "Reliable automatic assault rifle.";
        public WeaponRarity rarity = WeaponRarity.Standard;
        [Tooltip("Two-letter HUD glyph.")]
        public string hudGlyph = "AR";

        [Header("Firing")]
        public FireMode fireMode = FireMode.Auto;
        [Tooltip("Shots per trigger pull for Burst mode.")]
        public int burstCount = 3;
        [Tooltip("Seconds between shots inside a burst.")]
        public float burstDelay = .06f;
        [Tooltip("Shots per second.")]
        public float fireRate = 9f;
        [Tooltip("Damage per bullet/pellet.")]
        public float damage = 18f;
        public float headshotMultiplier = 2f;
        [Tooltip("Effective range in metres (hitscan only).")]
        public float range = 120f;
        [Min(1)] public int pellets = 1;

        [Header("Spread")]
        [Tooltip("Base cone half-angle in degrees.")]
        public float spread = .6f;
        public float spreadMoving = 1.2f;
        public float spreadAir = 3f;

        [Header("Recoil")]
        public float recoilPitch = .55f;
        public float recoilYaw = .18f;
        public float recoilRecovery = 9f;
        [Tooltip("Viewmodel kick on the Z axis.")]
        public float kick = .07f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 1.4f;

        [Header("Heat (energy weapons)")]
        [Tooltip("When true the weapon uses heat instead of ammo; magazine/reserve are ignored.")]
        public bool usesHeat = false;
        [Range(.01f, .5f)] public float heatPerShot = .07f;
        [Tooltip("Heat dissipated per second while not firing.")]
        public float heatCooldown = .3f;
        [Tooltip("Seconds the weapon locks out after overheating.")]
        public float overheatLockout = 1.1f;

        [Header("Projectile")]
        public bool isProjectile = false;
        public ProjectileSettings projectile = new ProjectileSettings();
        [Tooltip("Knockback applied to enemies by direct hits.")]
        public float knockback = 0f;

        [Header("Feedback")]
        public Color accentColor = new Color(.2f, .95f, 1f);
        public string shotSfx = "shot_rifle";
        [Range(0f, 1f)] public float sfxVolume = .8f;
        public float muzzleScale = 1f;
        public Color muzzleColor = new Color(1f, .7f, .25f);
        public float fovKick = .5f;
        [Range(0f, .4f)] public float shakePerShot = .05f;

        [Header("Progression")]
        public int unlockLevel = 1;
        public long unlockCost = 0;
    }
}
