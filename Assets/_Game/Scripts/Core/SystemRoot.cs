using UnityEngine;
using RogueArena.Audio;
using RogueArena.Content;
using RogueArena.Persistence;
using RogueArena.Progression;
using RogueArena.VFX;
using RogueArena.Visual;

namespace RogueArena.Core
{
    /// <summary>
    /// The single persistent bootstrap. Creates every global service in a
    /// deterministic order, keeps them alive across scenes, and disposes them
    /// cleanly on quit. Exactly one instance exists per process.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SystemRoot : MonoBehaviour
    {
        public static SystemRoot Instance { get; private set; }

        /// <summary>Guarantees the persistent root exists in every entry path.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<SystemRoot>() != null) return;
            var go = new GameObject("SystemRoot");
            go.AddComponent<SystemRoot>();
        }

        ScoreService score;
        ProgressionService progression;
        ChallengeService challenges;
        GameFlow flow;
        SceneDirector scenes;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SystemRoot] Duplicate SystemRoot destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            name = "SystemRoot";

            // Content first: everything else reads the catalog.
            GameContent.Initialize();
            MaterialLibrary.Initialize();

            // Services in dependency order.
            var save = new SaveService();
            var settings = new SettingsService(save);
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            var audio = audioGo.AddComponent<AudioDirector>();
            var time = new TimeController();
            var rng = new RngService(System.Environment.TickCount);
            var effects = EffectDirector.Create(transform);

            score = new ScoreService(save);
            progression = new ProgressionService(save);
            challenges = new ChallengeService(save, GameContent.Challenges);

            scenes = gameObject.AddComponent<SceneDirector>();
            flow = new GameFlow(scenes);

            // Register on the locator (marks the system ready).
            Services.Scenes = scenes;
            Services.Flow = flow;
            Services.Audio = audio;
            Services.Save = save;
            Services.Settings = settings;
            Services.Progression = progression;
            Services.Challenges = challenges;
            Services.Score = score;
            Services.Time = time;
            Services.Rng = rng;
            Services.Effects = effects;
            Services.Ready = true;

            // Apply persisted settings (volumes, quality).
            settings.ApplyAll();
            settings.Applied += () => ApplyAudioVolumes(audio, settings);
            ApplyAudioVolumes(audio, settings);

            // Preload common SFX so first playback never hitches.
            audio.Preload(new[]
            {
                "ui_click", "ui_back", "ui_hover", "pickup_health", "pickup_ammo",
                "player_hurt", "explosion", "wave_start", "enemy_alert",
            });

            Debug.Log("[SystemRoot] Services initialised.");
            Debugging.DebugOverlay.EnsureExists();
        }

        static void ApplyAudioVolumes(AudioDirector audio, SettingsService settings)
        {
            if (audio == null || settings == null) return;
            audio.SetCategoryVolume(AudioCategory.Master, settings.GetVolume(AudioCategory.Master));
            audio.SetCategoryVolume(AudioCategory.Music, settings.GetVolume(AudioCategory.Music));
            audio.SetCategoryVolume(AudioCategory.Sfx, settings.GetVolume(AudioCategory.Sfx));
            audio.SetCategoryVolume(AudioCategory.Voice, settings.GetVolume(AudioCategory.Voice));
            audio.SetCategoryVolume(AudioCategory.Ui, settings.GetVolume(AudioCategory.Ui));
        }

        void Update()
        {
            flow?.Tick();
            Services.Save?.TickAutosave(3f);
        }

        void Start()
        {
            // Direct-play support: opening an arena scene starts a default run.
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene.StartsWith("Arena_"))
            {
                string arenaId = scene.Substring("Arena_".Length).ToLowerInvariant();
                if (GameContent.Arena(arenaId) == null) arenaId = "industrial";
                Debug.Log($"[SystemRoot] Play-in-scene detected, starting survival run in '{arenaId}'.");
                flow.StartRun("survival", arenaId);
            }
            else
            {
                // Menu entry point: the scene ships empty, so run the standard
                // menu bootstrap (diorama + UI) through the scene director.
                flow.State.Force(GameState.MainMenu, "boot");
                scenes.LoadMenu();
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;

            score?.Dispose();
            progression?.Dispose();
            challenges?.Dispose();

            GameEvents.ResetAll();
            Services.Clear();
        }

        void OnApplicationQuit()
        {
            Services.Save?.SaveNow();
        }
    }
}
