using UnityEngine;
using RogueArena.Core;

namespace RogueArena.AI
{
    /// <summary>
    /// Flight locomotion for drones: no NavMesh, just smoothed steering toward a
    /// hover point with bobbing and banked turns. Keeps a comfortable distance
    /// from the player and off the floor.
    /// </summary>
    public class EnemyFlight : MonoBehaviour
    {
        EnemyBrain brain;
        EnemyDefinition def;
        Vector3 velocity;
        Vector3 targetPoint;
        float bobPhase;
        bool stopped;

        public void Initialize(EnemyBrain owner, EnemyDefinition definition)
        {
            brain = owner;
            def = definition;
            bobPhase = Random.value * Mathf.PI * 2f;
            targetPoint = transform.position;
        }

        public void MoveTo(Vector3 point, float speed)
        {
            stopped = false;
            // Fly toward a point at the definition's hover height.
            targetPoint = new Vector3(point.x, def.flyHeight + Mathf.PerlinNoise(point.x * .1f, point.z * .1f) * 1.2f, point.z);
        }

        public void Stop() => stopped = true;

        public void AddImpulse(Vector3 impulse) => velocity += impulse;

        void Update()
        {
            if (def == null || brain == null) return;
            float dt = Time.deltaTime;

            bobPhase += dt * 2.2f;
            Vector3 desired = stopped ? transform.position : targetPoint;
            desired.y += Mathf.Sin(bobPhase) * .35f;

            Vector3 toTarget = desired - transform.position;
            float maxSpeed = def.moveSpeed;
            Vector3 desiredVelocity = Vector3.ClampMagnitude(toTarget * 2.5f, maxSpeed);

            velocity = Vector3.Lerp(velocity, desiredVelocity, 4f * dt);

            // Axis-separated wall sliding: try the full step, then horizontal only,
            // then vertical only. Cheap (one CheckSphere per attempt) and stops
            // drones from ghosting through level geometry.
            Vector3 step = velocity * dt;
            Vector3 candidate = transform.position + step;
            if (Blocked(candidate))
            {
                Vector3 horizontal = transform.position + new Vector3(step.x, 0f, step.z);
                if (!Blocked(horizontal)) candidate = horizontal;
                else
                {
                    Vector3 vertical = transform.position + new Vector3(0f, step.y, 0f);
                    candidate = Blocked(vertical) ? transform.position : vertical;
                }
                velocity = Vector3.Lerp(velocity, Vector3.zero, 8f * dt);
            }
            transform.position = candidate;

            // Bank toward movement, face the player when engaging.
            Vector3 faceDirection = brain.Player != null && brain.State == EnemyState.Engage
                ? (brain.Player.position - transform.position).normalized
                : velocity.sqrMagnitude > .1f ? velocity.normalized : transform.forward;
            Vector3 flat = new Vector3(faceDirection.x, 0f, faceDirection.z);
            if (flat.sqrMagnitude > .0001f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 5f * dt);
            }
        }

        static readonly Collider[] wallBuffer = new Collider[1];

        bool Blocked(Vector3 position)
        {
            return Physics.OverlapSphereNonAlloc(position, .55f, wallBuffer, Layers.OccluderMask,
                QueryTriggerInteraction.Ignore) > 0;
        }
    }
}
