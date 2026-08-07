using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 이 로딩스크립트는 로비씬에서만 작동할 계획이라 네임스페이스를 Lobby로 지정.
namespace Mood.Lobby
{
    [AddComponentMenu("MOOD/Lobby/Loading Script")]
    [DisallowMultipleComponent]
    public sealed class LoadingScript : MonoBehaviour
    {
        private static LoadingScript instance;
        
        [SerializeField] private CanvasGroup loadingCanvasGroup;
        [SerializeField] private Image progresBar;
        [SerializeField] private float fadeDuration = 0.4f;

        private bool isLoading;

        public static LoadingScript Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<LoadingScript>();
                }
                return instance;
            }
        }
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadingCanvasGroup != null)
            {
                loadingCanvasGroup.alpha = 0f;
                loadingCanvasGroup.blocksRaycasts = false;
                loadingCanvasGroup.interactable = false;
            }

            if (progresBar != null)
            {
                progresBar.fillAmount = 0f;
            }
        }

        public void LoadScene(string sceneName)
        {
            gameObject.SetActive(true);
            if (isLoading)
                return;

            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isLoading = true;

            if (loadingCanvasGroup != null)
            {
                loadingCanvasGroup.blocksRaycasts = true;
                loadingCanvasGroup.interactable = true;
                yield return StartCoroutine(FadeIn());
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                isLoading = false;
                yield break;
            }

            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                if (progresBar != null)
                {
                    progresBar.fillAmount = Mathf.Clamp01(operation.progress / 0.9f);
                }

                yield return null;
            }

            if (progresBar != null)
            {
                progresBar.fillAmount = 1f;
            }

            operation.allowSceneActivation = true;
        }

        private IEnumerator FadeIn()
        {
            if (loadingCanvasGroup == null)
                yield break;

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                loadingCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                yield return null;
            }

            loadingCanvasGroup.alpha = 1f;
        }
    }
}
