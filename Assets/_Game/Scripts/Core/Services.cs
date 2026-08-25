using UnityEngine;
using RogueArena.Audio;
using RogueArena.Persistence;
using RogueArena.Progression;
using RogueArena.VFX;

namespace RogueArena.Core
{
    /// <summary>
    /// Minimal service locator for the persistent systems hosted on SystemRoot.
    /// Services are created once per session in a deterministic order and cleared
    /// when the application quits; scene-local systems (arena, player, HUD) are NOT
    /// registered here - they are wired directly by the scene builders.
    /// </summary>
    public static class Services
    {
        public static SceneDirector Scenes { get; internal set; }
        public static GameFlow Flow { get; internal set; }
        public static AudioDirector Audio { get; internal set; }
        public static SaveService Save { get; internal set; }
        public static SettingsService Settings { get; internal set; }
        public static ProgressionService Progression { get; internal set; }
        public static ChallengeService Challenges { get; internal set; }
        public static ScoreService Score { get; internal set; }
        public static TimeController Time { get; internal set; }
        public static RngService Rng { get; internal set; }
        public static EffectDirector Effects { get; internal set; }

        /// <summary>True when the persistent service root is alive and initialised.</summary>
        public static bool Ready { get; internal set; }

        /// <summary>Scene-local gameplay session for the currently loaded arena (null in menus).</summary>
        public static ArenaSession Session { get; internal set; }

        internal static void Clear()
        {
            Scenes = null; Flow = null; Audio = null; Save = null; Settings = null;
            Progression = null; Challenges = null; Score = null; Time = null;
            Rng = null; Effects = null; Session = null; Ready = false;
        }
    }
}
