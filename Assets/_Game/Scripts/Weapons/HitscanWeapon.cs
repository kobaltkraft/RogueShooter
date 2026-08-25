using System; using System.Collections; using UnityEngine; using RogueArena.Combat; using RogueArena.Player;
namespace RogueArena.Weapons
{
    public class HitscanWeapon : MonoBehaviour
    {
        WeaponDefinition data; Camera aim; PlayerInputReader input; int magazine,reserve; float nextShot; bool reloading; ParticleSystem muzzle; AudioSource audioSource; FirstPersonController controller;
        public int Magazine=>magazine; public int Reserve=>reserve; public bool IsReloading=>reloading; public event Action AmmoChanged; public event Action Hit;
        public void Initialize(WeaponDefinition definition,Camera camera,PlayerInputReader reader,ParticleSystem flash){data=definition;aim=camera;input=reader;muzzle=flash;magazine=data.magazineSize;reserve=data.reserveAmmo;audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;audioSource.clip=CreateClick();controller=camera.GetComponentInParent<FirstPersonController>();}
        void Update(){if(data==null)return;if(input.ReloadPressed)TryReload();if(input.FireHeld&&!reloading&&Time.time>=nextShot)Fire();}
        void Fire(){if(magazine<=0){TryReload();return;} nextShot=Time.time+1/data.fireRate;magazine--;AmmoChanged?.Invoke();muzzle?.Emit(1);controller?.AddRecoil(data.recoil);audioSource.PlayOneShot(audioSource.clip); Vector2 random=UnityEngine.Random.insideUnitCircle*data.spread; Vector3 direction=(aim.transform.forward+aim.transform.right*random.x+aim.transform.up*random.y).normalized; if(Physics.Raycast(aim.transform.position,direction,out RaycastHit hit,data.range,~0,QueryTriggerInteraction.Ignore)){bool head=hit.collider.name=="Head"; IDamageable target=hit.collider.GetComponentInParent<IDamageable>();if(target!=null&&target.IsAlive){target.TakeDamage(new DamageInfo(data.damage*(head?data.headshotMultiplier:1),hit.point,direction,head));Hit?.Invoke();} SpawnImpact(hit.point,hit.normal);} }
        void TryReload(){if(!reloading&&magazine<data.magazineSize&&reserve>0)StartCoroutine(Reload());}
        IEnumerator Reload(){reloading=true;AmmoChanged?.Invoke();yield return new WaitForSeconds(data.reloadTime);int count=Mathf.Min(data.magazineSize-magazine,reserve);magazine+=count;reserve-=count;reloading=false;AmmoChanged?.Invoke();}
        static void SpawnImpact(Vector3 p,Vector3 n){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Hit Spark";go.transform.position=p+n*.02f;go.transform.localScale=Vector3.one*.06f;UnityEngine.Object.Destroy(go.GetComponent<Collider>());UnityEngine.Object.Destroy(go,.12f);}
        static AudioClip CreateClick(){int rate=22050,length=900;float[] samples=new float[length];for(int i=0;i<length;i++)samples[i]=(UnityEngine.Random.value*2-1)*Mathf.Exp(-i/150f);var clip=AudioClip.Create("Rifle Placeholder",length,1,rate,false);clip.SetData(samples,0);return clip;}
    }
}
