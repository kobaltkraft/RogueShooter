using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Visual;

namespace RogueArena.AI
{
    /// <summary>
    /// Builds enemy GameObjects from definitions. Every archetype gets a distinct
    /// procedural silhouette (no shared grey capsule) while sharing the same
    /// component architecture: Health, EnemyBrain, EnemyCombat, EnemyVisual,
    /// EnemyElite and either NavMeshAgent or EnemyFlight.
    /// </summary>
    public static class EnemyFactory
    {
        public static EnemyBrain Create(EnemyDefinition definition, Vector3 position, Transform player,
            RngService rng, float difficultyScale, int eliteModifierCount, float speedScale = 1f)
        {
            if (definition == null) return null;

            var root = new GameObject("Enemy_" + definition.id);
            root.transform.position = position;
            Layers.SetLayerRecursive(root, Layers.Enemies);

            // --- body ---
            Transform body = BuildBody(definition, root.transform);
            Transform head = BuildHead(definition, root.transform);
            Transform muzzle = BuildWeapon(definition, root.transform);

            // --- components ---
            Health health = root.AddComponent<Health>();
            health.Configure(definition.maxHealth * Mathf.Max(.25f, difficultyScale));

            NavMeshAgent agent = null;
            if (!definition.flying)
            {
                agent = root.AddComponent<NavMeshAgent>();
                agent.radius = definition.agentRadius;
                agent.height = definition.agentHeight;
                agent.speed = definition.moveSpeed * speedScale;
                agent.acceleration = definition.acceleration;
                agent.angularSpeed = 540f;
                agent.stoppingDistance = Mathf.Min(1.4f, definition.preferredRange * .5f);
            }

            EnemyVisual visual = root.AddComponent<EnemyVisual>();
            EnemyCombat combat = root.AddComponent<EnemyCombat>();
            EnemyElite elite = root.AddComponent<EnemyElite>();
            EnemyFlight flight = definition.flying ? root.AddComponent<EnemyFlight>() : null;

            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            brain.Wire(health, agent, combat, elite, visual, flight, muzzle);
            visual.CollectRenderers();
            visual.AttachHealth(health);
            elite.Wire(brain, health, agent, visual, combat);

            brain.Initialize(definition, player, rng, difficultyScale);

            // --- elites ---
            if (eliteModifierCount > 0)
                elite.Apply(PickModifiers(rng, eliteModifierCount), rng);

            return brain;
        }

        /// <summary>Picks distinct, balanced modifier combinations.</summary>
        public static List<EliteModifierDefinition> PickModifiers(RngService rng, int count)
        {
            var pool = new List<EliteModifierDefinition>(Content.GameContent.EliteModifiers);
            var picked = new List<EliteModifierDefinition>(count);

            // Balance rules: at most one defensive multiplier and one explosive
            // modifier per elite, so combinations stay fair.
            bool hasExplosive = false, hasHealth = false;
            while (picked.Count < count && pool.Count > 0)
            {
                EliteModifierDefinition candidate = rng.Pick(pool);
                pool.Remove(candidate);

                if (candidate.kind == EliteModifierKind.Explosive && hasExplosive) continue;
                if (candidate.kind == EliteModifierKind.Armored && hasHealth) continue;

                if (candidate.kind == EliteModifierKind.Explosive) hasExplosive = true;
                if (candidate.kind == EliteModifierKind.Armored) hasHealth = true;
                picked.Add(candidate);
            }
            return picked;
        }

        // ---------------------------------------------------------------- bodies

        static Transform BuildBody(EnemyDefinition def, Transform parent)
        {
            GameObject body;
            switch (def.kind)
            {
                case EnemyKind.Rusher:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .9f, 0f), Vector3.one * .78f, MaterialLibrary.EnemyRusher);
                    AddEye(body.transform, new Color(1f, .6f, .1f), new Vector3(0f, .35f, .32f));
                    AddFin(body.transform, MaterialLibrary.EnemyRusher, new Vector3(.22f, .55f, -.1f), new Vector3(.06f, .5f, .3f), new Vector3(0f, 0f, -35f));
                    AddFin(body.transform, MaterialLibrary.EnemyRusher, new Vector3(-.22f, .55f, -.1f), new Vector3(.06f, .5f, .3f), new Vector3(0f, 0f, 35f));
                    break;

                case EnemyKind.Soldier:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .95f, 0f), new Vector3(.95f, 1f, .95f), MaterialLibrary.EnemyBody);
                    AddShoulder(body.transform, .3f);
                    AddEye(body.transform, new Color(1f, .2f, .15f), new Vector3(0f, .42f, .3f));
                    break;

                case EnemyKind.Assault:
                    body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, 1f, 0f), new Vector3(.85f, 1.15f, .55f), MaterialLibrary.EnemyBody);
                    AddPlate(body.transform, new Vector3(0f, .3f, .3f), new Vector3(.6f, .5f, .15f));
                    AddEye(body.transform, new Color(1f, .5f, .1f), new Vector3(0f, .45f, .3f));
                    break;

                case EnemyKind.Shotgunner:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .95f, 0f), new Vector3(1.2f, 1f, 1.05f), MaterialLibrary.EnemyBody);
                    AddShoulder(body.transform, .42f);
                    AddEye(body.transform, new Color(1f, .15f, .1f), new Vector3(0f, .4f, .42f));
                    break;

                case EnemyKind.Sniper:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, 1.05f, 0f), new Vector3(.78f, 1.1f, .78f), MaterialLibrary.EnemySniper);
                    AddPlate(body.transform, new Vector3(0f, .55f, .28f), new Vector3(.5f, .35f, .12f));
                    AddEye(body.transform, new Color(.2f, 1f, .5f), new Vector3(0f, .48f, .3f));
                    break;

                case EnemyKind.Medic:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .95f, 0f), new Vector3(.95f, 1f, .95f), MaterialLibrary.EnemyMedic);
                    AddCross(body.transform);
                    AddBackpack(body.transform, MaterialLibrary.EnemyMedic);
                    AddEye(body.transform, new Color(.2f, 1f, .4f), new Vector3(0f, .42f, .3f));
                    break;

                case EnemyKind.Explosive:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .95f, 0f), new Vector3(1.15f, .95f, 1.1f), MaterialLibrary.EnemyBody);
                    AddTube(body.transform);
                    AddPlate(body.transform, new Vector3(0f, .35f, .32f), new Vector3(.7f, .5f, .14f));
                    AddEye(body.transform, new Color(1f, .8f, .1f), new Vector3(0f, .42f, .38f));
                    break;

                case EnemyKind.Heavy:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, 1.05f, 0f), new Vector3(1.45f * .8f, 1.25f, 1.25f), MaterialLibrary.EnemyHeavy);
                    AddShoulder(body.transform, .55f);
                    AddPlate(body.transform, new Vector3(0f, .2f, .42f), new Vector3(1f, .6f, .16f));
                    AddEye(body.transform, new Color(1f, .3f, .1f), new Vector3(0f, .5f, .5f));
                    break;

                case EnemyKind.Tank:
                    body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, 1.25f, 0f), new Vector3(2f, 1.7f, 1.5f), MaterialLibrary.EnemyHeavy);
                    AddTread(body.transform, new Vector3(1.15f, 0f, 0f));
                    AddTread(body.transform, new Vector3(-1.15f, 0f, 0f));
                    AddPlate(body.transform, new Vector3(0f, .4f, .8f), new Vector3(1.4f, 1f, .2f));
                    AddEye(body.transform, new Color(1f, .2f, .1f), new Vector3(0f, .55f, .85f));
                    break;

                case EnemyKind.Drone:
                    body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    body.name = "Body";
                    SetPart(body, parent, Vector3.zero, Vector3.one * .9f, MaterialLibrary.EnemyDrone);
                    AddRotor(body.transform, new Vector3(.55f, .25f, .55f));
                    AddRotor(body.transform, new Vector3(-.55f, .25f, .55f));
                    AddRotor(body.transform, new Vector3(.55f, .25f, -.55f));
                    AddRotor(body.transform, new Vector3(-.55f, .25f, -.55f));
                    AddEye(body.transform, new Color(.3f, .9f, 1f), new Vector3(0f, -.05f, .45f), scale: .18f);
                    break;

                default: // Grunt - the original prototype drone, preserved.
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SetPart(body, parent, new Vector3(0f, .95f, 0f), Vector3.one, MaterialLibrary.EnemyBody);
                    AddEye(body.transform, new Color(1f, .3f, .2f), new Vector3(0f, .4f, .3f));
                    break;
            }

            body.transform.localScale *= def.bodyScale;
            return body.transform;
        }

        static Transform BuildHead(EnemyDefinition def, Transform parent)
        {
            if (def.kind == EnemyKind.Drone || def.kind == EnemyKind.Boss) return parent;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            float scale = def.kind == EnemyKind.Tank ? .7f : .5f;
            SetPart(head, parent, new Vector3(0f, 1.55f * def.bodyScale, 0f), Vector3.one * scale, MaterialLibrary.EnemyBody);
            head.AddComponent<HitZoneMarker>().zone = HitZone.Head;
            return head.transform;
        }

        static Transform BuildWeapon(EnemyDefinition def, Transform parent)
        {
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(parent, false);

            switch (def.attackStyle)
            {
                case AttackStyle.Melee:
                    muzzle.transform.localPosition = new Vector3(0f, 1f, .6f);
                    break;
                case AttackStyle.Hitscan:
                {
                    var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gun.name = "Gun";
                    Object.Destroy(gun.GetComponent<Collider>());
                    gun.transform.SetParent(parent, false);
                    gun.transform.localPosition = new Vector3(.35f, 1.1f, .35f);
                    gun.transform.localScale = def.kind == EnemyKind.Sniper
                        ? new Vector3(.09f, .09f, 1.1f)
                        : new Vector3(.12f, .14f, .6f);
                    gun.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GunDark;
                    muzzle.transform.localPosition = new Vector3(.35f, 1.1f, def.kind == EnemyKind.Sniper ? .95f : .7f);
                    break;
                }
                case AttackStyle.Projectile:
                {
                    var tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tube.name = "Launcher";
                    Object.Destroy(tube.GetComponent<Collider>());
                    tube.transform.SetParent(parent, false);
                    tube.transform.localPosition = new Vector3(.35f, 1.2f, .3f);
                    tube.transform.localScale = new Vector3(.18f, .18f, .55f);
                    tube.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GunDark;
                    muzzle.transform.localPosition = new Vector3(.35f, 1.2f, .65f);
                    break;
                }
                default:
                    muzzle.transform.localPosition = new Vector3(0f, 1.2f, .5f);
                    break;
            }
            return muzzle.transform;
        }

        // ---------------------------------------------------------------- part helpers

        static void SetPart(GameObject go, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static readonly Dictionary<Color, Material> eyeMaterials = new Dictionary<Color, Material>();

        static Material EyeMaterial(Color glow)
        {
            if (eyeMaterials.TryGetValue(glow, out Material cached)) return cached;
            Material material = MaterialLibrary.Emissive("Eye_" + ColorUtility.ToHtmlStringRGB(glow), glow * .3f, glow, 3f);
            eyeMaterials[glow] = material;
            return material;
        }

        static void AddEye(Transform parent, Color glow, Vector3 localPos, float scale = .14f)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            Object.Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = Vector3.one * scale;
            eye.GetComponent<Renderer>().sharedMaterial = EyeMaterial(glow);
        }

        static void AddFin(Transform parent, Material material, Vector3 localPos, Vector3 scale, Vector3 rotation)
        {
            var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "Fin";
            Object.Destroy(fin.GetComponent<Collider>());
            fin.transform.SetParent(parent, false);
            fin.transform.localPosition = localPos;
            fin.transform.localScale = scale;
            fin.transform.localRotation = Quaternion.Euler(rotation);
            fin.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void AddShoulder(Transform parent, float offset)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "ShoulderPad";
            Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(offset, .45f, 0f);
            pad.transform.localScale = new Vector3(.28f, .22f, .5f);
            pad.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.EnemyHeavy;
        }

        static void AddPlate(Transform parent, Vector3 localPos, Vector3 scale)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "ArmorPlate";
            Object.Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = localPos;
            plate.transform.localScale = scale;
            plate.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.MetalDark;
        }

        static void AddCross(Transform parent)
        {
            Material glow = MaterialLibrary.GlowGreen;
            var cross1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cross1.name = "CrossH";
            Object.Destroy(cross1.GetComponent<Collider>());
            cross1.transform.SetParent(parent, false);
            cross1.transform.localPosition = new Vector3(0f, .35f, .3f);
            cross1.transform.localScale = new Vector3(.3f, .09f, .05f);
            cross1.GetComponent<Renderer>().sharedMaterial = glow;

            var cross2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cross2.name = "CrossV";
            Object.Destroy(cross2.GetComponent<Collider>());
            cross2.transform.SetParent(parent, false);
            cross2.transform.localPosition = new Vector3(0f, .35f, .3f);
            cross2.transform.localScale = new Vector3(.09f, .3f, .05f);
            cross2.GetComponent<Renderer>().sharedMaterial = glow;
        }

        static void AddBackpack(Transform parent, Material material)
        {
            var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pack.name = "MedPack";
            Object.Destroy(pack.GetComponent<Collider>());
            pack.transform.SetParent(parent, false);
            pack.transform.localPosition = new Vector3(0f, .45f, -.3f);
            pack.transform.localScale = new Vector3(.4f, .5f, .25f);
            pack.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void AddTube(Transform parent)
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "ShoulderTube";
            Object.Destroy(tube.GetComponent<Collider>());
            tube.transform.SetParent(parent, false);
            tube.transform.localPosition = new Vector3(-.4f, .6f, 0f);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localScale = new Vector3(.25f, .5f, .25f);
            tube.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GunDark;
        }

        static void AddTread(Transform parent, Vector3 offset)
        {
            var tread = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tread.name = "Tread";
            Object.Destroy(tread.GetComponent<Collider>());
            tread.transform.SetParent(parent, false);
            tread.transform.localPosition = offset;
            tread.transform.localScale = new Vector3(.35f, 1.1f, 1.7f);
            tread.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GunDark;
        }

        static void AddRotor(Transform parent, Vector3 offset)
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "RotorArm";
            Object.Destroy(arm.GetComponent<Collider>());
            arm.transform.SetParent(parent, false);
            arm.transform.localPosition = offset * .5f;
            arm.transform.localScale = new Vector3(Mathf.Abs(offset.x) * .9f + .05f, .04f, .05f);
            arm.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GunDark;

            var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rotor.name = "Rotor";
            Object.Destroy(rotor.GetComponent<Collider>());
            rotor.transform.SetParent(parent, false);
            rotor.transform.localPosition = offset;
            rotor.transform.localScale = new Vector3(.3f, .02f, .3f);
            rotor.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.GlowCyan;
        }
    }
}
