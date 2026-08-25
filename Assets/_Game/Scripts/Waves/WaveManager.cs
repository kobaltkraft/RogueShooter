using System.Collections; using System.Collections.Generic; using UnityEngine; using RogueArena.AI; using RogueArena.Core; using RogueArena.Factories;
namespace RogueArena.Waves
{
    public class WaveManager : MonoBehaviour
    {
        readonly int[] waves={3,5,7}; readonly List<Transform> spawns=new(); Transform player; int wave=-1,alive; bool complete;
        public void Initialize(Transform target,IEnumerable<Transform> points){player=target;spawns.AddRange(points);GameEvents.EnemyDied+=OnEnemyDied;StartCoroutine(BeginNext(1));}
        void OnDestroy()=>GameEvents.EnemyDied-=OnEnemyDied;
        IEnumerator BeginNext(float delay){yield return new WaitForSeconds(delay);wave++;if(wave>=waves.Length){complete=true;GameEvents.Victory?.Invoke();yield break;}alive=waves[wave];GameEvents.WaveChanged?.Invoke(wave+1,alive);for(int i=0;i<alive;i++){SpawnEnemy(PickSpawn(i));yield return new WaitForSeconds(.25f);}}
        Transform PickSpawn(int seed){var valid=spawns.FindAll(s=>Vector3.Distance(s.position,player.position)>10);return (valid.Count>0?valid:spawns)[seed%(valid.Count>0?valid.Count:spawns.Count)];}
        void SpawnEnemy(Transform point)=>EnemyFactory.Create(point.position,player);
        void OnEnemyDied(EnemyBrain enemy){if(complete)return;alive=Mathf.Max(0,alive-1);GameEvents.WaveChanged?.Invoke(wave+1,alive);if(alive==0)StartCoroutine(BeginNext(2));}
    }
}
