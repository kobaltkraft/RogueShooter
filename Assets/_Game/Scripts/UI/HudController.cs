using System.Collections; using UnityEngine; using RogueArena.Combat; using RogueArena.Weapons; using RogueArena.Core;
namespace RogueArena.UI
{
    public class HudController : MonoBehaviour
    {
        Health health; HitscanWeapon weapon; int wave,enemies; float hitUntil; string endMessage=""; GUIStyle text,big,center; Texture2D white;
        public void Initialize(Health h,HitscanWeapon w){health=h;weapon=w;weapon.Hit+=()=>hitUntil=Time.time+.12f;GameEvents.WaveChanged+=OnWave;GameEvents.Victory+=()=>endMessage="VICTORY";GameEvents.Defeat+=()=>endMessage="DEFEAT";white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();}
        void OnDestroy(){GameEvents.WaveChanged-=OnWave;}
        void OnWave(int w,int e){wave=w;enemies=e;}
        void EnsureStyles(){if(text!=null)return;text=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold};big=new GUIStyle(text){fontSize=48,alignment=TextAnchor.MiddleCenter};center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};}
        void OnGUI(){EnsureStyles();GUI.color=Color.white;GUI.Label(new Rect(24,20,300,35),$"HEALTH  {Mathf.CeilToInt(health.Current)}",text);GUI.Label(new Rect(Screen.width-250,20,230,35),$"WAVE {wave}   HOSTILES {enemies}",text);GUI.Label(new Rect(Screen.width-250,Screen.height-55,230,35),$"{weapon.Magazine:00} / {weapon.Reserve:000}",text);if(weapon.IsReloading)GUI.Label(new Rect(Screen.width/2-100,Screen.height*.7f,200,30),"RELOADING",center);DrawCrosshair();if(Time.time<hitUntil)DrawHitMarker();if(endMessage.Length>0){GUI.color=new Color(0,0,0,.75f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),white);GUI.color=Color.white;GUI.Label(new Rect(0,Screen.height/2-100,Screen.width,70),endMessage,big);if(GUI.Button(new Rect(Screen.width/2-90,Screen.height/2,180,48),"RESTART"))GameManager.Restart();}}
        void DrawCrosshair(){GUI.color=Color.white;GUI.DrawTexture(new Rect(Screen.width/2-1,Screen.height/2-9,2,18),white);GUI.DrawTexture(new Rect(Screen.width/2-9,Screen.height/2-1,18,2),white);}
        void DrawHitMarker(){GUI.color=Color.red;float x=Screen.width/2,y=Screen.height/2;GUIUtility.RotateAroundPivot(45,new Vector2(x,y));GUI.DrawTexture(new Rect(x-12,y-1,24,2),white);GUIUtility.RotateAroundPivot(-45,new Vector2(x,y));}
    }
}
