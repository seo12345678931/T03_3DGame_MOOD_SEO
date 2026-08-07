using UnityEngine;
using System;
using Mood.Effects;
using UnityEngine.Events;

namespace Mood.Combat
{
    [AddComponentMenu("MOOD/Combat/Enemy Health")]
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField, Min(0f)] private float destroyDelay = 0f;
        [SerializeField] private bool disableCollidersOnDeath = true;

        [Header("Headshot")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private string headTransformName = "Head";
        [SerializeField, Min(1f)] private float headshotMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float headshotRadius = 0.25f;

        [Header("Hit Effect")]
        [SerializeField] private GameObject damageEffectPrefab;
        [SerializeField, Min(0f)] private float damageEffectLifetime = 2f;
        [SerializeField, Min(0f)] private float damageEffectOffset = 0.02f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDied;

        private float currentHealth;
        private float initialMaxHealth;
        private bool isDead;
        private bool hasInitializedBaseHealth;
        private bool pendingHeadshotDamage;
        private bool lastDamageWasHeadshot;

        public event Action<EnemyHealth, GameObject> Damaged;
        public event Action<EnemyHealth, GameObject> Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => isDead;
        public bool LastDamageWasHeadshot => lastDamageWasHeadshot;

        private void Reset()
        {
            AssignHeadTransform();
        }

        private void Awake()
        {
            CacheInitialMaxHealth();
            currentHealth = maxHealth;
            AssignHeadTransform();
        }

        public void ApplyMaxHealthMultiplier(float multiplier, bool restoreToFullHealth)
        {
            CacheInitialMaxHealth();
            multiplier = Mathf.Max(0.01f, multiplier);
            maxHealth = Mathf.Max(1f, initialMaxHealth * multiplier);

            if (!Application.isPlaying)
            {
                currentHealth = maxHealth;
                return;
            }

            if (isDead)
            {
                return;
            }

            if (restoreToFullHealth)
            {
                currentHealth = maxHealth;
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator)
        {
            if (isDead || damage <= 0f)
            {
                return;
            }

            lastDamageWasHeadshot = pendingHeadshotDamage;
            pendingHeadshotDamage = false;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            SpawnDamageEffect(hitPoint, hitNormal);
            Damaged?.Invoke(this, instigator);
            onDamaged?.Invoke();

            if (currentHealth > 0f)
            {
                return;
            }

            Die(instigator);
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void RestoreFullHealth()
        {
            if (isDead)
            {
                return;
            }

            currentHealth = maxHealth;
        }

        public void Kill()
        {
            if (isDead)
            {
                return;
            }

            currentHealth = 0f;
            Die(null);
        }

        private void Die(GameObject instigator)
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            Died?.Invoke(this, instigator);
            onDied?.Invoke();
            DisableCollidersOnDeath();

            if (!destroyOnDeath)
            {
                return;
            }

            Destroy(gameObject, destroyDelay);
        }

        private void DisableCollidersOnDeath()
        {
            if (!disableCollidersOnDeath)
            {
                return;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider targetCollider = colliders[index];
                if (targetCollider != null)
                {
                    targetCollider.enabled = false;
                }
            }
        }

        public float GetDamageMultiplier(Collider hitCollider, Vector3 hitPoint)
        {
            bool isHeadshot = IsHeadshot(hitCollider, hitPoint);
            pendingHeadshotDamage = isHeadshot;
            return isHeadshot ? headshotMultiplier : 1f;
        }

        private bool IsHeadshot(Collider hitCollider, Vector3 hitPoint)
        {
            if (headshotMultiplier <= 1f)
            {
                return false;
            }

            if (headTransform != null)
            {
                if (hitCollider != null)
                {
                    Transform hitTransform = hitCollider.transform;
                    if (hitTransform == headTransform || hitTransform.IsChildOf(headTransform))
                    {
                        return true;
                    }
                }

                if (headshotRadius > 0f)
                {
                    float distanceToHead = Vector3.Distance(hitPoint, headTransform.position);
                    if (distanceToHead <= headshotRadius)
                    {
                        return true;
                    }
                }
            }

            return hitCollider != null && hitCollider.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AssignHeadTransform()
        {
            if (headTransform == null && !string.IsNullOrWhiteSpace(headTransformName))
            {
                headTransform = FindChildRecursive(transform, headTransformName);
            }
        }

        private void CacheInitialMaxHealth()
        {
            if (hasInitializedBaseHealth)
            {
                return;
            }

            initialMaxHealth = Mathf.Max(1f, maxHealth);
            hasInitializedBaseHealth = true;
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform result = FindChildRecursive(root.GetChild(childIndex), targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void SpawnDamageEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (damageEffectPrefab == null)
            {
                return;
            }

            Vector3 effectNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : transform.forward;
            Vector3 spawnPosition = hitPoint + effectNormal * damageEffectOffset;
            Quaternion spawnRotation = Quaternion.LookRotation(effectNormal, Vector3.up);
            EffectPool.Spawn(damageEffectPrefab, spawnPosition, spawnRotation, damageEffectLifetime);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            if (!Application.isPlaying)
            {
                initialMaxHealth = maxHealth;
                hasInitializedBaseHealth = true;
            }

            if (!Application.isPlaying)
            {
                currentHealth = maxHealth;
            }
            else if (!isDead)
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }
        }
    }
}

