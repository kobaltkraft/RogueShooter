using System;
using UnityEngine;
using RogueArena.Content;
using RogueArena.Persistence;
using RogueArena.Waves;

namespace RogueArena.Core
{
    /// <summary>
    /// Owns the game state machine and the run lifecycle: starting runs, pausing,
    /// restarting, victory/defeat handling and end-of-run bookkeeping.
    /// </summary>
    public sealed class GameFlow
    {
        public GameStateMachine State { get; } = new GameStateMachine();

        public string ModeId { get; private set; }
        public string ArenaId { get; private set; }
        public GameModeDefinition Mode { get; private set; }
        public float RunSeconds { get; private set; }
        public bool RunActive { get; private set; }

        readonly SceneDirector scenes;
        bool runEnded;

        public GameFlow(SceneDirector sceneDirector)
        {
            scenes = sceneDirector;

            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.Victory += OnVictory;
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Begins a run: loads the arena scene and hands over to the bootstrap.</summary>
        public void StartRun(string modeId, string arenaId)
        {
            Mode = GameContent.Mode(modeId);
            if (Mode == null)
            {
                Debug.LogWarning($"[Flow] Unknown mode '{modeId}', falling back to survival.");
                Mode = GameContent.Mode("survival");
                modeId = Mode != null ? Mode.id : "survival";
            }
            ModeId = modeId;
            ArenaId = arenaId;

            if (State.Current is GameState.MainMenu or GameState.Victory or GameState.Defeat
                or GameState.Restarting or GameState.Boot or GameState.Loading)
            {
                if (!State.Set(GameState.Loading, "start run"))
                    State.Force(GameState.Loading, "start run");
            }

            scenes.LoadArena(arenaId, modeId);
        }

        /// <summary>Called by the scene bootstrap once the arena and player exist.</summary>
        public void OnArenaReady(ArenaSession session)
        {
            RunSeconds = 0f;
            runEnded = false;
            RunActive = session != null && session.Valid;
            State.Force(GameState.WaveStart, "arena ready");

            Services.Score?.Reset();
            Services.Challenges?.BeginRun(ModeId);
            Services.Progression?.NotifyRunStarted();

            if (session?.Waves != null) session.Waves.BeginRun();

            GameModeDefinition mode = Mode ?? GameContent.Mode(ModeId);
            if (mode != null && !string.IsNullOrEmpty(mode.musicId))
                Services.Audio?.PlayMusic(mode.musicId);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Frame tick from SystemRoot.</summary>
        public void Tick()
        {
            Services.Score?.Tick();

            if (RunActive && State.SimulationRunning)
            {
                RunSeconds += UnityEngine.Time.unscaledDeltaTime;
                Services.Challenges?.NotifyRunTime(RunSeconds);
            }
        }

        // ---------------------------------------------------------------- endings

        void OnPlayerDied()
        {
            EndRun(GameState.Defeat, "player died");
            Services.Audio?.StopMusic(2f);
            Services.Audio?.Play("defeat", .9f);
        }

        void OnVictory()
        {
            EndRun(GameState.Victory, "run won");
            Services.Audio?.Play("victory", .9f);
        }

        void EndRun(GameState endState, string context)
        {
            if (runEnded) return;
            runEnded = true;
            RunActive = false;

            State.Set(endState, context); // rejected while paused; handled below
            if (State.Current != endState)
            {
                // Paused -> unpause into the ending.
                Services.Time?.Clear(TimeController.Pause);
                State.Set(endState, context);
            }

            Services.Score?.RecordRun(ModeId, ArenaId);
            Services.Challenges?.NotifyBestWave(ModeId, Services.Session?.Waves?.WaveNumber ?? 0);
            Services.Progression?.NotifyRunSeconds(RunSeconds);
            Services.Save?.SaveNow();

            Services.Session?.Waves?.StopRun();

            // Freeze the player and calm the field so nothing fights behind the
            // victory/defeat screens.
            var session = Services.Session;
            if (session != null)
            {
                if (session.Locomotion != null) session.Locomotion.InputEnabled = false;
                session.Weapons?.SetEnabled(false);
                session.Spawner?.PacifyAll();
            }
        }

        // ---------------------------------------------------------------- pause / restart / quit

        public bool TryPause()
        {
            if (!State.TryPause()) return false;
            Services.Time?.Set(TimeController.Pause, 0f);
            return true;
        }

        public bool TryResume()
        {
            Services.Time?.Clear(TimeController.Pause);
            return State.TryResume();
        }

        /// <summary>Tears down the current run and reloads the same arena + mode.</summary>
        public void RestartRun()
        {
            if (State.Current is GameState.Victory or GameState.Defeat or GameState.Paused)
                State.Set(GameState.Restarting, "restart");
            else if (State.IsGameplay)
                State.Set(GameState.Restarting, "restart");

            Services.Time?.ResetAll();
            StartRun(ModeId, ArenaId);
        }

        /// <summary>Tears down the run and returns to the main menu.</summary>
        public void QuitToMenu()
        {
            if (State.IsGameplay)
            {
                // Gameplay -> Restarting -> Loading -> MainMenu keeps the machine strict.
                State.Set(GameState.Restarting, "quit to menu");
            }
            else if (State.Current == GameState.Paused)
            {
                State.Set(GameState.MainMenu, "quit to menu");
            }

            Services.Time?.ResetAll();
            RunActive = false;
            runEnded = false;
            Services.Audio?.StopMusic(1.5f);
            scenes.LoadMenu();
        }
    }
}
