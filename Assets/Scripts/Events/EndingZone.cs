using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mood.Input;

namespace Mood.Events
{
    [AddComponentMenu("MOOD/Events/Ending Zone")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EndingZone : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private LayerMask activatorLayers = ~0;
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private bool triggerOnce = true;

        [Header("Countdown UI")]
        [SerializeField] private GameObject countdown;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private int countNum = 3;
        [SerializeField] private float countInterval = 1f;

        [Header("Ending UI")]
        [SerializeField] private GameObject ending;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private InputManager inputManager; // 엔딩장면에 도달 시 플레이어의 게임조작을 잠그기

        private bool canUseEndingButtons;
        private bool hasTriggered;

        public bool IsCountdownFinished { get; private set; }
        public float FinishedTime { get; private set; }

        private void Awake()
        {
            canUseEndingButtons = false;
            IsCountdownFinished = false;
            FinishedTime = 0f;

            if (countdown != null)
                countdown.SetActive(false);

            if (ending != null)
                ending.SetActive(false);

            if (restartButton != null)
                restartButton.interactable = false;

            if (quitButton != null)
                quitButton.interactable = false;

            if (inputManager == null)
                inputManager = FindFirstObjectByType<InputManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered)
                return;

            if (((1 << other.gameObject.layer) & activatorLayers) == 0)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            hasTriggered = true;
            StartCoroutine(CountDownRoutine());
        }

        private IEnumerator CountDownRoutine()
        {
            int currentCount = countNum;

            if (countdown != null)
                countdown.SetActive(true);

            while (currentCount > 0)
            {
                if (countdownText != null)
                    countdownText.text = $"탈출까지 앞으로 {currentCount}초";

                yield return new WaitForSeconds(countInterval);
                currentCount--;
            }

            if (countdown != null)
                countdown.SetActive(false);

            if (inputManager != null)
                inputManager.SetInputLocked(true);

            if (ending != null)
                ending.SetActive(true);

            canUseEndingButtons = true;
            IsCountdownFinished = true;
            FinishedTime = Time.timeSinceLevelLoad;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (restartButton != null)
                restartButton.interactable = true;

            if (quitButton != null)
                quitButton.interactable = true;
        }

        private void Update()
        {
            if (!canUseEndingButtons)
                return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                restartButton?.onClick.Invoke();
                SetHighlightedColor(restartButton);
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                quitButton?.onClick.Invoke();
                SetHighlightedColor(quitButton);
                SceneManager.LoadScene("LobbyScene");
            }
        }

        private static void SetHighlightedColor(Button targetButton)
        {
            if (targetButton == null)
                return;

            ColorBlock colors = targetButton.colors;
            ColorBlock newColors = colors;
            newColors.normalColor = colors.highlightedColor;
            targetButton.colors = newColors;
        }

        public void OnRestartButtonClicked()
        {
            if (!canUseEndingButtons)
                return;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void OnQuitButtonClicked()
        {
            if (!canUseEndingButtons)
                return;
            SceneManager.LoadScene("LobbyScene");
        }
    }
}
