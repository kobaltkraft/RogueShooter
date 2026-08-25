using System;
using System.Collections.Generic;

namespace RogueArena.Persistence
{
    /// <summary>Root of the versioned save file. Additive fields default safely on old saves.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = SaveService.CurrentVersion;
        public ProgressData progress = new ProgressData();
        public RecordsData records = new RecordsData();
        public ChallengeData challenges = new ChallengeData();
        public SettingsData settings = new SettingsData();
    }

    // ------------------------------------------------------------------ progression

    [Serializable]
    public class ProgressData
    {
        public long xp;
        public int level = 1;
        public long currency;
        /** Unlocked weapon ids, e.g. "smg". Weapon ids are stable strings. */
        public List<string> unlockedWeapons = new List<string>();
        /** "weaponId:track:tier" entries, e.g. "rifle:damage:2". */
        public List<string> weaponUpgrades = new List<string>();
        /** "upgradeId:tier" entries, e.g. "health:1". */
        public List<string> playerUpgrades = new List<string>();
        /** Unlocked cosmetic ids (crosshair styles). */
        public List<string> cosmetics = new List<string>();
        /** Selected crosshair id. */
        public string crosshair = "cyan";
    }

    // ------------------------------------------------------------------ records

    [Serializable]
    public class RecordsData
    {
        /** "modeId|arenaId" -> score. */
        public List<StringLongEntry> bestScores = new List<StringLongEntry>();
        /** "modeId|arenaId" -> best time in seconds (time attack, lower is better). */
        public List<StringFloatEntry> bestTimes = new List<StringFloatEntry>();
        /** Highest wave reached per modeId. */
        public List<StringIntEntry> bestWaves = new List<StringIntEntry>();
        public int totalKills;
        public int headshotKills;
        public int eliteKills;
        public int barrelKills;
        public int explosiveKills;
        public int bossKills;
        public int runsPlayed;
        public int runsWon;
        public double totalPlaySeconds;
    }

    [Serializable] public class StringLongEntry { public string key; public long value; }
    [Serializable] public class StringFloatEntry { public string key; public float value; }
    [Serializable] public class StringIntEntry { public string key; public int value; }

    // ------------------------------------------------------------------ challenges

    [Serializable]
    public class ChallengeData
    {
        public List<string> completed = new List<string>();
        /** "challengeId" -> progress value. */
        public List<StringLongEntry> progress = new List<StringLongEntry>();
    }

    // ------------------------------------------------------------------ settings

    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public float musicVolume = .8f;
        public float sfxVolume = 1f;
        public float uiVolume = .9f;
        public float voiceVolume = 1f;
        public float mouseSensitivity = 1f;
        public float fieldOfView = 78f;
        /** 0 = low, 1 = medium, 2 = high. */
        public int quality = 2;
        public bool invertY;
        public bool headBob = true;
        public bool screenShake = true;
        public bool showFps;
    }
}
