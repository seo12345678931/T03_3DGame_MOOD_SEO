using System.Collections;
using Mood.Health;
using Mood.Player;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Health Pack Popup UI")]
    [DisallowMultipleComponent]
    public sealed class HealthPackPopupUI : MonoBehaviour
    {
        // HealthPickupSize에 따라 텍스트 색상을 다르게 출력(Small > Medium > Large)
        private static readonly Color SmallPopupColor = new Color32(0xFF, 0x00, 0x00, 0xFF);
        private static readonly Color MediumPopupColor = new Color32(0x00, 0xFF, 0x08, 0xFF);
        private static readonly Color LargePopupColor = new Color32(0x00, 0x16, 0xFF, 0xFF);

        [SerializeField] private PlayerHealth playerHealth;
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
            if (playerHealth != null)
            {
                playerHealth.Healed += HandleHealed;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Healed -= HandleHealed;
            }
        }

        private void AssignReferences()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }

            if (popupText == null)
            {
                popupText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void HandleHealed(PlayerHealth _, HealthChangeInfo changeInfo)
        {
            GameObject source = changeInfo.Source;
            if (source == null || !source.TryGetComponent(out HealthPickup healthPickup))
            {
                return;
            }

            ShowPopup(healthPickup);
        }

        private void ShowPopup(HealthPickup healthPickup)
        {
            if (popupText == null)
            {
                return;
            }

            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
            }

            popupText.color = GetPopupColor(healthPickup);
            popupText.text = $"+ {healthPickup.PickupData.HealAmount}";
            popupRoutine = StartCoroutine(ShowPopupRoutine());
        }

        // HealthPickupSize에 따른 텍스트 색상 출력 (switch문으로 사용함)
        private static Color GetPopupColor(HealthPickup healthPickup)
        {
            if (healthPickup == null || healthPickup.PickupData == null)
            {
                return Color.white;
            }

            switch (healthPickup.PickupData.Size)
            {
                case HealthPickupSize.Small:
                    return SmallPopupColor;
                case HealthPickupSize.Medium:
                    return MediumPopupColor;
                case HealthPickupSize.Large:
                    return LargePopupColor;
                default:
                    return Color.white;
            }
        }

        // visibleDuration초 동안 드러났다가 감추는 코루틴
        private IEnumerator ShowPopupRoutine()
        {
            popupText.gameObject.SetActive(true);
            yield return new WaitForSeconds(visibleDuration);
            popupText.gameObject.SetActive(false);
            popupRoutine = null;
        }
    }
}
