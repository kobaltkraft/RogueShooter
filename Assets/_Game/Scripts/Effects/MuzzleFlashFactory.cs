using UnityEngine;

namespace RogueArena.Factories
{
    public static class MuzzleFlashFactory
    {
        public static ParticleSystem Create(Transform parent)
        {
            var flashObject = new GameObject("Muzzle Flash");
            flashObject.transform.SetParent(parent);
            flashObject.transform.localPosition = new Vector3(0, 0, .55f);
            ParticleSystem particles = flashObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = .04f;
            main.startSpeed = 0;
            main.startSize = .12f;
            main.startColor = new Color(1, .65f, .15f);
            main.playOnAwake = false;
            var emission = particles.emission;
            emission.enabled = false;
            return particles;
        }
    }
}
