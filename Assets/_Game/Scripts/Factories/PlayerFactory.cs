using UnityEngine;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Environment;
using RogueArena.Player;
using RogueArena.Weapons;

namespace RogueArena.Factories
{
    public readonly struct PlayerAssembly
    {
        public readonly Transform Transform;
        public readonly Health Health;
        public readonly HitscanWeapon Weapon;
        public readonly DashController Dash;
        public PlayerAssembly(Transform transform, Health health, HitscanWeapon weapon, DashController dash)
        { Transform = transform; Health = health; Weapon = weapon; Dash = dash; }
    }

    public static class PlayerFactory
    {
        public static PlayerAssembly Create()
        {
            var root = new GameObject("Player");
            root.transform.position = new Vector3(0, 2, -2);
            CharacterController character = root.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = .38f;
            character.center = Vector3.up * .9f;

            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            Health health = root.AddComponent<Health>();
            health.Configure(100);
            FirstPersonController controller = root.AddComponent<FirstPersonController>();
            Camera camera = CreateCamera(root.transform);
            controller.Initialize(camera.transform);
            HitscanWeapon weapon = CreateWeapon(camera, input);
            DashController dash = CreateDash(root);
            return new PlayerAssembly(root.transform, health, weapon, dash);
        }

        static Camera CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("First Person Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.localPosition = new Vector3(0, 1.62f, 0);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.fieldOfView = 75;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        static HitscanWeapon CreateWeapon(Camera camera, PlayerInputReader input)
        {
            GameObject gun = PrimitiveBuilder.Box("AR-1", Vector3.zero, new Vector3(.16f, .15f, .65f), RuntimeMaterials.Wall);
            Object.Destroy(gun.GetComponent<Collider>());
            gun.transform.SetParent(camera.transform);
            gun.transform.localPosition = new Vector3(.3f, -.25f, .55f);
            gun.transform.localRotation = Quaternion.identity;

            ParticleSystem flash = MuzzleFlashFactory.Create(gun.transform);
            WeaponDefinition definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            HitscanWeapon weapon = gun.AddComponent<HitscanWeapon>();
            weapon.Initialize(definition, camera, input, flash);
            return weapon;
        }

        static DashController CreateDash(GameObject root)
        {
            // Build a default DashDefinition at runtime so the dash works
            // out-of-the-box. Designers can later replace this with an asset.
            DashDefinition def = ScriptableObject.CreateInstance<DashDefinition>();
            def.distance = 6f;
            def.duration = .18f;
            def.speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            def.cooldownBeforeRecharge = .4f;
            def.rechargeTime = 2.5f;
            def.maxCharges = 1;
            def.allowAirDash = false;
            def.liftHeight = .15f;
            def.fovKick = 8f;
            def.fovKickSpeed = 12f;

            DashController dash = root.AddComponent<DashController>();
            dash.Initialize(def);
            return dash;
        }
    }
}
