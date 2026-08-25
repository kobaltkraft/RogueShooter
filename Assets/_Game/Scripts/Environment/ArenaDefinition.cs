using UnityEngine;

namespace RogueArena.Environment
{
    public enum ArenaKind { Industrial, Desert, City }

    /// <summary>Describes one playable arena. Arenas are built at runtime by kind.</summary>
    [CreateAssetMenu(menuName = "Rogue Arena/Arena Definition", fileName = "Arena_")]
    public class ArenaDefinition : ScriptableObject
    {
        public string id = "industrial";
        public string displayName = "INDUSTRIAL COMPLEX";
        [TextArea] public string description = "Foundry halls, catwalks and container lanes.";
        public ArenaKind kind = ArenaKind.Industrial;
        public string sceneName = "Arena_Industrial";
        public Color uiAccent = new Color(.2f, .95f, 1f);
        public Color fogColor = new Color(.10f, .12f, .16f);
        public float fogDensity = .012f;
    }
}
