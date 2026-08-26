using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RogueArena.EditorTools
{
    /// <summary>
    /// Editor validation: checks that every scene needed by the game is in the
    /// build settings, all game layers/tags exist, the URP assets are assigned,
    /// and the content catalog can build without missing references. Available
    /// from Tools -> Rogue Arena -> Validate Project; also runs as part of
    /// CI-style smoke checks.
    /// </summary>
    public static class ProjectValidator
    {
        [MenuItem("Tools/Rogue Arena/Validate Project")]
        public static void ValidateAll()
        {
            ValidateInternal(showDialog: true);
        }

        /// <summary>
        /// Batch-mode entry point used by Tools/RunUnityCompatibilityMatrix.py.
        /// Every editor in the support matrix compiles the project and invokes
        /// this method in an isolated copy of the checkout.
        /// </summary>
        public static void ValidateForCi()
        {
            int errors = ValidateInternal(showDialog: false);
            if (Application.isBatchMode)
                EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static int ValidateInternal(bool showDialog)
        {
            int errors = 0;
            int warnings = 0;
            var log = new System.Text.StringBuilder();

            void Error(string message) { errors++; log.AppendLine("ERROR   " + message); }
            void Warn(string message) { warnings++; log.AppendLine("WARNING " + message); }
            void Info(string message) { log.AppendLine("ok      " + message); }

            // ---- build settings scenes ----
            string[] requiredScenes = { "MainMenu", "Arena_Industrial", "Arena_Desert", "Arena_City" };
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            var sceneNames = new HashSet<string>(
                buildScenes.Where(s => s != null && s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)));
            foreach (string scene in requiredScenes)
            {
                if (sceneNames.Contains(scene)) Info($"scene in build: {scene}");
                else Error($"scene missing from build settings: {scene}");
            }

            // ---- layers ----
            var requiredLayers = new Dictionary<int, string>
            {
                { 6, "Environment" }, { 7, "Enemies" }, { 8, "PlayerBody" }, { 9, "PlayerProjectiles" },
                { 10, "EnemyProjectiles" }, { 11, "Pickups" }, { 12, "Hazards" }, { 13, "Debris" },
            };
            for (int i = 0; i < 32; i++)
            {
                string layer = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(layer)) continue;
                if (requiredLayers.TryGetValue(i, out string expected))
                {
                    if (layer == expected) Info($"layer {i}: {layer}");
                    else Error($"layer {i} is '{layer}', expected '{expected}'");
                }
            }
            foreach (var pair in requiredLayers)
            {
                int found = LayerMask.NameToLayer(pair.Value);
                if (found != pair.Key) Error($"layer '{pair.Value}' not on slot {pair.Key} (found slot {found})");
            }

            // ---- Unity editor/package compatibility ----
            UnityCompatibility.Validate(Info, Error);

            // ---- URP ----
            var graphics = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (graphics != null) Info($"render pipeline: {graphics.name}");
            else Error("no render pipeline asset assigned (URP missing in Graphics settings).");

            // ---- compile-critical files present ----
            string scriptsRoot = Path.Combine(Application.dataPath, "_Game/Scripts");
            string[] criticalFiles =
            {
                "Core/SystemRoot.cs", "Core/SceneDirector.cs", "Core/GameFlow.cs", "Core/Services.cs",
                "Core/GameState.cs", "Core/GameEvents.cs", "Core/Layers.cs",
                "Player/PlayerFactory.cs", "Player/FirstPersonController.cs", "Player/CameraRig.cs",
                "Weapons/WeaponController.cs", "Weapons/Weapon.cs", "Weapons/Projectile.cs",
                "AI/EnemyBrain.cs", "AI/EnemyFactory.cs", "AI/AgentSpawner.cs", "AI/BossBrain.cs",
                "Waves/WaveManager.cs", "Waves/WaveModifier.cs",
                "Environment/ArenaBuilder.cs", "Environment/Interactive.cs",
                "UI/UiRoot.cs", "UI/Hud.cs", "UI/MenuScreen.cs",
                "Content/GameContent.cs",
            };
            foreach (string file in criticalFiles)
            {
                if (File.Exists(Path.Combine(scriptsRoot, file))) Info($"script: {file}");
                else Error($"missing script: {file}");
            }

            // ---- content catalog smoke test ----
            var initialize = typeof(Core.GameContent).GetMethod("Initialize",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (initialize == null)
            {
                Error("GameContent.Initialize not found.");
            }
            else
            {
                initialize.Invoke(null, null);
                int weapons = Content.GameContent.Weapons.Count;
                int enemies = Content.GameContent.Enemies.Count;
                int modes = Content.GameContent.Modes.Count;
                int arenas = Content.GameContent.Arenas.Count;
                if (weapons < 8) Error($"expected >= 8 weapons, found {weapons}");
                else Info($"weapons: {weapons}");
                if (enemies < 10) Error($"expected >= 10 enemies, found {enemies}");
                else Info($"enemies: {enemies}");
                if (modes < 4) Error($"expected >= 4 modes, found {modes}");
                else Info($"modes: {modes}");
                if (arenas < 3) Error($"expected >= 3 arenas, found {arenas}");
                else Info($"arenas: {arenas}");

                // Definition sanity: every enemy needs a positive budget cost.
                foreach (var enemy in Content.GameContent.Enemies)
                {
                    if (enemy == null) { Error("null enemy definition in catalog"); continue; }
                    if (enemy.budgetCost <= 0) Warn($"enemy '{enemy.id}' has budgetCost <= 0");
                    if (enemy.maxHealth <= 0) Error($"enemy '{enemy.id}' has maxHealth <= 0");
                }
                foreach (var weapon in Content.GameContent.Weapons)
                {
                    if (weapon == null) { Error("null weapon definition in catalog"); continue; }
                    if (weapon.damage <= 0) Error($"weapon '{weapon.id}' has damage <= 0");
                }
                foreach (var arena in Content.GameContent.Arenas)
                {
                    if (arena == null || string.IsNullOrEmpty(arena.sceneName)) { Error("arena without scene name"); continue; }
                    if (!sceneNames.Contains(arena.sceneName))
                        Error($"arena '{arena.id}' points at scene '{arena.sceneName}' which is not in the build.");
                }
            }

            // ---- input actions asset (documentation only) ----
            string inputActions = Path.Combine(Application.dataPath, "_Game/RogueArena.inputactions");
            if (File.Exists(inputActions))
            {
                string json = File.ReadAllText(inputActions);
                var invalidGuids = Regex.Matches(json, "\"guid\": \"(0{8}-0{4}-0{4}-0{4}-0{12}|)\"");
                if (invalidGuids.Count > 0) Warn($"input actions contains {invalidGuids.Count} empty/zero GUIDs (bindings inert).");
                else Info("input actions asset GUIDs");
            }

            // ---- summary ----
            log.AppendLine();
            log.AppendLine(errors == 0
                ? $"<color=#7CFF9B>VALIDATION PASSED — {warnings} warning(s)</color>"
                : $"<color=#FF6A5E>VALIDATION FAILED — {errors} error(s), {warnings} warning(s)</color>");

            Debug.Log(log.ToString());
            if (showDialog && errors > 0)
                EditorUtility.DisplayDialog("Rogue Arena Validation",
                    $"Validation failed with {errors} error(s) and {warnings} warning(s).\nSee the Console for details.", "OK");
            else if (showDialog)
                EditorUtility.DisplayDialog("Rogue Arena Validation",
                    $"All checks passed ({warnings} warning(s)).", "OK");

            return errors;
        }
    }
}
