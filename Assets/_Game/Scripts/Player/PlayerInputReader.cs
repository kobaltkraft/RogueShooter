using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueArena.Player
{
    /// <summary>
    /// Input abstraction. Actions are built in code so the game runs with zero
    /// asset wiring; the authored RogueArena.inputactions file mirrors these
    /// bindings for documentation.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        InputAction move, look, fire, jump, sprint, crouch, reload, dash;
        InputAction pause, interact, weapon1, weapon2, weapon3, scroll;

        public Vector2 Move => move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => look != null ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool FireHeld => fire != null && fire.IsPressed();
        public bool FirePressed => fire != null && fire.WasPressedThisFrame();
        public bool JumpPressed => jump != null && jump.WasPressedThisFrame();
        public bool SprintHeld => sprint != null && sprint.IsPressed();
        public bool CrouchHeld => crouch != null && crouch.IsPressed();
        public bool ReloadPressed => reload != null && reload.WasPressedThisFrame();
        public bool DashPressed => dash != null && dash.WasPressedThisFrame();
        public bool PausePressed => pause != null && pause.WasPressedThisFrame();
        public bool InteractPressed => interact != null && interact.WasPressedThisFrame();
        public bool Weapon1Pressed => weapon1 != null && weapon1.WasPressedThisFrame();
        public bool Weapon2Pressed => weapon2 != null && weapon2.WasPressedThisFrame();
        public bool Weapon3Pressed => weapon3 != null && weapon3.WasPressedThisFrame();
        /// <summary>Scroll delta; positive = up (next weapon).</summary>
        public float ScrollValue => scroll != null ? scroll.ReadValue<Vector2>().y : 0f;
        public bool ScrollUp => scroll != null && scroll.ReadValue<Vector2>().y > .1f && scroll.WasPerformedThisFrame();
        public bool ScrollDown => scroll != null && scroll.ReadValue<Vector2>().y < -.1f && scroll.WasPerformedThisFrame();

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

            look = new InputAction("Look", binding: "<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            fire = new InputAction("Fire", binding: "<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/rightTrigger");

            jump = new InputAction("Jump", binding: "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            sprint = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            crouch = new InputAction("Crouch", binding: "<Keyboard>/leftCtrl");
            crouch.AddBinding("<Gamepad>/buttonEast");

            reload = new InputAction("Reload", binding: "<Keyboard>/r");
            reload.AddBinding("<Gamepad>/buttonNorth");

            dash = new InputAction("Dash", binding: "<Keyboard>/q");
            dash.AddBinding("<Gamepad>/leftShoulder");

            pause = new InputAction("Pause", binding: "<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            interact = new InputAction("Interact", binding: "<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");

            weapon1 = new InputAction("Weapon1", binding: "<Keyboard>/1");
            weapon2 = new InputAction("Weapon2", binding: "<Keyboard>/2");
            weapon3 = new InputAction("Weapon3", binding: "<Keyboard>/3");

            scroll = new InputAction("Scroll", binding: "<Mouse>/scroll");
            scroll.AddBinding("<Gamepad>/dpad");
        }

        void OnEnable()
        {
            move?.Enable(); look?.Enable(); fire?.Enable(); jump?.Enable();
            sprint?.Enable(); crouch?.Enable(); reload?.Enable(); dash?.Enable();
            pause?.Enable(); interact?.Enable(); weapon1?.Enable(); weapon2?.Enable();
            weapon3?.Enable(); scroll?.Enable();
        }

        void OnDisable()
        {
            move?.Disable(); look?.Disable(); fire?.Disable(); jump?.Disable();
            sprint?.Disable(); crouch?.Disable(); reload?.Disable(); dash?.Disable();
            pause?.Disable(); interact?.Disable(); weapon1?.Disable(); weapon2?.Disable();
            weapon3?.Disable(); scroll?.Disable();
        }
    }
}
