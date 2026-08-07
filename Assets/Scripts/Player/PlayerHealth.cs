using System;
using System.Collections.Generic;
using Mood.Combat;
using Mood.Audio;
using Mood.Health;
using Mood.Input;
using Mood.Weapons;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using AudioProfile = Akila.FPSFramework.AudioProfile;

namespace Mood.Player
{
    [AddComponentMenu("MOOD/Player/Player Health")]
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable, IHealthReceiver
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool startAtMaxHealth = true;
        [SerializeField, Min(0f)] private float startingHealth = 100f;

        [Header("State")]
        [SerializeField] private bool invulnerable;
        [SerializeField] private bool destroyOnDeath;
        [SerializeField, Min(0f)] private float destroyDelay;
        [SerializeField] private Behaviour[] disableOnDeath;

        [Header("Damage Feedback")]
        [SerializeField] private CinemachineImpulseSource damageImpulseSource;
        [SerializeField, Min(0f)] private float baseImpulseForce = 0.8f;
        [SerializeField, Min(0f)] private float impulseForcePerDamage = 0.04f;
        [SerializeField, Min(0f)] private float maxImpulseForce = 2.5f;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private SfxPlayer damageSfxPlayer;
        [SerializeField] private AudioProfile damageAudioProfile;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onHealed;
        [SerializeField] private UnityEvent onDied;

        private float currentHealth;
        private bool isDead;

        public event Action<PlayerHealth, HealthChangeInfo> HealthChanged;
        public event Action<PlayerHealth, HealthChangeInfo> Healed;
        public event Action<PlayerHealth, DamageInfo> Damaged;
        public event Action<PlayerHealth, GameObject> Died;

        public Component Component => this;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        private void Reset()
        {
            if (damageImpulseSource == null)
            {
                damageImpulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);
            }

            AssignAudioReferences();

            if (disableOnDeath == null || disableOnDeath.Length == 0)
            {
                List<Behaviour> behaviours = new List<Behaviour>(3);
                AddBehaviourIfPresent(behaviours, GetComponent<InputManager>());
                AddBehaviourIfPresent(behaviours, GetComponent<HyperFpsFirstPersonController>());
                AddBehaviourIfPresent(behaviours, GetComponent<PlayerWeaponSystem>());
                disableOnDeath = behaviours.ToArray();
            }
        }

        private void Awake()
        {
            currentHealth = startAtMaxHealth ? maxHealth : Mathf.Clamp(startingHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;

            if (damageImpulseSource == null)
            {
                damageImpulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);
            }

            AssignAudioReferences();

            LogDebug($"Initialized. HP {currentHealth:0.##}/{maxHealth:0.##}");
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
            onHealed?.Invoke();
            return currentHealth - previousHealth;
        }

        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator)
        {
            if (isDead || invulnerable || damage <= 0f)
            {
                LogDebug($"Damage ignored. isDead={isDead}, invulnerable={invulnerable}, damage={damage:0.##}");
                return;
            }

            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - damage);

            HealthChangeInfo changeInfo = new HealthChangeInfo(previousHealth, currentHealth, maxHealth, instigator);
            DamageInfo damageInfo = new DamageInfo(previousHealth - currentHealth, hitPoint, hitNormal, instigator, changeInfo);
            HealthChanged?.Invoke(this, changeInfo);
            Damaged?.Invoke(this, damageInfo);
            GenerateDamageImpulse(hitPoint, hitNormal, damage);
            damageSfxPlayer?.Play(damageAudioProfile);
            onDamaged?.Invoke();
            LogDebug($"Took {damageInfo.DamageAmount:0.##} damage from {(instigator != null ? instigator.name : "Unknown")}. HP {previousHealth:0.##} -> {currentHealth:0.##}");

            if (currentHealth > 0f)
            {
                return;
            }

            isDead = true;
            DisableBehavioursOnDeath();
            Died?.Invoke(this, instigator);
            onDied?.Invoke();
            LogDebug($"Died from {(instigator != null ? instigator.name : "Unknown")}.");

            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
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

        public void Kill(GameObject instigator = null)
        {
            if (isDead)
            {
                return;
            }

            ApplyDamage(currentHealth, transform.position, -transform.forward, instigator);
        }

        private void GenerateDamageImpulse(Vector3 hitPoint, Vector3 hitNormal, float damage)
        {
            if (damageImpulseSource == null)
            {
                LogDebug("Damage impulse skipped. CinemachineImpulseSource not found.");
                return;
            }

            Vector3 impulseDirection = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -transform.forward;
            float impulseForce = baseImpulseForce + (damage * impulseForcePerDamage);
            if (maxImpulseForce > 0f)
            {
                impulseForce = Mathf.Min(impulseForce, maxImpulseForce);
            }

            if (impulseForce <= 0f)
            {
                return;
            }

            damageImpulseSource.GenerateImpulseAtPositionWithVelocity(hitPoint, impulseDirection * impulseForce);
        }

        private void AssignAudioReferences()
        {
            if (damageSfxPlayer == null)
            {
                damageSfxPlayer = GetComponentInChildren<SfxPlayer>(true);
            }

            if (damageSfxPlayer == null)
            {
                GameObject sfxObject = new GameObject("Player Damage SFX Player");
                sfxObject.transform.SetParent(transform, false);
                damageSfxPlayer = sfxObject.AddComponent<SfxPlayer>();
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth:{name}] {message}", this);
            }
        }

        private void DisableBehavioursOnDeath()
        {
            if (disableOnDeath == null)
            {
                return;
            }

            for (int index = 0; index < disableOnDeath.Length; index++)
            {
                Behaviour behaviour = disableOnDeath[index];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        private static void AddBehaviourIfPresent<T>(List<Behaviour> behaviours, T behaviour)
            where T : Behaviour
        {
            if (behaviour != null)
            {
                behaviours.Add(behaviour);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);

            if (!Application.isPlaying)
            {
                currentHealth = startAtMaxHealth ? maxHealth : startingHealth;
                isDead = currentHealth <= 0f;
            }
        }
    }
}
