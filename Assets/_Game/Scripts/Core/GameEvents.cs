using System; using RogueArena.AI;
namespace RogueArena.Core { public static class GameEvents { public static Action<EnemyBrain> EnemyDied; public static Action PlayerDied; public static Action<int,int> WaveChanged; public static Action Victory; public static Action Defeat; } }
