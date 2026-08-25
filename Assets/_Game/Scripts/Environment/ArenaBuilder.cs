using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Visual;

namespace RogueArena.Environment
{
    /// <summary>
    /// Procedurally builds one of the three arenas from an ArenaDefinition:
    /// perimeter, floor, cover clusters, catwalks, interactives (barrels, crates,
    /// doors, turrets, hazards, platforms), lighting and a baked NavMesh.
    /// Exposes spawn/pickup points for the wave and pickup systems.
    /// </summary>
    public class ArenaBuilder : MonoBehaviour
    {
        public Transform PlayerSpawn => playerSpawn;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public IReadOnlyList<Transform> WeaponPoints => weaponPoints;
        public IReadOnlyList<Transform> PowerupPoints => powerupPoints;

        readonly List<Transform> spawnPoints = new List<Transform>(16);
        readonly List<Transform> weaponPoints = new List<Transform>(6);
        readonly List<Transform> powerupPoints = new List<Transform>(6);
        Transform playerSpawn;

        static readonly System.Random layoutRng = new System.Random(48151623);

        public void Build(ArenaDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[Arena] No ArenaDefinition — arena will be empty.");
                return;
            }

            MaterialLibrary.Initialize();

            switch (definition.kind)
            {
                case ArenaKind.Desert: BuildDesert(definition); break;
                case ArenaKind.City: BuildCity(definition); break;
                default: BuildIndustrial(definition); break;
            }

            BuildLighting(definition);
            BuildNavMesh();
        }

        // ================================================================ shared

        Transform Point(string name, Vector3 position, List<Transform> list)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            list?.Add(go.transform);
            return go.transform;
        }

        /// <summary>Perimeter walls + floor slab used by every arena.</summary>
        void BuildShell(ArenaDefinition def, float size, Material floorMaterial, Material wallMaterial)
        {
            var floor = PrimitiveBuilder.Box("Floor", new Vector3(0f, -.5f, 0f),
                new Vector3(size, 1f, size), floorMaterial, isStatic: true);
            floor.layer = Layers.Environment;

            // Perimeter walls with light strips near the top, facing inward.
            float wallHeight = 9f;
            float half = size * .5f;
            Material accent = def.uiAccent == Color.clear ? MaterialLibrary.GlowCyan : AccentMaterial(def);

            // (position, size, yaw, strip local offset toward arena centre)
            (Vector3 pos, Vector3 size, float yaw, Vector3 stripOffset)[] walls =
            {
                (new Vector3(0f, wallHeight * .5f, half), new Vector3(size + 2f, wallHeight, 1.2f), 0f, new Vector3(0f, 0f, -.7f)),
                (new Vector3(0f, wallHeight * .5f, -half), new Vector3(size + 2f, wallHeight, 1.2f), 0f, new Vector3(0f, 0f, .7f)),
                (new Vector3(half, wallHeight * .5f, 0f), new Vector3(1.2f, wallHeight, size + 2f), 90f, new Vector3(-.7f, 0f, 0f)),
                (new Vector3(-half, wallHeight * .5f, 0f), new Vector3(1.2f, wallHeight, size + 2f), 90f, new Vector3(.7f, 0f, 0f)),
            };
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = PrimitiveBuilder.Box("Wall_" + i, walls[i].pos, walls[i].size, wallMaterial, isStatic: true);
                wall.layer = Layers.Environment;

                var strip = PrimitiveBuilder.LightStrip("WallStrip_" + i,
                    walls[i].pos + walls[i].stripOffset + new Vector3(0f, wallHeight * .42f, 0f),
                    new Vector3(Mathf.Min(size, 26f), .18f, .3f), accent,
                    walls[i].yaw == 0f ? null : Quaternion.Euler(0f, walls[i].yaw, 0f), isStatic: true);
                Object.Destroy(strip.GetComponent<Collider>());
            }
        }

        static Material AccentMaterial(ArenaDefinition def)
        {
            return MaterialLibrary.Emissive("ArenaAccent_" + def.id, def.uiAccent * .25f, def.uiAccent, 2f);
        }

        static GameObject SetEnv(GameObject go)
        {
            Layers.SetLayerRecursive(go, Layers.Environment);
            return go;
        }

        /// <summary>A cover cluster: low blocks with cover points registered for the AI.</summary>
        void CoverCluster(Vector3 center, float rotationY, int blocks, Material material, string prefix)
        {
            for (int i = 0; i < blocks; i++)
            {
                float angle = rotationY + i * (140f / Mathf.Max(1, blocks));
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * (2.6f + (i % 2) * 1.2f);
                var cover = PrimitiveBuilder.Box($"{prefix}_Cover_{i}", center + offset + Vector3.up * .65f,
                    new Vector3(1.9f, 1.3f, 1.1f), material, isStatic: true, rotationY: angle);
                cover.layer = Layers.Environment;
                cover.AddComponent<CoverPoint>();
            }
        }

        void Barrels(Vector3 center, int count, string seedName)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = center + new Vector3(
                    ((layoutRng.Next(-30, 31)) / 10f), 0f, ((layoutRng.Next(-30, 31)) / 10f));
                CreateBarrel(position, $"{seedName}_{i}");
            }
        }

        /// <summary>One explosive barrel with hazard stripes.</summary>
        GameObject CreateBarrel(Vector3 position, string name = "Barrel")
        {
            var barrel = PrimitiveBuilder.Cylinder(name, position + Vector3.up * .6f, .45f, 1.2f,
                MaterialLibrary.Barrel);
            barrel.layer = Layers.Hazards;

            var health = barrel.AddComponent<Combat.Health>();
            health.Configure(30f);

            var explosive = barrel.AddComponent<ExplosiveBarrel>();
            explosive.radius = 5.5f;
            explosive.damage = 75f;

            // Warning band.
            var band = PrimitiveBuilder.Cylinder(name + "_Band", position + Vector3.up * .85f, .47f, .18f,
                MaterialLibrary.GlowOrange);
            Object.Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(barrel.transform, true);
            return barrel;
        }

        GameObject CreateCrate(Vector3 position, float scale = 1f)
        {
            var crate = PrimitiveBuilder.Box("Crate", position + Vector3.up * .5f * scale,
                new Vector3(1f, 1f, 1f) * scale, MaterialLibrary.Crate, isStatic: false);
            crate.layer = Layers.Hazards;

            var health = crate.AddComponent<Combat.Health>();
            health.Configure(20f);
            crate.AddComponent<BreakableObject>();
            return crate;
        }

        void CreateTurret(Vector3 position, float yaw, string targetId = null)
        {
            var root = new GameObject("Turret");
            root.transform.position = position;
            root.layer = Layers.Hazards;

            var basePillar = PrimitiveBuilder.Cylinder("Base", position + Vector3.up * .8f, .4f, 1.6f,
                MaterialLibrary.MetalDark);
            basePillar.transform.SetParent(root.transform, true);
            basePillar.layer = Layers.Hazards;

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(root.transform, false);
            headGo.transform.position = position + Vector3.up * 1.7f;
            headGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var housing = PrimitiveBuilder.Box("Housing", headGo.transform.position, new Vector3(.7f, .5f, .9f),
                MaterialLibrary.Metal);
            housing.transform.SetParent(headGo.transform, true);
            housing.layer = Layers.Hazards;
            var eye = PrimitiveBuilder.Box("Eye", headGo.transform.position + headGo.transform.forward * .5f,
                new Vector3(.18f, .12f, .12f), MaterialLibrary.GlowRed);
            eye.transform.SetParent(headGo.transform, true);
            Object.Destroy(eye.GetComponent<Collider>());
            var barrelGo = PrimitiveBuilder.Cylinder("Gun", headGo.transform.position + headGo.transform.forward * .55f
                + Vector3.up * .05f, .07f, .8f, MaterialLibrary.MetalDark);
            barrelGo.transform.SetParent(headGo.transform, true);
            barrelGo.transform.rotation = Quaternion.LookRotation(headGo.transform.forward) * Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(barrelGo.GetComponent<Collider>());

            var health = root.AddComponent<Combat.Health>();
            health.Configure(120f);

            var turret = root.AddComponent<AutoTurret>();
            turret.Setup(headGo.transform, eye.transform);
            turret.range = 26f;
            turret.damage = 6f;
        }

        void CreateDoor(Vector3 position, float yaw, string id, Vector3? openOffset = null)
        {
            var root = new GameObject("Door_" + id);
            root.transform.position = position;
            root.layer = Layers.Hazards;

            // Frame.
            var frameLeft = PrimitiveBuilder.Box("FrameL", position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-1.6f, 1.7f, 0f),
                new Vector3(.6f, 3.4f, 1.4f), MaterialLibrary.WallDark);
            frameLeft.transform.SetParent(root.transform, true);
            frameLeft.layer = Layers.Environment; // frame is permanent geometry
            var frameRight = PrimitiveBuilder.Box("FrameR", position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.6f, 1.7f, 0f),
                new Vector3(.6f, 3.4f, 1.4f), MaterialLibrary.WallDark);
            frameRight.transform.SetParent(root.transform, true);
            frameRight.layer = Layers.Environment;

            // Sliding leaf.
            var leaf = PrimitiveBuilder.Box("Leaf", position + Vector3.up * 1.7f,
                new Vector3(2.6f, 3.2f, .3f), MaterialLibrary.Metal);
            leaf.transform.SetParent(root.transform, true);
            leaf.layer = Layers.Hazards;

            var door = root.AddComponent<SlidingDoor>();
            door.switchId = id;
            door.SetLeaf(leaf.transform);
            if (openOffset.HasValue) door.openOffset = openOffset.Value;

            ArenaRegistry.Register(id, root);
        }

        void CreateHazardZone(Vector3 position, float radius, string id = null, bool startActive = true)
        {
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zone.name = "HazardZone";
            zone.transform.position = position + Vector3.up * .06f;
            zone.transform.localScale = new Vector3(radius * 2f, .05f, radius * 2f);
            Object.Destroy(zone.GetComponent<Collider>());
            zone.layer = Layers.Hazards;

            var hazard = zone.AddComponent<HazardZone>();
            hazard.damagePerSecond = 22f;
            hazard.startActive = startActive;
            hazard.SetVisual(zone.GetComponent<Renderer>());
            if (!string.IsNullOrEmpty(id)) ArenaRegistry.Register(id, zone);
        }

        void CreatePlatform(Vector3 from, Vector3 offset, float pause = 1.4f)
        {
            var platform = PrimitiveBuilder.Box("Platform", from, new Vector3(3f, .4f, 3f), MaterialLibrary.Metal);
            platform.layer = Layers.Hazards;
            var mover = platform.AddComponent<MovingPlatform>();
            mover.offset = offset;
            mover.speed = 1.5f;
            mover.pauseTime = pause;
        }

        void CreateSwitch(Vector3 position, string targetId, string id)
        {
            var panel = PrimitiveBuilder.Box("Switch_" + id, position + Vector3.up * 1.1f,
                new Vector3(.4f, .6f, .16f), MaterialLibrary.MetalDark);
            panel.layer = Layers.Environment;
            var light = PrimitiveBuilder.Box("SwitchLight_" + id, position + Vector3.up * 1.35f,
                new Vector3(.24f, .12f, .06f), MaterialLibrary.GlowGreen);
            Object.Destroy(light.GetComponent<Collider>());
            light.transform.SetParent(panel.transform, true);

            var @switch = panel.AddComponent<InteractiveSwitch>();
            @switch.targetId = targetId;
            @switch.switchId = id;
        }

        void BuildNavMesh()
        {
            var surfaceGo = new GameObject("NavMeshSurface");
            surfaceGo.transform.SetParent(transform, false);
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            // Only permanent geometry is baked; props/interactables stay dynamic.
            surface.layerMask = 1 << Layers.Environment;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        void BuildLighting(ArenaDefinition def)
        {
            // Ambient rig: one main directional + arena fill points handled by
            // LightBuilder during arena construction. Directional for shadows.
            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(55f, 40f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = def.kind switch
            {
                ArenaKind.Desert => new Color(1f, .93f, .8f),
                ArenaKind.City => new Color(.75f, .82f, 1f),
                _ => new Color(.9f, .95f, 1f),
            };
            sun.intensity = def.kind == ArenaKind.Desert ? 1.3f : 1f;
            sun.shadows = LightShadows.Soft;
            sun.renderingLayerMask = 1; // default

            // RenderSettings per arena.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = def.fogColor;
            RenderSettings.fogDensity = def.fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSky = def.fogColor * 1.4f;
            RenderSettings.ambientEquator = def.fogColor;
            RenderSettings.ambientGround = def.fogColor * .5f;
        }

        // ================================================================ industrial

        void BuildIndustrial(ArenaDefinition def)
        {
            const float size = 64f;
            BuildShell(def, size, MaterialLibrary.FloorTech, MaterialLibrary.WallPanel);

            // --- centre: foundry machine block ---
            var core = PrimitiveBuilder.Box("Reactor", new Vector3(0f, 2.2f, 0f), new Vector3(7f, 4.4f, 7f),
                MaterialLibrary.Metal, isStatic: true);
            core.layer = Layers.Environment;
            var coreGlow = PrimitiveBuilder.Box("ReactorGlow", new Vector3(0f, 3.6f, 3.7f), new Vector3(4.5f, .8f, .2f),
                MaterialLibrary.GlowOrange, isStatic: true);
            Object.Destroy(coreGlow.GetComponent<Collider>());
            coreGlow.transform.SetParent(core.transform, true);
            Point("CoverPoint_Centre", new Vector3(0f, 0f, 0f), null).gameObject.AddComponent<CoverPoint>();

            // --- container lanes (four quadrants) ---
            var laneMaterial = MaterialLibrary.Metal;
            for (int q = 0; q < 4; q++)
            {
                float sx = q % 2 == 0 ? 1 : -1;
                float sz = q < 2 ? 1 : -1;
                Vector3 quadrant = new Vector3(sx * 17f, 0f, sz * 17f);

                // Containers: big walk-around cover.
                for (int c = 0; c < 2; c++)
                {
                    Vector3 pos = quadrant + new Vector3(c * 5.5f, 1.3f, c % 2 == 0 ? 0f : 3.2f);
                    var container = PrimitiveBuilder.Box($"Container_{q}_{c}", pos, new Vector3(6.2f, 2.6f, 2.6f),
                        c == 0 ? MaterialLibrary.WallPanel : MaterialLibrary.Trim, isStatic: true,
                        rotationY: q * 90f + c * 12f);
                    container.layer = Layers.Environment;
                    container.AddComponent<CoverPoint>();
                }

                CoverCluster(quadrant + new Vector3(0f, 0f, -8f), q * 37f, 3, MaterialLibrary.Concrete, $"Q{q}");
                Barrels(quadrant + new Vector3(6f, 0f, 6f), 3, $"B{q}");
            }

            // --- catwalk ring with pillars ---
            for (int p = 0; p < 4; p++)
            {
                float angle = p * 90f + 45f;
                Vector3 pillarPos = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * 21f;
                var pillar = PrimitiveBuilder.Pillar($"Pillar_{p}", pillarPos, 6.5f, .8f, MaterialLibrary.Concrete);
                Layers.SetLayerRecursive(pillar, Layers.Environment);
            }
            Layers.SetLayerRecursive(PrimitiveBuilder.Catwalk("Catwalk_N",
                new Vector3(-14f, 5.2f, -21f), new Vector3(14f, 5.2f, -21f), 2.4f,
                MaterialLibrary.Metal, MaterialLibrary.Trim), Layers.Environment);
            Layers.SetLayerRecursive(PrimitiveBuilder.Catwalk("Catwalk_S",
                new Vector3(-14f, 5.2f, 21f), new Vector3(14f, 5.2f, 21f), 2.4f,
                MaterialLibrary.Metal, MaterialLibrary.Trim), Layers.Environment);
            // Stairs up to catwalks.
            Layers.SetLayerRecursive(PrimitiveBuilder.Stairs("Stairs_E",
                new Vector3(19f, 0f, 17f), Vector3.back, 5.2f, 2.4f, MaterialLibrary.Concrete), Layers.Environment);
            Layers.SetLayerRecursive(PrimitiveBuilder.Stairs("Stairs_W",
                new Vector3(-19f, 0f, -17f), Vector3.forward, 5.2f, 2.4f, MaterialLibrary.Concrete), Layers.Environment);

            // --- pipes along the walls (visual dressing, no collision) ---
            for (int i = 0; i < 6; i++)
            {
                float t = -24f + i * 9.6f;
                var pipeA = PrimitiveBuilder.Pipe($"Pipe_N_{i}", new Vector3(t, 7.6f, -30.5f), new Vector3(t, 7.6f, 30.5f), .22f,
                    i % 2 == 0 ? MaterialLibrary.MetalDark : MaterialLibrary.Metal);
                var pipeB = PrimitiveBuilder.Pipe($"Pipe_W_{i}", new Vector3(-30.5f, 6.9f + (i % 3) * .5f, t), new Vector3(30.5f, 6.9f + (i % 3) * .5f, t),
                    .18f, MaterialLibrary.MetalDark);
                foreach (var pipe in new[] { pipeA, pipeB })
                {
                    foreach (var collider in pipe.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                    Layers.SetLayerRecursive(pipe, Layers.Environment);
                }
            }

            // --- interactive layer ---
            CreateTurret(new Vector3(-26f, 0f, 26f), 135f);
            CreateTurret(new Vector3(26f, 0f, -26f), -45f);
            CreateDoor(new Vector3(0f, 0f, -30f), 0f, "door_south");
            CreateHazardZone(new Vector3(13f, 0f, -13f), 3.2f, "hazard_1");
            CreateHazardZone(new Vector3(-13f, 0f, 13f), 3.2f, "hazard_2", startActive: false);
            CreateSwitch(new Vector3(3.2f, 0f, -28.6f), "hazard_2", "switch_hazard");
            CreatePlatform(new Vector3(-24f, .2f, -8f), new Vector3(0f, 0f, 16f));
            for (int i = 0; i < 8; i++)
                CreateCrate(new Vector3(-21f + i * 1.5f, 0f, 24f + (i % 3) * 1.2f), .9f + (i % 3) * .2f);

            // --- spawn + pickup points ---
            playerSpawn = Point("PlayerSpawn", new Vector3(0f, 0f, 24f), null);
            Vector3[] spawns =
            {
                new Vector3(-26f, 0f, -26f), new Vector3(26f, 0f, -26f), new Vector3(-26f, 0f, 0f), new Vector3(26f, 0f, 0f),
                new Vector3(0f, 0f, -26f), new Vector3(18f, 0f, 18f), new Vector3(-18f, 0f, 18f), new Vector3(0f, 5.4f, -21f),
            };
            foreach (Vector3 spawn in spawns) Point("SpawnPoint", spawn, spawnPoints);

            Point("WeaponPoint", new Vector3(10f, 0f, -6f), weaponPoints);
            Point("WeaponPoint", new Vector3(-10f, 0f, 6f), weaponPoints);
            Point("PowerupPoint", new Vector3(0f, 0f, -14f), powerupPoints);
            Point("PowerupPoint", new Vector3(20f, 0f, 20f), powerupPoints);
            Point("PowerupPoint", new Vector3(-20f, 0f, -20f), powerupPoints);
        }

        // ================================================================ desert

        void BuildDesert(ArenaDefinition def)
        {
            const float size = 72f;
            BuildShell(def, size, MaterialLibrary.Sand, MaterialLibrary.Sandstone);

            // Central dune plateau.
            var plateau = PrimitiveBuilder.Cylinder("Plateau", Vector3.zero, 10f, 1.6f, MaterialLibrary.Sandstone);
            plateau.layer = Layers.Environment;
            Layers.SetLayerRecursive(PrimitiveBuilder.Stairs("PlateauStairs_N",
                new Vector3(0f, 0f, 10.5f), Vector3.forward, 1.6f, 3f, MaterialLibrary.Sandstone), Layers.Environment);
            Layers.SetLayerRecursive(PrimitiveBuilder.Stairs("PlateauStairs_S",
                new Vector3(0f, 0f, -10.5f), Vector3.back, 1.6f, 3f, MaterialLibrary.Sandstone), Layers.Environment);
            Point("CoverPoint_Plateau", new Vector3(0f, 1.6f, 0f), null).gameObject.AddComponent<CoverPoint>();

            // Rock clusters.
            for (int r = 0; r < 8; r++)
            {
                float angle = r * 45f;
                Vector3 ring = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * (16f + (r % 3) * 7f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 pos = ring + new Vector3((i - 1) * 2.4f, 0f, (i % 2) * 2f);
                    var rock = PrimitiveBuilder.Box($"Rock_{r}_{i}", pos + Vector3.up * (.5f + i * .18f),
                        new Vector3(1.7f + i * .3f, 1f + i * .36f, 1.5f + i * .2f), MaterialLibrary.Sandstone,
                        isStatic: true, rotationY: r * 33f + i * 40f);
                    rock.layer = Layers.Environment;
                    if (i == 1) rock.AddComponent<CoverPoint>();
                }
            }

            // Ruined arches.
            for (int a = 0; a < 3; a++)
            {
                Vector3 basePos = new Vector3(-18f + a * 18f, 0f, a % 2 == 0 ? 22f : -22f);
                SetEnv(PrimitiveBuilder.Box($"ArchL_{a}", basePos + new Vector3(-2.4f, 2.4f, 0f), new Vector3(1f, 4.8f, 1f),
                    MaterialLibrary.Sandstone));
                SetEnv(PrimitiveBuilder.Box($"ArchR_{a}", basePos + new Vector3(2.4f, 2.4f, 0f), new Vector3(1f, 4.8f, 1f),
                    MaterialLibrary.Sandstone));
                SetEnv(PrimitiveBuilder.Box($"ArchTop_{a}", basePos + new Vector3(0f, 4.9f, 0f), new Vector3(6f, .8f, 1f),
                    MaterialLibrary.Sandstone));
            }

            // Sunken pit hazard with platform crossing.
            CreateHazardZone(new Vector3(0f, 0f, -26f), 4.5f, "pit_hazard");
            CreatePlatform(new Vector3(-8f, .2f, -26f), new Vector3(16f, 0f, 0f));

            // Camp crates + barrels.
            for (int i = 0; i < 6; i++)
                CreateCrate(new Vector3(24f + (i % 3) * 1.4f, 0f, -14f + i * 1.6f));
            Barrels(new Vector3(-24f, 0f, 14f), 4, "desert_b");

            // Turret on the plateau.
            CreateTurret(new Vector3(0f, 1.6f, 0f), 0f);

            playerSpawn = Point("PlayerSpawn", new Vector3(0f, 0f, 30f), null);
            Vector3[] spawns =
            {
                new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, -30f), new Vector3(-30f, 0f, 0f), new Vector3(30f, 0f, 0f),
                new Vector3(0f, 0f, -32f), new Vector3(22f, 0f, 22f), new Vector3(-22f, 0f, 22f), new Vector3(0f, 1.8f, 0f),
            };
            foreach (Vector3 spawn in spawns) Point("SpawnPoint", spawn, spawnPoints);

            Point("WeaponPoint", new Vector3(14f, 0f, 0f), weaponPoints);
            Point("WeaponPoint", new Vector3(-14f, 0f, 0f), weaponPoints);
            Point("PowerupPoint", new Vector3(0f, 1.8f, 6f), powerupPoints);
            Point("PowerupPoint", new Vector3(-26f, 0f, -26f), powerupPoints);
            Point("PowerupPoint", new Vector3(26f, 0f, 26f), powerupPoints);
        }

        // ================================================================ city

        void BuildCity(ArenaDefinition def)
        {
            const float size = 66f;
            BuildShell(def, size, MaterialLibrary.Asphalt, MaterialLibrary.Brick);

            // Central plaza fountain block.
            var fountain = PrimitiveBuilder.Cylinder("Fountain", Vector3.zero, 4.2f, 1.1f, MaterialLibrary.Concrete);
            fountain.layer = Layers.Environment;
            var water = PrimitiveBuilder.Cylinder("FountainWater", new Vector3(0f, 1.05f, 0f), 3.6f, .1f,
                MaterialLibrary.GlowCyan);
            Object.Destroy(water.GetComponent<Collider>());
            water.transform.SetParent(fountain.transform, true);
            Layers.SetLayerRecursive(fountain, Layers.Environment);

            // Buildings (four corner blocks with interiors).
            for (int b = 0; b < 4; b++)
            {
                float sx = b % 2 == 0 ? 1 : -1;
                float sz = b < 2 ? 1 : -1;
                Vector3 corner = new Vector3(sx * 21f, 0f, sz * 21f);

                // Two walls forming an L, gap in the middle = interior.
                var wallA = PrimitiveBuilder.Box($"Building{b}_A", corner + new Vector3(-sx * 4f, 3.5f, 0f),
                    new Vector3(9f, 7f, 1f), MaterialLibrary.Brick, isStatic: true);
                wallA.layer = Layers.Environment;
                wallA.AddComponent<CoverPoint>();
                var wallB = PrimitiveBuilder.Box($"Building{b}_B", corner + new Vector3(0f, 3.5f, -sz * 4f),
                    new Vector3(1f, 7f, 9f), MaterialLibrary.Brick, isStatic: true);
                wallB.layer = Layers.Environment;
                wallB.AddComponent<CoverPoint>();

                // Rooftop access: external stairs.
                Layers.SetLayerRecursive(PrimitiveBuilder.Stairs($"Building{b}_Stairs",
                    corner + new Vector3(sx * 4.5f, 0f, sz * 8.5f),
                    new Vector3(-sx, 0f, 0f), 5.4f, 3f, MaterialLibrary.Concrete), Layers.Environment);

                // Sign on the inner wall.
                var sign = PrimitiveBuilder.LightStrip($"Sign_{b}",
                    corner + new Vector3(-sx * 4.35f, 5.4f, 0f), new Vector3(5f, .8f, .25f),
                    b % 2 == 0 ? MaterialLibrary.GlowMagenta : MaterialLibrary.GlowCyan, isStatic: true);
                Object.Destroy(sign.GetComponent<Collider>());
            }

            // Street clutter: cars-as-cover (boxes), barricades.
            for (int s = 0; s < 6; s++)
            {
                Vector3 pos = new Vector3(-20f + s * 8f, 0f, s % 2 == 0 ? 10f : -10f);
                var car = PrimitiveBuilder.Box($"Car_{s}", pos + Vector3.up * .7f, new Vector3(2f, 1.2f, 4.4f),
                    s % 2 == 0 ? MaterialLibrary.Trim : MaterialLibrary.MetalDark, isStatic: true,
                    rotationY: s % 2 == 0 ? 90f : 0f);
                car.layer = Layers.Environment;
                car.AddComponent<CoverPoint>();
            }

            // Doors between plaza and one building interior.
            CreateDoor(new Vector3(21f, 0f, 12f), 0f, "door_city");
            CreateSwitch(new Vector3(17.5f, 0f, 12f), "door_city", "switch_city");
            CreateTurret(new Vector3(-21f, 5.4f, -21f), 45f); // rooftop turret
            CreatePlatform(new Vector3(0f, .2f, -18f), new Vector3(0f, 4.5f, 0f), 2f); // elevator
            CreateHazardZone(new Vector3(-14f, 0f, 0f), 3f, "city_hazard");
            Barrels(new Vector3(14f, 0f, -14f), 4, "city_b");
            for (int i = 0; i < 5; i++)
                CreateCrate(new Vector3(-24f + i * 1.4f, 0f, -18f + (i % 2) * 1.4f));

            playerSpawn = Point("PlayerSpawn", new Vector3(0f, 0f, 26f), null);
            Vector3[] spawns =
            {
                new Vector3(-28f, 0f, -28f), new Vector3(28f, 0f, -28f), new Vector3(-28f, 0f, 0f), new Vector3(28f, 0f, 0f),
                new Vector3(0f, 0f, -28f), new Vector3(21f, 0f, 21f), new Vector3(-21f, 0f, 21f), new Vector3(21f, 0f, -21f),
            };
            foreach (Vector3 spawn in spawns) Point("SpawnPoint", spawn, spawnPoints);

            Point("WeaponPoint", new Vector3(8f, 0f, -8f), weaponPoints);
            Point("WeaponPoint", new Vector3(-8f, 0f, 8f), weaponPoints);
            Point("PowerupPoint", new Vector3(0f, 0f, 14f), powerupPoints);
            Point("PowerupPoint", new Vector3(21f, 0f, 21f), powerupPoints);
            Point("PowerupPoint", new Vector3(-21f, 5.4f, -21f), powerupPoints);
        }
    }
}
