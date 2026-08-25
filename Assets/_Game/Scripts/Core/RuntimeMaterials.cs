using UnityEngine;

namespace RogueArena.Core
{
    public static class RuntimeMaterials
    {
        public static Material Floor { get; private set; }
        public static Material Wall { get; private set; }
        public static Material Cover { get; private set; }
        public static Material Enemy { get; private set; }
        public static Material Accent { get; private set; }

        public static void Initialize()
        {
            Floor = Create("Floor", new Color(.12f, .14f, .18f));
            Wall = Create("Walls", new Color(.23f, .27f, .34f));
            Cover = Create("Cover", new Color(.14f, .35f, .42f));
            Enemy = Create("Enemy", new Color(.75f, .16f, .1f));
            Accent = Create("Accent", new Color(.2f, .75f, 1f));
        }

        static Material Create(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { name = name, color = color };
        }
    }
}
