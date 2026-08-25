using System;
using UnityEngine;
namespace RogueArena.Player
{
    /// <summary>
    /// Modular dash component. Attach alongside <see cref="FirstPersonController"/>.
    /// Driven by a <see cref="DashDefinition"/> asset so designers can tune every
    /// parameter without touching code. Exposes <see cref="DashStarted"/> and
    /// <see cref="DashEnded"/> events so audio, VFX, or camera systems can react.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class DashController : MonoBehaviour
    {
        [Tooltip("ScriptableObject that defines distance, duration, cooldown, charges, and feel.")]
        public DashDefinition definition;

        /// <summary>Fired the instant a dash begins. Direction is the world-space vector.</summary>
        public event Action<Vector3> DashStarted;
        /// <summary>Fired when the dash finishes or is interrupted.</summary>
        public event Action DashEnded;

        public int ChargesRemaining => charges;
        public float CooldownProgress01 => definition == null ? 1f : Mathf.Clamp01(rechargeTimer / definition.rechargeTime);

        CharacterController controller;
        PlayerInputReader input;
        int charges;
        float rechargeTimer;
        float cooldownTimer;
        bool isDashing;
        float dashElapsed;
        Vector3 dashDirection;

        /// <summary>True while a dash is actively playing. Other systems (weapon spread, etc.) can query this.</summary>
        public bool IsDashing => isDashing;

        public void Initialize(DashDefinition def)
        {
            definition = def;
            charges = def.maxCharges;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        void Start()
        {
            if (definition != null) charges = definition.maxCharges;
        }

        void Update()
        {
            if (definition == null) return;

            // --- Recharge logic ---
            if (charges < definition.maxCharges)
            {
                if (cooldownTimer > 0)
                {
                    cooldownTimer -= Time.deltaTime;
                }
                else
                {
                    rechargeTimer += Time.deltaTime;
                    if (rechargeTimer >= definition.rechargeTime)
                    {
                        charges++;
                        rechargeTimer = 0;
                        if (charges < definition.maxCharges) cooldownTimer = definition.cooldownBeforeRecharge;
                    }
                }
            }

            // --- Input trigger ---
            if (!isDashing && input.DashPressed && charges > 0)
            {
                bool grounded = controller.isGrounded;
                if (grounded || definition.allowAirDash)
                {
                    BeginDash();
                }
            }

            // --- Active dash tick ---
            if (isDashing)
            {
                dashElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(dashElapsed / definition.duration);
                float curveSpeed = definition.speedCurve.Evaluate(t);

                float speed = (definition.distance / definition.duration) * curveSpeed;
                Vector3 motion = dashDirection * speed;
                // Gentle lift during dash to clear low obstacles
                float lift = Mathf.Sin(t * Mathf.PI) * definition.liftHeight;
                motion.y = lift / definition.duration;

                controller.Move(motion * Time.deltaTime);

                if (t >= 1f) EndDash();
            }
        }

        void BeginDash()
        {
            charges--;
            isDashing = true;
            dashElapsed = 0;

            // Determine dash direction from current input or facing
            Vector2 m = input.Move;
            Transform t = transform;
            if (m.sqrMagnitude > .01f)
                dashDirection = (t.right * m.x + t.forward * m.y).normalized;
            else
                dashDirection = t.forward;

            // Reset recharge tracking for next charge
            rechargeTimer = 0;
            cooldownTimer = definition.cooldownBeforeRecharge;

            DashStarted?.Invoke(dashDirection);
        }

        void EndDash()
        {
            isDashing = false;
            dashDirection = Vector3.zero;
            DashEnded?.Invoke();
        }
    }
}
