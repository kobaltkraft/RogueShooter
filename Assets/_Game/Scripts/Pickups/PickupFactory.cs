using UnityEngine;
using RogueArena.Core;
using RogueArena.Visual;
using RogueArena.Weapons;

namespace RogueArena.Pickups
{
    /// <summary>Builds reusable pickup prefabs procedurally (no authored assets).</summary>
    public static class PickupFactory
    {
        public static Pickup CreateWeapon(WeaponDefinition weapon, Vector3 position, bool oneShot = false)
        {
            Color accent = weapon.accentColor;
            var root = CreateRoot("Pickup_Weapon_" + weapon.id, position);
            var visual = BuildVisual(root.transform, ShapeKind.Box, accent, scale: .55f);
            BuildBase(root.transform, accent);
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Weapon;
            pickup.Weapon = weapon;
            pickup.OneShot = oneShot;
            pickup.RespawnSeconds = 30f;
            return pickup;
        }

        public static Pickup CreateAmmo(Vector3 position)
        {
            var root = CreateRoot("Pickup_Ammo", position);
            var visual = BuildVisual(root.transform, ShapeKind.Box, new Color(.75f, .65f, .3f), scale: .4f);
            BuildBase(root.transform, new Color(.75f, .65f, .3f));
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Ammo;
            pickup.RespawnSeconds = 20f;
            return pickup;
        }

        public static Pickup CreateHealth(Vector3 position, float amount = 30f)
        {
            var root = CreateRoot("Pickup_Health", position);
            var visual = BuildVisual(root.transform, ShapeKind.Sphere, new Color(.3f, 1f, .45f), scale: .35f);
            BuildBase(root.transform, new Color(.3f, 1f, .45f));
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Health;
            pickup.Amount = amount;
            pickup.RespawnSeconds = 28f;
            return pickup;
        }

        public static Pickup CreateArmor(Vector3 position, float amount = 40f)
        {
            var root = CreateRoot("Pickup_Armor", position);
            var visual = BuildVisual(root.transform, ShapeKind.Box, new Color(.45f, .65f, 1f), scale: .4f);
            BuildBase(root.transform, new Color(.45f, .65f, 1f));
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Armor;
            pickup.Amount = amount;
            pickup.RespawnSeconds = 32f;
            return pickup;
        }

        public static Pickup CreatePowerup(PowerupDefinition definition, Vector3 position)
        {
            var root = CreateRoot("Pickup_Powerup_" + definition.id, position);
            var visual = BuildVisual(root.transform, ShapeKind.Octahedron, definition.color, scale: .5f);
            BuildBase(root.transform, definition.color);
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Powerup;
            pickup.Powerup = definition;
            pickup.RespawnSeconds = definition.respawnSeconds;
            return pickup;
        }

        public static Pickup CreateCurrency(Vector3 position, int amount)
        {
            var root = CreateRoot("Pickup_Scrap", position);
            var visual = BuildVisual(root.transform, ShapeKind.Octahedron, new Color(1f, .85f, .3f), scale: .28f);
            var pickup = AddPickup(root, visual.transform);
            pickup.kind = Pickup.Kind.Currency;
            pickup.Amount = amount;
            pickup.OneShot = true;
            pickup.RespawnSeconds = 999f;
            return pickup;
        }

        // ---------------------------------------------------------------- builders

        static GameObject CreateRoot(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.layer = Layers.Pickups;
            return root;
        }

        static GameObject BuildVisual(Transform parent, ShapeKind shape, Color color, float scale = .4f)
        {
            GameObject go = shape switch
            {
                ShapeKind.Sphere => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                ShapeKind.Octahedron => GameObject.CreatePrimitive(PrimitiveType.Cube), // rotated into a gem
                _ => GameObject.CreatePrimitive(PrimitiveType.Cube),
            };
            go.name = "Visual";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 1.0f;
            go.transform.localScale = Vector3.one * scale;
            if (shape == ShapeKind.Octahedron) go.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Object.Destroy(go.GetComponent<Collider>()); // pickups never block movement or bullets
            go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Emissive(
                "PickupGlow_" + ColorUtility.ToHtmlStringRGB(color), color * .3f, color, 1.8f);

            // Accent ring around the pickup.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "Ring";
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = new Vector3(1.6f, .1f, .1f);
            ring.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Emissive(
                "PickupRing_" + ColorUtility.ToHtmlStringRGB(color), color * .3f, color, 2.2f);

            var ring2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring2.name = "Ring2";
            Object.Destroy(ring2.GetComponent<Collider>());
            ring2.transform.SetParent(go.transform, false);
            ring2.transform.localPosition = Vector3.zero;
            ring2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ring2.transform.localScale = new Vector3(1.6f, .1f, .1f);
            ring2.GetComponent<Renderer>().sharedMaterial = ring.GetComponent<Renderer>().sharedMaterial;

            return go;
        }

        static void BuildBase(Transform parent, Color color)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Base";
            Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(0f, .05f, 0f);
            pad.transform.localScale = new Vector3(1f, .04f, 1f);
            pad.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.MetalDark;
        }

        static Pickup AddPickup(GameObject root, Transform visual)
        {
            // Trigger volume: generous radius so pickups feel magnetic in motion.
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.1f;
            trigger.center = Vector3.up * .9f;
            return root.AddComponent<Pickup>();
        }

        enum ShapeKind { Box, Sphere, Octahedron }
    }
}
