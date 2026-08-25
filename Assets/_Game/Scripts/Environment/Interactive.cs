using System.Collections.Generic;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Visual;

namespace RogueArena.Environment
{
    /// <summary>
    /// Reusable interactive environment systems: explosive barrels, breakables,
    /// sliding doors, moving platforms/elevators, hazard zones, switches and
    /// automated turrets. All modular components, all safe against missing links.
    /// </summary>
    public static class ArenaRegistry
    {
        static readonly Dictionary<string, GameObject> byId = new Dictionary<string, GameObject>();
        public static void Register(string id, GameObject go) => byId[id] = go;
        public static GameObject Find(string id) => byId.TryGetValue(id, out GameObject go) ? go : null;
        public static void Clear() => byId.Clear();
    }

    // ---------------------------------------------------------------- barrel

    /// <summary>Explodes when destroyed, chains into other barrels.</summary>
    public class ExplosiveBarrel : MonoBehaviour
    {
        public float radius = 5f;
        public float damage = 70f;

        Health health;
        bool exploded;

        void Awake()
        {
            health = GetComponent<Health>();
            if (health != null) health.Died += OnDied;
        }

        void OnDied(DamageInfo info)
        {
            if (exploded) return;
            exploded = true;

            Services.Effects?.Explosion(transform.position, radius);
            Services.Audio?.Play("explosion", .85f, 1f, transform.position);
            Services.Session?.ShakeCamera(.6f);

            // Source passes through so barrel kills credit whoever shot the barrel.
            ExplosionDamage.Explode(transform.position, radius, damage, gameObject,
                DamageType.Explosive, 7f, 0f);

            Destroy(gameObject, .05f);
        }
    }

    // ---------------------------------------------------------------- breakable

    /// <summary>Crate/prop that bursts into debris and occasionally drops loot.</summary>
    public class BreakableObject : MonoBehaviour
    {
        public float scrapChance = .3f;

        void Awake()
        {
            var health = GetComponent<Health>();
            if (health != null) health.Died += OnDied;
        }

        void OnDied(DamageInfo info)
        {
            Services.Effects?.DeathBurst(transform.position, new Color(.55f, .4f, .22f), .8f);
            Services.Audio?.Play("crate_break", .7f, Random.Range(.9f, 1.15f), transform.position);

            if (Services.Rng != null && Services.Rng.Chance(scrapChance))
                Pickups.PickupFactory.CreateCurrency(transform.position, 15);

            Destroy(gameObject, .05f);
        }
    }

    // ---------------------------------------------------------------- door

    /// <summary>Proximity sliding door. Opens for the player or any enemy nearby.</summary>
    public class SlidingDoor : MonoBehaviour
    {
        public Vector3 openOffset = new Vector3(0f, 3.4f, 0f);
        public float speed = 3f;
        public float triggerRadius = 3.2f;
        public string switchId; // optional: only opens via switch when set

        Transform leaf;
        Vector3 closedPos;
        Vector3 openPos;
        bool open;
        bool switchUnlocked = true;
        float lastAudio;

        static readonly Collider[] proximity = new Collider[8];

        public void SetLeaf(Transform doorLeaf)
        {
            leaf = doorLeaf;
            closedPos = leaf.localPosition;
            openPos = closedPos + openOffset;
            if (!string.IsNullOrEmpty(switchId)) switchUnlocked = false;
        }

        void Update()
        {
            if (leaf == null) return;

            bool wantsOpen = switchUnlocked && SomeoneNearby();
            if (wantsOpen != open)
            {
                open = wantsOpen;
                if (Time.time - lastAudio > .4f)
                {
                    lastAudio = Time.time;
                    Services.Audio?.Play("door", .5f, 1f, transform.position);
                }
            }

            Vector3 target = open ? openPos : closedPos;
            leaf.localPosition = Vector3.MoveTowards(leaf.localPosition, target, speed * Time.deltaTime);
        }

        bool SomeoneNearby()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, triggerRadius, proximity,
                Layers.PlayerMask | Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            return count > 0;
        }

        /// <summary>Switch hook: unlocks (or re-locks) the door.</summary>
        public void ToggleLock(bool unlocked) => switchUnlocked = unlocked;
    }

    // ---------------------------------------------------------------- moving platform

    /// <summary>
    /// Moving platform / elevator. Ping-pongs between two points and carries the
    /// player (CharacterController) standing on it — detected geometrically, since
    /// CharacterControllers do not generate trigger events against solid ground.
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 4f, 0f);
        public float speed = 1.6f;
        public float pauseTime = 1.2f;

        BoxCollider platformCollider;
        CharacterController playerController;
        Vector3 start;
        Vector3 end;
        float t;
        bool forward = true;
        float pauseTimer;
        Vector3 lastPosition;

        void Start()
        {
            platformCollider = GetComponent<BoxCollider>();
            start = transform.position;
            end = start + offset;
            lastPosition = start;
        }

        void Update()
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
                float direction = forward ? 1f : -1f;
                t = Mathf.MoveTowards(t, direction > 0 ? 1f : 0f, speed * Time.deltaTime / Mathf.Max(.1f, offset.magnitude));
                if (t >= 1f) { forward = false; pauseTimer = pauseTime; }
                else if (t <= 0f) { forward = true; pauseTimer = pauseTime; }
            }

            transform.position = Vector3.Lerp(start, end, t);

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            // Carry the player when their capsule rests on the platform surface.
            if (delta.x != 0f || delta.y != 0f || delta.z != 0f)
            {
                var player = Services.Session?.Player;
                if (player != null)
                {
                    if (playerController == null) playerController = player.GetComponent<CharacterController>();
                    if (playerController != null && platformCollider != null)
                    {
                        Vector3 playerBottom = player.position
                            + Vector3.up * (playerController.center.y - playerController.height * .5f);
                        Bounds bounds = platformCollider.bounds;
                        bool onTop = playerBottom.y >= bounds.max.y - .35f
                            && playerBottom.y <= bounds.max.y + .75f
                            && Mathf.Abs(playerBottom.x - bounds.center.x) < bounds.extents.x + .35f
                            && Mathf.Abs(playerBottom.z - bounds.center.z) < bounds.extents.z + .35f;
                        if (onTop) playerController.Move(delta);
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- hazard zone

    /// <summary>Cycling floor hazard: safe -> warning -> active -> cooldown.</summary>
    public class HazardZone : MonoBehaviour
    {
        public float damagePerSecond = 25f;
        public float cycleTime = 4f;
        public float activeTime = 1.6f;
        public float warningTime = 1f;
        public bool startActive = true;

        float timer;
        bool warned;
        bool active;
        Renderer visual;
        Material baseMaterial;
        static readonly Collider[] victims = new Collider[4];

        public void SetVisual(Renderer zoneVisual)
        {
            visual = zoneVisual;
            if (visual != null) baseMaterial = visual.sharedMaterial;
        }

        void Update()
        {
            timer += Time.deltaTime;

            if (!startActive)
            {
                if (timer < warningTime) Warn();
                else if (timer < warningTime + activeTime) Activate();
                else if (timer < warningTime + activeTime + cycleTime) Deactivate();
                else timer = 0f;
                return;
            }

            // startActive pattern: active first, then a safe window.
            if (timer < activeTime) Activate();
            else if (timer < activeTime + cycleTime) Deactivate();
            else timer = 0f;
        }

        void Warn()
        {
            if (warned) return;
            warned = true;
            if (visual != null) visual.sharedMaterial = MaterialLibrary.GlowYellow;
        }

        void Activate()
        {
            if (!active)
            {
                active = true;
                warned = false;
                if (visual != null) visual.sharedMaterial = MaterialLibrary.GlowRed;
                Services.Audio?.Play("electric", .5f, 1f, transform.position);
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .5f, 2.2f, victims,
                Layers.PlayerMask, QueryTriggerInteraction.Ignore);

            var vitals = Services.Session?.Vitals;
            if (count > 0 && vitals != null && vitals.IsAlive)
            {
                vitals.Health.TakeDamage(new DamageInfo(damagePerSecond * Time.deltaTime,
                    transform.position + Vector3.up, Vector3.up, DamageType.Electric, gameObject));
            }
        }

        void Deactivate()
        {
            if (active || warned)
            {
                active = false;
                warned = false;
                if (visual != null && baseMaterial != null) visual.sharedMaterial = baseMaterial;
            }
        }
    }

    // ---------------------------------------------------------------- switch

    /// <summary>Interactive switch toggling a registered target (door, hazard, alarm).</summary>
    public class InteractiveSwitch : MonoBehaviour
    {
        public string targetId = "door_1";
        public string switchId = "switch_1";

        bool on;
        PlayerInputReader input; // cached; player assembly exists for the whole scene

        void Update()
        {
            // E to toggle when the player is close and looking roughly at us.
            var player = Services.Session?.Player;
            if (player == null) { input = null; return; }

            if (input == null) input = player.GetComponent<PlayerInputReader>();
            if (input == null || !input.InteractPressed) return;

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > 3.2f) return;

            on = !on;
            Services.Audio?.Play("ui_click", .7f, on ? 1.1f : .9f);
            GameEvents.Notification?.Invoke(on ? "SYSTEM ONLINE" : "SYSTEM OFFLINE",
                on ? new Color(.3f, 1f, .5f) : new Color(1f, .5f, .3f), 1.5f);

            ApplyToTarget(ArenaRegistry.Find(targetId), on);
        }

        void ApplyToTarget(GameObject target, bool on)
        {
            if (target == null)
            {
                // "alarm" switches have no registered target: they alert everything.
                if (!string.Equals(targetId, "alarm", System.StringComparison.OrdinalIgnoreCase)) return;
                TriggerAlarm();
                return;
            }

            var door = target.GetComponent<SlidingDoor>();
            if (door != null) { door.ToggleLock(on); return; }

            var hazard = target.GetComponent<HazardZone>();
            if (hazard != null) { hazard.startActive = on; hazard.enabled = on; return; }

            TriggerAlarm();
        }

        void TriggerAlarm()
        {
            var spawner = Object.FindAnyObjectByType<AI.AgentSpawner>();
            if (spawner != null && Services.Session?.Player != null)
            {
                Services.Audio?.Play("alarm", .8f);
                foreach (AI.EnemyBrain enemy in spawner.AliveEnemies)
                    enemy?.HearNoise(Services.Session.Player.position, 999f);
            }
        }
    }

    // ---------------------------------------------------------------- turret

    /// <summary>Automated hostile turret: tracks, telegraphs, fires bursts. Destructible.</summary>
    public class AutoTurret : MonoBehaviour
    {
        public float range = 24f;
        public float damage = 5f;
        public float burstInterval = 1.4f;
        public float rotationSpeed = 90f;

        Transform head;
        Transform muzzle;
        Health health;
        Transform player;
        PlayerVitals playerVitals;
        float fireTimer;
        float burstShots;
        float nextShotTime;

        public void Setup(Transform turretHead, Transform turretMuzzle)
        {
            head = turretHead;
            muzzle = turretMuzzle;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            if (health != null)
            {
                health.Died += _ =>
                {
                    Services.Effects?.Explosion(transform.position, 3f);
                    Services.Audio?.Play("turret_die", .8f, 1f, transform.position);
                    Destroy(gameObject, .05f);
                };
            }
        }

        void Update()
        {
            if (player == null && Services.Session?.Player != null)
            {
                player = Services.Session.Player;
                playerVitals = player.GetComponent<PlayerVitals>();
            }

            if (player == null || health == null || !health.IsAlive) return;
            if (playerVitals == null || !playerVitals.IsAlive) return;

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > range) return;

            Vector3 eye = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 target = player.position + Vector3.up * 1.1f;
            Vector3 direction = (target - eye).normalized;

            // LOS check.
            if (!Physics.Raycast(eye, direction, out _, distance, Layers.OccluderMask, QueryTriggerInteraction.Ignore))
            {
                // Track.
                if (head != null)
                    head.rotation = Quaternion.RotateTowards(head.rotation,
                        Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);

                // Fire.
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0f)
                {
                    fireTimer = burstInterval;
                    burstShots = 3;
                }
                if (burstShots > 0f && Time.time >= nextShotTime)
                {
                    burstShots -= 1f;
                    nextShotTime = Time.time + .12f;
                    Fire(direction);
                }
            }
        }

        void Fire(Vector3 direction)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, Layers.VisionMask, QueryTriggerInteraction.Ignore))
            {
                Services.Effects?.Tracer(origin, hit.point, new Color(1f, .3f, .3f));
                if (hit.collider.gameObject.layer == Layers.PlayerBody)
                {
                    var vitals = hit.collider.GetComponentInParent<PlayerVitals>();
                    vitals?.Health.TakeDamage(new DamageInfo(damage, hit.point, direction, DamageType.Bullet, gameObject));
                }
            }
            Services.Audio?.Play("turret_shot", .5f, 1f, origin);
        }
    }
}
