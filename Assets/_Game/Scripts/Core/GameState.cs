using System;
using UnityEngine;

namespace RogueArena.Core
{
    /// <summary>Every high-level state the game can be in.</summary>
    public enum GameState
    {
        Boot,        // initialising services
        MainMenu,    // main menu & sub menus are open
        Loading,     // async scene load / loading screen
        Playing,     // gameplay running, between wave beats
        WaveStart,   // preparation / wave announcement beat
        Combat,      // active combat wave
        Upgrade,     // between-wave upgrade choice beat
        Boss,        // boss encounter active
        Paused,      // pause menu open (remembers previous state)
        Victory,     // run completed successfully
        Defeat,      // player died
        Restarting,  // tearing the run down to start another
    }

    /// <summary>
    /// Small, strict state machine. Invalid transitions are rejected with a clear
    /// warning instead of silently putting the game into an inconsistent state.
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;
        public GameState Previous { get; private set; } = GameState.Boot;
        /// <summary>Gameplay state to return to when un-pausing.</summary>
        public GameState ResumeTarget { get; private set; } = GameState.Playing;

        public event Action<GameState, GameState> Changed;

        /// <summary>True while any gameplay state is active (movement, AI, weapons run).</summary>
        public bool IsGameplay => IsGameplayState(Current);

        /// <summary>True while time should flow for gameplay simulation.</summary>
        public bool SimulationRunning => IsGameplay;

        public bool InMenu => Current == GameState.MainMenu;

        public static bool IsGameplayState(GameState s) => s is GameState.Playing
            or GameState.WaveStart or GameState.Combat or GameState.Upgrade or GameState.Boss;

        public bool CanTransition(GameState to)
        {
            if (to == Current) return false;

            if (Current == GameState.Paused)
            {
                // From pause: resume to remembered state, quit to menu, or restart.
                return to == ResumeTarget || to is GameState.MainMenu or GameState.Restarting or GameState.Loading;
            }

            return Current switch
            {
                GameState.Boot => to is GameState.MainMenu or GameState.Loading,
                GameState.MainMenu => to is GameState.Loading,
                GameState.Loading => to is GameState.MainMenu or GameState.Playing or GameState.WaveStart or GameState.Combat,
                GameState.Upgrade => to is GameState.WaveStart or GameState.Combat or GameState.Playing
                    or GameState.Paused or GameState.Restarting or GameState.Loading,
                GameState.Boss => to is GameState.Combat or GameState.Playing or GameState.WaveStart
                    or GameState.Paused or GameState.Victory or GameState.Defeat or GameState.Restarting or GameState.Loading,
                GameState.Victory or GameState.Defeat => to is GameState.MainMenu or GameState.Restarting or GameState.Loading,
                GameState.Restarting => to is GameState.Loading or GameState.MainMenu or GameState.Playing or GameState.WaveStart or GameState.Combat,
                // Playing / WaveStart / Combat share the gameplay cluster.
                GameState.Playing or GameState.WaveStart or GameState.Combat =>
                    IsGameplayState(to)
                    || to is GameState.Paused or GameState.Victory or GameState.Defeat
                        or GameState.Restarting or GameState.Loading,
                _ => false,
            };
        }

        /// <summary>Attempts a transition. Returns false (and logs a warning) when illegal.</summary>
        public bool Set(GameState to, string context = null)
        {
            if (!CanTransition(to))
            {
                Debug.LogWarning($"[GameState] Rejected transition {Current} -> {to}"
                    + (string.IsNullOrEmpty(context) ? "" : $" ({context})"));
                return false;
            }

            Previous = Current;
            Current = to;
            if (IsGameplayState(to) && Previous != GameState.Paused)
                ResumeTarget = to; // remember latest gameplay state for pause-resume
            Changed?.Invoke(Previous, Current);
            return true;
        }

        /// <summary>Forces a state without validation. Reserved for the scene loader.</summary>
        public void Force(GameState to, string context)
        {
            if (IsGameplayState(to)) ResumeTarget = to;
            Previous = Current;
            Current = to;
            Changed?.Invoke(Previous, Current);
            Debug.Log($"[GameState] Forced {Previous} -> {to} ({context})");
        }

        /// <summary>Pauses from any gameplay state. Returns false when not pausable.</summary>
        public bool TryPause()
        {
            if (!IsGameplay) return false;
            ResumeTarget = Current;
            return Set(GameState.Paused, "pause requested");
        }

        /// <summary>Resumes to the remembered gameplay state.</summary>
        public bool TryResume() => Current == GameState.Paused && Set(ResumeTarget, "resume");
    }
}
