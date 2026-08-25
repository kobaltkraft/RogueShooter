using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RogueArena.Core;
using RogueArena.Player;
using RogueArena.Weapons;

namespace RogueArena.UI
{
    /// <summary>
    /// The in-game heads-up display. Subscribes to global events and the local
    /// player assembly; every element is null-safe so the HUD survives any
    /// missing reference and rebuilds cheaply each scene load.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;

        // Player vitals.
        Image healthFill;
        Image armorFill;
        Image healthGhost;
        Text healthText;
        float ghostHealth = 1f;

        // Weapon.
        Text weaponName;
        Text ammoText;
        RectTransform magBarRoot;
        readonly List<Image> magSegments = new List<Image>(24);
        const int MaxSegments = 24;

        // Crosshair + hit marker.
        RectTransform crosshairRoot;
        readonly List<RectTransform> crosshairArms = new List<RectTransform>(4);
        Text comboText;
        Image hitMarker;
        float hitMarkerUntil;

        // Wave / score.
        Text waveText;
        Text scoreText;
        Text notificationText;
        Image notificationBacking;
        Color notificationColor = Color.white;
        float notificationUntil;

        // Boss bar.
        RectTransform bossRoot;
        Image bossFill;
        Text bossName;
        Text bossPhase;

        // Dash + powerups.
        readonly List<Image> dashPips = new List<Image>(3);
        RectTransform powerupRoot;
        readonly List<Text> powerupLabels = new List<Text>(6);
        readonly List<Image> powerupFills = new List<Image>(6);

        // Low-health vignette.
        Image damageVignette;
        float lastHealthFraction = 1f;

        WeaponController weapons;
        DashController dash;
        PlayerVitals vitals;
        PlayerPowerups powerups;
        float crosshairSpread;
        float crosshairBaseSize = 8f;

        public void Build(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            var root = canvas.transform;

            BuildVitals(root);
            BuildWeapon(root);
            BuildCrosshair(root);
            BuildTopBar(root);
            BuildBossBar(root);
            BuildDashAndPowerups(root);
            BuildNotification(root);
            BuildVignette(root);

            GameEvents.Notification += OnNotification;
            GameEvents.WavePrepared += OnWavePrepared;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WaveChanged += OnWaveChanged;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.BossSpawned += OnBossSpawned;
            GameEvents.BossHealthChanged += OnBossHealthChanged;
            GameEvents.BossPhaseChanged += OnBossPhaseChanged;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.PowerupStarted += OnPowerupStarted;
            GameEvents.PowerupEnded += OnPowerupEnded;
            GameEvents.WeaponPickedUp += OnWeaponPickedUp;

            // Delayed player binding (player spawns the same frame).
            StartCoroutine(BindWhenReady());
        }

        IEnumerator BindWhenReady()
        {
            for (int i = 0; i < 60 && Services.Session == null; i++) yield return null;
            if (Services.Session == null) yield break;

            weapons = Services.Session.Weapons;
            vitals = Services.Session.Vitals;
            powerups = Services.Session.Powerups;
            dash = Services.Session.Player != null ? Services.Session.Player.GetComponent<DashController>() : null;

            if (weapons != null)
            {
                weapons.HitMarker += OnHitMarker;
                weapons.Switched += OnWeaponSwitched;
                OnWeaponSwitched(null);
            }
        }

        void OnDestroy()
        {
            GameEvents.Notification -= OnNotification;
            GameEvents.WavePrepared -= OnWavePrepared;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WaveChanged -= OnWaveChanged;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.BossSpawned -= OnBossSpawned;
            GameEvents.BossHealthChanged -= OnBossHealthChanged;
            GameEvents.BossPhaseChanged -= OnBossPhaseChanged;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.PowerupStarted -= OnPowerupStarted;
            GameEvents.PowerupEnded -= OnPowerupEnded;
            GameEvents.WeaponPickedUp -= OnWeaponPickedUp;

            if (weapons != null)
            {
                weapons.HitMarker -= OnHitMarker;
                weapons.Switched -= OnWeaponSwitched;
            }
        }

        // ---------------------------------------------------------------- builders

        void BuildVitals(Transform root)
        {
            var panel = UiKit.Panel(root, "VitalsPanel", UiKit.PanelDark);
            UiKit.Stretch(panel.rectTransform, left: 24f, bottom: 24f);
            panel.rectTransform.sizeDelta = new Vector2(320f, 96f);

            healthText = UiKit.Label(panel.transform, "HealthText", "100", 30, UiKit.TextMain,
                TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch(healthText.rectTransform, right: 12f, top: 8f);
            healthText.rectTransform.sizeDelta = new Vector2(70f, 40f);

            var barBack = UiKit.Fill(panel.transform, "HealthBack", new Color(.08f, .1f, .13f, .95f));
            UiKit.Stretch(barBack.rectTransform, left: 16f, right: 96f, bottom: 16f);
            barBack.rectTransform.sizeDelta = new Vector2(0f, 18f);

            healthGhost = UiKit.Fill(barBack.transform, "Ghost", new Color(1f, .35f, .3f, .6f));
            UiKit.Stretch(healthGhost.rectTransform);
            healthFill = UiKit.Fill(barBack.transform, "Fill", new Color(.25f, .95f, .45f));
            UiKit.Stretch(healthFill.rectTransform);

            var armorBack = UiKit.Fill(panel.transform, "ArmorBack", new Color(.08f, .1f, .13f, .95f));
            UiKit.Stretch(armorBack.rectTransform, left: 16f, right: 96f, bottom: 38f);
            armorBack.rectTransform.sizeDelta = new Vector2(0f, 10f);
            armorFill = UiKit.Fill(armorBack.transform, "Fill", new Color(.45f, .7f, 1f));
            UiKit.Stretch(armorFill.rectTransform);
        }

        void BuildWeapon(Transform root)
        {
            var panel = UiKit.Panel(root, "WeaponPanel", UiKit.PanelDark);
            UiKit.Stretch(panel.rectTransform, right: 24f, bottom: 24f);
            panel.rectTransform.sizeDelta = new Vector2(340f, 110f);

            weaponName = UiKit.Label(panel.transform, "WeaponName", "AR-7 RIFLE", 24, UiKit.Accent,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(weaponName.rectTransform, left: 16f, right: 16f, top: 10f);
            weaponName.rectTransform.sizeDelta = new Vector2(0f, 30f);

            ammoText = UiKit.Label(panel.transform, "Ammo", "30 / 90", 32, UiKit.TextMain,
                TextAnchor.UpperRight, FontStyle.Bold);
            UiKit.Stretch(ammoText.rectTransform, right: 16f, top: 8f);
            ammoText.rectTransform.sizeDelta = new Vector2(160f, 40f);

            magBarRoot = new GameObject("MagBar").AddComponent<RectTransform>();
            magBarRoot.transform.SetParent(panel.transform, false);
            UiKit.Stretch(magBarRoot, left: 16f, right: 16f, bottom: 18f);
            magBarRoot.sizeDelta = new Vector2(0f, 16f);

            for (int i = 0; i < MaxSegments; i++)
            {
                var segment = UiKit.Fill(magBarRoot, "Seg", new Color(.2f, .95f, 1f, .9f));
                segment.rectTransform.anchorMin = new Vector2(i / (float)MaxSegments, 0f);
                segment.rectTransform.anchorMax = new Vector2((i + 1f) / MaxSegments, 1f);
                segment.rectTransform.offsetMin = new Vector2(1.5f, 1f);
                segment.rectTransform.offsetMax = new Vector2(-1.5f, -1f);
                magSegments.Add(segment);
            }
        }

        void BuildCrosshair(Transform root)
        {
            crosshairRoot = new GameObject("Crosshair").AddComponent<RectTransform>();
            crosshairRoot.transform.SetParent(root, false);
            crosshairRoot.anchorMin = crosshairRoot.anchorMax = new Vector2(.5f, .5f);
            crosshairRoot.sizeDelta = new Vector2(80f, 80f);

            Color crosshairColor = UiKit.Accent;
            string cosmetic = Services.Save?.Data?.progress?.crosshair;
            if (cosmetic == "gold") crosshairColor = UiKit.Gold;
            else if (cosmetic == "magenta") crosshairColor = new Color(1f, .3f, .9f);

            for (int i = 0; i < 4; i++)
            {
                var arm = UiKit.Fill(crosshairRoot, "Arm" + i, crosshairColor);
                arm.rectTransform.anchorMin = arm.rectTransform.anchorMax = new Vector2(.5f, .5f);
                arm.rectTransform.pivot = new Vector2(.5f, .5f);
                arm.rectTransform.sizeDelta = new Vector2(2.5f, 12f);
                crosshairArms.Add(arm.rectTransform);
            }

            hitMarker = UiKit.Fill(crosshairRoot, "HitMarker", Color.white);
            hitMarker.rectTransform.anchorMin = hitMarker.rectTransform.anchorMax = new Vector2(.5f, .5f);
            hitMarker.rectTransform.sizeDelta = new Vector2(14f, 14f);
            hitMarker.rectTransform.rotation = Quaternion.Euler(0f, 0f, 45f);
            hitMarker.color = Color.clear;

            comboText = UiKit.Label(crosshairRoot, "Combo", "x1", 22, UiKit.Gold,
                TextAnchor.LowerCenter, FontStyle.Bold);
            comboText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            comboText.text = "";
        }

        void BuildTopBar(Transform root)
        {
            var top = new GameObject("TopBar").AddComponent<RectTransform>();
            top.transform.SetParent(root, false);
            UiKit.Stretch(top, left: 0f, right: 0f, top: 0f);
            top.sizeDelta = new Vector2(0f, 90f);

            waveText = UiKit.Label(top, "Wave", "WAVE 1", 30, UiKit.TextMain, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Stretch(waveText.rectTransform);
            waveText.rectTransform.sizeDelta = new Vector2(0f, 40f);

            scoreText = UiKit.Label(top, "Score", "0", 22, UiKit.TextDim, TextAnchor.UpperCenter);
            UiKit.Stretch(scoreText.rectTransform, top: 42f);
            scoreText.rectTransform.sizeDelta = new Vector2(0f, 26f);
        }

        void BuildBossBar(Transform root)
        {
            bossRoot = new GameObject("BossBar").AddComponent<RectTransform>();
            bossRoot.transform.SetParent(root, false);
            UiKit.Stretch(bossRoot, left: 260f, right: 260f, top: 24f);
            bossRoot.sizeDelta = new Vector2(0f, 58f);
            bossRoot.gameObject.SetActive(false);

            var back = UiKit.Panel(bossRoot, "Back", new Color(.05f, .02f, .02f, .9f));
            UiKit.Stretch(back.rectTransform);
            back.rectTransform.sizeDelta = Vector2.zero;

            bossName = UiKit.Label(bossRoot, "Name", "VULCAN-9", 22, new Color(1f, .5f, .35f),
                TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Stretch(bossName.rectTransform, top: 2f);
            bossName.rectTransform.sizeDelta = new Vector2(0f, 24f);

            var barBack = UiKit.Fill(bossRoot, "BarBack", new Color(.12f, .06f, .06f, .95f));
            UiKit.Stretch(barBack.rectTransform, left: 14f, right: 14f, bottom: 8f);
            barBack.rectTransform.sizeDelta = new Vector2(0f, 14f);
            bossFill = UiKit.Fill(barBack.transform, "Fill", new Color(1f, .3f, .2f));
            UiKit.Stretch(bossFill.rectTransform);

            bossPhase = UiKit.Label(bossRoot, "Phase", "PHASE I", 16, UiKit.TextDim, TextAnchor.LowerRight);
            UiKit.Stretch(bossPhase.rectTransform, right: 16f, bottom: 4f);
            bossPhase.rectTransform.sizeDelta = new Vector2(120f, 18f);
        }

        void BuildDashAndPowerups(Transform root)
        {
            // Dash pips under the crosshair.
            var dashRoot = new GameObject("DashPips").AddComponent<RectTransform>();
            dashRoot.transform.SetParent(root, false);
            dashRoot.anchorMin = dashRoot.anchorMax = new Vector2(.5f, .5f);
            dashRoot.anchoredPosition = new Vector2(0f, -64f);
            dashRoot.sizeDelta = new Vector2(80f, 14f);
            for (int i = 0; i < 3; i++)
            {
                var pip = UiKit.Fill(dashRoot, "Pip", UiKit.Accent);
                pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(.5f, .5f);
                pip.rectTransform.anchoredPosition = new Vector2(-24f + i * 24f, 0f);
                pip.rectTransform.sizeDelta = new Vector2(18f, 5f);
                dashPips.Add(pip);
            }

            // Active powerups bottom-centre.
            powerupRoot = new GameObject("Powerups").AddComponent<RectTransform>();
            powerupRoot.transform.SetParent(root, false);
            UiKit.Stretch(powerupRoot, left: 0f, right: 0f, bottom: 150f);
            powerupRoot.sizeDelta = new Vector2(0f, 30f);

            for (int i = 0; i < 6; i++)
            {
                var entry = new GameObject("Powerup" + i).AddComponent<RectTransform>();
                entry.transform.SetParent(powerupRoot, false);
                entry.anchorMin = entry.anchorMax = new Vector2(.5f, .5f);
                entry.anchoredPosition = new Vector2(-150f + i * 60f, 0f);
                entry.sizeDelta = new Vector2(56f, 26f);
                entry.gameObject.SetActive(false);

                var back = UiKit.Fill(entry, "Back", new Color(0f, 0f, 0f, .55f));
                UiKit.Stretch(back.rectTransform);
                var fill = UiKit.Fill(entry, "Fill", UiKit.Gold);
                UiKit.Stretch(fill.rectTransform);
                fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                var label = UiKit.Label(entry, "Label", "", 14, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(label.rectTransform);
                powerupLabels.Add(label);
                powerupFills.Add(fill);
            }
        }

        void BuildNotification(Transform root)
        {
            var anchor = new GameObject("Notification").AddComponent<RectTransform>();
            anchor.transform.SetParent(root, false);
            UiKit.Stretch(anchor, left: 0f, right: 0f, top: 150f);
            anchor.sizeDelta = new Vector2(0f, 60f);

            notificationBacking = UiKit.Panel(anchor, "Backing", new Color(.03f, .04f, .06f, .85f));
            UiKit.Stretch(notificationBacking.rectTransform, left: 600f, right: 600f);
            notificationBacking.rectTransform.sizeDelta = new Vector2(0f, 46f);
            notificationBacking.color = Color.clear;

            notificationText = UiKit.Label(anchor, "Text", "", 28, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(notificationText.rectTransform);
            notificationText.text = "";
        }

        void BuildVignette(Transform root)
        {
            damageVignette = UiKit.Fill(root, "DamageVignette", new Color(1f, .1f, .05f, 0f));
            UiKit.Stretch(damageVignette.rectTransform);
            damageVignette.raycastTarget = false;
            damageVignette.sprite = UiKit.PanelSprite;
            damageVignette.type = Image.Type.Sliced;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            UpdateVitals();
            UpdateWeapon();
            UpdateCrosshair();
            UpdateNotifications();
            UpdatePowerups();
            UpdateDash();
        }

        void UpdateVitals()
        {
            if (vitals == null) return;

            float fraction = vitals.HealthPercent;
            healthFill.fillAmount = fraction;
            healthFill.color = fraction > .5f ? new Color(.25f, .95f, .45f)
                : fraction > .25f ? new Color(1f, .8f, .25f) : new Color(1f, .3f, .25f);

            // Ghost bar chases the real value after damage.
            ghostHealth = Mathf.Lerp(ghostHealth, fraction, Time.deltaTime * 3f);
            if (ghostHealth < fraction) ghostHealth = fraction;
            healthGhost.fillAmount = ghostHealth;

            healthText.text = Mathf.CeilToInt(vitals.CurrentHealth).ToString();
            armorFill.fillAmount = vitals.CurrentArmor / 100f;

            // Damage flash on the vignette.
            if (fraction < lastHealthFraction - .001f)
            {
                damageVignette.color = new Color(1f, .1f, .05f, .45f * (1f - fraction));
            }
            lastHealthFraction = fraction;
            Color vignette = damageVignette.color;
            vignette.a = Mathf.Lerp(vignette.a, fraction < .3f ? .22f + Mathf.PingPong(Time.time * 1.5f, .1f) : 0f, Time.deltaTime * 4f);
            damageVignette.color = vignette;
        }

        int lastInMag = -1, lastReserve = -1, lastSegments = -1;
        bool lastReloading;

        void UpdateWeapon()
        {
            if (weapons == null || weapons.Current == null || weapons.Current.Definition == null) return;
            Weapon weapon = weapons.Current;
            WeaponDefinition definition = weapon.Definition;

            int magSize = Mathf.Max(1, Mathf.RoundToInt(definition.magazineSize * weapon.MagazineSizeMultiplier));
            int inMag = weapon.Magazine;
            int reserve = weapon.Reserve;
            int segments = Mathf.Min(MaxSegments, magSize);

            // Only touch the UI text when something actually changed (no per-frame
            // string allocations).
            if (inMag != lastInMag || reserve != lastReserve || weapon.IsReloading != lastReloading
                || segments != lastSegments)
            {
                lastInMag = inMag;
                lastReserve = reserve;
                lastReloading = weapon.IsReloading;
                lastSegments = segments;

                ammoText.text = weapon.IsReloading ? "RELOADING" : $"{inMag} / {reserve}";
                ammoText.color = weapon.IsReloading ? UiKit.Gold
                    : inMag == 0 && reserve == 0 ? UiKit.Danger : UiKit.TextMain;

                for (int i = 0; i < MaxSegments; i++)
                {
                    bool visible = i < segments && !weapon.InfiniteAmmo;
                    if (magSegments[i].gameObject.activeSelf != visible)
                        magSegments[i].gameObject.SetActive(visible);
                    if (visible)
                    {
                        bool loaded = i < inMag;
                        magSegments[i].color = loaded
                            ? (inMag <= segments * .25f ? UiKit.Danger : new Color(.2f, .95f, 1f, .9f))
                            : new Color(.15f, .18f, .22f, .9f);
                    }
                }
            }
        }

        void UpdateCrosshair()
        {
            // Spread from movement + firing, straight from the live weapon state.
            float moveSpread = 0f;
            var locomotion = Services.Session?.Locomotion;
            if (locomotion != null) moveSpread = locomotion.HorizontalSpeed01 * 9f;

            float weaponSpread = weapons != null && weapons.Current != null
                ? weapons.Current.CurrentSpread * 2.2f : 0f;

            crosshairSpread = Mathf.Lerp(crosshairSpread, crosshairBaseSize + moveSpread + weaponSpread,
                Time.deltaTime * 14f);

            float gap = crosshairSpread;
            for (int i = 0; i < crosshairArms.Count; i++)
            {
                RectTransform arm = crosshairArms[i];
                switch (i)
                {
                    case 0: arm.anchoredPosition = new Vector2(0f, gap + 6f); break; // up
                    case 1: arm.anchoredPosition = new Vector2(0f, -gap - 6f); break; // down
                    case 2: arm.anchoredPosition = new Vector2(-gap - 6f, 0f); arm.sizeDelta = new Vector2(12f, 2.5f); break;
                    case 3: arm.anchoredPosition = new Vector2(gap + 6f, 0f); arm.sizeDelta = new Vector2(12f, 2.5f); break;
                }
            }

            if (Time.unscaledTime > hitMarkerUntil && hitMarker.color.a > 0f)
                hitMarker.color = Color.Lerp(hitMarker.color, Color.clear, Time.deltaTime * 10f);
        }

        void UpdateNotifications()
        {
            if (notificationText.text.Length == 0) return;
            if (Time.unscaledTime > notificationUntil)
            {
                notificationText.text = "";
                notificationBacking.color = Color.clear;
            }
            else
            {
                float fade = Mathf.Clamp01((notificationUntil - Time.unscaledTime) / .4f);
                notificationText.color = new Color(notificationColor.r, notificationColor.g, notificationColor.b, fade);
            }
        }

        void UpdatePowerups()
        {
            if (powerups == null) return;
            var active = powerups.Active;
            for (int i = 0; i < powerupLabels.Count; i++)
            {
                if (i < active.Count)
                {
                    var entry = active[i];
                    powerupLabels[i].transform.parent.gameObject.SetActive(true);
                    powerupLabels[i].text = entry.Definition.glyph;
                    powerupFills[i].color = entry.Definition.color;
                    powerupFills[i].fillAmount = entry.Definition.duration > 0
                        ? entry.Remaining / entry.Definition.duration : 1f;
                }
                else powerupLabels[i].transform.parent.gameObject.SetActive(false);
            }
        }

        void UpdateDash()
        {
            if (dash == null) return;
            int max = Mathf.Min(dashPips.Count, dash.MaxCharges);
            for (int i = 0; i < dashPips.Count; i++)
            {
                bool visible = i < max;
                dashPips[i].gameObject.SetActive(visible);
                if (visible)
                {
                    bool charged = i < dash.ChargesRemaining;
                    bool recharging = i == dash.ChargesRemaining;
                    dashPips[i].color = charged ? UiKit.Accent
                        : recharging ? new Color(.2f, .5f, .6f, .6f) : new Color(.1f, .13f, .16f, .5f);
                    if (recharging) dashPips[i].fillAmount = 1f - dash.CooldownProgress01;
                    else dashPips[i].fillAmount = 1f;
                }
            }
        }

        // ---------------------------------------------------------------- events

        void OnNotification(string message, Color color, float duration)
        {
            notificationText.text = message;
            notificationColor = color;
            notificationUntil = Time.unscaledTime + duration;
            notificationBacking.color = new Color(.03f, .04f, .06f, .7f);
        }

        void OnWavePrepared(int wave, float prep) => waveText.text = $"WAVE {wave} INCOMING";
        void OnWaveStarted(int wave, int total) => waveText.text = $"WAVE {wave}";
        void OnWaveChanged(int wave, int remaining) => waveText.text = $"WAVE {wave} — {remaining} LEFT";
        void OnWaveCleared(int wave) => waveText.text = $"WAVE {wave} CLEARED";
        void OnScoreChanged(long score) => scoreText.text = score.ToString("N0");
        void OnComboChanged(float multiplier, float seconds)
        {
            comboText.text = multiplier > 1.05f ? $"x{multiplier:0.0}" : "";
        }

        void OnBossSpawned(string name, float maxHealth)
        {
            bossRoot.gameObject.SetActive(true);
            bossName.text = name;
            bossFill.fillAmount = 1f;
            bossPhase.text = "PHASE I";
        }

        void OnBossHealthChanged(float current, float max) => bossFill.fillAmount = max > 0 ? current / max : 0f;
        void OnBossPhaseChanged(int phase, string name) => bossPhase.text = name.Length > 0 ? name : $"PHASE {phase}";
        void OnBossDefeated(string name, bool final)
        {
            bossFill.fillAmount = 0f;
            StartCoroutine(HideBossBarSoon());
        }

        IEnumerator HideBossBarSoon()
        {
            yield return new WaitForSeconds(2.2f);
            bossRoot.gameObject.SetActive(false);
        }

        void OnPowerupStarted(string id, string label, float duration, Color color)
        {
            // Handled per-frame from PlayerPowerups.Active.
        }

        void OnPowerupEnded(string id) { }

        void OnWeaponPickedUp(string id, string name)
        {
            if (weaponName != null) weaponName.text = name;
        }

        void OnWeaponSwitched(Weapon weapon)
        {
            if (weaponName == null) return;
            weaponName.text = weapon != null && weapon.Definition != null
                ? weapon.Definition.displayName.ToUpperInvariant() : "";
        }

        void OnHitMarker(bool headshot, bool killed)
        {
            hitMarkerUntil = Time.unscaledTime + .18f;
            hitMarker.color = killed ? new Color(1f, .4f, .2f, .95f)
                : headshot ? new Color(1f, .9f, .4f, .95f) : new Color(1f, 1f, 1f, .85f);
        }
    }
}
