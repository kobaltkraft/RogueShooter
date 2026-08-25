using UnityEngine;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Weapons;

namespace RogueArena.Pickups
{
    /// <summary>
    /// A single world pickup: weapon, ammo, health, armour, powerup or currency.
    /// Auto-collects on touch, plays feedback, respects a re-trigger guard so it
    /// can never be collected twice in one frame, and respawns on a timer.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Weapon, Ammo, Health, Armor, Powerup, Currency }

        public Kind kind;
        public WeaponDefinition Weapon;
        public PowerupDefinition Powerup;
        public float Amount = 25f;
        public float RespawnSeconds = 25f;
        public bool OneShot;          // consumed permanently (weapon pickups in some arenas)

        Transform visual;
        float spinSpeed = 90f;
        float bobSpeed = 2.2f;
        float bobHeight = .12f;
        float baseY;
        float cooldownUntil;
        float respawnAt = -1f;
        bool collected;
        ParticleSystem sparkle;

        void Awake()
        {
            visual = transform.GetChild(0);
            baseY = visual.localPosition.y;
            BuildSparkle();
        }

        void BuildSparkle()
        {
            var go = new GameObject("Sparkle");
            go.transform.SetParent(visual, false);
            sparkle = go.AddComponent<ParticleSystem>();
            var main = sparkle.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .5f;
            main.startLifetime = .4f;
            main.startSpeed = 3f;
            main.startSize = .08f;
            main.maxParticles = 16;
            var emission = sparkle.emission;
            emission.enabled = false;
            var shape = sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .2f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = Visual.MaterialLibrary.SpriteMaterial(new Color(1f, 1f, .7f, .9f));
        }

        void Update()
        {
            // Respawn handling.
            if (collected)
            {
                if (OneShot) return;
                if (Time.time >= respawnAt && respawnAt > 0f)
                {
                    collected = false;
                    respawnAt = -1f;
                    visual.gameObject.SetActive(true);
                    Services.Effects?.Poof(transform.position + Vector3.up * .5f, new Color(.6f, .9f, 1f, .6f));
                }
                return;
            }

            // Idle animation.
            if (visual != null)
            {
                visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
                float y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                visual.localPosition = new Vector3(visual.localPosition.x, y, visual.localPosition.z);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (collected || Time.time < cooldownUntil) return;
            if (other.gameObject.layer != Layers.PlayerBody) return;

            var player = other.GetComponentInParent<PlayerAssemblyMarker>();
            if (player == null) return;

            Collect(player);
        }

        void Collect(PlayerAssemblyMarker player)
        {
            bool used = false;

            switch (kind)
            {
                case Kind.Weapon:
                    if (player.Weapons != null) { player.Weapons.PickupWeapon(Weapon); used = true; }
                    break;

                case Kind.Ammo:
                    if (player.Weapons != null)
                    {
                        player.Weapons.AddAmmoToAll(35);
                        Services.Audio?.Play("pickup_ammo", .7f);
                        used = true;
                    }
                    break;

                case Kind.Health:
                    player.Vitals?.Heal(Amount);
                    Services.Audio?.Play("pickup_health", .7f);
                    used = true;
                    break;

                case Kind.Armor:
                    player.Vitals?.AddArmor(Amount, 100f);
                    used = true;
                    break;

                case Kind.Powerup:
                    if (player.Powerups != null)
                    {
                        player.Powerups.Apply(Powerup);
                        used = true;
                    }
                    break;

                case Kind.Currency:
                    Services.Progression?.AddCurrency((int)Amount);
                    Services.Audio?.Play("pickup_ammo", .6f, 1.3f);
                    GameEvents.Notification?.Invoke($"+{Mathf.RoundToInt(Amount)} SCRAP", new Color(1f, .85f, .3f), 1.2f);
                    used = true;
                    break;
            }

            if (!used) return;

            collected = true;
            cooldownUntil = Time.time + .5f; // re-trigger guard
            sparkle?.Emit(12);
            Services.Effects?.Poof(transform.position + Vector3.up * .4f, new Color(.8f, .95f, 1f, .7f));

            if (OneShot)
            {
                // Consumed permanently: stop blocking physics and self-destruct
                // once the collect sparkle has played.
                GetComponent<Collider>().enabled = false;
                Destroy(gameObject, .8f);
            }
            else
            {
                respawnAt = Time.time + RespawnSeconds;
                visual.gameObject.SetActive(false);
            }
        }
    }
}
