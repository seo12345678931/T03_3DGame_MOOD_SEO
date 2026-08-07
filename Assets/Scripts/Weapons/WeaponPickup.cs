using UnityEngine;

namespace Mood.Weapons
{
    // 월드에 놓인 무기와 픽업 시 넘길 탄약 상태를 보관한다.
    [AddComponentMenu("MOOD/Weapons/Weapon Pickup")]
    [DisallowMultipleComponent]
    public sealed class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private int ammoInMagazine = -1;
        [SerializeField] private int reserveAmmo = -1;
        [SerializeField] private string interactionLabel = "Take";

        public WeaponData WeaponData => weaponData;
        public int AmmoInMagazine => weaponData == null ? Mathf.Max(0, ammoInMagazine) : Mathf.Clamp(ammoInMagazine < 0 ? weaponData.MagazineSize : ammoInMagazine, 0, weaponData.MagazineSize);
        public int ReserveAmmo => reserveAmmo < 0 ? (weaponData != null ? weaponData.InitialReserveAmmo : 0) : Mathf.Max(0, reserveAmmo);
        public string InteractionLabel => string.IsNullOrWhiteSpace(interactionLabel) ? "Take" : interactionLabel;

        public string GetInteractionText()
        {
            return weaponData == null ? InteractionLabel : $"{InteractionLabel} {weaponData.DisplayName}";
        }

        public void SetWeaponData(WeaponData data)
        {
            weaponData = data;
        }

        public void SetAmmoInMagazine(int ammo)
        {
            ammoInMagazine = ammo;
        }

        public void SetReserveAmmo(int ammo)
        {
            reserveAmmo = ammo;
        }

        private void OnValidate()
        {
            // -1은 WeaponData 기본값 사용 의미로 유지한다.
            if (ammoInMagazine < -1)
            {
                ammoInMagazine = -1;
            }

            if (reserveAmmo < -1)
            {
                reserveAmmo = -1;
            }

            if (weaponData != null && ammoInMagazine > weaponData.MagazineSize)
            {
                ammoInMagazine = weaponData.MagazineSize;
            }
        }
    }
}
