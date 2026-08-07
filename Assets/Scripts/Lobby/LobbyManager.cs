using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace Mood.Lobby
{
    [AddComponentMenu("MOOD/Lobby/Lobby Manager")]
    [DisallowMultipleComponent]
    public sealed class LobbyManager : MonoBehaviour
    {
        [Header("로비 컴포넌트")] 
        [SerializeField] private GameObject MainTitle;
        [SerializeField] private GameObject SelectMenu;
        [SerializeField] private GameObject CloseGamePopup;
        [SerializeField] private GameObject SettingMenu;
        
        [Header("UI 버튼 클릭음")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private LoadingScript loadingScript;
        
        private bool hasPlayedIntroSound = false;

        private void Start()
        {
            // 로비진입 시 마우스 잠금 해제 (게임오버에서 메인메뉴로 돌아갈 때도)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            MainTitle.SetActive(true);
            SelectMenu.SetActive(false);
            CloseGamePopup.SetActive(false);
            SettingMenu.SetActive(false);
        }

        private void Update()
        {
            // 아무 키 클릭 시 메인메뉴 진입
            bool pressedAnyInput =
                Keyboard.current.anyKey.wasPressedThisFrame ||
                Mouse.current.leftButton.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame;
            
            if (MainTitle.activeSelf && pressedAnyInput && !hasPlayedIntroSound)
            {
                hasPlayedIntroSound = true;
                audioSource.Play();
                GoToLobby();
            }

            if(SettingMenu.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                GoToLobby();
            }
        }

        public void GoToLobby()
        {
            hasPlayedIntroSound = false;
            MainTitle.SetActive(false);
            SelectMenu.SetActive(true);
            CloseGamePopup.SetActive(false);
            SettingMenu.SetActive(false);
        }

        public void GameStart()
        {
            if (loadingScript != null)
            {
                loadingScript.LoadScene("GameScene");
                return;
            }
        }
        
        public void GoToSetting()
        {
            MainTitle.SetActive(false);
            SelectMenu.SetActive(false);
            CloseGamePopup.SetActive(false);
            SettingMenu.SetActive(true);
        }

        public void CLoseGame()
        {
            MainTitle.SetActive(false);
            SelectMenu.SetActive(true);
            CloseGamePopup.SetActive(true);
        }
        public void IsGameClosed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}
