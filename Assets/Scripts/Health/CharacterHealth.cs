using System;
using Mood.Combat;
using UnityEngine;

namespace Mood.Health
{
    [AddComponentMenu("MOOD/Health/Character Health")]
    [DisallowMultipleComponent]
    public sealed class CharacterHealth : MonoBehaviour, IDamageable, IHealthReceiver
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool startAtMaxHealth = true;
        [SerializeField, Min(0f)] private float startingHealth = 100f;

        [Header("State")]
        [SerializeField] private bool invulnerable;
        [SerializeField] private bool destroyOnDeath;
        [SerializeField, Min(0f)] private float healthDestroyDelay;

        private float currentHealth;
        private float initialMaxHealth;
        private bool isDead;
        private bool hasInitializedBaseHealth;

        public event Action<CharacterHealth, HealthChangeInfo> HealthChanged;
        public event Action<CharacterHealth, HealthChangeInfo> Healed;
        public event Action<CharacterHealth, DamageInfo> Damaged;
        public event Action<CharacterHealth, GameObject> Died;

        public Component Component => this;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        private void Awake()
        {
            CacheInitialMaxHealth();
            currentHealth = startAtMaxHealth ? maxHealth : Mathf.Clamp(startingHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;
        }

        public void ApplyMaxHealthMultiplier(float multiplier, bool restoreToFullHealth)
        {
            CacheInitialMaxHealth();
            multiplier = Mathf.Max(0.01f, multiplier);
            maxHealth = Mathf.Max(1f, initialMaxHealth * multiplier);
            startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);

            if (!Application.isPlaying)
            {
                currentHealth = startAtMaxHealth || restoreToFullHealth
                    ? maxHealth
                    : Mathf.Clamp(startingHealth, 0f, maxHealth);
                isDead = currentHealth <= 0f;
                return;
            }

            if (isDead)
            {
                return;
            }

            currentHealth = restoreToFullHealth
                ? maxHealth
                : Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public bool CanReceiveHealing(float amount)
        {
            return !isDead && amount > 0f && currentHealth < maxHealth;
        }

        public float ReceiveHealing(float amount, GameObject source)
        {
            if (!CanReceiveHealing(amount))
            {
                return 0f;
            }

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            HealthChangeInfo changeInfo = new HealthChangeInfo(previousHealth, currentHealth, maxHealth, source);
            HealthChanged?.Invoke(this, changeInfo);
            Healed?.Invoke(this, changeInfo);

            return currentHealth - previousHealth;
        }

        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator)
        {
            if (isDead || invulnerable || damage <= 0f)
            {
                return;
            }

            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - damage);

            HealthChangeInfo changeInfo = new HealthChangeInfo(previousHealth, currentHealth, maxHealth, instigator);
            HealthChanged?.Invoke(this, changeInfo);
            Damaged?.Invoke(this, new DamageInfo(previousHealth - currentHealth, hitPoint, hitNormal, instigator, changeInfo));

            if (currentHealth > 0f)
            {
                return;
            }

            isDead = true;
            Died?.Invoke(this, instigator);

            if (destroyOnDeath)
            {
                Destroy(gameObject, healthDestroyDelay);
            }
        }

        public void RestoreFullHealth(GameObject source = null)
        {
            if (isDead)
            {
                return;
            }

            float missingHealth = maxHealth - currentHealth;
            if (missingHealth > 0f)
            {
                ReceiveHealing(missingHealth, source);
            }
        }

        public void SetInvulnerable(bool value)
        {
            invulnerable = value;
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

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            if (!Application.isPlaying)
            {
                initialMaxHealth = maxHealth;
                hasInitializedBaseHealth = true;
            }

            if (!Application.isPlaying)
            {
                currentHealth = startAtMaxHealth ? maxHealth : startingHealth;
                isDead = currentHealth <= 0f;
            }
        }
    }
}
