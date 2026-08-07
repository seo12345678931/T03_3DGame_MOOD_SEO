using System.Collections;
using Mood.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Weapon Slots")]
    [DisallowMultipleComponent]
    public sealed class WeaponSlotsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Slots")]
        [SerializeField] private Toggle[] weaponSlots;
        [SerializeField] private Image[] weaponIcons;   // 4개의 컴포넌트에 할당할 무기 아이콘 이미지 변수
        [SerializeField] private Image[] weaponIconsActivate;   // 4개의 컴포넌트에 할당할 무기 아이콘 이미지 변수(활성화 시)
        [SerializeField] private Sprite emptySlotIcon;  // 아무 무기가 없을 때 대신 표시하는 변수
        [SerializeField] private bool hideEmptySlotIcon = true; // 무기 획득여부를 확인하기 위한 bool 변수

        [Header("Fade")]
        [SerializeField, Min(0f)] private float visibleDuration = 2f;   // Fade 지연시간
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;    // canvasGroup.alpha가 0이 될 때까지 걸리는 시간

        private Coroutine fadeCoroutine;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
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
            if (weaponSystem == null)
            {
                weaponSystem = FindFirstObjectByType<PlayerWeaponSystem>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void Subscribe()
        {
            if (weaponSystem != null)
            {
                weaponSystem.WeaponChanged += HandleWeaponChanged;
            }
        }

        private void Unsubscribe()
        {
            if (weaponSystem != null)
            {
                weaponSystem.WeaponChanged -= HandleWeaponChanged;
            }
        }

        private void HandleWeaponChanged(PlayerWeaponSystem _)
        {
            Refresh();
            ShowTemporarily();  // canvasGroup.alpha값을 다시 1로 복귀하여 잠시 드러나기
        }

        public void Refresh()
        {
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            int currentSlotIndex = weaponSystem != null ? weaponSystem.CurrentSlotIndex : -1;
            int inactiveIconCount = weaponIcons != null ? weaponIcons.Length : 0;
            int activeIconCount = weaponIconsActivate != null ? weaponIconsActivate.Length : 0;

            for (int slotIndex = 0; slotIndex < weaponSlots.Length; slotIndex++)
            {
                bool isCurrentSlot = slotIndex == currentSlotIndex;

                Toggle slotToggle = weaponSlots[slotIndex];
                if (slotToggle != null)
                {
                    slotToggle.SetIsOnWithoutNotify(isCurrentSlot);
                }

                if (slotIndex < inactiveIconCount)
                {
                    UpdateSlotIcon(weaponIcons[slotIndex], slotIndex, !isCurrentSlot);
                }

                if (slotIndex < activeIconCount)
                {
                    UpdateSlotIcon(weaponIconsActivate[slotIndex], slotIndex, isCurrentSlot);
                }
            }
        }

        public void ShowTemporarily()
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            canvasGroup.alpha = 1f;
            fadeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        // 무기 획득 시 아이콘 갱신
        private void UpdateSlotIcon(Image iconImage, int slotIndex, bool isVisible)
        {
            if (iconImage == null)
            {
                return;
            }

            WeaponData slotWeaponData = weaponSystem != null ? weaponSystem.GetWeaponDataInSlot(slotIndex) : null;
            Sprite iconSprite = slotWeaponData != null ? slotWeaponData.WeaponIcon : emptySlotIcon;

            iconImage.sprite = iconSprite;
            bool hasDisplayableIcon = iconSprite != null || !hideEmptySlotIcon;
            iconImage.enabled = isVisible && hasDisplayableIcon;
        }

        // FadeOut 관련 스크립트
        private IEnumerator FadeOutRoutine()
        {
            // 2초 지연 후 페이드 아웃화 
            yield return new WaitForSeconds(visibleDuration);

            if (canvasGroup == null)
            {
                yield break;
            }

            if (fadeOutDuration <= 0f)
            {
                canvasGroup.alpha = 0f;
                fadeCoroutine = null;
                yield break;
            }

            // 0.35동안 투명해지기
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            fadeCoroutine = null;
        }
    }
}
