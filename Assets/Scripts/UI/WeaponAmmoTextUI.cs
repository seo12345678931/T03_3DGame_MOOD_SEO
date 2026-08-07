using Mood.Weapons;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Weapon Ammo Text")]
    [DisallowMultipleComponent]
    public sealed class WeaponAmmoTextUI : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private string noWeaponText = "<color=#E73C3C>--</color> / <color=#E73C3C>--</color>";
        [SerializeField] private bool hideWhenNoWeapon;

        [Header("Warnings")]
        [SerializeField] private float ammoAlertPercent = 0.25f;
        [SerializeField] private TMP_Text NeedReloadText;
        [SerializeField] private TMP_Text lowAmmoWarningText;
        [SerializeField] private TMP_Text NoAmmoText;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void AssignReferences()
        {
            if (ammoText == null)
            {
                ammoText = GetComponent<TMP_Text>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = FindFirstObjectByType<PlayerWeaponSystem>();
            }
        }

        private void Subscribe()
        {
            if (weaponSystem != null)
            {
                weaponSystem.AmmoChanged += HandleAmmoChanged;
            }
        }

        private void Unsubscribe()
        {
            if (weaponSystem != null)
            {
                weaponSystem.AmmoChanged -= HandleAmmoChanged;
            }
        }

        private void HandleAmmoChanged(PlayerWeaponSystem _)
        {
            Refresh();
        }

        public void Refresh()
        {
            SetActiveIfAssigned(NeedReloadText, false);
            SetActiveIfAssigned(lowAmmoWarningText, false);
            SetActiveIfAssigned(NoAmmoText, false);

            if (ammoText == null)
            {
                return;
            }

            bool hasWeapon = weaponSystem != null && weaponSystem.CurrentWeaponData != null;
            if (!hasWeapon)
            {
                ammoText.text = noWeaponText;
                ammoText.enabled = !hideWhenNoWeapon;
                return;
            }

            ammoText.enabled = true;

            bool hasInfiniteReserveAmmo = weaponSystem.CurrentWeaponHasInfiniteReserveAmmo;
            string reserveAmmoText = hasInfiniteReserveAmmo ? "∞" : weaponSystem.CurrentReserveAmmo.ToString();
            ammoText.text = $"{weaponSystem.CurrentAmmoInMagazine} / {reserveAmmoText}";

            float ammoRatio = (float)weaponSystem.CurrentAmmoInMagazine / weaponSystem.CurrentWeaponData.MagazineSize;

            // 무한 예비탄 무기는 탄창 수만 경고색으로 표시하고
            // 예비탄 부족/없음 관련 경고 UI는 표시하지 않는다.
            if (hasInfiniteReserveAmmo)
            {
                if (ammoRatio <= ammoAlertPercent)
                {
                    ammoText.text = $"<color=#E7613C>{weaponSystem.CurrentAmmoInMagazine}</color> / {reserveAmmoText}";
                    if (weaponSystem.CurrentAmmoInMagazine == 0)
                    {
                        ammoText.text = $"<color=#E73C3C>{weaponSystem.CurrentAmmoInMagazine}</color> / {reserveAmmoText}";
                    }
                }

                return;
            }

            if (ammoRatio <= ammoAlertPercent)
            {
                SetActiveIfAssigned(NeedReloadText, true);
                ammoText.text = $"<color=#E7613C>{weaponSystem.CurrentAmmoInMagazine}</color> / {reserveAmmoText}";
                if (weaponSystem.CurrentAmmoInMagazine == 0)
                {
                    ammoText.text = $"<color=#E73C3C>{weaponSystem.CurrentAmmoInMagazine}</color> / {reserveAmmoText}";
                }
            }

            if (weaponSystem.CurrentReserveAmmo == 0)
            {
                ammoText.text = $"{weaponSystem.CurrentAmmoInMagazine} / <color=#E73C3C>{reserveAmmoText}</color>";
            }

            if (ammoRatio <= 0.4f && weaponSystem.CurrentReserveAmmo == 0)
            {
                SetActiveIfAssigned(NeedReloadText, false);
                SetActiveIfAssigned(lowAmmoWarningText, true);
                ammoText.text = $"<color=#E7423C>{weaponSystem.CurrentAmmoInMagazine}</color> / " +
                                $"<color=#E73C3C>{reserveAmmoText}</color>";

                if (weaponSystem.CurrentAmmoInMagazine == 0)
                {
                    SetActiveIfAssigned(lowAmmoWarningText, false);
                    SetActiveIfAssigned(NoAmmoText, true);
                    ammoText.text = $"<color=#E73C3C>{weaponSystem.CurrentAmmoInMagazine}</color> / " +
                                    $"<color=#E73C3C>{reserveAmmoText}</color>";
                }
            }
        }

        private static void SetActiveIfAssigned(TMP_Text target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }
    }
}
