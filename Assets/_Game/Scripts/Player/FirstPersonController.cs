using UnityEngine;
using RogueArena.Audio;
using RogueArena.Core;

namespace RogueArena.Player
{
    /// <summary>
    /// Arcade first-person locomotion: smooth ground/air acceleration, sprint,
    /// crouch, slide, coyote-time jumps with buffering, bunny-hop momentum
    /// retention, external impulses (rocket jumps, explosions) and configurable
    /// gravity scaling (low-gravity wave modifier). Camera presentation is
    /// delegated to <see cref="CameraRig"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Speeds")]
        [SerializeField] float walkSpeed = 7f;
        [SerializeField] float sprintSpeed = 10.5f;
        [SerializeField] float crouchSpeed = 3.4f;

        [Header("Acceleration")]
        [SerializeField] float groundAccel = 14f;
        [SerializeField] float groundDecel = 18f;
        [SerializeField] float airAccel = 7f;
        [SerializeField] float airDecel = 1.2f;
        [Tooltip("How much steering control the player keeps while airborne (0-1).")]
        [SerializeField, Range(0f, 1f)] float airControl = .85f;

        [Header("Jump")]
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float gravity = -26f;
        [SerializeField] float coyoteTime = .12f;
        [SerializeField] float jumpBufferTime = .15f;
        [Tooltip("Horizontal speed retained when jump is buffered on landing (bunny hopping).")]
        [SerializeField, Range(.5f, 1f)] float bhopSpeedRetention = 1f;

        [Header("Slide")]
        [SerializeField] float slideBoost = 1.45f;
        [SerializeField] float slideDuration = .85f;
        [SerializeField] float slideCooldown = .6f;
        [SerializeField] float slideMinSpeed = 6f;

        [Header("Crouch")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchedHeight = 1.15f;
        [SerializeField] float crouchTransitionSpeed = 8f;
        [SerializeField] float standingEyeHeight = 1.62f;
        [SerializeField] float crouchedEyeHeight = .95f;

        [Header("Look")]
        [SerializeField] float mouseSensitivityBase = .12f;

        [Header("Audio")]
        [SerializeField] float footstepInterval = 2.6f; // world units travelled

        // --- components ---
        CharacterController controller;
        PlayerInputReader input;
        CameraRig rig;
        DashController dash;

        // --- movement state ---
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float groundTimer;
        float jumpBufferTimer;
        bool wasGrounded;
        bool sprinting;
        bool crouching;
        bool sliding;
        float slideTimer;
        float slideCooldownTimer;
        Vector3 slideDirection;

        // --- look ---
        float pitch;
        float sensitivityMultiplier = 1f;
        bool invertY;

        // --- modifiers ---
        float speedMultiplier = 1f;
        float gravityScale = 1f;

        // --- feedback ---
        float footstepDistance;
        float currentHeight;

        // --- public state for other systems ---
        public bool IsGrounded { get; private set; }
        public bool IsSprinting => sprinting && !crouching;
        public bool IsCrouching => crouching;
        public bool IsSliding => sliding;
        public bool IsDashing => dash != null && dash.IsDashing;
        public float HorizontalSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        public Vector3 HorizontalVelocityVector => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        public float HorizontalSpeed01 => Mathf.Clamp01(HorizontalSpeed / sprintSpeed);
        public float MaxRunSpeed => sprintSpeed * speedMultiplier;
        public bool InputEnabled { get; set; } = true;
        public CharacterController Controller => controller;

        public event System.Action Landed;

        public void Initialize(CameraRig cameraRig)
        {
            rig = cameraRig;
            ApplySettings();
            if (Services.Settings != null) Services.Settings.Applied += ApplySettings;
        }

        void OnDestroy()
        {
            if (Services.Settings != null) Services.Settings.Applied -= ApplySettings;
        }

        void ApplySettings()
        {
            if (Services.Settings == null) return;
            sensitivityMultiplier = Services.Settings.Data.mouseSensitivity;
            invertY = Services.Settings.Data.invertY;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            dash = GetComponent<DashController>();
            controller.height = standingHeight;
            controller.center = Vector3.up * (standingHeight * .5f);
            currentHeight = standingHeight;
        }

        void Start()
        {
            if (dash == null) dash = GetComponent<DashController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (rig == null) return;
            float dt = Time.deltaTime;

            HandleLook(dt);
            HandleCrouch(dt);
            HandleSlide(dt);
            HandleMovement(dt);

            // Publish state for the camera rig.
            rig.SourcePitch = pitch;
            rig.StrafeInput = input.Move.x;
            rig.HorizontalSpeed01 = HorizontalSpeed01;
            rig.Grounded = IsGrounded;
            rig.Sprinting = IsSprinting;
            rig.Sliding = sliding;
            rig.SlideDirection = Mathf.Sign(input.Move.x != 0 ? input.Move.x : 0f);
        }

        // ---------------------------------------------------------------- look

        void HandleLook(float dt)
        {
            // Ignore look while paused / cursor free (pause menu, death, end screens).
            if (!InputEnabled || Cursor.lockState != CursorLockMode.Locked)
            {
                rig.LookDelta = Vector2.zero;
                return;
            }

            Vector2 look = input.Look * (mouseSensitivityBase * sensitivityMultiplier);
            float yaw = look.x;
            float pitchDelta = invertY ? look.y : -look.y;
            pitch = Mathf.Clamp(pitch + pitchDelta, -88f, 88f);
            transform.Rotate(0f, yaw, 0f);
            rig.LookDelta = look;
        }

        // ---------------------------------------------------------------- crouch

        void HandleCrouch(float dt)
        {
            bool wantsCrouch = input.CrouchHeld && InputEnabled;

            // Start slide: crouch while sprinting fast.
            if (!sliding && !crouching && wantsCrouch && IsGrounded && !IsDashing
                && slideCooldownTimer <= 0f && HorizontalSpeed >= slideMinSpeed && sprinting)
            {
                BeginSlide();
            }

            crouching = sliding || (wantsCrouch && !IsDashing);

            float targetHeight = crouching ? crouchedHeight : standingHeight;

            // Do not stand up into a ceiling.
            if (!crouching && currentHeight < standingHeight - .01f && HasCeilingAbove())
                targetHeight = crouchedHeight;

            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchTransitionSpeed * dt);
            controller.height = currentHeight;
            controller.center = Vector3.up * (currentHeight * .5f);

            // Move the camera eye height smoothly.
            float eye = Mathf.Lerp(crouchedEyeHeight, standingEyeHeight,
                Mathf.InverseLerp(crouchedHeight, standingHeight, currentHeight));
            if (rig != null) rig.BaseLocalPosition = new Vector3(0f, eye, 0f);
        }

        bool HasCeilingAbove()
        {
            Vector3 origin = transform.position + Vector3.up * (currentHeight - controller.radius);
            float castDist = standingHeight - currentHeight + .05f;
            return Physics.SphereCast(origin, controller.radius * .9f, Vector3.up, out _, castDist,
                Layers.EnvironmentMask | Layers.HazardMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- slide

        void BeginSlide()
        {
            sliding = true;
            slideTimer = slideDuration;
            Vector3 dir = horizontalVelocity;
            dir.y = 0f;
            slideDirection = dir.sqrMagnitude > .1f ? dir.normalized : transform.forward;
            horizontalVelocity = slideDirection * (sprintSpeed * slideBoost);
            Services.Audio?.Play("dash", .5f, .8f);
        }

        void EndSlide()
        {
            sliding = false;
            slideCooldownTimer = slideCooldown;
        }

        void HandleSlide(float dt)
        {
            if (slideCooldownTimer > 0f) slideCooldownTimer -= dt;
            if (!sliding) return;

            slideTimer -= dt;
            bool crouchHeld = input.CrouchHeld;
            if (slideTimer <= 0f || !crouchHeld || !IsGrounded || HorizontalSpeed < crouchSpeed * 1.1f)
            {
                EndSlide();
                return;
            }

            // Slide keeps momentum with mild friction.
            float t = 1f - Mathf.Clamp01(slideTimer / slideDuration);
            float targetSpeed = Mathf.Lerp(sprintSpeed * slideBoost, crouchSpeed, t * t);
            horizontalVelocity = slideDirection * targetSpeed;
        }

        // ---------------------------------------------------------------- movement

        void HandleMovement(float dt)
        {
            bool grounded = controller.isGrounded;
            IsGrounded = grounded;

            // --- coyote time & jump buffering ---
            if (grounded) groundTimer = coyoteTime;
            else groundTimer -= dt;

            if (InputEnabled && input.JumpPressed) jumpBufferTimer = jumpBufferTime;
            else jumpBufferTimer -= dt;

            // --- desired direction ---
            Vector2 moveInput = InputEnabled ? input.Move : Vector2.zero;
            sprinting = InputEnabled && input.SprintHeld && moveInput.y > .1f;
            float inputMag = Mathf.Clamp01(moveInput.magnitude);
            Vector3 wishDir = (transform.right * moveInput.x + transform.forward * moveInput.y);
            wishDir.y = 0f;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            float targetSpeed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);
            targetSpeed *= speedMultiplier;

            if (sliding || IsDashing)
            {
                // Slide/dash manage horizontal velocity themselves.
            }
            else if (grounded)
            {
                Vector3 target = wishDir * (targetSpeed * inputMag);
                float accel = inputMag > .01f ? groundAccel : groundDecel;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * dt);

                // Bunny-hop: buffered jump on landing keeps momentum.
                if (jumpBufferTimer > 0f && inputMag > .01f)
                {
                    float projected = Vector3.Dot(horizontalVelocity.normalized, wishDir.normalized);
                    if (projected > .7f && HorizontalSpeed > targetSpeed * .8f)
                        horizontalVelocity = horizontalVelocity.normalized * (HorizontalSpeed * bhopSpeedRetention);
                }
            }
            else
            {
                // Air control: steer without exceeding the current speed budget.
                Vector3 target = wishDir * (targetSpeed * inputMag);
                float accel = (inputMag > .01f ? airAccel : airDecel) * airControl;
                Vector3 desired = Vector3.MoveTowards(horizontalVelocity, target, accel * dt);

                // Only allow the air steering to change direction, not to add speed
                // beyond what was carried from the ground (preserves rocket/bhop momentum).
                if (desired.magnitude > Mathf.Max(targetSpeed, horizontalVelocity.magnitude))
                    desired = desired.normalized * Mathf.Max(targetSpeed, horizontalVelocity.magnitude);
                horizontalVelocity = desired;
            }

            // --- jump ---
            bool dashBlocking = IsDashing;
            bool canJump = (grounded || groundTimer > 0f) && !crouching && !dashBlocking;
            if (canJump && jumpBufferTimer > 0f)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity * gravityScale);
                groundTimer = 0f;
                jumpBufferTimer = 0f;
                Services.Audio?.Play("jump", .4f, Random.Range(.9f, 1.1f));
                GameEvents.NoiseEmitted?.Invoke(transform.position, 10f);
            }

            // --- gravity ---
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2.5f;
            verticalVelocity += gravity * gravityScale * dt;

            // --- apply ---
            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            if (dashBlocking) motion.y = 0f; // dash controller owns vertical during dash
            controller.Move(motion * dt);

            // --- landing ---
            bool nowGrounded = controller.isGrounded;
            if (!wasGrounded && nowGrounded)
            {
                float impact01 = Mathf.Clamp01(-verticalVelocity / 26f);
                rig.NotifyLanded(impact01);
                Landed?.Invoke();
                if (impact01 > .1f)
                    Services.Audio?.Play("land", .5f * impact01 + .2f, 1f);
            }
            wasGrounded = nowGrounded;

            // --- footsteps ---
            if (nowGrounded && HorizontalSpeed > 1f && !sliding)
            {
                footstepDistance += HorizontalSpeed * dt;
                if (footstepDistance >= footstepInterval)
                {
                    footstepDistance = 0f;
                    Services.Audio?.Play("footstep", .35f, Random.Range(.92f, 1.08f));
                    GameEvents.NoiseEmitted?.Invoke(transform.position, 8f);
                }
            }
        }

        // ---------------------------------------------------------------- modifiers & impulses

        /// <summary>Sets the movement speed multiplier (powerups, upgrades).</summary>
        public void SetSpeedMultiplier(float multiplier) => speedMultiplier = multiplier;

        /// <summary>Sets gravity scaling (low-gravity wave modifier).</summary>
        public void SetGravityScale(float scale) => gravityScale = Mathf.Max(.05f, scale);

        /// <summary>External velocity impulse (explosions, rocket jumps).</summary>
        public void AddImpulse(Vector3 impulse)
        {
            horizontalVelocity += new Vector3(impulse.x, 0f, impulse.z);
            verticalVelocity = Mathf.Max(verticalVelocity, impulse.y);
        }

        /// <summary>Resets transient movement state (respawn / restart).</summary>
        public void ResetState()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pitch = 0f;
            sliding = false;
            crouching = false;
            slideCooldownTimer = 0f;
            if (rig != null) rig.ClearRecoil();
        }
    }
}
