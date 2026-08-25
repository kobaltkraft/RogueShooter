using UnityEngine;
using RogueArena.Weapons;

namespace RogueArena.Player
{
    /// <summary>
    /// Lightweight marker on the player root giving pickups and hazards a safe,
    /// cached handle to the gameplay systems without inspector wiring.
    /// </summary>
    public sealed class PlayerAssemblyMarker : MonoBehaviour
    {
        public WeaponController Weapons;
        public PlayerVitals Vitals;
        public PlayerPowerups Powerups;
        public FirstPersonController Locomotion;
    }
}
