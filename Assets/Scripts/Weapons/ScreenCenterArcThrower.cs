using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Screen Center Arc Thrower")]
    [DisallowMultipleComponent]
    public sealed class ScreenCenterArcThrower : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private Rigidbody throwablePrefab;

        [Header("Aim")]
        [SerializeField] private LayerMask aimMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Min(0.1f)] private float maxAimDistance = 80f;
        [SerializeField] private Vector2 aimViewportPoint = new Vector2(0.5f, 0.5f);

        [Header("Throw")]
        [SerializeField, Min(0.1f)] private float throwForce = 18f;
        [SerializeField, Min(0.05f)] private float minimumFlightTime = 0.35f;
        [SerializeField, Min(0f)] private float inheritVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float throwCooldown = 0.35f;
        [SerializeField, Min(0f)] private float minimumAimCorrectionDistance = 2f;
        [SerializeField, Min(0.1f)] private float maxLaunchSpeed = 22f;
        [SerializeField, Min(0.1f)] private float maxVerticalLaunchSpeed = 14f;
        [SerializeField] private bool alignToVelocity = true;
        [SerializeField] private bool ignoreInstigatorColliders = true;

        private float nextThrowTime;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
        }

        public bool TryThrow(GameObject instigator)
        {
            return TryThrow(instigator, out _);
        }

        public bool TryThrow(GameObject instigator, out Rigidbody spawnedThrowable)
        {
            spawnedThrowable = null;

            if (throwablePrefab == null || Time.time < nextThrowTime)
            {
                return false;
            }

            AssignReferences();

            Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position;
            Vector3 aimDirection = GetAimDirection();
            Vector3 inheritedVelocity = GetInheritedVelocity(instigator);
            Vector3 throwVelocity = aimDirection * throwForce;

            if (TryGetAimPoint(out Vector3 aimPoint) &&
                ShouldApplyAimCorrection(origin, aimPoint) &&
                TryCalculateLaunchVelocity(origin, aimPoint, out Vector3 aimedVelocity))
            {
                throwVelocity = aimedVelocity;
            }

            Quaternion spawnRotation = throwOrigin != null ? throwOrigin.rotation : transform.rotation;
            if (alignToVelocity && throwVelocity.sqrMagnitude > Mathf.Epsilon)
            {
                spawnRotation = Quaternion.LookRotation(throwVelocity.normalized, Vector3.up);
            }

            spawnedThrowable = Instantiate(throwablePrefab, origin, spawnRotation);

            if (ignoreInstigatorColliders)
            {
                IgnoreInstigatorCollisions(instigator, spawnedThrowable);
            }

            if (spawnedThrowable.TryGetComponent(out GrenadeExplosion grenadeExplosion))
            {
                grenadeExplosion.Initialize(instigator);
            }

            spawnedThrowable.linearVelocity = inheritedVelocity;
            spawnedThrowable.AddForce(throwVelocity, ForceMode.VelocityChange);
            nextThrowTime = Time.time + throwCooldown;
            return true;
        }

        public bool TryGetAimPoint(out Vector3 aimPoint)
        {
            if (aimCamera == null)
            {
                Transform origin = throwOrigin != null ? throwOrigin : transform;
                aimPoint = origin.position + (origin.forward * maxAimDistance);
                return true;
            }

            // 조준 보정은 aimMask에 포함된 표면에 레이가 맞았을 때만 적용한다.
            Ray aimRay = aimCamera.ViewportPointToRay(aimViewportPoint);
            if (Physics.Raycast(aimRay, out RaycastHit hit, maxAimDistance, aimMask, triggerInteraction))
            {
                aimPoint = hit.point;
                return true;
            }

            aimPoint = Vector3.zero;
            return false;
        }

        public bool TryCalculateLaunchVelocity(Vector3 origin, Vector3 targetPoint, out Vector3 launchVelocity)
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            if (gravity <= Mathf.Epsilon || throwForce <= Mathf.Epsilon)
            {
                launchVelocity = Vector3.zero;
                return false;
            }

            Vector3 displacement = targetPoint - origin;
            Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(displacement, Vector3.up);
            float horizontalDistance = horizontalDisplacement.magnitude;
            float travelTime = Mathf.Max(minimumFlightTime, horizontalDistance / throwForce);

            if (travelTime <= Mathf.Epsilon)
            {
                launchVelocity = ClampLaunchVelocity(GetAimDirection() * throwForce);
                return true;
            }

            Vector3 horizontalVelocity = horizontalDistance > 0.001f
                ? horizontalDisplacement / travelTime
                : Vector3.zero;
            float verticalVelocity = (displacement.y / travelTime) + (0.5f * gravity * travelTime);

            launchVelocity = ClampLaunchVelocity(horizontalVelocity + (Vector3.up * verticalVelocity));
            return true;
        }

        // 너무 가까운 조준점은 오히려 부자연스러운 보정을 만들 수 있어 기본 투척을 유지한다.
        private bool ShouldApplyAimCorrection(Vector3 origin, Vector3 aimPoint)
        {
            float correctionDistance = Vector3.Distance(origin, aimPoint);
            return correctionDistance > minimumAimCorrectionDistance;
        }

        // 조준점이 멀거나 높이차가 커도 수류탄 속도가 과도하게 커지지 않도록 제한한다.
        private Vector3 ClampLaunchVelocity(Vector3 launchVelocity)
        {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
            float clampedVerticalVelocity = Mathf.Clamp(launchVelocity.y, -maxVerticalLaunchSpeed, maxVerticalLaunchSpeed);

            if (horizontalVelocity.magnitude > maxLaunchSpeed)
            {
                horizontalVelocity = horizontalVelocity.normalized * maxLaunchSpeed;
            }

            Vector3 clampedVelocity = horizontalVelocity + (Vector3.up * clampedVerticalVelocity);
            if (clampedVelocity.magnitude > maxLaunchSpeed)
            {
                clampedVelocity = clampedVelocity.normalized * maxLaunchSpeed;
            }

            return clampedVelocity;
        }

        private void AssignReferences()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (throwOrigin == null)
            {
                throwOrigin = transform;
            }
        }

        private Vector3 GetAimDirection()
        {
            if (aimCamera != null)
            {
                return aimCamera.ViewportPointToRay(aimViewportPoint).direction.normalized;
            }

            Transform origin = throwOrigin != null ? throwOrigin : transform;
            return origin.forward.normalized;
        }

        private Vector3 GetInheritedVelocity(GameObject instigator)
        {
            if (instigator == null || inheritVelocityMultiplier <= 0f)
            {
                return Vector3.zero;
            }

            if (instigator.TryGetComponent(out CharacterController characterController))
            {
                return characterController.velocity * inheritVelocityMultiplier;
            }

            if (instigator.TryGetComponent(out Rigidbody attachedRigidbody))
            {
                return attachedRigidbody.linearVelocity * inheritVelocityMultiplier;
            }

            return Vector3.zero;
        }

        private static void IgnoreInstigatorCollisions(GameObject instigator, Rigidbody spawnedThrowable)
        {
            if (instigator == null || spawnedThrowable == null)
            {
                return;
            }

            Collider[] instigatorColliders = instigator.GetComponentsInChildren<Collider>(true);
            Collider[] throwableColliders = spawnedThrowable.GetComponentsInChildren<Collider>(true);

            for (int instigatorIndex = 0; instigatorIndex < instigatorColliders.Length; instigatorIndex++)
            {
                Collider instigatorCollider = instigatorColliders[instigatorIndex];
                if (instigatorCollider == null)
                {
                    continue;
                }

                for (int throwableIndex = 0; throwableIndex < throwableColliders.Length; throwableIndex++)
                {
                    Collider throwableCollider = throwableColliders[throwableIndex];
                    if (throwableCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(instigatorCollider, throwableCollider, true);
                }
            }
        }
    }
}
