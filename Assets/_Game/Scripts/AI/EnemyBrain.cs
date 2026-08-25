using UnityEngine; using UnityEngine.AI; using RogueArena.Combat; using RogueArena.Core;
namespace RogueArena.AI
{
    public enum EnemyState { Idle,Patrol,Chase,Attack,Search,Dead }
    [RequireComponent(typeof(NavMeshAgent),typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        [SerializeField] float sightDistance=24,fieldOfView=110,attackDistance=2.2f,attackDamage=12,attackInterval=1f,searchDuration=4f;
        NavMeshAgent agent; Health health; Transform player; Health playerHealth; Vector3 home,lastKnown; EnemyState state; float nextAttack,stateTimer;
        public EnemyState State=>state;
        public void Initialize(Transform target){player=target;playerHealth=target.GetComponent<Health>();home=transform.position;}
        void Awake(){agent=GetComponent<NavMeshAgent>();health=GetComponent<Health>();health.Died+=Die;agent.speed=3.6f;agent.angularSpeed=540;agent.stoppingDistance=attackDistance*.8f;}
        void Update(){if(state==EnemyState.Dead||player==null||!playerHealth.IsAlive)return;bool sees=CanSeePlayer();if(sees){lastKnown=player.position;float d=Vector3.Distance(transform.position,player.position);SetState(d<=attackDistance?EnemyState.Attack:EnemyState.Chase);}else if(state==EnemyState.Chase||state==EnemyState.Attack){SetState(EnemyState.Search);stateTimer=searchDuration;} switch(state){case EnemyState.Idle:case EnemyState.Patrol: if(sees)SetState(EnemyState.Chase);break;case EnemyState.Chase:agent.SetDestination(player.position);break;case EnemyState.Attack:agent.ResetPath();FacePlayer();if(Time.time>=nextAttack){nextAttack=Time.time+attackInterval;playerHealth.TakeDamage(new DamageInfo(attackDamage,player.position,(player.position-transform.position).normalized));}break;case EnemyState.Search:agent.SetDestination(lastKnown);stateTimer-=Time.deltaTime;if(sees)SetState(EnemyState.Chase);else if(stateTimer<=0&&(!agent.pathPending&&agent.remainingDistance<1))SetState(EnemyState.Idle);break;}}
        bool CanSeePlayer(){Vector3 eye=transform.position+Vector3.up*1.35f,target=player.position+Vector3.up*.9f,to=target-eye;if(to.sqrMagnitude>sightDistance*sightDistance||Vector3.Angle(transform.forward,to)>fieldOfView*.5f)return false;return Physics.Raycast(eye,to.normalized,out var hit,sightDistance,~0,QueryTriggerInteraction.Ignore)&&hit.transform.root==player.root;}
        void FacePlayer(){Vector3 d=player.position-transform.position;d.y=0;if(d.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),540*Time.deltaTime);}
        void SetState(EnemyState next){if(state==next)return;state=next;if(agent.isOnNavMesh&&next!=EnemyState.Chase&&next!=EnemyState.Search)agent.ResetPath();}
        void Die(DamageInfo info){state=EnemyState.Dead;agent.enabled=false;foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;transform.Rotate(75,0,20);GetComponentInChildren<Renderer>().material.color=Color.black;GameEvents.EnemyDied?.Invoke(this);Destroy(gameObject,1.5f);}
    }
}
