using System;
using System.Collections.Generic;
using Mood.Combat;
using Mood.Input;
using Mood.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Scoreboard UI")]
    [DisallowMultipleComponent]
    public sealed class ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private InputManager inputManager;
        [SerializeField] private GameObject scoreboardRoot;
        [SerializeField] private GameObject scoreboardRoot_PlayerUI;
        [SerializeField] private PlayerHealth playerHealth;
        
        [Header("TextMeshProGUI")]
        [SerializeField] private TextMeshProUGUI TimerTxt;
        [SerializeField] private TextMeshProUGUI TimerTxt_PlayerUI;
        [SerializeField] private TextMeshProUGUI KillCountTxt;
        [SerializeField] private TextMeshProUGUI KillCountTxt_PlayerUI;

        [Header("Count")] 
        public int killcount = 0;
        
        [Header("MinimapZoom")]
        [SerializeField] private Camera miniMapCamera;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 10f;
        [SerializeField] private float maxZoom = 25f;
        [SerializeField] private Scrollbar scrollbar;
        
        private readonly HashSet<EnemyHealth> trackedEnemies = new HashSet<EnemyHealth>();

        private void Start()
        {
            UpdateScrollbarValue();
        }

        private void Awake()
        {
            if (inputManager == null)
            {
                inputManager = FindFirstObjectByType<InputManager>();
            }

            if (scoreboardRoot != null)
            {
                scoreboardRoot.SetActive(false);
            }

            if (scoreboardRoot_PlayerUI != null)
            {
                scoreboardRoot_PlayerUI.SetActive(true);
            }
            
            if (scrollbar != null)
            {
                scrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);
            }
        }

        private void OnEnable()
        {
            RegisterExistingEnemies();
            RefreshTexts();
        }

        private void OnDisable()
        {
            foreach (EnemyHealth enemy in trackedEnemies)
            {
                if (enemy != null)
                {
                    enemy.Died -= HandleEnemyDied;
                }
            }

            trackedEnemies.Clear();
        }

        
        private void Update()
        {
            RegisterExistingEnemies();
            
            if (TimerTxt != null)
            {
                TimerTxt.text = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString("mm\\:ss");
            }
            
            if (TimerTxt_PlayerUI != null)
            {
                TimerTxt_PlayerUI.text = TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString("mm\\:ss");
            }
            
            HandleMinimapZoom();
            
            // playerHealth.CurrentHealth <= 0
            // : 플레이어 체력이 0이 되어 게임오버가 되면 ScoreboardUI 작동안되게 처리
            if (inputManager == null || scoreboardRoot == null || playerHealth.CurrentHealth <= 0)
            {
                return;
            }

            bool isScoreboardVisible = inputManager.ScoreboardHeld;

            scoreboardRoot.SetActive(isScoreboardVisible);

            if (scoreboardRoot_PlayerUI != null)
            {
                scoreboardRoot_PlayerUI.SetActive(!isScoreboardVisible);
            }
        }
        
        private void RegisterExistingEnemies()
        {
            EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || trackedEnemies.Contains(enemy))
                {
                    continue;
                }

                enemy.Died += HandleEnemyDied;
                trackedEnemies.Add(enemy);
            }
        }
        
        private void HandleEnemyDied(EnemyHealth enemyHealth, GameObject instigator)
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died -= HandleEnemyDied;
                trackedEnemies.Remove(enemyHealth);
            }

            if (playerHealth == null || instigator == null)
            {
                return;
            }

            PlayerHealth instigatorPlayer = instigator.GetComponentInParent<PlayerHealth>();
            if (instigatorPlayer != playerHealth)
            {
                return;
            }

            killcount++;

            RefreshTexts();
        }
        
        private void RefreshTexts()
        {
            if (KillCountTxt != null)
            {
                KillCountTxt.text = $"처치 수 : {killcount}마리";
            }
            
            if (KillCountTxt_PlayerUI != null)
            {
                KillCountTxt_PlayerUI.text = $"{killcount} <color=#981818>처치</color>";
            }
        }
        
        private void HandleMinimapZoom()
        {
            if (miniMapCamera == null || !miniMapCamera.orthographic || Mouse.current == null)
            {
                return;
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            float nextZoom = miniMapCamera.orthographicSize - (scroll * zoomSpeed);
            miniMapCamera.orthographicSize = Mathf.Clamp(nextZoom, minZoom, maxZoom);
            
            UpdateScrollbarValue();
        }
        
        private void UpdateScrollbarValue()
        {
            if (scrollbar == null || miniMapCamera == null)
            {
                return;
            }

            scrollbar.value = Mathf.InverseLerp(maxZoom, minZoom, miniMapCamera.orthographicSize);
        }
        private void OnScrollbarValueChanged(float value)
        {
            if (miniMapCamera == null)
            {
                return;
            }

            miniMapCamera.orthographicSize = Mathf.Lerp(maxZoom, minZoom, value);
        }
    }
}
