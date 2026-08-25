using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.AI
{
    /// <summary>
    /// A spot an enemy can crouch behind. Cover validity is checked live against
    /// the player's line of sight, and points can be reserved so enemies spread out.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        public float coverHeight = 1.2f;
        public EnemyBrain Occupant { get; set; }

        void OnEnable() => CoverRegistry.Register(this);
        void OnDisable() => CoverRegistry.Unregister(this);

        /// <summary>True when the player's eye-line to a shooter standing here is blocked.</summary>
        public bool BlocksSight(Vector3 threatEye)
        {
            Vector3 coverEye = transform.position + Vector3.up * coverHeight;
            Vector3 toCover = coverEye - threatEye;
            float distance = toCover.magnitude;
            if (distance < .5f) return false;
            return Physics.Raycast(threatEye, toCover.normalized, out _, distance - .3f,
                Layers.OccluderMask, QueryTriggerInteraction.Ignore);
        }

        public static void DrawGizmos(CoverPoint point)
        {
            Gizmos.color = point.Occupant != null ? new Color(1f, .3f, .2f, .8f) : new Color(.2f, .9f, 1f, .8f);
            Gizmos.DrawWireSphere(point.transform.position + Vector3.up * .1f, .5f);
            Gizmos.DrawLine(point.transform.position, point.transform.position + Vector3.up * point.coverHeight);
        }
    }

    /// <summary>Per-scene registry of cover points. Cleared on scene teardown.</summary>
    public static class CoverRegistry
    {
        static readonly List<CoverPoint> points = new List<CoverPoint>(64);

        public static void Register(CoverPoint point)
        {
            if (point != null && !points.Contains(point)) points.Add(point);
        }

        public static void Unregister(CoverPoint point)
        {
            points.Remove(point);
            if (point != null && point.Occupant != null) point.Occupant = null;
        }

        public static void Clear() => points.Clear();

        public static IReadOnlyList<CoverPoint> Points => points;

        /// <summary>
        /// Finds the nearest free cover point that actually blocks the threat's
        /// line of sight. Returns null when no useful cover exists.
        /// </summary>
        public static CoverPoint FindCover(Vector3 seekerPosition, Vector3 threatEye, float maxDistance)
        {
            CoverPoint best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                CoverPoint point = points[i];
                if (point == null || point.Occupant != null || !point.gameObject.activeInHierarchy) continue;

                float distance = Vector3.Distance(seekerPosition, point.transform.position);
                if (distance > maxDistance) continue;
                if (!point.BlocksSight(threatEye)) continue;

                // Prefer close cover with a shooting angle (not too close to the threat).
                float threatDistance = Vector3.Distance(point.transform.position, threatEye);
                float score = -distance + Mathf.Min(threatDistance, 20f) * .5f;
                if (score > bestScore) { bestScore = score; best = point; }
            }
            return best;
        }
    }
}
