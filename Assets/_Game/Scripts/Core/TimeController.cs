using System.Collections.Generic;
using UnityEngine;

namespace RogueArena.Core
{
    /// <summary>
    /// Single owner of <see cref="Time.timeScale"/>. Systems request a scale for a
    /// named reason (pause, hit-stop, cinematic); the smallest active request wins.
    /// This prevents pause and hit-stop fighting each other or getting stuck.
    /// </summary>
    public class TimeController
    {
        readonly Dictionary<string, float> requests = new Dictionary<string, float>();

        public const string Pause = "pause";
        public const string HitStop = "hitstop";
        public const string Cinematic = "cinematic";

        public float CurrentScale => Time.timeScale;

        public void Set(string reason, float scale)
        {
            requests[reason] = Mathf.Clamp(scale, 0f, 4f);
            Apply();
        }

        public void Clear(string reason)
        {
            if (requests.Remove(reason)) Apply();
        }

        /// <summary>Clears every request and restores normal time. Used on scene changes.</summary>
        public void ResetAll()
        {
            requests.Clear();
            Apply();
        }

        void Apply()
        {
            float scale = 1f;
            foreach (var pair in requests) scale = Mathf.Min(scale, pair.Value);
            if (!Mathf.Approximately(Time.timeScale, scale))
                Time.timeScale = scale;
        }
    }
}
