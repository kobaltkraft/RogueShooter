using UnityEngine;
namespace RogueArena.Player
{
    [CreateAssetMenu(menuName="Rogue Arena/Dash Definition")]
    public class DashDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("World-space distance covered during a single dash.")]
        public float distance = 6f;
        [Tooltip("Seconds the dash takes from start to end.")]
        public float duration = .18f;
        [Tooltip("Curve evaluated over normalised dash time (0→1). Applied as a multiplier on base speed so you can ease in/out.")]
        public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Cooldown & Charges")]
        [Tooltip("Seconds before a used charge begins recharging.")]
        public float cooldownBeforeRecharge = .4f;
        [Tooltip("Seconds to fully recharge one charge.")]
        public float rechargeTime = 2.5f;
        [Tooltip("Maximum stored dashes.")]
        [Min(1)] public int maxCharges = 1;

        [Header("Constraints")]
        [Tooltip("If false, dash is only allowed while grounded.")]
        public bool allowAirDash = false;
        [Tooltip("Height the controller is lifted during the dash so low obstacles are cleared.")]
        public float liftHeight = .15f;

        [Header("Feel")]
        [Tooltip("Camera FOV added at peak dash speed, then smoothed back.")]
        public float fovKick = 8f;
        [Tooltip("How quickly the FOV kick eases in and out.")]
        public float fovKickSpeed = 12f;
    }
}
