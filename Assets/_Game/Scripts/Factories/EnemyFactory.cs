using UnityEngine;
using UnityEngine.AI;
using RogueArena.AI;
using RogueArena.Combat;
using RogueArena.Core;

namespace RogueArena.Factories
{
    public static class EnemyFactory
    {
        public static EnemyBrain Create(Vector3 position, Transform player)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Arena Drone";
            root.transform.position = position + Vector3.up;
            root.GetComponent<Renderer>().material = RuntimeMaterials.Enemy;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = .45f;
            agent.height = 2;
            Health health = root.AddComponent<Health>();
            health.Configure(70);
            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            CreateHead(root.transform);
            brain.Initialize(player);
            return brain;
        }

        static void CreateHead(Transform parent)
        {
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(parent);
            head.transform.localPosition = new Vector3(0, .65f, 0);
            head.transform.localScale = Vector3.one * .55f;
            head.GetComponent<Renderer>().material = RuntimeMaterials.Accent;
        }
    }
}
