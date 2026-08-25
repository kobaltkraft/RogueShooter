using UnityEngine;
using RogueArena.Player;

namespace RogueArena.Weapons
{
    /// <summary>
    /// Animates a weapon view model: sway from mouse movement, bob following the
    /// camera, recoil kick springs, reload dip and switch lower/raise. Purely
    /// cosmetic - gameplay is unaffected if this component is missing.
    /// </summary>
    public class WeaponViewModel : MonoBehaviour
    {
        [Header("Sway")]
        [SerializeField] float swayAmount = .012f;
        [SerializeField] float swayMax = .05f;
        [SerializeField] float swaySpeed = 10f;

        [Header("Kick")]
        [SerializeField] float kickDecay = 12f;
        [SerializeField] float kickRotDecay = 10f;

        public Transform ModelRoot { get; set; }

        CameraRig rig;
        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 sway;
        float kickZ;
        float kickPitch;
        float reloadDip;
        float switchLower;     // 0 = raised, 1 = lowered
        float kickInput;

        public void Initialize(Weapon weapon)
        {
            ModelRoot = transform;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;

            if (weapon != null)
            {
                weapon.Fired += () => kickInput += weapon.Definition != null ? weapon.Definition.kick : .05f;
                weapon.ReloadStarted += () => reloadDip = 1f;
                weapon.ReloadFinished += () => reloadDip = 0f;
            }
        }

        /// <summary>Attaches the camera rig after the player is wired up.</summary>
        public void Bind(CameraRig cameraRig) => rig = cameraRig;

        /// <summary>Called by the weapon controller (0 = raised, 1 = lowered).</summary>
        public void SetSwitchLower(float value) => switchLower = value;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (ModelRoot == null || dt <= 0f) return;

            // --- sway from look input ---
            Vector2 look = rig != null ? rig.LookDelta : Vector2.zero;
            Vector3 swayTarget = new Vector3(
                Mathf.Clamp(-look.x * swayAmount, -swayMax, swayMax),
                Mathf.Clamp(-look.y * swayAmount * .6f, -swayMax, swayMax),
                0f);
            sway = Vector3.Lerp(sway, swayTarget, swaySpeed * dt);

            // --- recoil kick springs ---
            kickZ = Mathf.Lerp(kickZ, 0f, kickDecay * dt);
            kickPitch = Mathf.Lerp(kickPitch, 0f, kickRotDecay * dt);
            if (kickInput > 0f)
            {
                kickZ += kickInput;
                kickPitch += kickInput * 220f;
                kickInput = 0f;
            }

            // --- bob follows the camera rig ---
            Vector3 bob = rig != null ? rig.BobOffset * .55f : Vector3.zero;

            // --- compose ---
            float dip = reloadDip * .12f + switchLower * .42f;
            Vector3 pos = restPosition + sway + bob + new Vector3(0f, -dip, 0f) + new Vector3(0f, 0f, kickZ * .35f);
            float reloadRoll = Mathf.Sin(Time.time * 9f) * reloadDip * 8f;
            Quaternion rot = restRotation * Quaternion.Euler(-kickPitch, sway.x * 120f, reloadRoll + sway.x * 200f);

            ModelRoot.localPosition = pos;
            ModelRoot.localRotation = rot;
        }
    }
}
