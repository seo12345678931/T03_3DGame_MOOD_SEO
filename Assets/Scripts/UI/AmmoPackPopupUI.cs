using System.Collections;
using System.Collections.Generic;
using Mood.Ammo;
using Mood.Weapons;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Ammo Pack Popup UI")]
    [DisallowMultipleComponent]
    public sealed class AmmoPackPopupUI : MonoBehaviour
    {
        private static readonly Color SmallPopupColor = new Color32(0xFF, 0x00, 0x00, 0xFF);
        private static readonly Color MediumPopupColor = new Color32(0x00, 0xFF, 0x08, 0xFF);
        private static readonly Color LargePopupColor = new Color32(0x00, 0x16, 0xFF, 0xFF);

        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private TMP_Text popupText;
        
        [Tooltip("드러났다가 감추는데 걸리는 시간 (ShowPopupRoutine에 참조)")]
        [SerializeField, Min(0f)] private float visibleDuration = 1f;

        private Coroutine popupRoutine;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();

            if (popupText != null)
            {
                popupText.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (weaponSystem != null)
            {
                weaponSystem.AmmoReceived += HandleAmmoReceived;
            }
        }

        private void OnDisable()
        {
            if (weaponSystem != null)
            {
                weaponSystem.AmmoReceived -= HandleAmmoReceived;
            }
        }

        private void AssignReferences()
        {
            if (weaponSystem == null)
            {
                weaponSystem = GetComponentInParent<PlayerWeaponSystem>();
            }

            if (popupText == null)
            {
                popupText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void HandleAmmoReceived(GameObject source, IReadOnlyDictionary<AmmoTypeData, int> receivedAmmo)
        {
            if (popupText == null || source == null || !source.TryGetComponent(out AmmoPickup ammoPickup))
            {
                return;
            }

            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
            }

            popupText.color = GetPopupColor(ammoPickup);
            popupText.text = BuildAmmoPopupText(ammoPickup, receivedAmmo);
            popupRoutine = StartCoroutine(ShowPopupRoutine());
        }

        private static Color GetPopupColor(AmmoPickup ammoPickup)
        {
            if (ammoPickup.PickupData == null)
            {
                return Color.white;
            }

            switch (ammoPickup.PickupData.Size)
            {
                case AmmoPickupSize.Small:
                    return SmallPopupColor;
                case AmmoPickupSize.Medium:
                    return MediumPopupColor;
                case AmmoPickupSize.Large:
                    return LargePopupColor;
                default:
                    return Color.white;
            }
        }
        
        private static string BuildAmmoPopupText(AmmoPickup ammoPickup, IReadOnlyDictionary<AmmoTypeData, int> receivedAmmo)
        {
            if (ammoPickup == null || ammoPickup.PickupData == null)
            {
                return string.Empty;
            }

            if (receivedAmmo == null || receivedAmmo.Count == 0)
            {
                return $"+ {ammoPickup.PickupData.DisplayName}";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            bool isFirstLine = true;

            foreach (KeyValuePair<AmmoTypeData, int> entry in receivedAmmo)
            {
                if (entry.Key == null || entry.Value <= 0)
                {
                    continue;
                }

                if (!isFirstLine)
                {
                    builder.Append('\n');
                }

                builder.Append("+ ");
                builder.Append(entry.Value);
                builder.Append(' ');
                builder.Append(entry.Key.DisplayName);
                isFirstLine = false;
            }

            return builder.Length > 0 ? builder.ToString() : $"+ {ammoPickup.PickupData.DisplayName}";
        }

        private IEnumerator ShowPopupRoutine()
        {
            popupText.gameObject.SetActive(true);
            yield return new WaitForSeconds(visibleDuration);
            popupText.gameObject.SetActive(false);
            popupRoutine = null;
        }
    }
}
