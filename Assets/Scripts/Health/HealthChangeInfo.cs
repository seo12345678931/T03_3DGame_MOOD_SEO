using UnityEngine;

namespace Mood.Health
{
    public readonly struct HealthChangeInfo
    {
        public HealthChangeInfo(float previousHealth, float currentHealth, float maxHealth, GameObject source)
        {
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Source = source;
        }

        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public GameObject Source { get; }
        public float Delta => CurrentHealth - PreviousHealth;
        public float NormalizedHealth => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;
    }

    public readonly struct DamageInfo
    {
        public DamageInfo(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator, HealthChangeInfo changeInfo)
        {
            DamageAmount = damageAmount;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            Instigator = instigator;
            ChangeInfo = changeInfo;
        }

        public float DamageAmount { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitNormal { get; }
        public GameObject Instigator { get; }
        public HealthChangeInfo ChangeInfo { get; }
    }
}
