using UnityEngine;

namespace RogueArena.Combat
{
    public enum HitZone { Body, Head }

    /// <summary>
    /// Marks a collider as a headshot zone. Falls back to collider name "Head"
    /// for compatibility with the original prototype enemy.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        public HitZone zone = HitZone.Head;

        public static bool IsHead(Collider collider)
        {
            if (collider == null) return false;
            var marker = collider.GetComponent<HitZone>();
            if (marker != null) return marker.zone == HitZone.Head;
            return collider.name == "Head";
        }
    }
}
