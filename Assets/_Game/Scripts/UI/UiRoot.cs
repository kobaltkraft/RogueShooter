using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using RogueArena.Core;

namespace RogueArena.UI
{
    /// <summary>
    /// Scene-local UI host. Creates the canvas + event system once per scene and
    /// spawns the right screen set: HUD + pause + end screens in arenas, the main
    /// menu in the menu scene. Also routes the Esc key to pause/resume.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class UiRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }

        Hud hud;
        PauseScreen pause;
        EndScreens end;
        MenuScreen menu;

        public static UiRoot Create(Transform parent)
        {
            var go = new GameObject("UiRoot");
            go.transform.SetParent(parent, false);
            return go.AddComponent<UiRoot>();
        }

        void Awake()
        {
            EnsureEventSystem();
            UiKit.CreateCanvasRoot("Canvas", transform, out Canvas canvas, out _);
            Canvas = canvas;
        }

        void Start()
        {
            if (Services.Session != null)
            {
                hud = gameObject.AddComponent<Hud>();
                hud.Build(Canvas);
                pause = PauseScreen.Build(this);
                end = EndScreens.Build(this);
            }
            else
            {
                menu = gameObject.AddComponent<MenuScreen>();
                menu.Build(Canvas);
            }
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void Update()
        {
            if (Services.Flow == null) return;

            // Esc / Start toggles pause only during gameplay or pause.
            bool pauseKey = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
            if (!pauseKey) return;

            var state = Services.Flow.State;
            if (state.IsGameplay)
            {
                Services.Flow.TryPause();
                pause?.Show();
            }
            else if (state.Current == GameState.Paused)
            {
                Resume();
            }
        }

        public void Resume()
        {
            Services.Flow.TryResume();
            pause?.Hide();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (hud != null) Destroy(hud);
            if (pause != null) Destroy(pause);
            if (end != null) Destroy(end);
            if (menu != null) Destroy(menu);
        }
    }
}
