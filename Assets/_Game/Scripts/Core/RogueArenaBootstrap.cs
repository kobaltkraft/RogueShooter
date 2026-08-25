using UnityEngine; using Unity.AI.Navigation; using RogueArena.Waves; using RogueArena.Environment; using RogueArena.Factories; using RogueArena.UI;
namespace RogueArena.Core
{
    public static class RogueArenaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Build(){if(Object.FindAnyObjectByType<GameManager>()!=null)return;var arena=new GameObject("Rogue Arena Runtime");RuntimeMaterials.Initialize();var spawns=ArenaBuilder.Build();var surface=arena.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.All;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();CreateLighting();var player=PlayerFactory.Create();var game=arena.AddComponent<GameManager>();game.Initialize(player.Health);var waves=arena.AddComponent<WaveManager>();waves.Initialize(player.Transform,spawns);var hud=arena.AddComponent<HudController>();hud.Initialize(player.Health,player.Weapon);}
        static void CreateLighting(){var lightGo=new GameObject("Arena Sun");lightGo.transform.rotation=Quaternion.Euler(48,-35,0);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.color=new Color(1,.92f,.82f);RenderSettings.ambientLight=new Color(.22f,.25f,.32f);}
    }
}
