using System;
using System.Collections.Generic;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Progression;

namespace RogueArena.Weapons
{
    /// <summary>
    /// Owns the player's weapon inventory: building view models, switching
    /// (keys 1-3, mouse wheel), pickups, ammo, and applying upgrade/powerup
    /// modifiers to every weapon. Designed to survive rapid switching, reload
    /// interruptions and death mid-animation without corrupting state.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        public const int MaxSlots = 3;

        public Camera Camera { get; private set; }
        public Weapon Current => index >= 0 && index < weapons.Count ? weapons[index] : null;
        public string CurrentWeaponId => Current?.Definition?.id;
        public IReadOnlyList<Weapon> Weapons => weapons;
        public int CurrentIndex => index;
        public int SlotCount => weapons.Count;

        /// <summary>headshot, killed - for the HUD hit marker.</summary>
        public event Action<bool, bool> HitMarker;
        public event Action<Weapon> Switched;

        readonly List<Weapon> weapons = new List<Weapon>(MaxSlots);
        readonly List<WeaponViewModel> viewModels = new List<WeaponViewModel>(MaxSlots);
        PlayerInputReader input;
        FirstPersonController locomotion;
        CameraRig rig;
        PlayerPowerups powerups;
        int index = -1;

        // switch animation state
        enum SwitchState { Stable, Lowering, Raising }
        SwitchState switchState;
        float switchTimer;
        int pendingIndex;
        float lower01;
        const float LowerTime = .13f;
        const float RaiseTime = .16f;

        bool enabledInput = true;

        public void Initialize(Camera camera, PlayerInputReader inputReader, FirstPersonController locomotionRef, CameraRig cameraRig)
        {
            Camera = camera;
            input = inputReader;
            locomotion = locomotionRef;
            rig = cameraRig;

            powerups = GetComponent<PlayerPowerups>();
            if (powerups != null)
            {
                powerups.EffectsChanged += ApplyPowerups;
                ApplyPowerups();
            }
        }

        void OnDestroy()
        {
            if (powerups != null) powerups.EffectsChanged -= ApplyPowerups;
        }

        /// <summary>Adds a weapon to the loadout and switches to it if it's the first.</summary>
        public void AddWeapon(WeaponDefinition definition)
        {
            if (definition == null) return;

            var holdPoint = Camera != null ? Camera.transform : transform;
            WeaponFactory.WeaponBuild build = WeaponFactory.Create(definition, holdPoint);
            build.Weapon.Bind(this);
            build.ViewModel.Bind(rig);

            // Apply purchased weapon upgrade tracks from the save.
            if (Services.Progression != null)
            {
                string id = definition.id;
                int damageTier = Services.Progression.WeaponUpgradeTier(id, WeaponUpgradeTracks.Damage);
                int magazineTier = Services.Progression.WeaponUpgradeTier(id, WeaponUpgradeTracks.Magazine);
                int reloadTier = Services.Progression.WeaponUpgradeTier(id, WeaponUpgradeTracks.Reload);
                upgradeDamage[build.Weapon] = WeaponUpgradeTracks.DamageMultiplier(damageTier);
                build.Weapon.MagazineSizeMultiplier = WeaponUpgradeTracks.MagazineMultiplier(magazineTier);
                build.Weapon.ReloadSpeedMultiplier = WeaponUpgradeTracks.ReloadMultiplier(reloadTier);
            }

            // Feedback hooks.
            build.Weapon.Fired += () => OnWeaponFired(build.Weapon);
            build.Weapon.EnemyHit += (head, killed, _) => HitMarker?.Invoke(head, killed);

            weapons.Add(build.Weapon);
            viewModels.Add(build.ViewModel);
            build.Root.SetActive(false);

            if (weapons.Count == 1) EquipImmediate(0);
            else if (weapons.Count <= MaxSlots) RequestSwitch(weapons.Count - 1);
        }

        void OnWeaponFired(Weapon weapon)
        {
            if (weapon?.Definition == null || rig == null) return;
            var def = weapon.Definition;
            rig.AddRecoil(def.recoilPitch, UnityEngine.Random.Range(-def.recoilYaw, def.recoilYaw));
            rig.FireFovKick(def.fovKick * .4f);
            rig.AddShake(def.shakePerShot);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (!enabledInput || weapons.Count == 0) return;

            // No input while paused (time is frozen; ignore buffered presses too).
            if (Services.Flow != null && !Services.Flow.State.IsGameplay) return;

            // --- switching input ---
            if (input.Weapon1Pressed) RequestSwitch(0);
            else if (input.Weapon2Pressed) RequestSwitch(1);
            else if (input.Weapon3Pressed) RequestSwitch(2);
            else if (input.ScrollUp) RequestSwitch((index + 1) % weapons.Count);
            else if (input.ScrollDown) RequestSwitch((index - 1 + weapons.Count) % weapons.Count);

            // --- switch animation ---
            switch (switchState)
            {
                case SwitchState.Lowering:
                    switchTimer += Time.deltaTime;
                    lower01 = Mathf.Clamp01(switchTimer / LowerTime);
                    if (switchTimer >= LowerTime)
                    {
                        SwapTo(pendingIndex);
                        switchState = SwitchState.Raising;
                        switchTimer = 0f;
                    }
                    break;
                case SwitchState.Raising:
                    switchTimer += Time.deltaTime;
                    lower01 = 1f - Mathf.Clamp01(switchTimer / RaiseTime);
                    if (switchTimer >= RaiseTime)
                    {
                        lower01 = 0f;
                        switchState = SwitchState.Stable;
                    }
                    break;
            }

            Weapon weapon = Current;
            if (weapon == null) return;

            // Apply switch lower to the active view model.
            if (index < viewModels.Count && viewModels[index] != null)
                viewModels[index].SetSwitchLower(lower01);

            // Weapon only fires while fully raised.
            bool canFire = switchState == SwitchState.Stable && locomotion != null && locomotion.InputEnabled;
            if (input.ReloadPressed) weapon.TryReload();
            weapon.Tick(canFire && input.FireHeld, canFire && input.FirePressed);
        }

        // ---------------------------------------------------------------- switching

        /// <summary>Queues a weapon switch. Safe to spam; latest request wins.</summary>
        public void RequestSwitch(int target)
        {
            if (target < 0 || target >= weapons.Count || target == index) return;
            pendingIndex = target;
            if (switchState == SwitchState.Raising || switchState == SwitchState.Stable)
            {
                switchState = SwitchState.Lowering;
                switchTimer = 0f;
            }
            // If already lowering, the new pendingIndex is picked up at the swap.
        }

        void SwapTo(int target)
        {
            if (index >= 0 && index < weapons.Count) weapons[index].CancelReload();
            if (index >= 0 && index < viewModels.Count && viewModels[index] != null)
                viewModels[index].SetSwitchLower(1f);

            for (int i = 0; i < weapons.Count; i++)
                weapons[i].gameObject.SetActive(i == target);

            index = target;
            Services.Audio?.Play("ui_click", .35f, .8f);
            Switched?.Invoke(weapons[index]);
        }

        void EquipImmediate(int target)
        {
            for (int i = 0; i < weapons.Count; i++)
                weapons[i].gameObject.SetActive(i == target);
            index = target;
            lower01 = 0f;
            switchState = SwitchState.Raising;
            switchTimer = RaiseTime * .5f;
            Switched?.Invoke(weapons[index]);
        }

        // ---------------------------------------------------------------- pickups

        /// <summary>Handles a weapon pickup: refills ammo if owned, otherwise equips.</summary>
        public void PickupWeapon(WeaponDefinition definition)
        {
            if (definition == null) return;

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].Definition.id == definition.id)
                {
                    int added = weapons[i].AddAmmo(Mathf.Max(20, definition.reserveAmmo / 2));
                    if (added > 0) Services.Audio?.Play("pickup_ammo", .6f);
                    return;
                }
            }

            if (weapons.Count < MaxSlots)
            {
                AddWeapon(definition);
                Services.Audio?.Play("pickup_weapon", .7f);
                GameEvents.WeaponPickedUp?.Invoke(definition.id, definition.displayName);
                return;
            }

            // Inventory full: replace the current slot.
            ReplaceCurrent(definition);
            Services.Audio?.Play("pickup_weapon", .7f);
            GameEvents.WeaponPickedUp?.Invoke(definition.id, definition.displayName);
        }

        void ReplaceCurrent(WeaponDefinition definition)
        {
            Weapon old = Current;
            if (old != null)
            {
                old.CancelReload();
                int oldIndex = index;
                if (oldIndex < viewModels.Count && viewModels[oldIndex] != null)
                    viewModels[oldIndex].SetSwitchLower(1f);
                Destroy(old.gameObject);
                upgradeDamage.Remove(old);
                weapons.RemoveAt(oldIndex);
                viewModels.RemoveAt(oldIndex);
                // Force re-equip so the slot never ends up pointing at an
                // inactive weapon after the swap.
                index = -1;
            }
            AddWeapon(definition);
        }

        public void AddAmmoToAll(int fraction)
        {
            foreach (Weapon w in weapons)
                w.AddAmmo(Mathf.Max(10, w.Definition.reserveAmmo * fraction / 100));
        }

        public void RefillAll()
        {
            foreach (Weapon w in weapons) w.Refill();
        }

        // ---------------------------------------------------------------- modifiers

        /// <summary>Permanent damage multiplier from the WEAPON DAMAGE player upgrade.</summary>
        public void SetPermanentDamageMultiplier(float multiplier)
        {
            permanentDamageMultiplier = Mathf.Max(.1f, multiplier);
            ApplyPowerups();
        }

        float permanentDamageMultiplier = 1f;
        float waveFireRateMultiplier = 1f;
        // Per-weapon damage upgrade tiers (survive ApplyPowerups re-computation).
        readonly Dictionary<Weapon, float> upgradeDamage = new Dictionary<Weapon, float>();

        /// <summary>Wave modifier hook (Rapid Fire waves).</summary>
        public void SetWaveFireRateMultiplier(float multiplier)
        {
            waveFireRateMultiplier = Mathf.Max(.2f, multiplier);
            ApplyPowerups();
        }

        void ApplyPowerups()
        {
            float damage = permanentDamageMultiplier;
            float fireRate = waveFireRateMultiplier;
            bool infinite = false;

            if (powerups != null)
            {
                damage *= powerups.DamageMultiplier;
                fireRate *= powerups.FireRateMultiplier;
                infinite = powerups.InfiniteAmmo;
            }

            foreach (Weapon weapon in weapons)
            {
                upgradeDamage.TryGetValue(weapon, out float upgrade);
                weapon.DamageMultiplier = damage * (upgrade > 0f ? upgrade : 1f);
                weapon.FireRateMultiplier = fireRate;
                weapon.InfiniteAmmo = infinite;
                weapon.RefreshEffectiveStats();
            }
        }

        // ---------------------------------------------------------------- state helpers

        /// <summary>Extra spread from movement for the current weapon state.</summary>
        public float GetSpreadState(WeaponDefinition def)
        {
            if (locomotion == null) return 0f;
            float extra = 0f;
            if (locomotion.IsGrounded)
            {
                if (locomotion.HorizontalSpeed01 > .2f) extra += def.spreadMoving;
                if (locomotion.IsCrouching) extra -= def.spread * .4f;
            }
            else extra += def.spreadAir;
            return extra;
        }

        public void SetEnabled(bool value) => enabledInput = value;
    }
}
