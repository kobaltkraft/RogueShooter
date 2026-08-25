using System;
using UnityEngine;
using RogueArena.Audio;
using RogueArena.Core;

namespace RogueArena.Player
{
    /// <summary>
    /// Modular dash ability driven by a <see cref="DashDefinition"/> asset.
    /// Direction-aware, configurable distance/duration/curve/cooldown/charges,
    /// air-dash toggle, lift height and FOV kick - all data, no code changes
    /// needed to retune. Exposes start/end events for audio, VFX and camera hooks.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class DashController : MonoBehaviour
    {
        [Tooltip("ScriptableObject that defines distance, duration, cooldown, charges, and feel.")]
        public DashDefinition definition;

        /// <summary>Fired the instant a dash begins. Direction is world-space.</summary>
        public event Action<Vector3> DashStarted;
        /// <summary>Fired when the dash finishes or is interrupted.</summary>
        public event Action DashEnded;

        public int ChargesRemaining => charges;
        public int MaxCharges => definition != null ? definition.maxCharges : 0;
        public bool IsDashing => isDashing;
        /// <summary>0 = ready, 1 = just used. For HUD cooldown bars.</summary>
        public float CooldownProgress01
        {
            get
            {
                if (definition == null) return 1f;
                if (charges >= definition.maxCharges) return 0f;
                if (cooldownTimer > 0f) return .95f;
                return Mathf.Clamp01(rechargeTimer / RechargeTime);
            }
        }

        CharacterController controller;
        PlayerInputReader input;
        CameraRig rig;

        int charges;
        float rechargeTimer;
        float cooldownTimer;
        bool isDashing;
        float dashElapsed;
        Vector3 dashDirection;
        float rechargeMultiplier = 1f;

        /// <summary>Upgrade hook: values > 1 recharge faster.</summary>
        public void SetRechargeMultiplier(float multiplier) => rechargeMultiplier = Mathf.Max(.2f, multiplier);

        float RechargeTime => definition != null ? Mathf.Max(.1f, definition.rechargeTime / rechargeMultiplier) : 1f;

        public void Initialize(DashDefinition def)
        {
            definition = def;
            charges = def != null ? def.maxCharges : 0;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            rig = GetComponentInChildren<CameraRig>();
        }

        void Update()
        {
            if (definition == null) return;
            float dt = Time.deltaTime;

            // --- recharge ---
            if (charges < definition.maxCharges)
            {
                if (cooldownTimer > 0f) cooldownTimer -= dt;
                else
                {
                    rechargeTimer += dt;
                    if (rechargeTimer >= RechargeTime)
                    {
                        charges++;
                        rechargeTimer = 0f;
                        if (charges < definition.maxCharges) cooldownTimer = definition.cooldownBeforeRecharge;
                    }
                }
            }

            // --- input trigger ---
            if (!isDashing && input.DashPressed && charges > 0)
            {
                bool grounded = controller.isGrounded;
                if (grounded || definition.allowAirDash) BeginDash();
            }

            // --- active dash ---
            if (isDashing)
            {
                dashElapsed += dt;
                float t = Mathf.Clamp01(dashElapsed / definition.duration);
                float curveSpeed = definition.speedCurve.Evaluate(t);
                float speed = (definition.distance / definition.duration) * curveSpeed;
                Vector3 motion = dashDirection * speed;
                float lift = Mathf.Sin(t * Mathf.PI) * definition.liftHeight;
                motion.y = lift / definition.duration;
                controller.Move(motion * dt);

                // FOV kick eased in/out over the dash.
                if (rig != null) rig.SetDashFov(definition.fovKick * Mathf.Sin(t * Mathf.PI));

                if (t >= 1f) EndDash();
            }
        }

        void BeginDash()
        {
            charges--;
            isDashing = true;
            dashElapsed = 0f;

            Vector2 m = input.Move;
            Transform t = transform;
            dashDirection = m.sqrMagnitude > .01f
                ? (t.right * m.x + t.forward * m.y).normalized
                : t.forward;

            rechargeTimer = 0f;
            cooldownTimer = definition.cooldownBeforeRecharge;

            Services.Audio?.Play("dash", .6f);
            GameEvents.NoiseEmitted?.Invoke(transform.position, 12f);
            Services.Effects?.Poof(transform.position + Vector3.down * .6f, new Color(.5f, .8f, 1f, .6f));
            DashStarted?.Invoke(dashDirection);
        }

        void EndDash()
        {
            isDashing = false;
            dashDirection = Vector3.zero;
            if (rig != null) rig.SetDashFov(0f);
            DashEnded?.Invoke();
        }

        /// <summary>Interrupts an active dash (death, scene teardown).</summary>
        public void CancelDash()
        {
            if (!isDashing) return;
            EndDash();
        }
    }
}
