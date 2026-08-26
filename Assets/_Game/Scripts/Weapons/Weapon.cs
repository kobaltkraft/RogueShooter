using System;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Combat;
using RogueArena.Core;
using RogueArena.Player;

namespace RogueArena.Weapons
{
    /// <summary>
    /// Runtime weapon logic for every weapon in the game. A single class driven by
    /// <see cref="WeaponDefinition"/> data handles auto/semi/burst/pump fire,
    /// hitscan and projectiles, heat-based energy weapons, reloads and upgrades.
    /// Reload uses a timer (not a coroutine) so pause, death and rapid switching
    /// can never corrupt state.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public WeaponDefinition Definition { get; private set; }
        public Transform Muzzle { get; private set; }
        public ParticleSystem MuzzleFlash { get; private set; }

        // --- state ---
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public float Heat { get; private set; }          // 0..1 for heat weapons
        public bool Overheated { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress01 { get; private set; }
        public float Cooldown01 => nextShotTime <= 0f ? 0f : Mathf.Clamp01((nextShotTime - Time.time) * EffectiveFireRate);

        // --- modifiers applied by controller (powerups / upgrades) ---
        public float DamageMultiplier = 1f;
        public float FireRateMultiplier = 1f;
        public float ReloadSpeedMultiplier = 1f;
        public float MagazineSizeMultiplier = 1f;
        public bool InfiniteAmmo;
        public float SpreadMultiplier = 1f;

        /// <summary>Current spread cone (degrees) for the dynamic crosshair.</summary>
        public float CurrentSpread { get; private set; }

        public event Action StateChanged;
        /// <summary>headshot, killed, damageType - fired when this weapon damages an enemy.</summary>
        public event Action<bool, bool, DamageType> EnemyHit;
        public event Action Fired;
        public event Action ReloadStarted;
        public event Action ReloadFinished;

        // internals
        WeaponController controller;
        Camera aim;
        float nextShotTime;
        float reloadEndTime;
        float overheatEndTime;
        float effectiveReloadTime;
        int burstShotsLeft;
        float nextBurstShotTime;
        bool burstActive;
        int effectiveMagazineSize;
        float effectiveDamage;

        public void Initialize(WeaponDefinition definition, Transform muzzle, ParticleSystem muzzleFlash)
        {
            Definition = definition;
            Muzzle = muzzle;
            MuzzleFlash = muzzleFlash;
            effectiveMagazineSize = Mathf.Max(1, definition.magazineSize);
            effectiveDamage = definition.damage;
            Magazine = definition.usesHeat ? 0 : effectiveMagazineSize;
            Reserve = definition.usesHeat ? 0 : definition.reserveAmmo;
            CurrentSpread = definition.spread;
        }

        /// <summary>Attaches the owning controller after both exist.</summary>
        public void Bind(WeaponController owner)
        {
            controller = owner;
            aim = owner != null ? owner.Camera : Camera.main;
        }

        /// <summary>Re-applies magazine size / damage after upgrades change.</summary>
        public void RefreshEffectiveStats()
        {
            int newMag = Mathf.Max(1, Mathf.RoundToInt(Definition.magazineSize * MagazineSizeMultiplier));
            int delta = newMag - effectiveMagazineSize;
            effectiveMagazineSize = newMag;
            if (delta > 0) Magazine = Mathf.Min(newMag, Magazine + delta);
            Magazine = Mathf.Min(Magazine, newMag);
            effectiveDamage = Definition.damage * DamageMultiplier;
            StateChanged?.Invoke();
        }

        public float EffectiveFireRate => Definition.fireRate * Mathf.Max(.1f, FireRateMultiplier);
        public int EffectiveMagazineSize => effectiveMagazineSize;
        public float EffectiveDamage => effectiveDamage;
        public bool UsesHeat => Definition.usesHeat;
        public bool HasAmmo => Definition.usesHeat ? !Overheated : (Magazine > 0 || Reserve > 0);

        /// <summary>Per-frame update. Called by the controller only while gameplay runs.</summary>
        public void Tick(bool fireHeld, bool firePressed)
        {
            if (Definition == null) return;

            float time = Time.time;

            // --- heat dissipation ---
            if (Definition.usesHeat)
            {
                if (Overheated)
                {
                    if (time >= overheatEndTime && Heat <= .05f)
                    {
                        Overheated = false;
                        Heat = 0f;
                        StateChanged?.Invoke();
                    }
                    else if (time >= overheatEndTime)
                    {
                        Heat = Mathf.Max(0f, Heat - Definition.heatCooldown * Time.deltaTime);
                    }
                }
                else if (Heat > 0f)
                {
                    Heat = Mathf.Max(0f, Heat - Definition.heatCooldown * Time.deltaTime);
                }
            }

            // --- reload timer ---
            if (IsReloading)
            {
                ReloadProgress01 = Mathf.Clamp01(1f - (reloadEndTime - time) / Mathf.Max(.01f, effectiveReloadTime));
                if (time >= reloadEndTime) FinishReload();
            }

            // --- burst continuation ---
            if (burstActive)
            {
                if (burstShotsLeft > 0 && time >= nextBurstShotTime)
                {
                    TryFireOneShot();
                    burstShotsLeft--;
                    nextBurstShotTime = time + Definition.burstDelay;
                    if (burstShotsLeft <= 0) burstActive = false;
                }
                return; // no new inputs mid-burst
            }

            // --- fire input ---
            bool canShoot = !IsReloading && !Overheated && time >= nextShotTime;
            bool wantsShot = Definition.fireMode switch
            {
                FireMode.Auto => fireHeld,
                FireMode.Semi or FireMode.Pump => firePressed,
                FireMode.Burst => firePressed,
                _ => false,
            };

            if (canShoot && wantsShot)
            {
                if (Definition.fireMode == FireMode.Burst)
                {
                    if (Definition.usesHeat ? !Overheated : Magazine > 0)
                    {
                        burstActive = true;
                        burstShotsLeft = Definition.burstCount;
                        nextBurstShotTime = time;
                    }
                    else TryFireOneShot(); // handles dry fire / auto reload
                }
                else
                {
                    TryFireOneShot();
                }
            }

            // Track current spread for the crosshair.
            float moveSpread = controller != null ? controller.GetSpreadState(Definition) : 0f;
            CurrentSpread = Mathf.Max(.1f, (Definition.spread + moveSpread) * SpreadMultiplier);
        }

        void TryFireOneShot()
        {
            if (IsReloading || Overheated) return;

            if (!Definition.usesHeat && Magazine <= 0)
            {
                if (Reserve > 0) TryReload();
                else Services.Audio?.Play("dry_fire", .5f);
                burstActive = false;
                return;
            }

            // Consume ammo / heat.
            if (Definition.usesHeat)
            {
                Heat += Definition.heatPerShot;
                if (Heat >= 1f)
                {
                    Heat = 1f;
                    Overheated = true;
                    overheatEndTime = Time.time + Definition.overheatLockout;
                    Services.Audio?.Play("dry_fire", .7f);
                    StateChanged?.Invoke();
                }
            }
            else if (!InfiniteAmmo)
            {
                Magazine--;
            }

            nextShotTime = Time.time + 1f / EffectiveFireRate;
            FireShot();
            StateChanged?.Invoke();

            if (!Definition.usesHeat && Magazine <= 0 && Reserve > 0)
                TryReload(); // auto reload on empty
        }

        void FireShot()
        {
            Vector3 origin = Muzzle != null ? Muzzle.position : aim.transform.position;
            Vector3 baseDir = aim.transform.forward;

            if (Definition.isProjectile)
            {
                Vector3 dir = ApplySpread(baseDir, CurrentSpread);
                var settings = Definition.projectile;
                Projectile.Spawn(origin + dir * .4f, dir, settings, effectiveDamage,
                    Projectile.Owner.Player, controller != null ? controller.gameObject : gameObject);
            }
            else
            {
                int pellets = Mathf.Max(1, Definition.pellets);
                for (int i = 0; i < pellets; i++)
                {
                    Vector3 dir = ApplySpread(baseDir, CurrentSpread);
                    FireHitscan(origin, dir);
                }
            }

            // --- feedback ---
            if (MuzzleFlash != null)
            {
                var main = MuzzleFlash.main;
                main.startSize = .14f * Definition.muzzleScale;
                main.startColor = Definition.muzzleColor;
                MuzzleFlash.Emit(Definition.pellets > 1 ? 10 : 6);
            }
            Services.Audio?.Play(Definition.shotSfx, Definition.sfxVolume, 1f + UnityEngine.Random.Range(-.04f, .04f), origin);
            GameEvents.NoiseEmitted?.Invoke(transform.position, 26f);
            Fired?.Invoke();
        }

        void FireHitscan(Vector3 origin, Vector3 direction)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, Definition.range, Layers.BulletHitMask, QueryTriggerInteraction.Ignore))
            {
                bool head = HitZoneMarker.IsHead(hit.collider);
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                bool killed = false;
                bool damaged = false;

                if (target != null && target.IsAlive)
                {
                    float damage = effectiveDamage * (head ? Definition.headshotMultiplier : 1f);
                    target.TakeDamage(new DamageInfo(damage, hit.point, direction, DamageType.Bullet,
                        controller != null ? controller.gameObject : gameObject, headshot: head,
                        knockback: Definition.knockback, normal: hit.normal));
                    killed = target is Health h && !h.IsAlive;
                    damaged = true;
                }

                Vector3 tracerFrom = Muzzle != null ? Muzzle.position : origin;
                Services.Effects?.Tracer(tracerFrom, hit.point, Definition.accentColor);
                if (damaged)
                {
                    EnemyHit?.Invoke(head, killed, DamageType.Bullet);
                    if (!killed) Services.Effects?.Impact(hit.point, hit.normal, Definition.accentColor);
                }
                else
                {
                    Services.Effects?.Impact(hit.point, hit.normal, new Color(.8f, .8f, .7f));
                    Services.Audio?.Play("impact_bullet", .35f, 1f, hit.point);
                }
            }
            else
            {
                Vector3 tracerFrom = Muzzle != null ? Muzzle.position : origin;
                Services.Effects?.Tracer(tracerFrom, origin + direction * Mathf.Min(Definition.range, 60f), Definition.accentColor);
            }
        }

        Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            if (spreadDegrees <= .01f) return direction;
            float radians = spreadDegrees * Mathf.Deg2Rad;
            Vector2 random = UnityEngine.Random.insideUnitCircle * Mathf.Tan(radians);
            Vector3 result = direction
                + aim.transform.right * random.x
                + aim.transform.up * random.y;
            return result.normalized;
        }

        // ---------------------------------------------------------------- reload

        public void TryReload()
        {
            if (Definition == null || Definition.usesHeat || IsReloading) return;
            if (Magazine >= effectiveMagazineSize || Reserve <= 0) return;

            effectiveReloadTime = Definition.reloadTime * Mathf.Max(.3f, ReloadSpeedMultiplier);
            reloadEndTime = Time.time + effectiveReloadTime;
            IsReloading = true;
            ReloadProgress01 = 0f;
            Services.Audio?.Play("reload_start", .6f);
            ReloadStarted?.Invoke();
            StateChanged?.Invoke();
        }

        void FinishReload()
        {
            IsReloading = false;
            int need = effectiveMagazineSize - Magazine;
            int take = Mathf.Min(need, Reserve);
            Magazine += take;
            if (!InfiniteAmmo) Reserve -= take;
            ReloadProgress01 = 0f;
            Services.Audio?.Play("reload_end", .6f);
            ReloadFinished?.Invoke();
            StateChanged?.Invoke();
        }

        public void CancelReload()
        {
            if (!IsReloading) return;
            IsReloading = false;
            ReloadProgress01 = 0f;
            StateChanged?.Invoke();
        }

        /// <summary>Adds reserve ammo (pickups). Returns amount actually added.</summary>
        public int AddAmmo(int amount)
        {
            if (Definition.usesHeat || amount <= 0) return 0;
            int before = Reserve;
            Reserve = Mathf.Min(Reserve + amount, Definition.reserveAmmo * 3);
            if (Reserve != before) StateChanged?.Invoke();
            return Reserve - before;
        }

        /// <summary>Refills to full (used between waves / debug).</summary>
        public void Refill()
        {
            if (Definition.usesHeat) { Heat = 0f; Overheated = false; }
            else { Magazine = effectiveMagazineSize; Reserve = Definition.reserveAmmo; }
            StateChanged?.Invoke();
        }
    }
}
