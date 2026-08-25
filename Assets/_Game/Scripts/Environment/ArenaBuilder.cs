using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.Environment
{
    public static class ArenaBuilder
    {
        static readonly Vector3[] CoverPositions =
        {
            new(-8, 1, -5), new(8, 1, 5), new(-8, 1, 8), new(8, 1, -8),
            new(0, 1, 11), new(0, 1, -11), new(-13, 1, 0), new(13, 1, 0)
        };

        static readonly Vector3[] SpawnPositions =
        {
            new(-17, 0, -17), new(17, 0, -17), new(-17, 0, 17), new(17, 0, 17),
            new(0, 0, 17), new(0, 0, -17), new(17, 0, 0), new(-17, 0, 0)
        };

        public static List<Transform> Build()
        {
            BuildBounds();
            BuildCover();
            return BuildSpawnPoints();
        }

        static void BuildBounds()
        {
            PrimitiveBuilder.Box("Arena Floor", new Vector3(0, -.5f, 0), new Vector3(42, 1, 42), RuntimeMaterials.Floor);
            PrimitiveBuilder.Box("North Wall", new Vector3(0, 2, 20.5f), new Vector3(42, 5, 1), RuntimeMaterials.Wall);
            PrimitiveBuilder.Box("South Wall", new Vector3(0, 2, -20.5f), new Vector3(42, 5, 1), RuntimeMaterials.Wall);
            PrimitiveBuilder.Box("East Wall", new Vector3(20.5f, 2, 0), new Vector3(1, 5, 42), RuntimeMaterials.Wall);
            PrimitiveBuilder.Box("West Wall", new Vector3(-20.5f, 2, 0), new Vector3(1, 5, 42), RuntimeMaterials.Wall);
        }

        static void BuildCover()
        {
            foreach (Vector3 position in CoverPositions)
                PrimitiveBuilder.Box("Cover", position, new Vector3(4, 2, 1.5f), RuntimeMaterials.Cover);

            PrimitiveBuilder.Box("Raised Platform", new Vector3(0, .75f, 0), new Vector3(7, 1.5f, 7), RuntimeMaterials.Wall);
            for (int i = 0; i < 4; i++)
                PrimitiveBuilder.Box("Platform Step", new Vector3(0, .15f + i * .3f, -5.1f + i * .65f), new Vector3(3, .3f, 1.3f), RuntimeMaterials.Cover);
        }

        static List<Transform> BuildSpawnPoints()
        {
            var points = new List<Transform>();
            foreach (Vector3 position in SpawnPositions)
            {
                var marker = new GameObject("Enemy Spawn");
                marker.transform.position = position;
                points.Add(marker.transform);
            }
            return points;
        }
    }
}
