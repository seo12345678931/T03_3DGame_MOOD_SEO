using UnityEngine;
using UnityEngine.InputSystem;
using Mood.Player;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Game Over UI")]
    [DisallowMultipleComponent]
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject GameOverUIPrefab;
        [SerializeField] private PlayerHealth playerHealth;
        
        [SerializeField] private Button RestartButton;
        [SerializeField] private Button QuitButton;
        
        // 게임 시작 시 Awake로 게임오버 타이틀 감추기
        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }

            if (GameOverUIPrefab != null)
            {
                GameOverUIPrefab.SetActive(false);
            }
        }

        private void Update()
        {
            // 플레이어가 체력 0이하로 도달했을 때 게임오버 타이틀 뜨기
            if (playerHealth != null && playerHealth.CurrentHealth <= 0)
            {
                if (GameOverUIPrefab != null)
                {
                    GameOverUIPrefab.SetActive(true);
                }
                
                // R키를 누르면 현재 게임씬 재시작하기 (InputSystem으로 설정했음)
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    SetHighlightedColor_RestartButton();
                    RestartButton.onClick.Invoke();
                    // 별도의 이름이 아닌 현재 게임씬을 재시작
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
                // ESC키를 누르면 게임나가기 (아직은 메인메뉴 구현이 안되어 에디터 종료로만 작동하게 했음)
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    SetHighlightedColor_QuitButton();
                    QuitButton.onClick.Invoke();
                    SceneManager.LoadScene("LobbyScene");
                }
            }
        }
        
        // 특정 키 입력 시 Button 색상을 highlightedColor로 변환
        private void SetHighlightedColor_RestartButton()
        {
            ColorBlock colors = RestartButton.colors;
            Color highlighted = colors.highlightedColor;

            ColorBlock newColors = colors;
            newColors.normalColor = highlighted;
            RestartButton.colors = newColors;
        }
        private void SetHighlightedColor_QuitButton()
        {
            ColorBlock colors = QuitButton.colors;
            Color highlighted = colors.highlightedColor;

            ColorBlock newColors = colors;
            newColors.normalColor = highlighted;
            QuitButton.colors = newColors;
        }
    }
}
