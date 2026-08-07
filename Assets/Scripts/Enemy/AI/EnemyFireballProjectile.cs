using Mood.Combat;
using Mood.Effects;
using Mood.Audio;
using Mood.Player;
using UnityEngine;
using AudioProfile = Akila.FPSFramework.AudioProfile;

namespace Mood.AI
{
    [AddComponentMenu("MOOD/AI/Enemy Fireball Projectile")]
    [DisallowMultipleComponent]
    public sealed class EnemyFireballProjectile : MonoBehaviour
    {
        private const int MaxOverlapHits = 8;

        [Header("Projectile")]
        [SerializeField, Min(0f)] private float speed = 18f;
        [SerializeField, Min(0f)] private float damage = 15f;
        [SerializeField, Min(0.01f)] private float lifeTime = 5f;
        [SerializeField, Min(0f)] private float sphereCastRadius = 0.15f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private LayerMask passThroughLayers;
        [SerializeField] private bool ignoreTriggerColliders = true;
        [SerializeField, Min(0f)] private float minimumBlockingThickness = 0.35f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private Vector3 spawnOffset;

        [Header("Impact")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField, Min(0f)] private float impactEffectLifetime = 2f;
        [SerializeField, Min(0f)] private float impactForce = 0f;

        [Header("Audio")]
        [SerializeField] private SfxPlayer sfxPlayer;
        [SerializeField] private AudioProfile launchAudioProfile;
        [SerializeField] private AudioProfile impactAudioProfile;

        private readonly RaycastHit[] hitResults = new RaycastHit[MaxOverlapHits];
        private readonly Collider[] overlapResults = new Collider[MaxOverlapHits];

        private GameObject instigator;
        private Vector3 direction = Vector3.forward;
        private float despawnTime;
        private bool initialized;
        private bool hasImpacted;
        private SphereCollider sphereCollider;
        private Rigidbody body;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            AssignAudioReferences();
        }

        private void OnEnable()
        {
            despawnTime = Time.time + lifeTime;
            hasImpacted = false;
        }

        private void FixedUpdate()
        {
            if (hasImpacted)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            Vector3 movement = direction * (speed * deltaTime);
            float movementDistance = movement.magnitude;

            if (movementDistance > 0f && TryHit(movementDistance, out RaycastHit hit))
            {
                ProcessHit(hit.collider, hit.point, hit.normal);
                return;
            }

            body.MovePosition(transform.position + movement);

            if (TryOverlapHit(out Collider overlapCollider))
            {
                Vector3 hitPoint = overlapCollider.ClosestPoint(transform.position);
                Vector3 hitNormal = GetImpactNormal(hitPoint);
                ProcessHit(overlapCollider, hitPoint, hitNormal);
                return;
            }

            if (movement.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            if (Time.time >= despawnTime)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(GameObject owner, Vector3 shotDirection)
        {
            instigator = owner;
            direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : transform.forward;
            initialized = true;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.position += transform.TransformDirection(spawnOffset);
            IgnoreInstigatorCollisions();
            sfxPlayer?.Play(launchAudioProfile);
        }

        private bool TryHit(float movementDistance, out RaycastHit hit)
        {
            Vector3 castDirection = initialized ? direction : transform.forward;
            int hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                GetCastRadius(),
                castDirection,
                hitResults,
                movementDistance,
                hitMask,
                triggerInteraction);

            float closestDistance = float.MaxValue;
            hit = default;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit candidateHit = hitResults[hitIndex];
                Collider candidateCollider = candidateHit.collider;
                if (candidateCollider == null || ShouldIgnoreHit(candidateCollider))
                {
                    continue;
                }

                if (candidateHit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = candidateHit.distance;
                hit = candidateHit;
            }

            return closestDistance < float.MaxValue;
        }

        private bool TryOverlapHit(out Collider hitCollider)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, GetCastRadius(), overlapResults, hitMask, triggerInteraction);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider overlapCollider = overlapResults[hitIndex];
                if (overlapCollider == null || ShouldIgnoreHit(overlapCollider))
                {
                    continue;
                }

                hitCollider = overlapCollider;
                return true;
            }

            hitCollider = null;
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasImpacted || other == null || ShouldIgnoreHit(other))
            {
                return;
            }

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = GetImpactNormal(hitPoint);
            ProcessHit(other, hitPoint, hitNormal);
        }

        private void ProcessHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (hasImpacted || hitCollider == null)
            {
                return;
            }

            if (ShouldIgnoreHit(hitCollider))
            {
                return;
            }

            hasImpacted = true;
            if (hitNormal.sqrMagnitude <= 0.0001f)
            {
                hitNormal = -direction;
            }

            sfxPlayer?.Play(impactAudioProfile);
            PlayerHealth playerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
            playerHealth?.ApplyDamage(damage, hitPoint, hitNormal, instigator);

            Rigidbody hitRigidbody = hitCollider.attachedRigidbody;
            if (hitRigidbody != null && impactForce > 0f)
            {
                hitRigidbody.AddForceAtPosition(direction * impactForce, hitPoint, ForceMode.Impulse);
            }

            SpawnImpactEffect(hitPoint, hitNormal);
            Destroy(gameObject);
        }

        private float GetCastRadius()
        {
            float radius = sphereCastRadius;
            if (sphereCollider != null)
            {
                float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                radius = Mathf.Max(radius, sphereCollider.radius * maxScale);
            }

            return Mathf.Max(radius, 0.01f);
        }

        private Vector3 GetImpactNormal(Vector3 hitPoint)
        {
            Vector3 normal = transform.position - hitPoint;
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : -direction;
        }

        private void SpawnImpactEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (impactEffectPrefab == null)
            {
                return;
            }

            Quaternion effectRotation = Quaternion.LookRotation(hitNormal, Vector3.up);
            EffectPool.Spawn(impactEffectPrefab, hitPoint, effectRotation, impactEffectLifetime);
        }

        private void AssignAudioReferences()
        {
            if (sfxPlayer == null)
            {
                sfxPlayer = GetComponentInChildren<SfxPlayer>(true);
            }

            if (sfxPlayer == null)
            {
                GameObject sfxObject = new GameObject("Enemy Fireball SFX Player");
                sfxObject.transform.SetParent(transform, false);
                sfxPlayer = sfxObject.AddComponent<SfxPlayer>();
            }
        }

        private bool IsInstigatorTransform(Transform hitTransform)
        {
            if (instigator == null || hitTransform == null)
            {
                return false;
            }

            Transform instigatorTransform = instigator.transform;
            return hitTransform == instigatorTransform || hitTransform.IsChildOf(instigatorTransform);
        }

        private static bool IsEnemyTransform(Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return false;
            }

            if (hitTransform.GetComponentInParent<PlayerHealth>() != null)
            {
                return false;
            }

            return hitTransform.GetComponentInParent<EnemyHealth>() != null;
        }

        private bool ShouldIgnoreHit(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return true;
            }

            Transform hitTransform = hitCollider.transform;
            if (IsInstigatorTransform(hitTransform) || IsEnemyTransform(hitTransform))
            {
                return true;
            }

            if (hitCollider.GetComponentInParent<PlayerHealth>() != null)
            {
                return false;
            }

            if (!IsLayerIncluded(hitCollider.gameObject.layer, hitMask))
            {
                return true;
            }

            if (ignoreTriggerColliders && hitCollider.isTrigger)
            {
                return true;
            }

            if (IsLayerIncluded(hitCollider.gameObject.layer, passThroughLayers))
            {
                return true;
            }

            return ShouldIgnoreThinObstacle(hitCollider);
        }

        private bool ShouldIgnoreThinObstacle(Collider hitCollider)
        {
            if (minimumBlockingThickness <= 0f || hitCollider == null)
            {
                return false;
            }

            Rigidbody hitRigidbody = hitCollider.attachedRigidbody;
            if (hitRigidbody != null && !hitRigidbody.isKinematic)
            {
                return false;
            }

            Bounds hitBounds = hitCollider.bounds;
            if (hitBounds.size.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 travelDirection = initialized ? direction : transform.forward;
            if (travelDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            travelDirection.Normalize();
            Vector3 absoluteTravelDirection = new Vector3(Mathf.Abs(travelDirection.x), Mathf.Abs(travelDirection.y), Mathf.Abs(travelDirection.z));
            float obstacleThickness = Vector3.Dot(hitBounds.size, absoluteTravelDirection);
            return obstacleThickness > 0f && obstacleThickness < minimumBlockingThickness;
        }

        private static bool IsLayerIncluded(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private void IgnoreInstigatorCollisions()
        {
            if (instigator == null)
            {
                return;
            }

            Collider[] ownerColliders = instigator.GetComponentsInChildren<Collider>(true);
            Collider[] projectileColliders = GetComponentsInChildren<Collider>(true);
            for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
            {
                Collider ownerCollider = ownerColliders[ownerIndex];
                if (ownerCollider == null)
                {
                    continue;
                }

                for (int projectileIndex = 0; projectileIndex < projectileColliders.Length; projectileIndex++)
                {
                    Collider projectileCollider = projectileColliders[projectileIndex];
                    if (projectileCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownerCollider, projectileCollider, true);
                }
            }
        }
    }
}


