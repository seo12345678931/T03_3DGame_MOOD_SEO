using UnityEngine;

namespace Mood.Ammo
{
    [CreateAssetMenu(fileName = "AmmoType", menuName = "MOOD/Ammo/Ammo Type")]
    public sealed class AmmoTypeData : ScriptableObject
    {
        [SerializeField] private string displayName = "Rifle Ammo";
        [SerializeField, Min(1)] private int maxReserveAmmo = 180;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int MaxReserveAmmo => maxReserveAmmo;
    }
}
