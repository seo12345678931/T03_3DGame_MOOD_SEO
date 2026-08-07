using System.Collections;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Zone Announcement UI")]
    [DisallowMultipleComponent]
    public sealed class ZoneAnnouncementUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0f)] private float visibleDuration = 2f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool hideOnAwake = true;

        private Coroutine displayRoutine;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();

            if (hideOnAwake)
            {
                SetVisible(false);
            }
        }

        private void OnDisable()
        {
            if (displayRoutine != null)
            {
                StopCoroutine(displayRoutine);
                displayRoutine = null;
            }

            SetVisible(false);
        }

        public void ShowMessage(string message)
        {
            if (messageText == null)
            {
                Debug.LogWarning($"[{nameof(ZoneAnnouncementUI)}:{name}] TMP_Text reference is missing.", this);
                return;
            }

            if (displayRoutine != null)
            {
                StopCoroutine(displayRoutine);
            }

            messageText.text = message;
            displayRoutine = StartCoroutine(DisplayRoutine());
        }

        private IEnumerator DisplayRoutine()
        {
            SetVisible(true);

            if (visibleDuration > 0f)
            {
                if (useUnscaledTime)
                {
                    yield return new WaitForSecondsRealtime(visibleDuration);
                }
                else
                {
                    yield return new WaitForSeconds(visibleDuration);
                }
            }

            if (fadeDuration <= 0f || canvasGroup == null)
            {
                SetVisible(false);
                displayRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += DeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }

            SetVisible(false);
            displayRoutine = null;
        }

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private void AssignReferences()
        {
            if (messageText == null)
            {
                messageText = GetComponent<TMP_Text>();
                if (messageText == null)
                {
                    messageText = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }

            if (messageText != null)
            {
                messageText.enabled = visible;
            }
        }
    }
}
