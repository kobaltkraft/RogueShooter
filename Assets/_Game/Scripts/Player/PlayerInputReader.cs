using UnityEngine;
using UnityEngine.InputSystem;
namespace RogueArena.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        InputAction move,look,fire,jump,sprint,crouch,reload;
        public Vector2 Move=>move.ReadValue<Vector2>(); public Vector2 Look=>look.ReadValue<Vector2>();
        public bool FireHeld=>fire.IsPressed(); public bool JumpPressed=>jump.WasPressedThisFrame(); public bool SprintHeld=>sprint.IsPressed();
        public bool CrouchHeld=>crouch.IsPressed(); public bool ReloadPressed=>reload.WasPressedThisFrame();
        void Awake(){ move=new("Move",InputActionType.Value,"<Gamepad>/leftStick"); move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d"); look=new("Look",binding:"<Mouse>/delta"); fire=new("Fire",binding:"<Mouse>/leftButton"); jump=new("Jump",binding:"<Keyboard>/space"); sprint=new("Sprint",binding:"<Keyboard>/leftShift"); crouch=new("Crouch",binding:"<Keyboard>/leftCtrl"); reload=new("Reload",binding:"<Keyboard>/r"); }
        void OnEnable(){move.Enable();look.Enable();fire.Enable();jump.Enable();sprint.Enable();crouch.Enable();reload.Enable();}
        void OnDisable(){move.Disable();look.Disable();fire.Disable();jump.Disable();sprint.Disable();crouch.Disable();reload.Disable();}
    }
}
