using UnityEngine;
using RogueArena.AI;
using RogueArena.Pickups;
using RogueArena.Player;
using RogueArena.Waves;
using RogueArena.Weapons;

namespace RogueArena.Core
{
    /// <summary>
    /// Scene-local handle to everything in the currently running arena: the player
    /// assembly, the wave manager, spawner and pickups. Created by the scene
    /// bootstrap, cleared when the arena unloads. All members may be null -
    /// callers use null-conditional access.
    /// </summary>
    public sealed class ArenaSession
    {
        public string ArenaId;
        public string ModeId;
        public ArenaDefinition Arena;

        // Player assembly.
        public Transform Player;
        public Camera MainCamera;
        public CameraRig Rig;
        public WeaponController Weapons;
        public PlayerVitals Vitals;
        public FirstPersonController Locomotion;

        // Arena systems.
        public AgentSpawner Spawner;
        public WaveManager Waves;
        public PickupManager Pickups;

        /// <summary>Shakes the player camera if one exists.</summary>
        public void ShakeCamera(float amount) => Rig?.AddShake(amount);

        /// <summary>True while the run is live and the player assembly exists.</summary>
        public bool Valid => Player != null && Waves != null;
    }
}
