using System.Collections.Generic;
using Mood.Combat;
using Mood.Effects;
using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Grenade Explosion")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GrenadeExplosion : MonoBehaviour
    {
        private const int MaxOverlapCount = 64;

        [Header("Fuse")]
        [SerializeField, Min(0f)] private float fuseTime = 3f;
        [SerializeField, Min(0f)] private float armDelay = 0.1f;
        [SerializeField] private bool explodeOnImpact;
        [SerializeField] private bool ignoreInstigator = true;

        [Header("Explosion")]
        [SerializeField, Min(0f)] private float explosionRadius = 6f;
        [SerializeField, Min(0f)] private float maxDamage = 120f;
        [SerializeField, Min(0f)] private float explosionForce = 12f;
        [SerializeField, Min(0f)] private float upwardsModifier = 0.25f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Effects")]
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField, Min(0f)] private float explosionEffectLifetime = 3f;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private readonly HashSet<Rigidbody> affectedRigidbodies = new HashSet<Rigidbody>();
        private readonly Collider[] overlapResults = new Collider[MaxOverlapCount];

        private Rigidbody cachedRigidbody;
        private GameObject instigator;
        private float armedAtTime;
        private float explodeAtTime;
        private bool exploded;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            ResetFuse();
        }

        private void OnEnable()
        {
            ResetFuse();
        }

        private void Update()
        {
            if (!exploded && Time.time >= explodeAtTime)
            {
                Explode();
            }
        }

        public void Initialize(GameObject owner)
        {
            instigator = owner;
            ResetFuse();
        }

        public void SetRemainingFuse(float remainingFuse)
        {
            explodeAtTime = Time.time + Mathf.Max(0f, remainingFuse);
        }

        public void Explode()
        {
            if (exploded)
            {
                return;
            }

            exploded = true;
            Vector3 explosionPosition = transform.position;
            SpawnExplosionEffect(explosionPosition);
            ApplyExplosion(explosionPosition);
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!explodeOnImpact || exploded || Time.time < armedAtTime)
            {
                return;
            }

            if (ignoreInstigator && collision.collider != null && IsInstigatorTransform(collision.collider.transform))
            {
                return;
            }

            Explode();
        }

        private void ApplyExplosion(Vector3 explosionPosition)
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

                if (ignoreInstigator && IsInstigatorTransform(hitTransform))
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
                float damage = maxDamage * damageMultiplier;

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable != null && damage > 0f && damagedTargets.Add(damageable))
                {
                    damageable.ApplyDamage(damage, hitPoint, hitNormal, instigator);
                }

                Rigidbody targetRigidbody = hitCollider.attachedRigidbody;
                if (targetRigidbody != null && targetRigidbody != cachedRigidbody && affectedRigidbodies.Add(targetRigidbody))
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

        private bool IsInstigatorTransform(Transform target)
        {
            if (instigator == null || target == null)
            {
                return false;
            }

            Transform instigatorTransform = instigator.transform;
            return target == instigatorTransform || target.IsChildOf(instigatorTransform);
        }

        private void SpawnExplosionEffect(Vector3 explosionPosition)
        {
            if (explosionEffectPrefab == null)
            {
                return;
            }

            EffectPool.Spawn(explosionEffectPrefab, explosionPosition, Quaternion.identity, explosionEffectLifetime);
        }

        private void ResetFuse()
        {
            armedAtTime = Time.time + armDelay;
            explodeAtTime = Time.time + fuseTime;
            exploded = false;
        }
    }
}
