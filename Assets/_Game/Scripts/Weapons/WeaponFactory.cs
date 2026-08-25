using UnityEngine;
using RogueArena.Visual;

namespace RogueArena.Weapons
{
    /// <summary>
    /// Builds procedural weapon view models. Every weapon has a distinct
    /// silhouette assembled from primitives with emissive accents - no external
    /// assets, and each weapon reads at a glance.
    /// </summary>
    public static class WeaponFactory
    {
        public struct WeaponBuild
        {
            public GameObject Root;
            public Weapon Weapon;
            public WeaponViewModel ViewModel;
            public Transform Muzzle;
            public ParticleSystem MuzzleFlash;
        }

        public static WeaponBuild Create(WeaponDefinition definition, Transform holdPoint)
        {
            var root = new GameObject("Weapon_" + definition.id);
            root.transform.SetParent(holdPoint, false);
            root.transform.localPosition = new Vector3(.28f, -.26f, .5f);
            root.transform.localRotation = Quaternion.identity;

            var modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(root.transform, false);

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(modelRoot.transform, false);

            BuildBody(definition, modelRoot.transform, muzzleGo.transform);

            var weapon = root.AddComponent<Weapon>();
            var viewModel = modelRoot.AddComponent<WeaponViewModel>();
            ParticleSystem flash = BuildMuzzleFlash(muzzleGo.transform, definition.muzzleColor, definition.muzzleScale);

            weapon.Initialize(definition, muzzleGo.transform, flash);
            viewModel.Initialize(weapon);

            return new WeaponBuild
            {
                Root = root,
                Weapon = weapon,
                ViewModel = viewModel,
                Muzzle = muzzleGo.transform,
                MuzzleFlash = flash,
            };
        }

        static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 size, Material material, Vector3? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(rotation ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void BuildBody(WeaponDefinition def, Transform model, Transform muzzle)
        {
            Material accent = MaterialLibrary.GunAccentCyan;
            if (def.accentColor.g > .8f && def.accentColor.b < .6f) accent = MaterialLibrary.GunAccentOrange;

            switch (def.id)
            {
                case "shotgun": BuildShotgun(model, muzzle, accent); break;
                case "rocket": BuildRocket(model, muzzle, accent); break;
                case "smg": BuildSmg(model, muzzle, accent); break;
                case "burst": BuildBurstRifle(model, muzzle, accent); break;
                case "marksman": BuildMarksman(model, muzzle, accent); break;
                case "plasma": BuildPlasma(model, muzzle); break;
                case "grenade": BuildGrenadeLauncher(model, muzzle, accent); break;
                default: BuildRifle(model, muzzle, accent); break;
            }
        }

        // ---------------------------------------------------------------- bodies

        static void BuildRifle(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, .05f), new Vector3(.11f, .15f, .5f), MaterialLibrary.Gunmetal);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .02f, .42f), new Vector3(.07f, .07f, .45f), MaterialLibrary.GunDark);
            Part("MuzzleTip", model, PrimitiveType.Cube, new Vector3(0, .02f, .62f), new Vector3(.09f, .09f, .1f), MaterialLibrary.GunDark);
            Part("Magazine", model, PrimitiveType.Cube, new Vector3(0, -.16f, .02f), new Vector3(.08f, .2f, .14f), MaterialLibrary.GunDark);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.14f, -.16f), new Vector3(.07f, .18f, .1f), MaterialLibrary.Gunmetal, new Vector3(14f, 0, 0));
            Part("Sight", model, PrimitiveType.Cube, new Vector3(0, .12f, .05f), new Vector3(.05f, .06f, .2f), accent);
            Part("Stock", model, PrimitiveType.Cube, new Vector3(0, -.02f, -.32f), new Vector3(.08f, .12f, .22f), MaterialLibrary.GunDark);
            muzzle.localPosition = new Vector3(0, .02f, .7f);
        }

        static void BuildShotgun(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, 0f), new Vector3(.15f, .16f, .42f), MaterialLibrary.Gunmetal);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .045f, .38f), new Vector3(.11f, .1f, .5f), MaterialLibrary.GunDark);
            Part("Tube", model, PrimitiveType.Cube, new Vector3(0, -.06f, .38f), new Vector3(.08f, .08f, .46f), MaterialLibrary.GunDark);
            Part("Pump", model, PrimitiveType.Cube, new Vector3(0, -.06f, .3f), new Vector3(.13f, .11f, .18f), accent);
            Part("Stock", model, PrimitiveType.Cube, new Vector3(0, -.05f, -.3f), new Vector3(.09f, .15f, .24f), MaterialLibrary.Crate);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.15f, -.14f), new Vector3(.08f, .16f, .1f), MaterialLibrary.Crate, new Vector3(16f, 0, 0));
            muzzle.localPosition = new Vector3(0, .045f, .66f);
        }

        static void BuildRocket(Transform model, Transform muzzle, Material accent)
        {
            Part("Tube", model, PrimitiveType.Cube, new Vector3(0, .02f, .1f), new Vector3(.19f, .19f, .8f), MaterialLibrary.GunDark);
            Part("TubeTip", model, PrimitiveType.Cube, new Vector3(0, .02f, .5f), new Vector3(.23f, .23f, .12f), accent);
            Part("Exhaust", model, PrimitiveType.Cube, new Vector3(0, .02f, -.32f), new Vector3(.15f, .15f, .12f), MaterialLibrary.Gunmetal);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.17f, -.08f), new Vector3(.08f, .18f, .11f), MaterialLibrary.Gunmetal, new Vector3(12f, 0, 0));
            Part("SightRing", model, PrimitiveType.Cube, new Vector3(0, .16f, .1f), new Vector3(.09f, .09f, .09f), accent);
            muzzle.localPosition = new Vector3(0, .02f, .62f);
        }

        static void BuildSmg(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, 0f), new Vector3(.09f, .13f, .34f), MaterialLibrary.Gunmetal);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .02f, .26f), new Vector3(.05f, .05f, .26f), MaterialLibrary.GunDark);
            Part("Magazine", model, PrimitiveType.Cube, new Vector3(0, -.16f, .02f), new Vector3(.07f, .22f, .1f), MaterialLibrary.GunDark);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.13f, -.12f), new Vector3(.06f, .15f, .08f), MaterialLibrary.Gunmetal, new Vector3(12f, 0, 0));
            Part("Sight", model, PrimitiveType.Cube, new Vector3(0, .1f, .02f), new Vector3(.04f, .05f, .12f), accent);
            muzzle.localPosition = new Vector3(0, .02f, .42f);
        }

        static void BuildBurstRifle(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, .02f), new Vector3(.1f, .14f, .46f), MaterialLibrary.Gunmetal);
            Part("UpperRail", model, PrimitiveType.Cube, new Vector3(0, .1f, .05f), new Vector3(.06f, .05f, .4f), MaterialLibrary.GunDark);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .01f, .4f), new Vector3(.05f, .05f, .4f), MaterialLibrary.GunDark);
            Part("AngledFore", model, PrimitiveType.Cube, new Vector3(0, -.04f, .3f), new Vector3(.08f, .08f, .2f), accent, new Vector3(-8f, 0, 0));
            Part("Magazine", model, PrimitiveType.Cube, new Vector3(0, -.16f, .04f), new Vector3(.07f, .2f, .12f), MaterialLibrary.GunDark);
            Part("Stock", model, PrimitiveType.Cube, new Vector3(0, 0f, -.32f), new Vector3(.07f, .12f, .2f), MaterialLibrary.GunDark);
            muzzle.localPosition = new Vector3(0, .01f, .62f);
        }

        static void BuildMarksman(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, 0f), new Vector3(.09f, .13f, .5f), MaterialLibrary.Gunmetal);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .02f, .48f), new Vector3(.045f, .045f, .55f), MaterialLibrary.GunDark);
            Part("MuzzleBrake", model, PrimitiveType.Cube, new Vector3(0, .02f, .74f), new Vector3(.07f, .07f, .09f), accent);
            Part("Scope", model, PrimitiveType.Cube, new Vector3(0, .14f, .02f), new Vector3(.05f, .05f, .3f), MaterialLibrary.GunDark);
            Part("ScopeLens", model, PrimitiveType.Cube, new Vector3(0, .14f, .18f), new Vector3(.045f, .045f, .03f), accent);
            Part("Magazine", model, PrimitiveType.Cube, new Vector3(0, -.14f, .06f), new Vector3(.06f, .14f, .1f), MaterialLibrary.GunDark);
            Part("Stock", model, PrimitiveType.Cube, new Vector3(0, -.01f, -.34f), new Vector3(.07f, .13f, .24f), MaterialLibrary.GunDark);
            muzzle.localPosition = new Vector3(0, .02f, .8f);
        }

        static void BuildPlasma(Transform model, Transform muzzle)
        {
            Material glow = MaterialLibrary.GlowCyan;
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, .02f), new Vector3(.12f, .14f, .44f), MaterialLibrary.Gunmetal);
            Part("Core", model, PrimitiveType.Cube, new Vector3(0, .01f, .18f), new Vector3(.08f, .08f, .2f), glow);
            Part("Coil1", model, PrimitiveType.Cube, new Vector3(0, .01f, .3f), new Vector3(.1f, .1f, .04f), glow);
            Part("Coil2", model, PrimitiveType.Cube, new Vector3(0, .01f, .4f), new Vector3(.09f, .09f, .04f), glow);
            Part("Coil3", model, PrimitiveType.Cube, new Vector3(0, .01f, .5f), new Vector3(.08f, .08f, .04f), glow);
            Part("Magazine", model, PrimitiveType.Cube, new Vector3(0, -.15f, .02f), new Vector3(.08f, .16f, .12f), MaterialLibrary.GunDark);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.13f, -.12f), new Vector3(.06f, .15f, .09f), MaterialLibrary.Gunmetal, new Vector3(12f, 0, 0));
            muzzle.localPosition = new Vector3(0, .01f, .6f);
        }

        static void BuildGrenadeLauncher(Transform model, Transform muzzle, Material accent)
        {
            Part("Receiver", model, PrimitiveType.Cube, new Vector3(0, 0, 0f), new Vector3(.13f, .15f, .36f), MaterialLibrary.Gunmetal);
            Part("Barrel", model, PrimitiveType.Cube, new Vector3(0, .02f, .34f), new Vector3(.13f, .13f, .36f), MaterialLibrary.GunDark);
            Part("Drum", model, PrimitiveType.Cube, new Vector3(0, -.09f, .12f), new Vector3(.15f, .12f, .15f), accent);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -.16f, -.1f), new Vector3(.07f, .16f, .1f), MaterialLibrary.Gunmetal, new Vector3(12f, 0, 0));
            Part("Stock", model, PrimitiveType.Cube, new Vector3(0, -.01f, -.26f), new Vector3(.08f, .12f, .18f), MaterialLibrary.GunDark);
            muzzle.localPosition = new Vector3(0, .02f, .56f);
        }

        // ---------------------------------------------------------------- muzzle flash

        static ParticleSystem BuildMuzzleFlash(Transform muzzle, Color color, float scale)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(muzzle, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .05f;
            main.startLifetime = .05f;
            main.startSpeed = 1.2f;
            main.startSize = .13f * scale;
            main.startColor = color;
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = .02f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1.4f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, .3f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.7f, .5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(color.r, color.g, color.b, .95f));
            renderer.alignment = ParticleSystemRenderSpace.Local;
            return ps;
        }
    }
}
