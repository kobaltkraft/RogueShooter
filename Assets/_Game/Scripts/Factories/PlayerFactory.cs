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
        public PlayerAssembly(Transform transform, Health health, HitscanWeapon weapon)
        { Transform = transform; Health = health; Weapon = weapon; }
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
            return new PlayerAssembly(root.transform, health, weapon);
        }

        static Camera CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("First Person Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.localPosition = new Vector3(0, 1.62f, 0);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .05f;
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
    }
}
