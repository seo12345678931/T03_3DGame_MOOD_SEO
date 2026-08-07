using System.Collections.Generic;
using Mood.Effects;
using UnityEngine;
using UnityEngine.Events;

namespace Mood.Combat
{
    [AddComponentMenu("MOOD/Combat/Shootable Explosive")]
    [DisallowMultipleComponent]
    public sealed class ShootableExplosive : MonoBehaviour, IDamageable
    {
        private const int MaxOverlapCount = 64;

        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 40f;
        [SerializeField] private bool explodeOnAnyDamage = true;

        [Header("Explosion")]
        [SerializeField, Min(0f)] private float explosionRadius = 5f;
        [SerializeField, Min(0f)] private float maxExplosionDamage = 80f;
        [SerializeField, Min(0f)] private float explosionForce = 12f;
        [SerializeField, Min(0f)] private float upwardsModifier = 0.25f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Gizmos")]
        [SerializeField] private bool drawExplosionGizmo = true;
        [SerializeField] private Color explosionGizmoColor = new Color(1f, 0.45f, 0.1f, 0.2f);

        [Header("Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField, Min(0f)] private float hitEffectLifetime = 2f;
        [SerializeField, Min(0f)] private float hitEffectOffset = 0.02f;
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField, Min(0f)] private float explosionEffectLifetime = 3f;

        [Header("Lifecycle")]
        [SerializeField] private bool destroyOnExplode = true;
        [SerializeField, Min(0f)] private float destroyDelay = 0f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onExploded;

        private readonly Collider[] overlapResults = new Collider[MaxOverlapCount];
        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private readonly HashSet<Rigidbody> affectedRigidbodies = new HashSet<Rigidbody>();

        private float currentHealth;
        private bool exploded;

        public float CurrentHealth => currentHealth;
        public bool Exploded => exploded;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator)
        {
            if (exploded || damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            SpawnHitEffect(hitPoint, hitNormal);
            onDamaged?.Invoke();

            if (explodeOnAnyDamage || currentHealth <= 0f)
            {
                Explode(instigator);
            }
        }

        public void Explode(GameObject instigator)
        {
            if (exploded)
            {
                return;
            }

            exploded = true;

            Vector3 explosionPosition = transform.position;
            SpawnExplosionEffect(explosionPosition);
            ApplyExplosion(explosionPosition, instigator);
            onExploded?.Invoke();

            if (destroyOnExplode)
            {
                Destroy(gameObject, destroyDelay);
            }
        }

        private void ApplyExplosion(Vector3 explosionPosition, GameObject instigator)
        {
            damagedTargets.Clear();
            affectedRigidbodies.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(explosionPosition, explosionRadius, overlapResults, affectedLayers, triggerInteraction);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = overlapResults[hitIndex];
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                Vector3 hitPoint = hitCollider.ClosestPoint(explosionPosition);
                Vector3 hitDirection = hitPoint - explosionPosition;
                float distance = hitDirection.magnitude;
                Vector3 hitNormal = distance > 0.001f ? hitDirection / distance : Vector3.up;

                if (requireLineOfSight && !HasLineOfSight(explosionPosition, hitPoint, hitTransform))
                {
                    continue;
                }

                float damageMultiplier = 1f - Mathf.Clamp01(distance / Mathf.Max(explosionRadius, 0.001f));
                float damage = maxExplosionDamage * damageMultiplier;

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable != null && damage > 0f && damagedTargets.Add(damageable))
                {
                    damageable.ApplyDamage(damage, hitPoint, hitNormal, instigator);
                }

                Rigidbody targetRigidbody = hitCollider.attachedRigidbody;
                if (targetRigidbody != null && affectedRigidbodies.Add(targetRigidbody))
                {
                    targetRigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier, ForceMode.Impulse);
                }
            }
        }

        private bool HasLineOfSight(Vector3 explosionPosition, Vector3 targetPoint, Transform targetTransform)
        {
            Vector3 direction = targetPoint - explosionPosition;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
            {
                return true;
            }

            direction /= distance;
            Vector3 origin = explosionPosition + (direction * 0.05f);
            float castDistance = Mathf.Max(0f, distance - 0.05f);

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, castDistance, obstructionMask, triggerInteraction))
            {
                return true;
            }

            Transform hitTransform = hit.transform;
            return hitTransform == targetTransform || hitTransform.IsChildOf(targetTransform) || targetTransform.IsChildOf(hitTransform);
        }

        private void SpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (hitEffectPrefab == null)
            {
                return;
            }

            Vector3 effectNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : transform.forward;
            Vector3 spawnPosition = hitPoint + (effectNormal * hitEffectOffset);
            Quaternion spawnRotation = Quaternion.LookRotation(effectNormal, Vector3.up);
            EffectPool.Spawn(hitEffectPrefab, spawnPosition, spawnRotation, hitEffectLifetime);
        }

        private void SpawnExplosionEffect(Vector3 explosionPosition)
        {
            if (explosionEffectPrefab == null)
            {
                return;
            }

            EffectPool.Spawn(explosionEffectPrefab, explosionPosition, Quaternion.identity, explosionEffectLifetime);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawExplosionGizmo)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = explosionGizmoColor;
            Gizmos.DrawSphere(transform.position, explosionRadius);

            Color wireColor = explosionGizmoColor;
            wireColor.a = 1f;
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
            Gizmos.color = previousColor;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            explosionRadius = Mathf.Max(0f, explosionRadius);

            if (!Application.isPlaying)
            {
                currentHealth = maxHealth;
            }
            else if (!exploded)
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }
        }
    }
}

