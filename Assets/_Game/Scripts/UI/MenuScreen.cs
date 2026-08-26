using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RogueArena.Content;
using RogueArena.Core;
using RogueArena.Environment;
using RogueArena.Progression;
using RogueArena.Weapons;
using RogueArena.Waves;

namespace RogueArena.UI
{
    /// <summary>
    /// The main menu: title, mode + arena pickers, DEPLOY button, and tab panels
    /// for the armory (weapon unlocks/upgrades), player upgrades, challenges,
    /// records and settings. Fully procedural, driven by GameContent + save data.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        string selectedMode = "survival";
        string selectedArena = "industrial";
        GameObject panelRoot;
        Text modeDescription;
        Text arenaDescription;
        Text currencyLabel;
        readonly Dictionary<string, Text> tabLabels = new Dictionary<string, Text>();

        public void Build(Canvas canvas)
        {
            // Dim backdrop over the diorama.
            var backdrop = UiKit.Fill(canvas.transform, "Backdrop", new Color(0f, 0f, 0f, .35f));
            UiKit.Stretch(backdrop.rectTransform);

            // Title.
            var title = UiKit.Label(canvas.transform, "Title", "ROGUE ARENA", 84, UiKit.Accent,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Stretch(title.rectTransform, top: 40f);
            title.rectTransform.sizeDelta = new Vector2(0f, 100f);

            var subtitle = UiKit.Label(canvas.transform, "Subtitle", "A FAST, MODULAR ARENA SHOOTER", 20,
                UiKit.TextDim, TextAnchor.UpperCenter);
            UiKit.Stretch(subtitle.rectTransform, top: 140f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 28f);

            // Left column: mode + arena pickers + deploy.
            BuildSelector(canvas.transform);

            // Right column: tabbed panels.
            BuildTabs(canvas.transform);

            // Bottom bar: currency + version.
            currencyLabel = UiKit.Label(canvas.transform, "Currency", "", 24, UiKit.Gold,
                TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Stretch(currencyLabel.rectTransform, left: 30f, bottom: 20f);
            currencyLabel.rectTransform.sizeDelta = new Vector2(400f, 32f);

            var version = UiKit.Label(canvas.transform, "Version", "v1.0 — built with Unity 6", 16,
                UiKit.TextDim, TextAnchor.LowerRight);
            UiKit.Stretch(version.rectTransform, right: 30f, bottom: 24f);
            version.rectTransform.sizeDelta = new Vector2(400f, 24f);

            GameEvents.CurrencyChanged += OnCurrencyChanged;
            GameEvents.LevelUp += OnLevelUp;
            RefreshCurrency();
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            GameEvents.CurrencyChanged -= OnCurrencyChanged;
            GameEvents.LevelUp -= OnLevelUp;
        }

        void OnCurrencyChanged(long total) => RefreshCurrency();
        void OnLevelUp(int level) => RefreshCurrency();

        void RefreshCurrency()
        {
            if (currencyLabel != null && Services.Progression != null)
                currencyLabel.text = $"SCRAP  {Services.Progression.Currency:N0}    LV {Services.Progression.Level}";
        }

        // ---------------------------------------------------------------- selector

        void BuildSelector(Transform canvas)
        {
            var panel = UiKit.Panel(canvas, "Selector", UiKit.PanelDark);
            panel.rectTransform.anchorMin = new Vector2(0f, .5f);
            panel.rectTransform.anchorMax = new Vector2(0f, .5f);
            panel.rectTransform.anchoredPosition = new Vector2(330f, 0f);
            panel.rectTransform.sizeDelta = new Vector2(560f, 520f);

            var label = UiKit.Label(panel.transform, "Header", "MODE", 22, UiKit.Accent,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(label.rectTransform, left: 24f, top: 16f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);

            // Mode buttons.
            float y = -60f;
            foreach (GameModeDefinition mode in GameContent.Modes)
            {
                GameModeDefinition current = mode;
                var button = UiKit.Button(panel.transform, "Mode_" + mode.id, mode.displayName, 20,
                    () => { selectedMode = current.id; RefreshSelection(); });
                Place(button.transform, y, 285f, 470f);
                y -= 62f;
            }

            var arenaHeader = UiKit.Label(panel.transform, "ArenaHeader", "ARENA", 22, UiKit.Accent,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(arenaHeader.rectTransform, left: 24f, top: 344f);
            arenaHeader.rectTransform.sizeDelta = new Vector2(0f, 30f);

            // Arena buttons (2 columns).
            for (int i = 0; i < GameContent.Arenas.Count; i++)
            {
                ArenaDefinition arena = GameContent.Arenas[i];
                var button = UiKit.Button(panel.transform, "Arena_" + arena.id, arena.displayName, 16,
                    () => { selectedArena = arena.id; RefreshSelection(); });
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(130f + (i % 2) * 235f, -410f);
                rect.sizeDelta = new Vector2(220f, 52f);
            }

            modeDescription = UiKit.Label(panel.transform, "ModeDesc", "", 15, UiKit.TextDim, TextAnchor.UpperLeft);
            UiKit.Stretch(modeDescription.rectTransform, left: 24f, right: 24f, top: 248f);
            modeDescription.rectTransform.sizeDelta = new Vector2(0f, 70f);

            arenaDescription = UiKit.Label(panel.transform, "ArenaDesc", "", 15, UiKit.TextDim, TextAnchor.UpperLeft);
            UiKit.Stretch(arenaDescription.rectTransform, left: 24f, right: 24f, top: 470f);
            arenaDescription.rectTransform.sizeDelta = new Vector2(0f, 44f);

            // DEPLOY.
            var deploy = UiKit.Button(panel.transform, "Deploy", "DEPLOY", 30, Deploy,
                new Color(.06f, .2f, .24f, .95f));
            deploy.transform.SetParent(panel.transform, false);
            var deployRect = (RectTransform)deploy.transform;
            deployRect.anchorMin = deployRect.anchorMax = new Vector2(.5f, 0f);
            deployRect.anchoredPosition = new Vector2(0f, 24f);
            deployRect.sizeDelta = new Vector2(240f, 64f);

            RefreshSelection();
        }

        void Place(Transform transform, float y, float x, float width)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 52f);
        }

        void RefreshSelection()
        {
            GameModeDefinition mode = GameContent.Mode(selectedMode);
            if (mode != null && modeDescription != null)
                modeDescription.text = mode.description;

            ArenaDefinition arena = GameContent.Arena(selectedArena);
            if (arena != null && arenaDescription != null)
                arenaDescription.text = arena.description;
        }

        void Deploy()
        {
            if (Services.Flow == null) return;
            Services.Flow.StartRun(selectedMode, selectedArena);
        }

        // ---------------------------------------------------------------- tabs

        string activeTab = "armory";

        void BuildTabs(Transform canvas)
        {
            var panel = UiKit.Panel(canvas, "Tabs", UiKit.PanelDark);
            panel.rectTransform.anchorMin = new Vector2(1f, .5f);
            panel.rectTransform.anchorMax = new Vector2(1f, .5f);
            panel.rectTransform.anchoredPosition = new Vector2(-330f, 0f);
            panel.rectTransform.sizeDelta = new Vector2(560f, 620f);

            string[] tabs = { "armory", "upgrades", "challenges", "records", "settings" };
            float x = 20f;
            foreach (string tab in tabs)
            {
                string current = tab;
                var button = UiKit.Button(panel.transform, "Tab_" + tab, tab.ToUpperInvariant(), 16,
                    () => { activeTab = current; ShowTab(); });
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x + 52f, -28f);
                rect.sizeDelta = new Vector2(104f, 40f);
                tabLabels[tab] = button.GetComponentInChildren<Text>();
                x += 108f;
            }

            panelRoot = new GameObject("PanelRoot").AddComponent<RectTransform>().gameObject;
            panelRoot.transform.SetParent(panel.transform, false);
            var rootRect = (RectTransform)panelRoot.transform;
            UiKit.Stretch(rootRect, left: 20f, right: 20f, top: 60f, bottom: 20f);

            ShowTab();
        }

        void ShowTab()
        {
            ClearChildren(panelRoot.transform);
            switch (activeTab)
            {
                case "upgrades": BuildUpgradePanel(); break;
                case "challenges": BuildChallengePanel(); break;
                case "records": BuildRecordPanel(); break;
                case "settings": BuildSettingsPanel(); break;
                default: BuildArmoryPanel(); break;
            }

            foreach (var pair in tabLabels)
            {
                if (pair.Value != null)
                    pair.Value.color = pair.Key == activeTab ? UiKit.Accent : UiKit.TextDim;
            }
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        void AddHeading(Transform parent, string text, float y)
        {
            var label = UiKit.Label(parent, "Heading_" + text, text, 20, UiKit.Accent,
                TextAnchor.UpperLeft, FontStyle.Bold);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(4f, -y);
            label.rectTransform.sizeDelta = new Vector2(520f, 26f);
        }

        // ---------------------------------------------------------------- armory

        void BuildArmoryPanel()
        {
            if (Services.Progression == null) return;
            var parent = panelRoot.transform;

            AddHeading(parent, "WEAPON UNLOCKS", 8f);
            float y = 42f;
            foreach (WeaponDefinition weapon in GameContent.Weapons)
            {
                BuildWeaponRow(parent, weapon, y);
                y += 54f;
            }
        }

        void BuildWeaponRow(Transform parent, WeaponDefinition weapon, float y)
        {
            var row = new GameObject("Weapon_" + weapon.id).AddComponent<RectTransform>();
            row.transform.SetParent(parent, false);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(260f, -y - 22f);
            row.sizeDelta = new Vector2(500f, 46f);

            bool unlocked = Services.Progression.IsWeaponUnlocked(weapon.id);
            string status = unlocked ? weapon.displayName
                : $"{weapon.displayName}  —  {weapon.unlockCost} SCRAP";

            var name = UiKit.Label(row, "Name", status, 18, unlocked ? UiKit.TextMain : UiKit.TextDim,
                TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(.6f, 1f);

            if (!unlocked && weapon.unlockCost > 0)
            {
                var buy = UiKit.Button(row, "Unlock", "UNLOCK", 16,
                    () => { Services.Progression?.TryUnlockWeapon(weapon.id, weapon.unlockCost); ShowTab(); });
                buy.transform.SetParent(row, false);
                var rect = (RectTransform)buy.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                rect.anchoredPosition = new Vector2(-60f, 0f);
                rect.sizeDelta = new Vector2(110f, 38f);
                buy.interactable = Services.Progression != null
                    && Services.Progression.Currency >= weapon.unlockCost;
            }
            else
            {
                // Upgrade tracks.
                for (int t = 0; t < WeaponUpgradeTracks.All.Length; t++)
                {
                    string track = WeaponUpgradeTracks.All[t];
                    int tier = Services.Progression.WeaponUpgradeTier(weapon.id, track);
                    int maxTier = WeaponUpgradeTracks.MaxTier(track);
                    long cost = WeaponUpgradeTracks.CostAtTier(track, tier);
                    string trackId = track;

                    var upgrade = UiKit.Button(row, "Up_" + track, $"{WeaponUpgradeTracks.Label(track)} {tier}/{maxTier}", 12,
                        () =>
                        {
                            Services.Progression?.TryPurchaseWeaponUpgrade(weapon.id, trackId,
                                WeaponUpgradeTracks.CostAtTier(trackId,
                                    Services.Progression.WeaponUpgradeTier(weapon.id, trackId)),
                                WeaponUpgradeTracks.MaxTier(trackId));
                            ShowTab();
                        });
                    upgrade.transform.SetParent(row, false);
                    var rect = (RectTransform)upgrade.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                    rect.anchoredPosition = new Vector2(-90f - t * 78f, 0f);
                    rect.sizeDelta = new Vector2(74f, 36f);
                    upgrade.interactable = unlocked && tier < maxTier
                        && Services.Progression != null && Services.Progression.Currency >= cost;
                }
            }
        }

        // ---------------------------------------------------------------- upgrades

        void BuildUpgradePanel()
        {
            if (Services.Progression == null) return;
            var parent = panelRoot.transform;

            AddHeading(parent, "PLAYER UPGRADES", 8f);
            float y = 46f;
            foreach (PlayerUpgradeDefinition upgrade in GameContent.PlayerUpgrades)
            {
                PlayerUpgradeDefinition current = upgrade;
                int tier = Services.Progression.PlayerUpgradeTier(upgrade);
                long cost = upgrade.CostAtTier(tier);

                var row = new GameObject("Upgrade_" + upgrade.id).AddComponent<RectTransform>();
                row.transform.SetParent(parent, false);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(260f, -y - 24f);
                row.sizeDelta = new Vector2(500f, 52f);

                var label = UiKit.Label(row, "Name", $"{upgrade.displayName}  {tier}/{upgrade.maxTier}", 18,
                    UiKit.TextMain, TextAnchor.MiddleLeft);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(.62f, 1f);

                var desc = UiKit.Label(row, "Desc", upgrade.description, 13, UiKit.TextDim, TextAnchor.LowerLeft);
                desc.rectTransform.anchorMin = new Vector2(0f, 0f);
                desc.rectTransform.anchorMax = new Vector2(.62f, .45f);

                var buy = UiKit.Button(row, "Buy", tier >= upgrade.maxTier ? "MAX" : $"{cost} SCRAP", 15,
                    () =>
                    {
                        if (current.maxTier > tier) Services.Progression?.TryPurchasePlayerUpgrade(current);
                        ShowTab();
                    });
                buy.transform.SetParent(row, false);
                var rect = (RectTransform)buy.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                rect.anchoredPosition = new Vector2(-70f, 0f);
                rect.sizeDelta = new Vector2(130f, 40f);
                buy.interactable = tier < upgrade.maxTier
                    && Services.Progression.Currency >= cost;

                y += 62f;
            }
        }

        // ---------------------------------------------------------------- challenges

        void BuildChallengePanel()
        {
            var parent = panelRoot.transform;
            AddHeading(parent, "CHALLENGES", 8f);

            float y = 44f;
            foreach (ChallengeDefinition challenge in GameContent.Challenges)
            {
                bool complete = Services.Challenges != null && Services.Challenges.IsCompleted(challenge);
                float progress = Services.Challenges != null ? Services.Challenges.Progress(challenge) : 0f;

                var row = new GameObject("Challenge_" + challenge.id).AddComponent<RectTransform>();
                row.transform.SetParent(parent, false);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(260f, -y - 20f);
                row.sizeDelta = new Vector2(500f, 40f);

                var title = UiKit.Label(row, "Title",
                    (complete ? "[DONE] " : "") + challenge.title, 17,
                    complete ? UiKit.Good : UiKit.TextMain, TextAnchor.MiddleLeft);
                title.rectTransform.anchorMin = new Vector2(0f, .5f);
                title.rectTransform.anchorMax = new Vector2(.65f, 1f);

                var desc = UiKit.Label(row, "Desc", challenge.description, 13, UiKit.TextDim, TextAnchor.LowerLeft);
                desc.rectTransform.anchorMin = new Vector2(0f, 0f);
                desc.rectTransform.anchorMax = new Vector2(.65f, .5f);

                var barBack = UiKit.Fill(row, "BarBack", new Color(.1f, .12f, .16f, .9f));
                barBack.rectTransform.anchorMin = new Vector2(.7f, .35f);
                barBack.rectTransform.anchorMax = new Vector2(1f, .65f);
                var fill = UiKit.Fill(barBack.transform, "Fill", complete ? UiKit.Good : UiKit.Accent);
                UiKit.Stretch(fill.rectTransform);
                fill.fillAmount = complete ? 1f : progress;

                y += 48f;
            }
        }

        // ---------------------------------------------------------------- records

        void BuildRecordPanel()
        {
            var parent = panelRoot.transform;
            AddHeading(parent, "RECORDS", 8f);

            var records = Services.Save?.Data?.records;
            if (records == null) return;

            string text =
                $"RUNS PLAYED      {records.runsPlayed}\n" +
                $"RUNS WON         {records.runsWon}\n" +
                $"TOTAL KILLS      {records.totalKills}\n" +
                $"HEADSHOT KILLS   {records.headshotKills}\n" +
                $"ELITE KILLS      {records.eliteKills}\n" +
                $"BARREL KILLS     {records.barrelKills}\n" +
                $"EXPLOSIVE KILLS  {records.explosiveKills}\n" +
                $"BOSS KILLS       {records.bossKills}\n" +
                $"TIME PLAYED      {(records.totalPlaySeconds / 60):F0} MIN\n\n";

            foreach (var entry in records.bestScores)
            {
                string[] parts = entry.key.Split('|');
                if (parts.Length == 2)
                    text += $"{parts[0].ToUpperInvariant()} @ {parts[1].ToUpperInvariant()}   {entry.value:N0}\n";
            }

            var label = UiKit.Label(parent, "Records", text, 19, UiKit.TextMain, TextAnchor.UpperLeft);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(4f, -50f);
            label.rectTransform.sizeDelta = new Vector2(520f, 520f);
        }

        // ---------------------------------------------------------------- settings

        void BuildSettingsPanel()
        {
            var parent = panelRoot.transform;
            if (Services.Settings == null) return;
            var data = Services.Settings.Data;

            AddHeading(parent, "SETTINGS", 8f);
            float y = 50f;

            SettingsSlider("MASTER VOLUME", y, Audio.AudioCategory.Master, data.masterVolume); y += 58f;
            SettingsSlider("MUSIC VOLUME", y, Audio.AudioCategory.Music, data.musicVolume); y += 58f;
            SettingsSlider("SFX VOLUME", y, Audio.AudioCategory.Sfx, data.sfxVolume); y += 58f;
            SettingsSlider("MOUSE SENSITIVITY", y,
                Mathf.InverseLerp(.05f, 6f, data.mouseSensitivity),
                v => Services.Settings.SetSensitivity(Mathf.Lerp(.05f, 6f, v))); y += 58f;
            SettingsSlider("FIELD OF VIEW", y, (data.fieldOfView - 60f) / 50f,
                v => Services.Settings.SetFieldOfView(Mathf.Lerp(60f, 110f, v))); y += 70f;

            SettingsToggle("INVERT Y", y, data.invertY, v => Services.Settings.SetInvertY(v)); y += 44f;
            SettingsToggle("HEAD BOB", y, data.headBob, v => Services.Settings.SetHeadBob(v)); y += 44f;
            SettingsToggle("SCREEN SHAKE", y, data.screenShake, v => Services.Settings.SetScreenShake(v)); y += 44f;
            SettingsToggle("SHOW FPS", y, data.showFps, v => Services.Settings.SetShowFps(v));
        }

        void SettingsSlider(string label, float y, Audio.AudioCategory category, float value)
        {
            SettingsSlider(label, y, value, v => Services.Settings.SetVolume(category, v));
        }

        void SettingsSlider(string label, float y, float value01, System.Action<float> onChange)
        {
            var row = new GameObject("Setting_" + label).AddComponent<RectTransform>();
            row.transform.SetParent(panelRoot.transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(260f, -y);
            row.sizeDelta = new Vector2(500f, 40f);

            var text = UiKit.Label(row, "Label", label, 17, UiKit.TextDim, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(.45f, 1f);

            var slider = UiKit.Slider(row, "Slider", value01, onChange);
            slider.transform.SetParent(row, false);
            var rect = (RectTransform)slider.transform;
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(1f, .5f);
            rect.sizeDelta = new Vector2(0f, 24f);
        }

        void SettingsToggle(string label, float y, bool value, System.Action<bool> onChange)
        {
            var row = new GameObject("Toggle_" + label).AddComponent<RectTransform>();
            row.transform.SetParent(panelRoot.transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(260f, -y);
            row.sizeDelta = new Vector2(500f, 36f);

            var text = UiKit.Label(row, "Label", label, 17, UiKit.TextDim, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(.5f, 1f);

            var button = UiKit.Button(row, "Toggle", value ? "ON" : "OFF", 16,
                () => { onChange(!GetToggleState(row)); ShowTab(); },
                value ? new Color(.08f, .3f, .22f, .95f) : new Color(.2f, .1f, .1f, .95f));
            button.transform.SetParent(row, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-40f, 0f);
            rect.sizeDelta = new Vector2(80f, 32f);
        }

        static bool GetToggleState(Transform row) => row.Find("Toggle")?.GetComponentInChildren<Text>()?.text == "ON";
    }
}
