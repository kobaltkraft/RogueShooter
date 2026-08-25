using UnityEngine;

namespace RogueArena.Progression
{
    public enum PlayerUpgradeKind
    {
        MaxHealth,
        MoveSpeed,
        DashRecharge,
        StartArmor,
        Regen,
        WeaponDamage,
    }

    /// <summary>A permanent, tiered player upgrade purchased with currency.</summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Player Upgrade", fileName = "Upgrade_")]
    public class PlayerUpgradeDefinition : ScriptableObject
    {
        public string id = "health";
        public string displayName = "REINFORCED FRAME";
        [TextArea] public string description = "+25 max health per tier.";
        public PlayerUpgradeKind kind = PlayerUpgradeKind.MaxHealth;
        [Min(1)] public int maxTier = 3;
        public long baseCost = 150;
        public float costGrowth = 1.7f;
        public float valuePerTier = 25f;

        public long CostAtTier(int tier)
        {
            // tier 0 buys the first upgrade.
            return (long)(baseCost * Mathf.Pow(costGrowth, tier));
        }
    }

    /// <summary>Generic per-weapon upgrade tracks. Shared by every weapon.</summary>
    public static class WeaponUpgradeTracks
    {
        public const string Damage = "damage";
        public const string Magazine = "magazine";
        public const string Reload = "reload";

        public static readonly string[] All = { Damage, Magazine, Reload };

        public static string Label(string track) => track switch
        {
            Damage => "DAMAGE",
            Magazine => "MAGAZINE",
            Reload => "RELOAD",
            _ => track.ToUpperInvariant(),
        };

        public static int MaxTier(string track) => track switch
        {
            Damage => 3,
            Magazine => 2,
            Reload => 2,
            _ => 0,
        };

        public static long CostAtTier(string track, int tier) => track switch
        {
            Damage => 200 + tier * 220,
            Magazine => 180 + tier * 200,
            Reload => 160 + tier * 180,
            _ => 0,
        };

        /// <summary>Damage multiplier for the tier.</summary>
        public static float DamageMultiplier(int tier) => 1f + tier * .15f;

        /// <summary>Magazine size multiplier for the tier.</summary>
        public static float MagazineMultiplier(int tier) => 1f + tier * .5f;

        /// <summary>Reload time multiplier for the tier (lower is better).</summary>
        public static float ReloadMultiplier(int tier) => 1f - tier * .2f;
    }
}
