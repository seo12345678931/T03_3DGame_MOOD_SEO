using System.Collections;
using Mood.Weapons;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [DisallowMultipleComponent]
    public sealed class GrenadeUI : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text grenadeCountText;
        [SerializeField, Min(0f)] private float blinkDuration = 0.1f;
        [SerializeField, Min(0f)] private float blinkSpeed = 6f;

        private Coroutine clickFadeRoutine;
        private int lastGrenadeCount = -1;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            // PlayerWeaponSystem의 Awake 초기화가 모두 끝난 뒤 한 번 더 갱신해
            // 게임 최초 시작 시 0개로 남는 표시 문제를 방지한다.
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

            if (grenadeCountText == null)
            {
                grenadeCountText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void Subscribe()
        {
            if (weaponSystem == null)
            {
                return;
            }

            weaponSystem.GrenadeCountChanged += HandleGrenadeCountChanged;
        }

        private void Unsubscribe()
        {
            if (weaponSystem == null)
            {
                return;
            }

            weaponSystem.GrenadeCountChanged -= HandleGrenadeCountChanged;
        }

        private void HandleGrenadeCountChanged(PlayerWeaponSystem currentWeaponSystem)
        {
            int previousGrenadeCount = lastGrenadeCount;
            Refresh();

            if (previousGrenadeCount > currentWeaponSystem.CurrentGrenadeCount)
            {
                PlayThrowFeedback();
            }
        }

        public void Refresh()
        {
            if (weaponSystem == null)
            {
                AssignReferences();
            }

            int currentGrenadeCount = weaponSystem != null ? weaponSystem.CurrentGrenadeCount : 0;
            int maxGrenadeCount = weaponSystem != null ? weaponSystem.MaxGrenadeCount : 0;

            if (grenadeCountText != null)
            {
                grenadeCountText.text = maxGrenadeCount > 0
                    ? $"{currentGrenadeCount}"
                    : currentGrenadeCount.ToString();
            }

            lastGrenadeCount = currentGrenadeCount;

            if (currentGrenadeCount <= 0)
            {
                if (grenadeCountText != null)
                {
                    grenadeCountText.text = $"<color=#E73C3C>{currentGrenadeCount}</color>";
                }
            }
        }

        private void PlayThrowFeedback()
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (clickFadeRoutine != null)
            {
                StopCoroutine(clickFadeRoutine);
            }

            clickFadeRoutine = StartCoroutine(ClickFade());
        }

        private IEnumerator ClickFade()
        {
            float elapsed = 0f;

            while (elapsed < blinkDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed * blinkSpeed;
                canvasGroup.alpha = Mathf.PingPong(normalizedTime, 1f);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            clickFadeRoutine = null;
        }
    }
}
