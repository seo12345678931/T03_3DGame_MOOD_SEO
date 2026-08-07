using System.Collections;
using Mood.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Key Guide UI_GameScene")]
    [DisallowMultipleComponent]
    public class KeyGuideUI_GameScene : MonoBehaviour
    {
        [SerializeField] private GameObject KeyGuideUIPrefab;
        [SerializeField] private float fadeDuration = 1f;
        [SerializeField] private PlayerInputLockController inputLockController;
        [SerializeField] private InputManager inputManager;

        private CanvasGroup keyGuideCanvasGroup;
        private bool hasStartedFadeOut;
        private bool hasLockedInput;

        private void Awake()
        {
            if (inputLockController == null)
            {
                inputLockController = FindFirstObjectByType<PlayerInputLockController>();
            }

            if (inputManager == null)
            {
                inputManager = FindFirstObjectByType<InputManager>();
            }

            if (KeyGuideUIPrefab != null)
            {
                KeyGuideUIPrefab.SetActive(true);
                keyGuideCanvasGroup = KeyGuideUIPrefab.GetComponent<CanvasGroup>();

                if (keyGuideCanvasGroup == null)
                {
                    keyGuideCanvasGroup = KeyGuideUIPrefab.AddComponent<CanvasGroup>();
                }

                keyGuideCanvasGroup.alpha = 1f;
                keyGuideCanvasGroup.blocksRaycasts = true;
                keyGuideCanvasGroup.interactable = true;
            }

            LockGameplayInput();
        }

        private void OnDisable()
        {
            ReleaseGameplayInput();
        }

        private void Update()
        {
            if (hasStartedFadeOut)
                return;

            bool pressedAnyInput =
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

            if (pressedAnyInput)
            {
                hasStartedFadeOut = true;
                StartCoroutine(FadeOut());
            }
        }

        private IEnumerator FadeOut()
        {
            if (KeyGuideUIPrefab == null || keyGuideCanvasGroup == null)
                yield break;

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                keyGuideCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }

            keyGuideCanvasGroup.alpha = 0f;
            keyGuideCanvasGroup.blocksRaycasts = false;
            keyGuideCanvasGroup.interactable = false;
            ReleaseGameplayInput();
            KeyGuideUIPrefab.SetActive(false);
        }

        // 가이드 UI가 보이는 동안에는 게임플레이 입력을 잠가 아래 화면에 입력이 전달되지 않도록 한다.
        private void LockGameplayInput()
        {
            if (hasLockedInput)
            {
                return;
            }

            if (inputLockController != null)
            {
                inputLockController.TryLockInput(this);
                hasLockedInput = true;
                return;
            }

            if (inputManager != null)
            {
                inputManager.SetInputLocked(true);
                hasLockedInput = true;
            }
        }

        // UI가 닫힐 때 이 UI가 잠가 둔 입력만 다시 복원한다.
        private void ReleaseGameplayInput()
        {
            if (!hasLockedInput)
            {
                return;
            }

            if (inputLockController != null)
            {
                inputLockController.UnlockInput(this);
            }
            else if (inputManager != null)
            {
                inputManager.SetInputLocked(false);
            }

            hasLockedInput = false;
        }
    }
}
