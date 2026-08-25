using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.Waves
{
    [Serializable]
    public class WaveEntry
    {
        public EnemyKind kind = EnemyKind.Grunt;
        public int count = 3;
    }

    /// <summary>One authored wave. Composition is fully data-driven.</summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Wave Definition", fileName = "Wave_")]
    public class WaveDefinition : ScriptableObject
    {
        public int waveNumber = 1;
        [Tooltip("Optional custom announcement, e.g. 'ELITE INCURSION'.")]
        public string label;
        public List<WaveEntry> entries = new List<WaveEntry>();

        [Header("Pacing")]
        public float prepTime = 5f;
        [Tooltip("Seconds between individual spawns.")]
        public float spawnInterval = .35f;
        [Tooltip("Maximum enemies alive at once before holding the queue.")]
        public int maxAlive = 22;

        [Header("Elites")]
        [Range(0f, 1f)] public float eliteChance;
        public int eliteMinModifiers = 1;
        public int eliteMaxModifiers = 1;

        [Header("Special")]
        public bool isBossWave;
        public string bossId = "vulcan";
        [Tooltip("Boss Rush: scaling multiplier applied to the boss (wave index based).")]
        public float bossStatScale = 1f;
        public bool allowWaveModifier = true;

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                foreach (WaveEntry entry in entries) total += entry.count;
                return total;
            }
        }
    }

    public enum GameModeKind { Survival, TimeAttack, BossRush, Endless }

    /// <summary>
    /// A complete game mode. Modes are modular: the wave manager asks the mode for
    /// the next wave and how to react to kills; new modes are just new definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Game Mode", fileName = "Mode_")]
    public class GameModeDefinition : ScriptableObject
    {
        public string id = "survival";
        public string displayName = "SURVIVAL";
        [TextArea] public string description = "Endless waves. How long can you last?";
        public GameModeKind kind = GameModeKind.Survival;

        [Header("Waves")]
        [Tooltip("Authored waves played in order; afterwards endless generation takes over.")]
        public List<WaveDefinition> scriptedWaves = new List<WaveDefinition>();
        public bool endlessAfterScripted = true;
        [Tooltip("Health/damage scaling per endless wave (0.06 = +6% per wave).")]
        public float difficultyScalePerWave = .06f;
        [Tooltip("Enemy variety ceiling rises with this rate in generated waves.")]
        public float varietyRamp = 1f;

        [Header("Mode specifics")]
        [Tooltip("Time Attack: kills needed to finish the run.")]
        public int killQuota = 40;
        [Tooltip("Boss Rush: bosses fought in order.")]
        public List<string> bossOrder = new List<string> { "vulcan" };

        [Header("Presentation")]
        public string musicId = "music_combat";
        public Color uiColor = new Color(.2f, .95f, 1f);
    }
}
