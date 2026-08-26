using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueArena.AI;
using RogueArena.Content;
using RogueArena.Environment;
using RogueArena.Pickups;
using RogueArena.Player;
using RogueArena.Visual;
using RogueArena.Weapons;
using RogueArena.Waves;

namespace RogueArena.Core
{
    /// <summary>
    /// Owns scene transitions: async loads, static-state teardown between scenes,
    /// and arena bootstrapping (build arena -> spawn player -> wire systems ->
    /// notify the flow). Guarded against double loads and missing scenes.
    /// </summary>
    public class SceneDirector : MonoBehaviour
    {
        public bool IsLoading { get; private set; }
        public string ActiveSceneName { get; private set; } = "MainMenu";

        Coroutine activeLoad;

        public void LoadMenu() => BeginLoad("MainMenu", null, null);

        public void LoadArena(string arenaId, string modeId)
        {
            ArenaDefinition definition = GameContent.Arena(arenaId);
            if (definition == null)
            {
                Debug.LogWarning($"[Scenes] Unknown arena '{arenaId}', falling back to industrial.");
                definition = GameContent.Arena("industrial");
            }
            if (definition == null)
            {
                Debug.LogError("[Scenes] No arena definitions at all — cannot load.");
                return;
            }

            BeginLoad(definition.sceneName, definition.id, modeId);
        }

        void BeginLoad(string sceneName, string arenaId, string modeId)
        {
            if (activeLoad != null)
            {
                Debug.LogWarning($"[Scenes] Load of '{sceneName}' ignored — a load is already running.");
                return;
            }
            activeLoad = StartCoroutine(LoadRoutine(sceneName, arenaId, modeId));
        }

        IEnumerator LoadRoutine(string sceneName, string arenaId, string modeId)
        {
            IsLoading = true;
            Time.timeScale = 1f;

            // Tear down static state from the outgoing scene before it unloads.
            TeardownStatics();

            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Scenes] Failed to start loading '{sceneName}': {exception.Message}");
            }

            if (operation == null)
            {
                Debug.LogError($"[Scenes] Scene '{sceneName}' is not in the build settings.");
                IsLoading = false;
                activeLoad = null;
                if (sceneName != "MainMenu") LoadMenu();
                yield break;
            }

            while (!operation.isDone) yield return null;
            ActiveSceneName = sceneName;

            if (arenaId != null)
            {
                BootstrapArena(arenaId, modeId);
            }
            else
            {
                // Menu scene: diorama backdrop + menu UI.
                Services.Session = null;
                UI.MenuDiorama.Create();
                UI.UiRoot.Create(null);
                Services.Flow?.State.Force(GameState.MainMenu, "menu loaded");
                Services.Audio?.PlayMusic("music_menu", 2f);
            }

            IsLoading = false;
            activeLoad = null;
        }

        /// <summary>Clears scene-local static state. Called on every scene change.</summary>
        public static void TeardownStatics()
        {
            Weapons.Projectile.DisposePool();
            AI.CoverRegistry.Clear();
            Environment.ArenaRegistry.Clear();
            Services.Session = null;
            GameEvents.NoiseEmitted = null;
        }

        // ---------------------------------------------------------------- arena bootstrap

        void BootstrapArena(string arenaId, string modeId)
        {
            ArenaDefinition definition = GameContent.Arena(arenaId);
            if (definition == null)
            {
                Debug.LogError("[Scenes] Arena definition vanished mid-load.");
                return;
            }

            MaterialLibrary.Initialize();
            GameContent.Initialize();

            // 1. Build the arena.
            var builderGo = new GameObject("ArenaBuilder");
            var builder = builderGo.AddComponent<ArenaBuilder>();
            builder.Build(definition);

            // 2. Player.
            WeaponDefinition starter = GameContent.Weapon("rifle");
            var player = PlayerFactory.Create(
                builder.PlayerSpawn != null ? builder.PlayerSpawn.position : new Vector3(0f, 1f, 20f),
                starter, GameContent.PlayerUpgrades);

            // 3. Spawner.
            var spawnerGo = new GameObject("AgentSpawner");
            var spawner = spawnerGo.AddComponent<AgentSpawner>();
            spawner.Initialize(Services.Rng, player.transform);

            // 4. Pickups.
            var pickupGo = new GameObject("PickupManager");
            var pickups = pickupGo.AddComponent<PickupManager>();
            List<WeaponDefinition> weaponPool = BuildWeaponPool();
            pickups.Initialize(Services.Rng, new List<Transform>(builder.WeaponPoints),
                new List<Transform>(builder.PowerupPoints), weaponPool);

            // 5. Waves.
            GameModeDefinition mode = GameContent.Mode(modeId) ?? GameContent.Mode("survival");
            var waveGo = new GameObject("WaveManager");
            var waves = waveGo.AddComponent<WaveManager>();
            waves.Initialize(mode, spawner, builder.SpawnPoints, player.transform, Services.Rng);

            // 6. Session.
            var session = new ArenaSession
            {
                ArenaId = arenaId,
                ModeId = modeId,
                Arena = definition,
                Player = player.transform,
                MainCamera = player.GetComponentInChildren<Camera>(),
                Rig = player.GetComponentInChildren<Player.CameraRig>(),
                Weapons = player.Weapons,
                Vitals = player.Vitals,
                Locomotion = player.Locomotion,
                Spawner = spawner,
                Waves = waves,
                Pickups = pickups,
            };
            Services.Session = session;

            // 7. UI (HUD + pause + end screens).
            UI.UiRoot.Create(null);

            // Notify the flow (starts waves, music, scoring).
            Services.Flow?.OnArenaReady(session);
        }

        static List<WeaponDefinition> BuildWeaponPool()
        {
            // Arena weapon pickups: everything the player owns plus free weapons.
            var pool = new List<WeaponDefinition>(8);
            foreach (WeaponDefinition weapon in GameContent.Weapons)
            {
                if (weapon == null || weapon.id == "rifle") continue; // starter, never a pickup
                bool unlocked = weapon.unlockCost <= 0
                    || (Services.Progression != null && Services.Progression.IsWeaponUnlocked(weapon.id));
                if (unlocked) pool.Add(weapon);
            }
            return pool;
        }
    }
}
