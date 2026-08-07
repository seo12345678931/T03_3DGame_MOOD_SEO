using Mood.Combat;
using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Screen Center Projectile")]
    [DisallowMultipleComponent]
    public sealed class ScreenCenterProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float speed = 80f;
        [SerializeField, Min(0.01f)] private float lifeTime = 3f;

        [Header("Visual")]
        [SerializeField] private bool useAutoScaling = true;
        [SerializeField, Min(0.01f)] private float scaleMultiplier = 45f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0f)] private float impactForce = 10f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private GameObject surfaceImpactEffectPrefab;
        [SerializeField, Min(0f)] private float surfaceImpactEffectLifetime = 2f;
        [SerializeField, Min(0.0001f)] private float surfaceImpactEffectOffset = 0.002f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        private Vector3 moveDirection = Vector3.forward;
        private GameObject instigator;
        private float lifeTimer;
        private bool isLaunched;
        private TrailRenderer trail;

        private void Awake()
        {
            trail = GetComponentInChildren<TrailRenderer>(true);
            InitializeVisualScale();
        }

        public void Launch(Vector3 direction, GameObject owner, WeaponData weaponData)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = transform.forward;
            }

            if (weaponData != null)
            {
                damage = weaponData.Damage;
                impactForce = weaponData.ImpactForce;
                hitMask = weaponData.HitMask;
                surfaceImpactEffectPrefab = weaponData.SurfaceImpactEffectPrefab;
                surfaceImpactEffectLifetime = weaponData.SurfaceImpactEffectLifetime;
                surfaceImpactEffectOffset = weaponData.SurfaceImpactEffectOffset;
            }

            moveDirection = direction.normalized;
            instigator = owner;
            isLaunched = true;
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        private void Update()
        {
            if (!isLaunched)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            lifeTimer += deltaTime;
            if (lifeTimer >= lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            float stepDistance = speed * deltaTime;
            Vector3 currentPosition = transform.position;

            if (TryGetHit(currentPosition, moveDirection, stepDistance, out RaycastHit hit))
            {
                transform.position = hit.point;
                ProcessHit(hit);
                return;
            }

            transform.position = currentPosition + moveDirection * stepDistance;
            UpdateVisualScale();
        }

        private bool TryGetHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                distance,
                hitMask,
                triggerInteraction);

            float closestDistance = float.MaxValue;
            closestHit = default;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = hitBuffer[hitIndex];
                if (hit.collider == null || IsInstigatorCollider(hit.collider))
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
            }

            return closestDistance < float.MaxValue;
        }

        private void ProcessHit(RaycastHit hit)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            EnemyHealth enemyHealth = damageable as EnemyHealth;
            float appliedDamage = damage;
            if (enemyHealth != null)
            {
                appliedDamage *= enemyHealth.GetDamageMultiplier(hit.collider, hit.point);
            }

            damageable?.ApplyDamage(appliedDamage, hit.point, hit.normal, instigator);
            if (enemyHealth != null && enemyHealth.LastDamageWasHeadshot)
            {
                instigator?.GetComponent<PlayerWeaponSystem>()?.NotifyHeadshotHit();
            }

            if (damageable == null)
            {
                Mood.Effects.SurfaceImpactEffect.Spawn(hit, surfaceImpactEffectPrefab, surfaceImpactEffectLifetime, surfaceImpactEffectOffset);
            }

            if (hit.rigidbody != null && impactForce > 0f)
            {
                hit.rigidbody.AddForceAtPosition(moveDirection * impactForce, hit.point, ForceMode.Impulse);
            }

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }

        private bool IsInstigatorCollider(Collider hitCollider)
        {
            if (instigator == null || hitCollider == null)
            {
                return false;
            }

            Transform instigatorTransform = instigator.transform;
            return hitCollider.transform == instigatorTransform || hitCollider.transform.IsChildOf(instigatorTransform);
        }

        private void InitializeVisualScale()
        {
            if (useAutoScaling)
            {
                transform.localScale = Vector3.zero;
                if (trail != null)
                {
                    trail.widthMultiplier = 0f;
                }

                return;
            }

            transform.localScale = Vector3.one * scaleMultiplier;
        }

        private void UpdateVisualScale()
        {
            if (!useAutoScaling)
            {
                transform.localScale = Vector3.one * scaleMultiplier;
                return;
            }

            Camera currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, currentCamera.transform.position);
            float scale = (distance / scaleMultiplier) * (currentCamera.fieldOfView / 360f);

            transform.localScale = Vector3.one * scale;
            if (trail != null)
            {
                trail.widthMultiplier = scale;
            }
        }
    }
}
