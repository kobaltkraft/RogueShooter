using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;
using RogueArena.Visual;

namespace RogueArena.VFX
{
    /// <summary>
    /// Scene-local VFX director. Every combat effect in the game routes through
    /// here: impacts, tracers, explosions, death bursts, telegraphs and flash
    /// lights. Everything is pooled - spawning effects never allocates GameObjects.
    /// </summary>
    public class EffectDirector : MonoBehaviour
    {
        struct RingState
        {
            public MeshRenderer Renderer;
            public float From, To, Duration, StartTime;
            public Color Color;
        }

        struct TracerState
        {
            public LineRenderer Line;
            public Color Color;
            public float Alpha;
        }

        ComponentPool<ParticleSystem> impacts;
        ComponentPool<ParticleSystem> explosions;
        ComponentPool<ParticleSystem> bursts;
        ComponentPool<ParticleSystem> poofs;
        ComponentPool<LineRenderer> tracers;
        ComponentPool<MeshRenderer> rings;
        ComponentPool<Light> flashLights;

        readonly List<ParticleSystem> playingParticles = new List<ParticleSystem>(64);
        readonly List<TracerState> playingTracers = new List<TracerState>(32);
        readonly List<RingState> playingRings = new List<RingState>(8);
        readonly Queue<Light> pendingLightReturns = new Queue<Light>();

        Transform poolRoot;

        public static EffectDirector Create(Transform parent)
        {
            var go = new GameObject("Effect Director");
            go.transform.SetParent(parent, false);
            var director = go.AddComponent<EffectDirector>();
            director.Build();
            return director;
        }

        void Build()
        {
            poolRoot = new GameObject("Pooled VFX").transform;
            poolRoot.SetParent(transform, false);

            impacts = new ComponentPool<ParticleSystem>(BuildImpact, 14, Stop);
            explosions = new ComponentPool<ParticleSystem>(BuildExplosion, 4, Stop);
            bursts = new ComponentPool<ParticleSystem>(BuildBurst, 10, Stop);
            poofs = new ComponentPool<ParticleSystem>(BuildPoof, 6, Stop);
            tracers = new ComponentPool<LineRenderer>(BuildTracer, 16, line => line.enabled = false);
            rings = new ComponentPool<MeshRenderer>(BuildRing, 6, r => r.enabled = false);
            flashLights = new ComponentPool<Light>(BuildFlashLight, 3, l => l.enabled = false);
        }

        static void Stop(ParticleSystem ps) => ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // ---------------------------------------------------------------- builders

        ParticleSystem BuildImpact()
        {
            var go = new GameObject("Impact FX");
            go.transform.SetParent(poolRoot, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .3f;
            main.startLifetime = .26f;
            main.startSpeed = 5.5f;
            main.startSize = .055f;
            main.gravityModifier = 1.2f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = .03f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(1f, .8f, .4f, .95f));
            renderer.alignment = ParticleSystemRenderSpace.View;
            return ps;
        }

        ParticleSystem BuildExplosion()
        {
            var go = new GameObject("Explosion FX");
            go.transform.SetParent(poolRoot, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .7f);
            main.startSpeed = 12f;
            main.startSize = .7f;
            main.gravityModifier = .2f;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .2f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .85f, .4f), 0f), new GradientColorKey(new Color(1f, .3f, .1f), .5f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(1f, .7f, .3f, .95f));
            return ps;
        }

        ParticleSystem BuildBurst()
        {
            var go = new GameObject("Death Burst FX");
            go.transform.SetParent(poolRoot, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .3f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .35f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(1f, .4f, .25f, .95f));
            return ps;
        }

        ParticleSystem BuildPoof()
        {
            var go = new GameObject("Poof FX");
            go.transform.SetParent(poolRoot, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .5f;
            main.startLifetime = .5f;
            main.startSpeed = 1.6f;
            main.startSize = .5f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(.8f, .85f, 1f, .5f));
            return ps;
        }

        LineRenderer BuildTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(poolRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = .035f;
            line.endWidth = .012f;
            line.material = MaterialLibrary.SpriteMaterial(new Color(1f, .85f, .4f, .9f));
            line.enabled = false;
            return line;
        }

        MeshRenderer BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Telegraph Ring";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(poolRoot, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.material = MaterialLibrary.SpriteMaterial(new Color(1f, .3f, .2f, .55f));
            renderer.enabled = false;
            return renderer;
        }

        Light BuildFlashLight()
        {
            var go = new GameObject("Flash Light");
            go.transform.SetParent(poolRoot, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 0f;
            light.color = new Color(1f, .7f, .35f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            // Particles: return to pool once dead.
            for (int i = playingParticles.Count - 1; i >= 0; i--)
            {
                ParticleSystem ps = playingParticles[i];
                if (ps == null) { playingParticles.RemoveAt(i); continue; }
                if (!ps.IsAlive(true))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ReturnToPool(ps);
                    playingParticles.RemoveAt(i);
                }
            }

            // Tracers: fade alpha, then return.
            for (int i = playingTracers.Count - 1; i >= 0; i--)
            {
                TracerState state = playingTracers[i];
                if (state.Line == null) { playingTracers.RemoveAt(i); continue; }
                state.Alpha -= Time.deltaTime * 7f;
                if (state.Alpha <= 0f)
                {
                    tracers.Return(state.Line);
                    playingTracers.RemoveAt(i);
                }
                else
                {
                    state.Color.a = state.Alpha;
                    state.Line.startColor = state.Color;
                    state.Line.endColor = new Color(state.Color.r, state.Color.g, state.Color.b, state.Alpha * .4f);
                    playingTracers[i] = state;
                }
            }

            // Rings: expand + fade, then return.
            for (int i = playingRings.Count - 1; i >= 0; i--)
            {
                RingState ring = playingRings[i];
                if (ring.Renderer == null) { playingRings.RemoveAt(i); continue; }
                float t = (Time.time - ring.StartTime) / Mathf.Max(.01f, ring.Duration);
                if (t >= 1f)
                {
                    rings.Return(ring.Renderer);
                    playingRings.RemoveAt(i);
                }
                else
                {
                    float scale = Mathf.Lerp(ring.From, ring.To, Mathf.SmoothStep(0f, 1f, t));
                    ring.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
                    var mat = ring.Renderer.material;
                    Color c = ring.Color;
                    c.a = Mathf.Lerp(.6f, .05f, t);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                }
            }

            // Flash lights are returned by their coroutine.
        }

        void ReturnToPool(ParticleSystem ps)
        {
            switch (ps.name)
            {
                case "Impact FX": impacts.Return(ps); break;
                case "Explosion FX": explosions.Return(ps); break;
                case "Death Burst FX": bursts.Return(ps); break;
                case "Poof FX": poofs.Return(ps); break;
                default: Destroy(ps.gameObject); break;
            }
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Impact sparks. Energy impacts just use a different tint.</summary>
        public void Impact(Vector3 point, Vector3 normal, Color tint, bool energy = false)
        {
            ParticleSystem ps = impacts.Get();
            if (ps == null) return;

            ps.transform.position = point;
            ps.transform.rotation = Quaternion.LookRotation(normal);
            var main = ps.main;
            main.startColor = energy ? tint : new Color(tint.r * .9f + .3f, tint.g * .7f + .2f, tint.b * .4f, .95f);
            ps.Emit(energy ? 10 : 14);
            playingParticles.Add(ps);
        }

        /// <summary>Instant hit tracer line.</summary>
        public void Tracer(Vector3 from, Vector3 to, Color color)
        {
            LineRenderer line = tracers.Get();
            if (line == null) return;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.enabled = true;
            playingTracers.Add(new TracerState { Line = line, Color = color, Alpha = 1f });
        }

        /// <summary>Explosion visuals (particles + shockwave ring + light flash).</summary>
        public void Explosion(Vector3 position, float radius)
        {
            ParticleSystem ps = explosions.Get();
            if (ps != null)
            {
                ps.transform.position = position;
                var main = ps.main;
                main.startSpeed = radius * 2.6f;
                main.startSize = radius * .3f;
                ps.Emit(36);
                playingParticles.Add(ps);
            }

            Ring(position, radius * .4f, radius * 1.35f, new Color(1f, .55f, .2f, .8f), .45f);
            FlashLight(position, radius * 2.5f, new Color(1f, .6f, .25f), .3f);
        }

        /// <summary>Enemy death burst.</summary>
        public void DeathBurst(Vector3 position, Color tint, float scale = 1f)
        {
            ParticleSystem ps = bursts.Get();
            if (ps == null) return;
            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * Mathf.Clamp(scale, .5f, 3f);
            var main = ps.main;
            main.startColor = tint;
            ps.Emit(22);
            playingParticles.Add(ps);
        }

        /// <summary>Soft smoke puff (teleports, spawns).</summary>
        public void Poof(Vector3 position, Color tint)
        {
            ParticleSystem ps = poofs.Get();
            if (ps == null) return;
            ps.transform.position = position;
            var main = ps.main;
            main.startColor = tint;
            ps.Emit(10);
            playingParticles.Add(ps);
        }

        /// <summary>Expanding floor ring used for telegraphs and shockwaves.</summary>
        public void Ring(Vector3 position, float fromRadius, float toRadius, Color color, float duration)
        {
            MeshRenderer ring = rings.Get();
            if (ring == null) return;
            // Recolour the renderer's own material instance (created once in
            // BuildRing). Never assign a fresh material here - pooled renderers
            // would leak an instance on every play.
            Material ringMaterial = ring.material;
            if (ringMaterial.HasProperty("_Color")) ringMaterial.SetColor("_Color", color);
            if (ringMaterial.HasProperty("_BaseColor")) ringMaterial.SetColor("_BaseColor", color);
            ring.transform.position = position + Vector3.up * .06f;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(fromRadius, fromRadius, 1f);
            ring.enabled = true;
            playingRings.Add(new RingState
            {
                Renderer = ring,
                From = fromRadius,
                To = toRadius,
                Duration = duration,
                StartTime = Time.time,
                Color = color,
            });
        }

        /// <summary>Pooled point-light flash with automatic decay.</summary>
        public void FlashLight(Vector3 position, float range, Color color, float duration)
        {
            Light light = flashLights.Get();
            if (light == null) return;
            light.transform.position = position;
            light.range = range;
            light.color = color;
            light.intensity = 6f;
            light.enabled = true;
            StartCoroutine(DecayLight(light, duration));
        }

        System.Collections.IEnumerator DecayLight(Light light, float duration)
        {
            float t = 0f;
            while (t < duration && light != null)
            {
                t += Time.deltaTime;
                light.intensity = 6f * (1f - t / duration);
                yield return null;
            }
            if (light != null)
            {
                light.intensity = 0f;
                flashLights.Return(light);
            }
        }
    }
}
