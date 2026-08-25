using UnityEngine;
using RogueArena.Visual;

namespace RogueArena.Environment
{
    /// <summary>
    /// Procedural environment kit: every prop, wall and platform in the arenas is
    /// assembled from Unity primitives configured here. Keeping geometry creation
    /// in one place gives the arenas a consistent, reusable visual language.
    /// </summary>
    public static class PrimitiveBuilder
    {
        /// <summary>Creates a box and marks it static (batched at the end of arena build).</summary>
        public static GameObject Box(string name, Vector3 position, Vector3 size, Material material,
            Quaternion? rotation = null, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            Assign(go, material, isStatic);
            return go;
        }

        /// <summary>Box overload taking a yaw rotation in degrees.</summary>
        public static GameObject Box(string name, Vector3 position, Vector3 size, Material material,
            float rotationY, bool isStatic = true)
            => Box(name, position, size, material, Quaternion.Euler(0f, rotationY, 0f), isStatic);

        public static GameObject Cylinder(string name, Vector3 position, float radius, float height,
            Material material, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = position;
            // Unity cylinder primitive: height 2, radius 0.5 at scale 1.
            go.transform.localScale = new Vector3(radius * 2f, height * .5f, radius * 2f);
            Assign(go, material, isStatic);
            return go;
        }

        public static GameObject Sphere(string name, Vector3 position, float radius, Material material, bool isStatic = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);
            Assign(go, material, isStatic);
            return go;
        }

        /// <summary>A thin floor quad (used for telegraph decals). Faces up.</summary>
        public static GameObject FloorQuad(string name, Vector3 position, float size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * size;
            Assign(go, material, isStatic: false);
            return go;
        }

        static void Assign(GameObject go, Material material, bool isStatic)
        {
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (isStatic) go.isStatic = true;
        }

        // ---------------------------------------------------------------- composite props

        /// <summary>Stairs from boxes. Rises toward +direction over totalRise metres.</summary>
        public static GameObject Stairs(string name, Vector3 basePosition, Vector3 direction,
            int steps, float stepRise, float stepRun, float width, Material material)
        {
            var root = new GameObject(name);
            root.transform.position = basePosition;
            Vector3 dir = direction.normalized;
            Vector3 right = new Vector3(-dir.z, 0f, dir.x);
            for (int i = 0; i < steps; i++)
            {
                float height = stepRise * (i + 1);
                var step = Box("Step", basePosition + dir * (stepRun * (i + .5f)) + Vector3.up * (height * .5f - stepRise),
                    new Vector3(
                        Mathf.Abs(right.x) * width + Mathf.Abs(dir.x) * stepRun,
                        height,
                        Mathf.Abs(right.z) * width + Mathf.Abs(dir.z) * stepRun),
                    material);
                step.transform.SetParent(root.transform, true);
            }
            return root;
        }

        /// <summary>Convenience stairs: derives step count from the total rise.</summary>
        public static GameObject Stairs(string name, Vector3 basePosition, Vector3 direction,
            float totalRise, float width, Material material = null)
        {
            int steps = Mathf.Max(3, Mathf.CeilToInt(totalRise / .34f));
            float rise = totalRise / steps;
            return Stairs(name, basePosition, direction, steps, rise, .48f, width, material);
        }

        /// <summary>A catwalk: walkable surface with side railings.</summary>
        public static GameObject Catwalk(string name, Vector3 start, Vector3 end, float width, Material deck, Material railMaterial)
        {
            var root = new GameObject(name);
            Vector3 delta = end - start;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float length = flat.magnitude;
            var rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            var deckGo = Box("Deck", start + delta * .5f,
                new Vector3(width, .22f, length), deck, rotation);
            deckGo.transform.SetParent(root.transform, true);

            var railL = Box("Rail L", start + delta * .5f + rotation * new Vector3(-width * .5f + .06f, .95f, 0f),
                new Vector3(.1f, .08f, length), railMaterial, rotation);
            railL.transform.SetParent(root.transform, true);
            var railR = Box("Rail R", start + delta * .5f + rotation * new Vector3(width * .5f - .06f, .95f, 0f),
                new Vector3(.1f, .08f, length), railMaterial, rotation);
            railR.transform.SetParent(root.transform, true);

            for (int i = 0; i <= Mathf.RoundToInt(length / 3f); i++)
            {
                float t = i / (float)Mathf.Max(1, Mathf.RoundToInt(length / 3f));
                var postL = Box("Post L", Vector3.Lerp(start, end, t) + rotation * new Vector3(-width * .5f + .06f, .5f, 0f),
                    new Vector3(.09f, 1f, .09f), railMaterial, rotation);
                postL.transform.SetParent(root.transform, true);
                var postR = Box("Post R", Vector3.Lerp(start, end, t) + rotation * new Vector3(width * .5f - .06f, .5f, 0f),
                    new Vector3(.09f, 1f, .09f), railMaterial, rotation);
                postR.transform.SetParent(root.transform, true);
            }
            return root;
        }

        /// <summary>Vertical industrial pipe with optional mounts.</summary>
        public static GameObject Pipe(string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            var go = Cylinder(name, Vector3.Lerp(start, end, .5f), radius, Vector3.Distance(start, end), material);
            go.transform.up = (end - start).normalized;
            return go;
        }

        /// <summary>An emissive light strip that reads as part of the arena lighting.</summary>
        public static GameObject LightStrip(string name, Vector3 position, Vector3 size, Material glowMaterial,
            Quaternion? rotation = null, bool isStatic = true)
        {
            return Box(name, position, size, glowMaterial, rotation, isStatic);
        }

        /// <summary>Stack of two crates, slightly rotated for visual variety.</summary>
        public static GameObject CrateStack(string name, Vector3 basePosition, float scale = 1f, int count = 2)
        {
            var root = new GameObject(name);
            root.transform.position = basePosition;
            for (int i = 0; i < count; i++)
            {
                float size = scale * (i % 2 == 0 ? 1f : .8f);
                var crate = Box("Crate", basePosition + Vector3.up * (scale * .5f + i * scale * .95f),
                    Vector3.one * size, MaterialLibrary.Crate,
                    Quaternion.Euler(0f, 17f * i + 6f, 0f));
                crate.transform.SetParent(root.transform, true);
            }
            return root;
        }

        /// <summary>Support pillar.</summary>
        public static GameObject Pillar(string name, Vector3 basePosition, float height, float radius, Material material)
        {
            return Cylinder(name, basePosition + Vector3.up * (height * .5f), radius, height, material);
        }
    }
}
