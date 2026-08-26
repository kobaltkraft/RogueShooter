using UnityEngine;
using RogueArena.AI;
using RogueArena.Core;
using RogueArena.Environment;
using RogueArena.Visual;

namespace RogueArena.UI
{
    /// <summary>
    /// Living backdrop for the main menu: a slowly orbiting camera over a small
    /// platform with three rotating enemy statues and accent lighting. Cheap,
    /// self-contained, and safe if the content catalog is missing.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
        Transform pivot;
        Transform orbit;
        Light key;
        float spin;

        public static MenuDiorama Create()
        {
            var go = new GameObject("MenuDiorama");
            return go.AddComponent<MenuDiorama>();
        }

        void Start()
        {
            MaterialLibrary.Initialize();

            // Camera rig.
            var cameraGo = new GameObject("DioramaCamera");
            cameraGo.transform.SetParent(transform, false);
            cameraGo.transform.position = new Vector3(0f, 2.4f, 7.5f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.02f, .03f, .045f);
            cameraGo.AddComponent<AudioListener>();
            if (MaterialLibrary.UsingUrp)
                cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            // Orbit pivot at the display.
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.position = Vector3.zero;
            cameraGo.transform.SetParent(pivot, true);
            orbit = pivot;

            // Display platform.
            var platform = PrimitiveBuilder.Cylinder("Platform", new Vector3(0f, -.35f, 0f), 4.6f, .7f,
                MaterialLibrary.MetalDark);
            platform.layer = Layers.Environment;
            var ring = PrimitiveBuilder.Cylinder("PlatformRing", new Vector3(0f, .02f, 0f), 4.75f, .12f,
                MaterialLibrary.GlowCyan);
            Object.Destroy(ring.GetComponent<Collider>());

            // Three enemy statues on the platform.
            SpawnStatue(EnemyKind.Soldier, new Vector3(-2.4f, 0f, .8f), 160f);
            SpawnStatue(EnemyKind.Heavy, new Vector3(0f, 0f, -1.1f), 0f);
            SpawnStatue(EnemyKind.Drone, new Vector3(2.4f, 0f, .8f), -160f);

            // Key light.
            key = gameObject.AddComponent<Light>();
            key.type = LightType.Point;
            key.color = new Color(.4f, .85f, 1f);
            key.intensity = 2.4f;
            key.range = 18f;
            key.transform.position = new Vector3(0f, 4f, 2f);
            key.shadows = LightShadows.Soft;

            // Gentle fog for depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.02f, .03f, .045f);
            RenderSettings.fogDensity = .035f;
        }

        void SpawnStatue(EnemyKind kind, Vector3 position, float yaw)
        {
            EnemyDefinition definition = Content.GameContent.EnemyOf(kind);
            if (definition == null) return;

            var statue = EnemyFactory.Create(definition, position + Vector3.up * .2f, null,
                Services.Rng, 1f, 0, 1f);
            if (statue == null) return;

            statue.name = "Statue_" + kind;
            statue.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            statue.gameObject.SetActive(false); // no brains ticking in the menu

            // Keep only the visuals: strip behaviour so the menu stays cheap.
            foreach (var behaviour in statue.GetComponentsInChildren<Behaviour>())
            {
                if (behaviour is EnemyVisual || behaviour is EnemyFlight) continue;
                behaviour.enabled = false;
            }
            statue.gameObject.SetActive(true);
        }

        void Update()
        {
            if (orbit != null)
            {
                spin += Time.unscaledDeltaTime * 6f;
                orbit.rotation = Quaternion.Euler(0f, spin, 0f);
            }
        }
    }
}
