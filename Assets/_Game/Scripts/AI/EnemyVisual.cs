using System.Collections.Generic;
using UnityEngine;
using RogueArena.Combat;
using RogueArena.Visual;

namespace RogueArena.AI
{
    /// <summary>
    /// Owns an enemy's visual state: hit flashes, attack charge glow, elite tint
    /// and elite crown. Built by the factory, driven by brain/combat/health events.
    /// </summary>
    public class EnemyVisual : MonoBehaviour
    {
        readonly List<Renderer> tintedRenderers = new List<Renderer>(8);
        readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>(8);
        readonly List<Material> instancedMaterials = new List<Material>(8);

        float flashTimer;
        float chargeLevel;
        Color flashColor = Color.white;
        Color eliteTint = Color.clear;
        float eliteTintAmount;
        Transform crown;
        Light coreLight;

        public Transform Crown => crown;

        public void CollectRenderers()
        {
            tintedRenderers.Clear();
            baseColors.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer) continue;
                // Instance the material so tinting never leaks into the shared library.
                var material = renderer.material;
                instancedMaterials.Add(material);
                renderer.sharedMaterial = material;
                tintedRenderers.Add(renderer);
                baseColors[renderer] = MaterialLibrary.GetColor(material);
            }
        }

        public void AttachHealth(Health health)
        {
            if (health == null) return;
            health.Damaged += (_, _) => Flash(Color.white, .12f);
        }

        public void Flash(Color color, float duration)
        {
            flashColor = color;
            flashTimer = duration;
        }

        /// <summary>0..1 pre-attack charge glow.</summary>
        public void SetCharge(float level) => chargeLevel = Mathf.Clamp01(level);

        public void ApplyEliteTint(Color tint, float amount)
        {
            eliteTint = tint;
            eliteTintAmount = amount;
        }

        public void AttachCrown(Transform crownTransform)
        {
            crown = crownTransform;
        }

        public void AttachCoreLight(Light light) => coreLight = light;

        void Update()
        {
            if (tintedRenderers.Count == 0) return;

            float dt = Time.deltaTime;
            if (flashTimer > 0f) flashTimer -= dt;

            bool flash = flashTimer > 0f;
            float charge = chargeLevel;

            for (int i = 0; i < tintedRenderers.Count; i++)
            {
                Renderer renderer = tintedRenderers[i];
                if (renderer == null) continue;

                Color baseColor = baseColors.TryGetValue(renderer, out Color c) ? c : Color.gray;
                Color target = baseColor;

                if (eliteTintAmount > 0f)
                    target = Color.Lerp(target, eliteTint, eliteTintAmount * .6f);

                if (charge > 0f)
                    target = Color.Lerp(target, new Color(1f, .35f, .15f), charge * .8f);

                if (flash)
                    target = Color.Lerp(target, flashColor, .75f);

                if (instancedMaterials[i] != null)
                    MaterialLibrary.SetColor(instancedMaterials[i], target);
            }

            if (coreLight != null)
                coreLight.intensity = Mathf.Lerp(coreLight.intensity, charge * 4f, 10f * dt);
        }

        void OnDestroy()
        {
            // Instanced materials must be freed explicitly.
            foreach (Material material in instancedMaterials)
                if (material != null) Destroy(material);
        }
    }
}
