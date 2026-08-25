using UnityEngine;
namespace RogueArena.Player
{
    /// <summary>
    /// Arcade-feel first-person locomotion with smooth acceleration curves,
    /// head bob, FOV kick on sprint, coyote-time jumps, jump buffering,
    /// landing impact feedback, and integration with <see cref="DashController"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        // --- Tunables (serialised) ---
        [Header("Speeds")]
        [SerializeField] float walkSpeed = 6;
        [SerializeField] float sprintSpeed = 9;
        [SerializeField] float crouchSpeed = 3;

        [Header("Acceleration")]
        [SerializeField] float groundAccel = 12;
        [SerializeField] float groundDecel = 16;
        [SerializeField] float airAccel = 5;
        [SerializeField] float airDecel = 2.5f;

        [Header("Jump")]
        [SerializeField] float jumpHeight = 1.3f;
        [SerializeField] float gravity = -25;
        [Tooltip("Extra seconds after walking off a ledge where jumping is still allowed.")]
        [SerializeField] float coyoteTime = .12f;
        [Tooltip("Seconds a jump press is remembered before landing, so it's not lost.")]
        [SerializeField] float jumpBufferTime = .15f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = .12f;

        [Header("Head Bob")]
        [SerializeField] float bobFrequency = 9f;
        [SerializeField] float bobAmplitude = .04f;

        [Header("FOV Kick")]
        [SerializeField] float sprintFovKick = 6f;
        [SerializeField] float fovKickSpeed = 10f;

        [Header("Landing Impact")]
        [SerializeField] float landImpactAmount = .12f;
        [SerializeField] float landImpactRecovery = 6f;

        // --- Internal state ---
        CharacterController controller;
        PlayerInputReader input;
        Transform view;
        Camera viewCamera;
        float pitch, vertical, recoilPitch;
        float standingHeight = 1.8f;
        float currentSpeed;         // Smoothed horizontal speed
        float bobTimer;             // Head-bob phase
        float fovKickCurrent;       // Current FOV kick amount
        float landImpact;           // Camera dip on landing (negative = down)
        float baseFov;              // Camera's configured FOV

        // Coyote & buffer timers
        float groundTimer;          // Time since last grounded
        float jumpBufferTimer;      // Time since last jump press

        // Dash integration
        DashController dash;
        bool wasGrounded;

        // --- Public API ---

        public void Initialize(Transform cameraTransform)
        {
            view = cameraTransform;
            viewCamera = cameraTransform.GetComponent<Camera>();
            if (viewCamera) baseFov = viewCamera.fieldOfView;
        }

        public void AddRecoil(float amount) { recoilPitch = Mathf.Min(recoilPitch + amount, 8f); }

        // --- Lifecycle ---

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            dash = GetComponent<DashController>(); // May be null if added later; Start re-checks.
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (dash == null) dash = GetComponent<DashController>();
        }

        void Update()
        {
            if (view == null) return;

            float dt = Time.deltaTime;

            HandleLook(dt);
            HandleCrouch(dt);
            HandleMovement(dt);
            HandleHeadBob(dt);
            HandleLandingImpact(dt);
            ApplyFovKick(dt);
        }

        // --- Look ---

        void HandleLook(float dt)
        {
            Vector2 look = input.Look * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y, -88, 88);
            recoilPitch = Mathf.MoveTowards(recoilPitch, 0, 8f * dt);
            view.localRotation = Quaternion.Euler(pitch - recoilPitch + landImpact, 0, 0);
            transform.Rotate(0, look.x, 0);
        }

        // --- Crouch ---

        void HandleCrouch(float dt)
        {
            bool crouch = input.CrouchHeld;
            float target = crouch ? 1.1f : standingHeight;
            controller.height = Mathf.MoveTowards(controller.height, target, 5 * dt);
            controller.center = Vector3.up * controller.height * .5f;
        }

        // --- Movement ---

        void HandleMovement(float dt)
        {
            bool grounded = controller.isGrounded;
            bool crouch = input.CrouchHeld;
            bool sprinting = input.SprintHeld && !crouch;

            // --- Coyote time & jump buffering ---
            if (grounded) groundTimer = coyoteTime;
            else groundTimer -= dt;

            if (input.JumpPressed) jumpBufferTimer = jumpBufferTime;
            else jumpBufferTimer -= dt;

            // --- Target speed ---
            float targetSpeed = crouch ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);
            Vector2 m = input.Move;
            float inputMag = m.magnitude;

            // Smoothly blend current speed toward desired
            if (grounded)
            {
                if (inputMag > .01f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * inputMag, groundAccel * dt);
                else
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, groundDecel * dt);
            }
            else
            {
                // Air: gentler accel, faster decel so you don't fly uncontrollably
                if (inputMag > .01f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * inputMag, airAccel * dt);
                else
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, airDecel * dt);
            }

            // --- Horizontal motion ---
            Vector3 motion = (transform.right * m.x + transform.forward * m.y) * currentSpeed;

            // --- Jump ---
            bool canJump = grounded || groundTimer > 0;
            bool wantsJump = jumpBufferTimer > 0;
            bool dashBlocking = dash != null && dash.IsDashing;

            if (canJump && wantsJump && !crouch && !dashBlocking)
            {
                vertical = Mathf.Sqrt(jumpHeight * -2 * gravity);
                groundTimer = 0;
                jumpBufferTimer = 0;
            }

            // --- Gravity ---
            if (grounded && vertical < 0)
                vertical = -2; // Snap to ground; avoids accumulation on slopes
            vertical += gravity * dt;
            motion.y = vertical;

            // --- Apply ---
            controller.Move(motion * dt);

            // --- Landing detection ---
            bool nowGrounded = controller.isGrounded;
            if (!wasGrounded && nowGrounded && vertical < -4f)
            {
                float impact = Mathf.Clamp01(-vertical / 30f) * landImpactAmount;
                landImpact = -impact; // Camera dips down
            }
            wasGrounded = nowGrounded;
        }

        // --- Head Bob ---

        void HandleHeadBob(float dt)
        {
            if (!controller.isGrounded || currentSpeed < .5f)
            {
                // Smoothly return to neutral
                bobTimer = Mathf.MoveTowards(bobTimer, 0, 4 * dt);
                return;
            }

            bobTimer += dt * bobFrequency * (currentSpeed / walkSpeed);
            float yOffset = Mathf.Sin(bobTimer * Mathf.PI * 2) * bobAmplitude;
            view.localPosition = new Vector3(view.localPosition.x, 1.62f + yOffset, view.localPosition.z);
        }

        // --- Landing Impact ---

        void HandleLandingImpact(float dt)
        {
            landImpact = Mathf.MoveTowards(landImpact, 0, landImpactRecovery * dt);
        }

        // --- FOV Kick ---

        void ApplyFovKick(float dt)
        {
            float target = (input.SprintHeld && currentSpeed > walkSpeed) ? sprintFovKick : 0;
            fovKickCurrent = Mathf.MoveTowards(fovKickCurrent, target, fovKickSpeed * dt);

            if (viewCamera)
                viewCamera.fieldOfView = baseFov + fovKickCurrent;
        }
    }
}
