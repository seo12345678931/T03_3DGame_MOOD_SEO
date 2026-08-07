using UnityEngine;

namespace Mood.Speed
{
    [CreateAssetMenu(fileName = "SpeedPickupData", menuName = "MOOD/Speed/Speed Pickup Data")]
    public sealed class SpeedPickupData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Speed Boost";
        [SerializeField] private SpeedPickup pickupPrefab;

        [Header("Buff")]
        [SerializeField, Min(0.01f)] private float moveSpeedBonus = 0.2f;
        [SerializeField, Min(0.1f)] private float duration = 5f;

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float pickupRadius = 1f;
        [SerializeField, Min(0f)] private float autoAbsorbRadius = 4f;
        [SerializeField, Min(0.01f)] private float attractionSpeed = 16f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public SpeedPickup PickupPrefab => pickupPrefab;
        public float MoveSpeedBonus => moveSpeedBonus;
        public float Duration => duration;
        public float PickupRadius => pickupRadius;
        public float AutoAbsorbRadius => autoAbsorbRadius;
        public float AttractionSpeed => attractionSpeed;
    }
}
