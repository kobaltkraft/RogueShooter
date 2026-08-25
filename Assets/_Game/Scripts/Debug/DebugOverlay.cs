using System.Text;
using UnityEngine;
using UnityEngine.UI;
using RogueArena.Core;
using RogueArena.UI;

namespace RogueArena.Debugging
{
    /// <summary>
    /// F3 debug overlay: FPS, state, enemy counts, wave info and quick cheats
    /// (god mode, give ammo, kill all, skip wave). Built lazily on first toggle.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        public static DebugOverlay Instance { get; private set; }

        Text text;
        float fps;
        int frames;
        float fpsTimer;
        float godModeUntil;
        StringBuilder builder = new StringBuilder(512);

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("DebugOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugOverlay>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            UiKit.CreateCanvasRoot("DebugCanvas", transform, out Canvas canvas, out _);
            canvas.sortingOrder = 500;

            text = UiKit.Label(canvas.transform, "DebugText", "", 14, new Color(.7f, 1f, .8f, .95f),
                TextAnchor.UpperLeft);
            UiKit.Stretch(text.rectTransform, left: 12f, top: 12f);
            text.rectTransform.sizeDelta = new Vector2(560f, 700f);
            text.gameObject.SetActive(false);
        }

        void Update()
        {
            // Toggle.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                DebugSettings.ShowDebugOverlay = !DebugSettings.ShowDebugOverlay;
                text.gameObject.SetActive(DebugSettings.ShowDebugOverlay);
            }
            if (!DebugSettings.ShowDebugOverlay || text == null) return;

            // FPS.
            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= .5f)
            {
                fps = frames / fpsTimer;
                frames = 0;
                fpsTimer = 0f;
            }

            // Cheats (only in-game).
            if (keyboard != null && Services.Session != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Services.Session.Vitals?.Heal(100f);
                if (keyboard.digit2Key.wasPressedThisFrame) Services.Session.Weapons?.AddAmmoToAll(200);
                if (keyboard.digit3Key.wasPressedThisFrame) godModeUntil = Time.time + 30f;
                if (keyboard.digit4Key.wasPressedThisFrame) Services.Session.Spawner?.ClearAll();
                if (keyboard.digit5Key.wasPressedThisFrame) Services.Session.Vitals?.RestoreFull();

                // God mode = continuous heal.
                if (Time.time < godModeUntil)
                    Services.Session.Vitals?.Heal(1000f * Time.deltaTime);
            }

            text.text = BuildReport();
        }

        string BuildReport()
        {
            builder.Clear();
            builder.AppendLine($"FPS {fps:0}   {(fps >= 55 ? "OK" : fps >= 30 ? "BUSY" : "LOW")}");
            builder.AppendLine($"state {Services.Flow?.State.Current.ToString() ?? "-"}");

            var session = Services.Session;
            if (session != null)
            {
                builder.AppendLine($"arena {session.ArenaId}  mode {session.ModeId}");
                if (session.Waves != null)
                    builder.AppendLine($"wave {session.Waves.WaveNumber}  remaining {session.Waves.EnemiesRemaining}  kills {session.Waves.KillsThisRun}");
                if (session.Spawner != null)
                    builder.AppendLine($"enemies {session.Spawner.AliveCount}/{AI.AgentSpawner.GlobalEnemyCap}");
                if (session.Vitals != null)
                    builder.AppendLine($"hp {session.Vitals.CurrentHealth:0}/{session.Vitals.MaxHealth:0}  armor {session.Vitals.CurrentArmor:0}");
                if (Services.Score != null)
                    builder.AppendLine($"score {Services.Score.Score:N0}  combo x{Services.Score.ComboMultiplier:0.0}");
                builder.AppendLine($"time {Services.Flow?.RunSeconds ?? 0f:0}s  seed {Services.Rng?.Seed ?? 0}");
                if (Time.time < godModeUntil) builder.AppendLine("GOD MODE (30s)");
            }

            builder.AppendLine("F3 overlay  1 heal  2 ammo  3 god  4 kill-all  5 restore");
            return builder.ToString();
        }
    }
}
