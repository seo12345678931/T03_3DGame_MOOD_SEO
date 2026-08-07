using UnityEngine;

namespace Mood.Health
{
    [CreateAssetMenu(fileName = "HealthPickupData", menuName = "MOOD/Health/Health Pickup Data")]
    public sealed class HealthPickupData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Medium Health Pack";
        [SerializeField] private HealthPickupSize size = HealthPickupSize.Medium;
        [SerializeField] private HealthPickup pickupPrefab;

        [Header("Healing")]
        [SerializeField, Min(1f)] private float healAmount = 35f;
        [SerializeField] private bool requireMissingHealth = true;

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float pickupRadius = 1f;
        [SerializeField, Min(0f)] private float autoAbsorbRadius = 4f;
        [SerializeField, Min(0.01f)] private float attractionSpeed = 16f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public HealthPickupSize Size => size;
        public HealthPickup PickupPrefab => pickupPrefab;
        public float HealAmount => healAmount;
        public bool RequireMissingHealth => requireMissingHealth;
        public float PickupRadius => pickupRadius;
        public float AutoAbsorbRadius => autoAbsorbRadius;
        public float AttractionSpeed => attractionSpeed;
    }
}
