using UnityEngine;

namespace RogueArena.Core
{
    /// <summary>Named physics layers and the raycast masks derived from them.</summary>
    /// <remarks>
    /// Layer indices must match <c>ProjectSettings/TagManager.asset</c>.
    /// Layers are used for query filtering only; the physics collision matrix stays default.
    /// </remarks>
    public static class Layers
    {
        public const int Environment = 6;
        public const int Enemies = 7;
        public const int PlayerBody = 8;
        public const int PlayerProjectiles = 9;
        public const int EnemyProjectiles = 10;
        public const int Pickups = 11;
        public const int Hazards = 12;
        public const int Debris = 13;

        public static readonly int EnvironmentMask = 1 << Environment;
        public static readonly int EnemyMask = 1 << Enemies;
        public static readonly int PlayerMask = 1 << PlayerBody;
        public static readonly int PickupMask = 1 << Pickups;
        public static readonly int HazardMask = 1 << Hazards;

        /// <summary>Everything a player bullet can hit: world, enemies, hazards (barrels).</summary>
        public static readonly int BulletHitMask = EnvironmentMask | EnemyMask | HazardMask | PickupMask;

        /// <summary>Line-of-sight occluders: the static world plus destructibles.</summary>
        public static readonly int OccluderMask = EnvironmentMask | HazardMask;

        /// <summary>What enemy vision checks look for: the player or the world blocking it.</summary>
        public static readonly int VisionMask = EnvironmentMask | HazardMask | PlayerMask;

        /// <summary>Everything that can block or catch an enemy projectile.</summary>
        public static readonly int EnemyProjectileHitMask = EnvironmentMask | PlayerMask | HazardMask;

        public static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            var children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++) children[i].gameObject.layer = layer;
        }
    }
}
