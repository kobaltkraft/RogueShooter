using System;
using UnityEngine;
using RogueArena.AI;
using RogueArena.Combat;
using RogueArena.Content;
using RogueArena.Core;
using RogueArena.Persistence;

namespace RogueArena.Core
{
    /// <summary>
    /// Run score with a decay combo multiplier. Subscribes to global kill events;
    /// owned by SystemRoot and reset between runs.
    /// </summary>
    public sealed class ScoreService : IDisposable
    {
        public long Score { get; private set; }
        public int Combo { get; private set; }
        public float ComboMultiplier => 1f + Mathf.Min(Combo, 20) * .1f;
        public float ComboSecondsLeft => Mathf.Max(0f, comboExpire - Time.unscaledTime);
        public long BestScore { get; private set; }

        const float ComboWindow = 3.5f;
        readonly SaveService save;
        float comboExpire;
        bool comboDirty;

        public ScoreService(SaveService saveService)
        {
            save = saveService;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.Victory += OnVictory;
            BestScore = CurrentBest();
        }

        RecordsData CurrentRecords => save?.Data?.records;

        long CurrentBest()
        {
            var records = CurrentRecords;
            long best = 0;
            if (records != null)
                foreach (var entry in records.bestScores)
                    if (entry.value > best) best = entry.value;
            return best;
        }

        /// <summary>Records the final run score against "mode|arena". Call at run end.</summary>
        public void RecordRun(string modeId, string arenaId)
        {
            var records = CurrentRecords;
            if (records == null) return;

            string key = modeId + "|" + arenaId;
            var entry = records.bestScores.Find(e => e.key == key);
            if (entry == null)
            {
                entry = new StringLongEntry { key = key, value = Score };
                records.bestScores.Add(entry);
            }
            else if (Score > entry.value) entry.value = Score;

            if (Score > BestScore) BestScore = Score;
            save?.MarkDirty();
        }

        public void Reset()
        {
            Score = 0;
            Combo = 0;
            comboExpire = 0f;
            GameEvents.ScoreChanged?.Invoke(Score);
            GameEvents.ComboChanged?.Invoke(1f, 0f);
        }

        void OnEnemyKilled(KillInfo kill)
        {
            if (!kill.KilledByPlayer) return;

            EnemyDefinition definition = GameContent.EnemyOf(kill.Kind);
            long baseScore = definition != null ? definition.scoreValue : 100;
            if (kill.WasElite) baseScore = (long)(baseScore * (1f + kill.EliteModifierCount * .5f));

            Score += (long)(baseScore * ComboMultiplier
                * (kill.Headshot ? 1.5f : 1f)
                * (kill.PlayerWasAirborne ? 1.25f : 1f));
            Combo = Mathf.Min(Combo + 1, 20);
            comboExpire = Time.unscaledTime + ComboWindow;
            comboDirty = true;
            GameEvents.ScoreChanged?.Invoke(Score);
            GameEvents.ComboChanged?.Invoke(ComboMultiplier, ComboWindow);
        }

        void OnVictory()
        {
            Score += 2500; // survival bonus
            GameEvents.ScoreChanged?.Invoke(Score);
        }

        public void Tick()
        {
            if (comboDirty && Combo > 0 && Time.unscaledTime > comboExpire)
            {
                Combo = 0;
                comboDirty = false;
                GameEvents.ComboChanged?.Invoke(1f, 0f);
            }
        }

        public void Dispose()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.Victory -= OnVictory;
        }
    }
}
