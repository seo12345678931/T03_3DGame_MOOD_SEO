using UnityEngine;

namespace Mood.Pickups
{
    [AddComponentMenu("MOOD/Pickups/Pickup Drop Physics")]
    [DisallowMultipleComponent]
    public sealed class PickupDropPhysics : MonoBehaviour
    {
        private const int RaycastBufferSize = 16;

        [Header("Drop Motion")]
        [SerializeField, Min(0f)] private float gravityScale = 2.5f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 30f;
        [SerializeField, Min(0f)] private float settleDelay = 0.1f;

        [Header("Ground Snap")]
        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private string fallbackGroundTag = "Ground";
        [SerializeField, Min(0f)] private float hoverHeight = 0.3f;
        [SerializeField, Min(0.05f)] private float probeStartOffset = 0.6f;
        [SerializeField, Min(0.1f)] private float probeDistance = 40f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[RaycastBufferSize];

        private bool settled;
        private bool simulating;
        private float activeTime;
        private Vector3 velocity;

        public bool IsSimulating => simulating && !settled;

        public void SetHoverHeight(float value)
        {
            hoverHeight = Mathf.Max(0f, value);
        }

        public void SetGroundLayers(LayerMask value)
        {
            groundLayers = value;
        }

        public void BeginDrop(Vector3 initialVelocity)
        {
            settled = false;
            simulating = true;
            activeTime = 0f;
            velocity = initialVelocity;
        }

        public void StopSimulation()
        {
            settled = true;
            simulating = false;
            velocity = Vector3.zero;
        }

        private void Update()
        {
            if (!simulating || settled)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            activeTime += deltaTime;

            float gravity = Physics.gravity.y * gravityScale;
            velocity.y = Mathf.Max(velocity.y + (gravity * deltaTime), -maxFallSpeed);

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + (velocity * deltaTime);

            if (activeTime >= settleDelay && TryFindGround(out RaycastHit groundHit))
            {
                float targetY = groundHit.point.y + hoverHeight;
                if (nextPosition.y <= targetY)
                {
                    Vector3 settledPosition = nextPosition;
                    settledPosition.y = targetY;
                    transform.position = settledPosition;
                    SnapRotationUpright();
                    StopSimulation();
                    return;
                }
            }

            transform.position = nextPosition;
        }

        private bool TryFindGround(out RaycastHit groundHit)
        {
            Vector3 rayOrigin = transform.position + (Vector3.up * probeStartOffset);
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, raycastHits, probeDistance, ~0, QueryTriggerInteraction.Ignore);

            groundHit = default;
            float closestDistance = float.MaxValue;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = raycastHits[index];
                if (candidate.collider == null)
                {
                    continue;
                }

                if (!IsValidGroundCollider(candidate.collider))
                {
                    continue;
                }

                if (candidate.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = candidate.distance;
                groundHit = candidate;
            }

            for (int index = 0; index < hitCount; index++)
            {
                raycastHits[index] = default;
            }

            return closestDistance < float.MaxValue;
        }

        private void SnapRotationUpright()
        {
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        private bool IsValidGroundCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (groundLayers.value != 0)
            {
                int candidateLayerMask = 1 << candidate.gameObject.layer;
                return (groundLayers.value & candidateLayerMask) != 0;
            }

            return candidate.CompareTag(fallbackGroundTag);
        }
    }
}
