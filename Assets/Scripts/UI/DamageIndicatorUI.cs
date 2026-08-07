using System;
using System.Collections;
using Mood.Health;
using Mood.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Damage Indicator")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DamageIndicatorUI : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private CanvasGroup screenEffects;
        [SerializeField] private GameObject bloodPeffect;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;

        private Coroutine fadeRoutine;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
            // 눈에 거슬리는 Blood Effects를 작업 시에 감췄다가 실제 게임 실행 시키면 자동으로 활성화
            if (bloodPeffect != null)
            {
                bloodPeffect.SetActive(true);
            }

            if (screenEffects != null)
            {
                screenEffects.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged -= HandleDamaged;
            }
        }

        private void AssignReferences()
        {
            if (screenEffects == null)
            {
                screenEffects = GetComponent<CanvasGroup>();
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }
        }

        private void HandleDamaged(PlayerHealth _, DamageInfo __)
        {
            Show();
        }

        public void Show()
        {
            Show(alpha);
        }

        public void Show(float targetAlpha)
        {
            if (screenEffects == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            screenEffects.alpha = Mathf.Clamp01(targetAlpha);
            fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            if (fadeDuration <= 0f)
            {
                screenEffects.alpha = 0f;
                fadeRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            float startAlpha = screenEffects.alpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                screenEffects.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }

            screenEffects.alpha = 0f;
            fadeRoutine = null;
        }
    }
}
