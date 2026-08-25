using UnityEngine;
using RogueArena.Core;
using RogueArena.Persistence;

namespace RogueArena.Player
{
    /// <summary>
    /// Owns everything the camera does beyond aiming: recoil springs, trauma-based
    /// shake, strafe/slide tilt, head bob, landing dip and the FOV stack
    /// (sprint, dash, fire kicks). <see cref="FirstPersonController"/> writes the
    /// raw look angles; this component composes the final camera transform.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] float maxShakeOffset = .22f;
        [SerializeField] float maxShakeAngle = 1.6f;
        [SerializeField] float traumaDecay = 1.6f;

        [Header("Recoil")]
        [SerializeField] float recoilRecovery = 9f;

        [Header("FOV")]
        [SerializeField] float sprintFovKick = 7f;
        [SerializeField] float fovLerpSpeed = 9f;

        [Header("Tilt")]
        [SerializeField] float strafeTiltDegrees = 4f;
        [SerializeField] float tiltLerpSpeed = 8f;

        [Header("Bob")]
        [SerializeField] float bobFrequency = 9.2f;
        [SerializeField] float bobAmplitude = .045f;
        [SerializeField] float bobSideAmplitude = .028f;

        [Header("Landing")]
        [SerializeField] float landDipMax = .16f;
        [SerializeField] float landDipRecovery = 7f;

        // --- external inputs (written by FirstPersonController) ---
        public float SourcePitch;
        public float SourceYawDelta;
        public Vector2 LookDelta;
        public float HorizontalSpeed01;   // 0..1 relative to run speed
        public bool Grounded;
        public float StrafeInput;         // -1..1
        public bool Sprinting;
        public bool Sliding;
        public float SlideDirection;      // -1..1 sign of lateral movement
        public Vector3 BaseLocalPosition = new Vector3(0f, 1.62f, 0f);

        public Camera Camera { get; private set; }

        // --- internal state ---
        float trauma;
        float recoilPitch, recoilYaw;
        float fovAdd;
        float fireFovKick;
        float dashFov;
        float tiltCurrent;
        float bobPhase;
        float landDip;
        float baseFov = 78f;
        bool shakeEnabled = true;
        bool bobEnabled = true;
        float bobShakeSeedX, bobShakeSeedZ;

        public float RecoilPitch => recoilPitch;
        public float CurrentFov => Camera != null ? Camera.fieldOfView : baseFov;
        /// <summary>Bob offset exposed for the weapon view model to follow.</summary>
        public Vector3 BobOffset { get; private set; }

        public void Initialize(Camera camera)
        {
            Camera = camera;
            baseFov = Services.Settings?.Data.fieldOfView ?? 78f;
            camera.fieldOfView = baseFov;
            bobShakeSeedX = Random.value * 100f;
            bobShakeSeedZ = Random.value * 100f;

            if (Services.Settings != null)
            {
                bobEnabled = Services.Settings.Data.headBob;
                shakeEnabled = Services.Settings.Data.screenShake;
                Services.Settings.Applied += OnSettingsApplied;
            }
        }

        void OnDestroy()
        {
            if (Services.Settings != null) Services.Settings.Applied -= OnSettingsApplied;
        }

        void OnSettingsApplied()
        {
            if (Services.Settings == null) return;
            bobEnabled = Services.Settings.Data.headBob;
            shakeEnabled = Services.Settings.Data.screenShake;
            baseFov = Services.Settings.Data.fieldOfView;
        }

        // ---------------------------------------------------------------- events

        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            recoilPitch += pitchDegrees;
            recoilYaw += yawDegrees;
        }

        public void AddShake(float amount) => trauma = Mathf.Clamp01(trauma + (shakeEnabled ? amount : 0f));

        public void FireFovKick(float amount) => fireFovKick = Mathf.Min(fireFovKick + amount, 6f);

        public void SetDashFov(float amount) => dashFov = amount;

        public void NotifyLanded(float impactSpeed01)
        {
            landDip = Mathf.Min(landDipMax, impactSpeed01 * landDipMax * 1.4f);
            AddShake(impactSpeed01 * .35f);
        }

        public void ClearRecoil()
        {
            recoilPitch = 0f;
            recoilYaw = 0f;
        }

        // ---------------------------------------------------------------- update

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateRecoil(dt);
            UpdateShake(dt);
            UpdateTilt(dt);
            UpdateBob(dt);
            UpdateFov(dt);
            UpdateLandDip(dt);

            ComposeTransform();
        }

        void UpdateRecoil(float dt)
        {
            float recovery = recoilRecovery * dt;
            recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, recovery);
            recoilYaw = Mathf.MoveTowards(recoilYaw, 0f, recovery * .7f);
        }

        void UpdateShake(float dt)
        {
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
        }

        void UpdateTilt(float dt)
        {
            float target = 0f;
            if (Sliding) target = -SlideDirection * 5.5f;
            else target = -StrafeInput * strafeTiltDegrees;
            tiltCurrent = Mathf.Lerp(tiltCurrent, target, tiltLerpSpeed * dt);
        }

        void UpdateBob(float dt)
        {
            if (!bobEnabled || !Grounded || HorizontalSpeed01 < .05f)
            {
                BobOffset = Vector3.Lerp(BobOffset, Vector3.zero, 8f * dt);
                return;
            }

            bobPhase += dt * bobFrequency * Mathf.Max(.5f, HorizontalSpeed01);
            float vertical = Mathf.Sin(bobPhase * Mathf.PI * 2f) * bobAmplitude * HorizontalSpeed01;
            float lateral = Mathf.Cos(bobPhase * Mathf.PI) * bobSideAmplitude * HorizontalSpeed01;
            BobOffset = new Vector3(lateral, vertical, 0f);
        }

        void UpdateFov(float dt)
        {
            fireFovKick = Mathf.MoveTowards(fireFovKick, 0f, 6f * dt);

            float target = 0f;
            if (Sprinting && HorizontalSpeed01 > .5f) target += sprintFovKick;
            if (Sliding) target += sprintFovKick * .6f;
            target += dashFov;
            target += fireFovKick;

            fovAdd = Mathf.Lerp(fovAdd, target, fovLerpSpeed * dt);
            if (Camera != null) Camera.fieldOfView = baseFov + fovAdd;
        }

        void UpdateLandDip(float dt)
        {
            landDip = Mathf.MoveTowards(landDip, 0f, landDipRecovery * dt);
        }

        void ComposeTransform()
        {
            float shakeScale = trauma * trauma;

            // Rotation: pitch from controller + recoil + shake + land dip; roll from tilt + shake.
            float pitch = SourcePitch + recoilPitch - landDip * 30f;
            float yawShake = (Mathf.PerlinNoise(bobShakeSeedX, Time.time * 9f) - .5f) * maxShakeAngle * shakeScale * 2f;
            float roll = tiltCurrent + (Mathf.PerlinNoise(bobShakeSeedZ, Time.time * 11f) - .5f) * maxShakeAngle * shakeScale * 2f;
            transform.localRotation = Quaternion.Euler(pitch, yawShake + recoilYaw, roll);

            // Position: base + bob + shake offsets.
            Vector3 shakeOffset = new Vector3(
                (Mathf.PerlinNoise(bobShakeSeedX, Time.time * 13f) - .5f) * maxShakeOffset * shakeScale,
                (Mathf.PerlinNoise(bobShakeSeedZ + 40f, Time.time * 12f) - .5f) * maxShakeOffset * shakeScale,
                0f);
            transform.localPosition = BaseLocalPosition + BobOffset + shakeOffset;
        }
    }
}
