using System;
using System.IO;
using UnityEngine;

namespace RogueArena.Persistence
{
    /// <summary>
    /// Versioned, corruption-tolerant JSON save service.
    /// - Writes are atomic (temp file + swap) with a rolling backup.
    /// - Reads fall back to the backup, then to defaults.
    /// - A schema version supports future migrations.
    /// </summary>
    public class SaveService
    {
        public const int CurrentVersion = 1;

        public SaveData Data { get; private set; } = new SaveData();
        public bool LoadedFromBackup { get; private set; }
        public string SavePath { get; }

        const string FileName = "roguearena_save.json";
        const string BackupSuffix = ".bak";

        double lastAutosave;
        bool dirty;

        public SaveService()
        {
            SavePath = Path.Combine(Application.persistentDataPath, FileName);
            Load();
        }

        // ------------------------------------------------------------------ load

        /// <summary>Loads the save file safely, falling back to backup then defaults.</summary>
        public void Load()
        {
            Data = TryRead(SavePath, out bool ok, out string error);
            if (ok) { LoadedFromBackup = false; Migrate(); return; }

            string backupPath = SavePath + BackupSuffix;
            if (File.Exists(backupPath))
            {
                Data = TryRead(backupPath, out ok, out error);
                if (ok)
                {
                    LoadedFromBackup = true;
                    Debug.LogWarning("[Save] Primary save was unreadable, restored from backup: " + error);
                    Migrate();
                    return;
                }
            }

            Debug.LogWarning("[Save] No usable save data found, starting with defaults: " + error);
            Data = new SaveData { version = CurrentVersion };
            LoadedFromBackup = false;
            Sanitize();
        }

        static SaveData TryRead(string path, out bool ok, out string error)
        {
            ok = false;
            error = null;
            try
            {
                if (!File.Exists(path)) { error = "file missing"; return null; }
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) { error = "file empty"; return null; }

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) { error = "parsed to null"; return null; }
                ok = true;
                return data;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        /// <summary>Upgrades old schemas and repairs missing/corrupt sections.</summary>
        void Migrate()
        {
            if (Data.version > CurrentVersion)
                Debug.LogWarning($"[Save] Save from a newer version ({Data.version} > {CurrentVersion}); loading what we understand.");

            switch (Data.version)
            {
                case 0: // pre-versioned placeholder data
                case 1:
                    // nothing to migrate yet
                    break;
            }
            Data.version = CurrentVersion;
            Sanitize();
        }

        /// <summary>Recreates any null section so downstream code never needs null checks.</summary>
        void Sanitize()
        {
            Data.progress ??= new ProgressData();
            Data.records ??= new RecordsData();
            Data.challenges ??= new ChallengeData();
            Data.settings ??= new SettingsData();
            Data.progress.unlockedWeapons ??= new System.Collections.Generic.List<string>();
            Data.progress.weaponUpgrades ??= new System.Collections.Generic.List<string>();
            Data.progress.playerUpgrades ??= new System.Collections.Generic.List<string>();
            Data.progress.cosmetics ??= new System.Collections.Generic.List<string>();
            Data.records.bestScores ??= new System.Collections.Generic.List<StringLongEntry>();
            Data.records.bestTimes ??= new System.Collections.Generic.List<StringFloatEntry>();
            Data.records.bestWaves ??= new System.Collections.Generic.List<StringIntEntry>();
            Data.challenges.completed ??= new System.Collections.Generic.List<string>();
            Data.challenges.progress ??= new System.Collections.Generic.List<StringLongEntry>();

            ValidateValues();
        }

        /// <summary>
        /// Clamps every persisted number. Save files are external input: hand-edited
        /// or corrupted values must never reach gameplay as negatives, NaNs or
        /// out-of-range multipliers.
        /// </summary>
        void ValidateValues()
        {
            // Progress.
            Data.progress.xp = Math.Max(0, Data.progress.xp);
            Data.progress.level = Math.Max(1, Data.progress.level);
            Data.progress.currency = Math.Max(0, Data.progress.currency);

            // Records.
            Data.records.totalKills = Math.Max(0, Data.records.totalKills);
            Data.records.headshotKills = Math.Max(0, Data.records.headshotKills);
            Data.records.eliteKills = Math.Max(0, Data.records.eliteKills);
            Data.records.barrelKills = Math.Max(0, Data.records.barrelKills);
            Data.records.explosiveKills = Math.Max(0, Data.records.explosiveKills);
            Data.records.bossKills = Math.Max(0, Data.records.bossKills);
            Data.records.runsPlayed = Math.Max(0, Data.records.runsPlayed);
            Data.records.runsWon = Math.Max(0, Data.records.runsWon);
            if (Data.records.totalPlaySeconds < 0) Data.records.totalPlaySeconds = 0;
            ClampList(Data.records.bestScores, e => e.value = Math.Max(0, e.value));
            ClampList(Data.records.bestTimes, e => e.value = Math.Max(0, e.value));
            ClampList(Data.records.bestWaves, e => e.value = Math.Max(0, e.value));
            ClampList(Data.challenges.progress, e => e.value = Math.Max(0, e.value));

            // Settings.
            Data.settings.masterVolume = Clamp01(Data.settings.masterVolume);
            Data.settings.musicVolume = Clamp01(Data.settings.musicVolume);
            Data.settings.sfxVolume = Clamp01(Data.settings.sfxVolume);
            Data.settings.uiVolume = Clamp01(Data.settings.uiVolume);
            Data.settings.voiceVolume = Clamp01(Data.settings.voiceVolume);
            if (float.IsNaN(Data.settings.mouseSensitivity)) Data.settings.mouseSensitivity = 1f;
            Data.settings.mouseSensitivity = Mathf.Clamp(Data.settings.mouseSensitivity, .05f, 6f);
            if (float.IsNaN(Data.settings.fieldOfView)) Data.settings.fieldOfView = 78f;
            Data.settings.fieldOfView = Mathf.Clamp(Data.settings.fieldOfView, 60f, 110f);

            // Structural lists: drop null/empty entries.
            Data.progress.unlockedWeapons.RemoveAll(string.IsNullOrEmpty);
            Data.progress.weaponUpgrades.RemoveAll(string.IsNullOrEmpty);
            Data.progress.playerUpgrades.RemoveAll(string.IsNullOrEmpty);
            Data.progress.cosmetics.RemoveAll(string.IsNullOrEmpty);
            Data.challenges.completed.RemoveAll(string.IsNullOrEmpty);
        }

        static float Clamp01(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);

        static void ClampList<T>(System.Collections.Generic.List<T> list, System.Action<T> clamp)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) clamp(list[i]);
        }

        // ------------------------------------------------------------------ save

        public void MarkDirty() => dirty = true;

        /// <summary>Immediate save. Returns false when the write fails.</summary>
        public bool SaveNow()
        {
            dirty = false;
            lastAutosave = Time.unscaledTime;
            try
            {
                string json = JsonUtility.ToJson(Data, prettyPrint: false);
                if (string.IsNullOrEmpty(json)) return false;

                string tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, json);

                // Keep the previous good file as backup, then swap in the new one.
                if (File.Exists(SavePath))
                {
                    string backupPath = SavePath + BackupSuffix;
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(SavePath, backupPath);
                }
                File.Move(tempPath, SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Failed to write save file: {e.Message}");
                return false;
            }
        }

        /// <summary>Call from a persistent Update: throttled autosave for dirty data.</summary>
        public void TickAutosave(float interval = 3f)
        {
            if (!dirty) return;
            if (Time.unscaledTime - lastAutosave < interval) return;
            SaveNow();
        }
    }
}
