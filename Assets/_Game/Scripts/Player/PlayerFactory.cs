using System.Collections.Generic;
using UnityEngine;
using RogueArena.Combat;
using RogueArena.Content;
using RogueArena.Core;
using RogueArena.Progression;
using RogueArena.Visual;
using RogueArena.Weapons;

namespace RogueArena.Player
{
    /// <summary>
    /// Builds the complete player assembly procedurally: capsule + input reader +
    /// locomotion + vitals + powerups + dash + weapon controller + camera rig
    /// with URP camera. No prefabs or scene wiring required.
    /// </summary>
    public static class PlayerFactory
    {
        public static PlayerAssemblyMarker Create(Vector3 spawnPosition,
            WeaponDefinition startingWeapon, List<PlayerUpgradeDefinition> upgradeCatalog = null)
        {
            // ------------------------------------------------------------ root
            var root = new GameObject("Player");
            root.layer = Layers.PlayerBody;
            root.transform.position = spawnPosition + Vector3.up * .1f;

            root.AddComponent<PlayerMarker>();
            var input = root.AddComponent<PlayerInputReader>();
            var controller = root.AddComponent<CharacterController>();
            controller.radius = .35f;
            controller.height = 1.8f;
            controller.skinWidth = .08f;
            controller.slopeLimit = 50f;
            controller.stepOffset = .45f;

            var locomotion = root.AddComponent<FirstPersonController>();
            var vitals = root.AddComponent<PlayerVitals>();
            var powerups = root.AddComponent<PlayerPowerups>();
            var dash = root.AddComponent<DashController>();
            var weapons = root.AddComponent<WeaponController>();

            // ------------------------------------------------------------ camera
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.SetParent(root.transform, false);
            rigGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            var rig = rigGo.AddComponent<CameraRig>();

            var cameraGo = new GameObject("Camera");
            cameraGo.transform.SetParent(rigGo.transform, false);
            cameraGo.transform.localPosition = Vector3.zero;
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 320f;
            camera.fieldOfView = 78f;
            camera.useOcclusionCulling = true;
            cameraGo.AddComponent<AudioListener>();
            if (MaterialLibrary.UsingUrp)
            {
                var cameraData = cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = true;
            }
            rig.Initialize(camera);

            // Subtle global post stack: gentle bloom on emissives + soft vignette.
            var volumeGo = new GameObject("PostVolume");
            volumeGo.transform.SetParent(root.transform, false);
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            profile.name = "GameplayPost";
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>();
            if (bloom != null)
            {
                bloom.intensity.Override(0.55f);
                bloom.threshold.Override(1.05f);
                bloom.scatter.Override(.75f);
            }
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>();
            if (vignette != null)
            {
                vignette.intensity.Override(.3f);
                vignette.smoothness.Override(.45f);
            }
            volume.sharedProfile = profile;

            // ------------------------------------------------------------ vitals
            var health = root.AddComponent<Health>();
            float maxHealth = 100f;
            float startArmor = 0f;
            float regen = 0f;
            if (upgradeCatalog != null && Services.Progression != null)
            {
                maxHealth += Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.MaxHealth, upgradeCatalog);
                startArmor = Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.StartArmor, upgradeCatalog);
                regen = Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.Regen, upgradeCatalog);
            }
            vitals.Initialize(health, maxHealth, startArmor, regen);

            // ------------------------------------------------------------ abilities
            var dashDefinition = ScriptableObject.CreateInstance<DashDefinition>();
            dashDefinition.distance = 6.5f;
            dashDefinition.duration = .18f;
            dashDefinition.maxCharges = 2;
            dashDefinition.rechargeTime = 2.4f;
            dashDefinition.allowAirDash = true;
            dashDefinition.fovKick = 9f;
            dash.Initialize(dashDefinition);

            float weaponDamageBonus = 0f;
            if (upgradeCatalog != null && Services.Progression != null)
            {
                float dashRecharge = Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.DashRecharge, upgradeCatalog);
                if (dashRecharge > 0f) dash.SetRechargeMultiplier(1f + dashRecharge);

                // MoveSpeed value is already fractional (+.05 per tier).
                float moveSpeed = Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.MoveSpeed, upgradeCatalog);
                if (moveSpeed > 0f) locomotion.SetSpeedMultiplier(1f + moveSpeed);

                weaponDamageBonus = Services.Progression.PlayerUpgradeValue(PlayerUpgradeKind.WeaponDamage, upgradeCatalog);
            }

            locomotion.Initialize(rig);

            // ------------------------------------------------------------ weapons
            weapons.Initialize(camera, input, locomotion, rig);
            if (weaponDamageBonus > 0f) weapons.SetPermanentDamageMultiplier(1f + weaponDamageBonus);
            WeaponDefinition starter = startingWeapon ?? GameContent.Weapon("rifle");
            if (starter != null) weapons.AddWeapon(starter);

            // ------------------------------------------------------------ assembly handle
            var assembly = root.AddComponent<PlayerAssemblyMarker>();
            assembly.Weapons = weapons;
            assembly.Vitals = vitals;
            assembly.Powerups = powerups;
            assembly.Locomotion = locomotion;

            // Spawn puff.
            Services.Effects?.Poof(spawnPosition + Vector3.up, new Color(.5f, .9f, 1f, .8f));
            return assembly;
        }
    }
}
