using UnityEngine; using UnityEngine.SceneManagement; using RogueArena.Combat;
namespace RogueArena.Core
{
    public class GameManager : MonoBehaviour
    {
        bool ended; public void Initialize(Health player){player.Died+=_=>Defeat();GameEvents.Victory+=OnVictory;}
        void OnDestroy(){GameEvents.Victory-=OnVictory;}
        void OnVictory(){if(ended)return;ended=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void Defeat(){if(ended)return;ended=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;GameEvents.Defeat?.Invoke();}
        public static void Restart()=>SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
