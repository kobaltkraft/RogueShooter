using System;
using UnityEngine;
using RogueArena.Combat;

namespace RogueArena.Core
{
    /// <summary>Central gameplay event hub.</summary>
    /// <remarks>
    /// All listeners MUST unsubscribe when they are disabled or destroyed.
    /// <see cref="SystemRoot"/> clears every subscription when a scene transition
    /// begins, which guarantees no listener can ever survive into a reloaded scene
    /// even if a system forgets to unsubscribe.
    /// </remarks>
    public static class GameEvents
    {
        // --- Player combat ---
        public static Action<DamageInfo, float> PlayerDamaged;   // damage info, health remaining after
        public static Action<float, float> PlayerHealed;         // amount, health after
        public static Action PlayerDied;

        // --- Enemy combat ---
        public static Action<KillInfo> EnemyKilled;

        // --- Waves ---
        public static Action<int, float> WavePrepared;           // wave number, preparation seconds
        public static Action<int, int> WaveStarted;              // wave number, total enemies this wave
        public static Action<int, int> WaveChanged;              // wave number, enemies remaining (kept from prototype)
        public static Action<int> WaveCleared;                   // wave number just finished
        public static Action<string, string> WaveModifierApplied;// modifier title, description

        // --- Session ---
        public static Action Victory;
        public static Action Defeat;

        // --- Score / combo ---
        public static Action<long> ScoreChanged;
        public static Action<int, float> ComboChanged;           // multiplier, seconds remaining
        public static Action<string, Color, float> Notification; // message, color, duration

        // --- Powerups / pickups ---
        public static Action<string, string, float, Color> PowerupStarted; // id, label, duration, color
        public static Action<string> PowerupEnded;
        public static Action<string, string> WeaponPickedUp;     // weapon id, display name

        // --- Boss ---
        public static Action<string, float> BossSpawned;         // boss name, max health
        public static Action<float, float> BossHealthChanged;    // current, max
        public static Action<int, string> BossPhaseChanged;      // phase index (1-based), phase name
        public static Action<string, bool> BossDefeated;         // boss name, was final boss of the run

        // --- Progression ---
        public static Action<int, long> XpGained;                // amount, total xp
        public static Action<int> LevelUp;                       // new level
        public static Action<long> CurrencyChanged;              // total currency
        public static Action<string, string> ChallengeCompleted; // id, title

        // --- State ---
        public static Action<GameState, GameState> GameStateChanged; // previous, next

        // --- AI perception ---
        public static Action<Vector3, float> NoiseEmitted;       // world position, hearing radius

        /// <summary>Clears every subscription. Called by SystemRoot on scene transitions.</summary>
        public static void ResetAll()
        {
            PlayerDamaged = null;
            PlayerHealed = null;
            PlayerDied = null;
            EnemyKilled = null;
            WavePrepared = null;
            WaveStarted = null;
            WaveChanged = null;
            WaveCleared = null;
            WaveModifierApplied = null;
            Victory = null;
            Defeat = null;
            ScoreChanged = null;
            ComboChanged = null;
            Notification = null;
            PowerupStarted = null;
            PowerupEnded = null;
            WeaponPickedUp = null;
            BossSpawned = null;
            BossHealthChanged = null;
            BossPhaseChanged = null;
            BossDefeated = null;
            XpGained = null;
            LevelUp = null;
            CurrencyChanged = null;
            ChallengeCompleted = null;
            GameStateChanged = null;
            NoiseEmitted = null;
        }
    }
}
