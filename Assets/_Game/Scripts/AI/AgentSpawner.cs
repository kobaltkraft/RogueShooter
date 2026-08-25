using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RogueArena.Core;

namespace RogueArena.AI
{
    /// <summary>
    /// Scene-local spawning service: owns live enemy counts (max-concurrent caps
    /// per type, global cap for performance) and safe spawn placement. Both the
    /// wave manager and summoning elites route through here so nothing
    /// double-spawns or exceeds the performance budget.
    /// </summary>
    public class AgentSpawner : MonoBehaviour
    {
        public static AgentSpawner Instance { get; private set; }

        public const int GlobalEnemyCap = 34;

        readonly List<EnemyBrain> alive = new List<EnemyBrain>(48);
        public IReadOnlyList<EnemyBrain> AliveEnemies => alive;

        RngService rng;
        Transform player;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(RngService random, Transform playerTransform)
        {
            rng = random;
            player = playerTransform;
        }

        public int AliveCount => alive.Count;

        public int CountOfKind(EnemyKind kind)
        {
            int count = 0;
            for (int i = 0; i < alive.Count; i++)
                if (alive[i] != null && alive[i].Definition != null && alive[i].Definition.kind == kind) count++;
            return count;
        }

        public bool CanSpawn(EnemyDefinition definition)
        {
            if (definition == null) return false;
            if (alive.Count >= GlobalEnemyCap) return false;
            if (definition.maxConcurrent > 0 && CountOfKind(definition.kind) >= definition.maxConcurrent) return false;
            return true;
        }

        /// <summary>Spawns an enemy of a kind at a position. Returns null if capped or invalid.</summary>
        public EnemyBrain Spawn(EnemyKind kind, Vector3 position, bool elite)
        {
            EnemyDefinition definition = Content.GameContent.EnemyOf(kind);
            if (definition == null)
            {
                Debug.LogWarning($"[Spawner] No definition for enemy kind '{kind}'.");
                return null;
            }

            if (!CanSpawn(definition)) return null;

            position = FindSafePosition(position, definition);
            EnemyBrain brain = EnemyFactory.Create(definition, position, player, rng, 1f, elite ? 1 : 0, 1f);
            if (brain == null) return null;

            alive.Add(brain);
            brain.OnDestroyed += () => alive.Remove(brain);
            return brain;
        }

        /// <summary>Spawns with difficulty scaling (waves).</summary>
        public EnemyBrain SpawnScaled(EnemyDefinition definition, Vector3 position, float difficultyScale,
            int eliteModifierCount, float speedScale = 1f)
        {
            if (definition == null) return null;
            if (!CanSpawn(definition)) return null;

            position = FindSafePosition(position, definition);
            EnemyBrain brain = EnemyFactory.Create(definition, position, player, rng, difficultyScale, eliteModifierCount, speedScale);
            if (brain == null) return null;

            alive.Add(brain);
            brain.OnDestroyed += () => alive.Remove(brain);
            return brain;
        }

        /// <summary>Nudges spawn positions onto walkable ground away from the player.</summary>
        Vector3 FindSafePosition(Vector3 position, EnemyDefinition definition)
        {
            if (definition.flying) return position + Vector3.up * definition.flyHeight;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                position = hit.position;

            // Never spawn directly on top of the player.
            if (player != null)
            {
                float distance = Vector3.Distance(position, player.position);
                if (distance < 3f)
                {
                    Vector3 away = (position - player.position);
                    away.y = 0f;
                    if (away.sqrMagnitude < .1f) away = Random.insideUnitSphere;
                    position += away.normalized * (4f - distance);
                    if (NavMesh.SamplePosition(position, out hit, 5f, NavMesh.AllAreas))
                        position = hit.position;
                }
            }
            return position;
        }

        /// <summary>Kills and clears everything (scene teardown, restart safety).</summary>
        public void ClearAll()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                EnemyBrain brain = alive[i];
                if (brain != null) Destroy(brain.gameObject);
            }
            alive.Clear();
        }

        /// <summary>Idles every living enemy without destroying them (run ended).</summary>
        public void PacifyAll()
        {
            for (int i = 0; i < alive.Count; i++)
                alive[i]?.Pacify();
        }
    }
}
