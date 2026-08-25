using System.Collections.Generic;
using UnityEngine;

namespace RogueArena.Visual
{
    /// <summary>
    /// Runtime material library - the project's cohesive palette.
    /// Creates URP Lit materials (with a built-in fallback) once per session and
    /// reuses them everywhere so the whole game shares one consistent look and
    /// Unity can batch aggressively.
    /// </summary>
    public static class MaterialLibrary
    {
        public static bool UsingUrp { get; private set; }

        // --- Environment ---
        public static Material FloorTech;
        public static Material Concrete;
        public static Material WallPanel;
        public static Material WallDark;
        public static Material Ceiling;
        public static Material Metal;
        public static Material MetalDark;
        public static Material Trim;
        public static Material Crate;
        public static Material Barrel;
        public static Material Sand;
        public static Material Sandstone;
        public static Material Asphalt;
        public static Material Brick;
        public static Material GlassDark;

        // --- Emissive accents (signs, strips, holo) ---
        public static Material GlowCyan;
        public static Material GlowOrange;
        public static Material GlowMagenta;
        public static Material GlowGreen;
        public static Material GlowRed;
        public static Material GlowYellow;
        public static Material HazardStripe;

        // --- Enemies ---
        public static Material EnemyBody;
        public static Material EnemyRusher;
        public static Material EnemyHeavy;
        public static Material EnemySniper;
        public static Material EnemyMedic;
        public static Material EnemyDrone;
        public static Material EnemyElite;
        public static Material Boss;

        // --- Weapons / effects ---
        public static Material Gunmetal;
        public static Material GunDark;
        public static Material GunAccentCyan;
        public static Material GunAccentOrange;
        public static readonly Dictionary<Color, Material> SpriteCache = new Dictionary<Color, Material>();

        static Shader litShader;
        static Shader spriteShader;

        public static void Initialize()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            UsingUrp = litShader != null;
            if (!UsingUrp)
            {
                litShader = Shader.Find("Standard");
                Debug.LogWarning("[Materials] URP Lit shader not found - falling back to Standard. " +
                                 "Assign the URP asset in Graphics settings for full visuals.");
            }
            spriteShader = Shader.Find("Sprites/Default");

            // --- Environment ---
            FloorTech = Lit("FloorTech", new Color(.13f, .15f, .19f), .1f, .42f);
            Concrete = Lit("Concrete", new Color(.32f, .32f, .34f), 0f, .78f);
            WallPanel = Lit("WallPanel", new Color(.20f, .24f, .30f), .05f, .5f);
            WallDark = Lit("WallDark", new Color(.12f, .13f, .17f), .1f, .55f);
            Ceiling = Lit("Ceiling", new Color(.07f, .08f, .10f), 0f, .7f);
            Metal = Lit("Metal", new Color(.45f, .48f, .52f), .85f, .38f);
            MetalDark = Lit("MetalDark", new Color(.19f, .21f, .24f), .8f, .45f);
            Trim = Lit("Trim", new Color(.62f, .66f, .70f), .5f, .5f);
            Crate = Lit("Crate", new Color(.42f, .32f, .18f), .05f, .72f);
            Barrel = Lit("Barrel", new Color(.55f, .16f, .10f), .6f, .5f);
            Sand = Lit("Sand", new Color(.78f, .68f, .46f), 0f, .95f);
            Sandstone = Lit("Sandstone", new Color(.66f, .55f, .38f), 0f, .85f);
            Asphalt = Lit("Asphalt", new Color(.16f, .16f, .18f), 0f, .82f);
            Brick = Lit("Brick", new Color(.25f, .17f, .15f), 0f, .88f);
            GlassDark = Lit("GlassDark", new Color(.08f, .13f, .18f), .9f, .12f);

            // --- Accents ---
            GlowCyan = Emissive("GlowCyan", new Color(.10f, .30f, .38f), new Color(.1f, .9f, 1f), 2.4f);
            GlowOrange = Emissive("GlowOrange", new Color(.35f, .18f, .06f), new Color(1f, .55f, .12f), 2.4f);
            GlowMagenta = Emissive("GlowMagenta", new Color(.30f, .08f, .30f), new Color(1f, .2f, .85f), 2.4f);
            GlowGreen = Emissive("GlowGreen", new Color(.08f, .30f, .12f), new Color(.25f, 1f, .4f), 2.2f);
            GlowRed = Emissive("GlowRed", new Color(.35f, .07f, .07f), new Color(1f, .15f, .1f), 2.4f);
            GlowYellow = Emissive("GlowYellow", new Color(.35f, .28f, .05f), new Color(1f, .85f, .2f), 2.2f);
            HazardStripe = Emissive("HazardStripe", new Color(.30f, .24f, .04f), new Color(.9f, .75f, .15f), 1.2f);

            // --- Enemies ---
            EnemyBody = Lit("EnemyBody", new Color(.62f, .18f, .12f), .35f, .5f);
            EnemyRusher = Lit("EnemyRusher", new Color(.85f, .38f, .08f), .2f, .45f);
            EnemyHeavy = Lit("EnemyHeavy", new Color(.24f, .26f, .30f), .8f, .4f);
            EnemySniper = Lit("EnemySniper", new Color(.55f, .60f, .48f), .3f, .5f);
            EnemyMedic = Lit("EnemyMedic", new Color(.80f, .86f, .82f), .2f, .55f);
            EnemyDrone = Lit("EnemyDrone", new Color(.25f, .30f, .36f), .75f, .35f);
            EnemyElite = Emissive("EnemyElite", new Color(.30f, .10f, .40f), new Color(.75f, .25f, 1f), 1.6f);
            Boss = Lit("Boss", new Color(.30f, .28f, .32f), .8f, .42f);

            // --- Weapons ---
            Gunmetal = Lit("Gunmetal", new Color(.22f, .24f, .28f), .8f, .35f);
            GunDark = Lit("GunDark", new Color(.11f, .12f, .14f), .7f, .45f);
            GunAccentCyan = Emissive("GunAccentCyan", new Color(.08f, .2f, .26f), new Color(.2f, .95f, 1f), 1.8f);
            GunAccentOrange = Emissive("GunAccentOrange", new Color(.3f, .15f, .05f), new Color(1f, .5f, .1f), 1.8f);
        }

        // ---------------------------------------------------------------- factories

        // Lit/Emissive materials are immutable once created, so they are shared by
        // name. Bounded: names come from a small fixed set of content colours.
        static readonly Dictionary<string, Material> nameCache = new Dictionary<string, Material>(128);

        public static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            if (nameCache.TryGetValue(name, out Material cached) && cached != null) return cached;
            var m = new Material(litShader) { name = name };
            SetColor(m, color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            nameCache[name] = m;
            return m;
        }

        public static Material Emissive(string name, Color baseColor, Color emission, float intensity)
        {
            if (nameCache.TryGetValue(name, out Material cached) && cached != null) return cached;
            var m = new Material(litShader) { name = name };
            SetColor(m, baseColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", .2f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .5f);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission * intensity);
            if (m.HasProperty("_EmissionIntensity")) m.SetFloat("_EmissionIntensity", intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            nameCache[name] = m;
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        public static Color GetColor(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
            : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        /// <summary>Transparent, unlit sprite material used by particles and tracers.</summary>
        public static Material SpriteMaterial(Color tint)
        {
            if (SpriteCache.TryGetValue(tint, out Material cached)) return cached;
            Shader shader = spriteShader != null ? spriteShader : Shader.Find("Sprites/Default");
            if (shader == null) shader = litShader;
            var m = new Material(shader) { name = "Sprite " + tint };
            SetColor(m, tint);
            SpriteCache[tint] = m;
            return m;
        }

        /// <summary>Replaces the shared material's color for a single renderer without touching the library.</summary>
        public static Material TintedClone(Material source, Color tint)
        {
            if (source == null) return null;
            var m = new Material(source);
            SetColor(m, tint);
            return m;
        }
    }
}
