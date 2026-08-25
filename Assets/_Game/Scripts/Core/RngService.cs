using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueArena.Core
{
    /// <summary>
    /// Deterministic random number service. All procedural content (wave compositions,
    /// elite modifiers, pickups) draws from a seeded stream so runs can be reproduced
    /// and balanced - no hidden global <see cref="UnityEngine.Random"/> state.
    /// </summary>
    public sealed class RngService
    {
        System.Random rng;

        public int Seed { get; private set; }

        public RngService(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        /// <summary>Re-seed for a new run.</summary>
        public void Reseed(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        public int Next() => rng.Next();
        public int Next(int exclusiveMax) => rng.Next(exclusiveMax);
        public int Range(int inclusiveMin, int exclusiveMax) => rng.Next(inclusiveMin, exclusiveMax);
        public float Value => (float)rng.NextDouble();
        public float Range(float inclusiveMin, float inclusiveMax) => inclusiveMin + (float)rng.NextDouble() * (inclusiveMax - inclusiveMin);

        /// <summary>True with the given 0-1 probability.</summary>
        public bool Chance(float probability01) => probability01 >= 1f || (probability01 > 0f && rng.NextDouble() < probability01);

        public Vector2 InsideUnitCircle()
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        public Vector3 InsideUnitSphere()
        {
            Vector2 c = InsideUnitCircle();
            float y = Range(-1f, 1f);
            float rem = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(c.x * rem, y, c.y * rem);
        }

        /// <summary>Picks a random element. Returns default for empty lists.</summary>
        public T Pick<T>(IReadOnlyList<T> list) => list.Count == 0 ? default : list[rng.Next(list.Count)];

        /// <summary>Picks a random element satisfying a predicate. Returns default when none match.</summary>
        public T Pick<T>(IReadOnlyList<T> list, Func<T, bool> predicate)
        {
            int count = list.Count;
            int start = rng.Next(count);
            for (int i = 0; i < count; i++)
            {
                int index = (start + i) % count;
                if (predicate(list[index])) return list[index];
            }
            return default;
        }

        /// <summary>Fisher-Yates shuffle into a reusable list (avoids allocations).</summary>
        public void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }

    public static class RngExtensions
    {
        /// <summary>Convenience access when a local rng is unavailable. Not reproducible.</summary>
        public static float NextFloat(this System.Random r) => (float)r.NextDouble();
    }
}
