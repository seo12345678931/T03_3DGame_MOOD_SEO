using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mood.Ammo
{
    [CreateAssetMenu(fileName = "AmmoPickupData", menuName = "MOOD/Ammo/Ammo Pickup Data")]
    public sealed class AmmoPickupData : ScriptableObject
    {
        [Serializable]
        public sealed class AmmoGrant
        {
            [SerializeField] private AmmoTypeData ammoType;
            [SerializeField, Min(1)] private int amount = 10;

            public AmmoTypeData AmmoType => ammoType;
            public int Amount => amount;
            public bool IsValid => ammoType != null && amount > 0;
        }

        [Header("Identity")]
        [SerializeField] private string displayName = "Medium Ammo Box";
        [SerializeField] private AmmoPickupSize size = AmmoPickupSize.Medium;
        [SerializeField] private AmmoPickup pickupPrefab;

        [Header("Ammo Grants")]
        [SerializeField] private List<AmmoGrant> grants = new List<AmmoGrant>();

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float pickupRadius = 1f;
        [SerializeField, Min(0f)] private float autoAbsorbRadius = 4f;
        [SerializeField, Min(0.01f)] private float attractionSpeed = 16f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public AmmoPickupSize Size => size;
        public AmmoPickup PickupPrefab => pickupPrefab;
        public IReadOnlyList<AmmoGrant> Grants => grants;
        public float PickupRadius => pickupRadius;
        public float AutoAbsorbRadius => autoAbsorbRadius;
        public float AttractionSpeed => attractionSpeed;
    }
}
