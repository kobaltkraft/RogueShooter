using UnityEngine;
using UnityEngine.AI;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Visual;

namespace RogueArena.AI
{
    /// <summary>Builds and spawns boss GameObjects from boss definitions.</summary>
    public static class BossFactory
    {
        public static BossBrain Spawn(BossDefinition definition, Vector3 position, Transform player, float statScale)
        {
            var root = new GameObject("Boss_" + definition.id);

            // Snap onto walkable ground so the boss can never spawn wedged in a wall.
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                position = hit.position;
            root.transform.position = position + Vector3.up * .2f;
            Layers.SetLayerRecursive(root, Layers.Enemies);

            BuildBody(definition, root.transform);

            Health health = root.AddComponent<Health>();
            health.Configure(definition.maxHealth * statScale);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = definition.agentRadius;
            agent.height = definition.agentHeight;
            agent.speed = definition.moveSpeed;

            var visual = root.AddComponent<EnemyVisual>();
            BossBrain brain = root.AddComponent<BossBrain>();

            brain.Initialize(definition, player, statScale);
            visual.CollectRenderers();
            visual.AttachHealth(health);

            return brain;
        }

        static void BuildBody(BossDefinition definition, Transform parent)
        {
            // Chassis: broad armoured torso on heavy legs with shoulder cannons
            // and a glowing core weak point (crits on core hits).
            Part(parent, "Chassis", PrimitiveType.Cube, new Vector3(0f, 2.6f, 0f), new Vector3(2.6f, 2.2f, 2f), MaterialLibrary.Boss);
            Part(parent, "HullPlate", PrimitiveType.Cube, new Vector3(0f, 2.6f, 1.05f), new Vector3(2.2f, 1.4f, .25f), MaterialLibrary.MetalDark);
            Part(parent, "LegL", PrimitiveType.Cube, new Vector3(1.1f, .9f, 0f), new Vector3(.7f, 1.8f, 1.2f), MaterialLibrary.GunDark);
            Part(parent, "LegR", PrimitiveType.Cube, new Vector3(-1.1f, .9f, 0f), new Vector3(.7f, 1.8f, 1.2f), MaterialLibrary.GunDark);
            Part(parent, "FootL", PrimitiveType.Cube, new Vector3(1.1f, .2f, .3f), new Vector3(.9f, .5f, 1.7f), MaterialLibrary.MetalDark);
            Part(parent, "FootR", PrimitiveType.Cube, new Vector3(-1.1f, .2f, .3f), new Vector3(.9f, .5f, 1.7f), MaterialLibrary.MetalDark);
            Part(parent, "ShoulderL", PrimitiveType.Cube, new Vector3(1.7f, 3.4f, 0f), new Vector3(.9f, .8f, 1.1f), MaterialLibrary.Boss);
            Part(parent, "ShoulderR", PrimitiveType.Cube, new Vector3(-1.7f, 3.4f, 0f), new Vector3(.9f, .8f, 1.1f), MaterialLibrary.Boss);
            Part(parent, "CannonL", PrimitiveType.Cube, new Vector3(1.7f, 3.1f, .9f), new Vector3(.45f, .45f, 1.6f), MaterialLibrary.GunDark);
            Part(parent, "CannonR", PrimitiveType.Cube, new Vector3(-1.7f, 3.1f, .9f), new Vector3(.45f, .45f, 1.6f), MaterialLibrary.GunDark);
            Part(parent, "Visor", PrimitiveType.Cube, new Vector3(0f, 3.6f, .9f), new Vector3(1.4f, .3f, .2f), MaterialLibrary.GlowOrange);

            // Glowing core weak point - headshot (critical) zone.
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(parent, false);
            core.transform.localPosition = new Vector3(0f, 2.6f, 1.15f);
            core.transform.localScale = Vector3.one * .8f;
            core.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GlowRed;
            var hitZone = core.AddComponent<HitZone>();
            hitZone.zone = HitZone.Head;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
