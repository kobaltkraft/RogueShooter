namespace RogueArena.Core
{
    /// <summary>
    /// Development-time toggles. Everything defaults off and is compiled out of
    /// release behaviour via runtime checks (gizmos are editor-only anyway).
    /// </summary>
    public static class DebugSettings
    {
        /// <summary>Enemy state, path, vision and cover gizmos (editor only).</summary>
        public static bool ShowAiGizmos;

        /// <summary>On-screen debug overlay (F3 in game toggles it at runtime).</summary>
        public static bool ShowDebugOverlay;

        /// <summary>Cover point gizmos (editor only).</summary>
        public static bool ShowCoverGizmos;
    }
}
