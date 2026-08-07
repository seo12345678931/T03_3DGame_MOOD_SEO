using Mood.Input;
using Mood.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/PauseController UI")]
    [DisallowMultipleComponent]
    public sealed class PauseControllerUI : MonoBehaviour
    {
        [SerializeField] private InputManager inputManager;
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject CloseGamePopup;
        
        [Tooltip("게임오버 화면이 나올 시 정지 UI표시하는 할당키(ESC)와 겹치지 않게 미리 막아두기")]
        [SerializeField] private GameObject GameOverUI;
        
        [Header("조작 가이드 메뉴 진입을 위한 오브젝트 제어")]
        [SerializeField] private GameObject KeyGuideUIPrefab;

        [SerializeField] private GameObject Title;
        [SerializeField] private GameObject ButtonGroup;
        
        private bool isPaused;

        private void Awake()
        {
            pauseMenu.SetActive(false);
            CloseGamePopup.SetActive(false);
            KeyGuideUIPrefab.SetActive(false);
            
            if (inputManager == null)
            {
                inputManager = FindFirstObjectByType<InputManager>();
            }
        }

        private void Update()
        {
            if (inputManager == null || !inputManager.PausePressed || GameOverUI.activeSelf )
            {
                return;
            }

            isPaused = !isPaused;
            if (isPaused)
            {
                inputManager.SetInputLocked(true);
                
                // 정지화면 진입 시 마우스 잠금 해제
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
                pauseMenu.SetActive(true);
                Time.timeScale = 0;
            }
            else
            {
                OnDisable();
            }
        }
        
        public void OnDisable()
        {
            inputManager.SetInputLocked(false);
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Time.timeScale = 1f;
            pauseMenu.SetActive(false);
        }

        public void CLoseGame()
        {
            CloseGamePopup.SetActive(true);
        }
        public void CLoseGame_Else()
        {
            CloseGamePopup.SetActive(false);
        }

        public void IsGameClosed()
        {
            SceneManager.LoadScene("LobbyScene");
        }

        public void KeyGuide()
        {
            KeyGuideUIPrefab.SetActive(true);
            Title.SetActive(false);
            ButtonGroup.SetActive(false);
        }

        public void BackToMenu()
        {
            KeyGuideUIPrefab.SetActive(false);
            Title.SetActive(true);
            ButtonGroup.SetActive(true);
        }
    }
}
