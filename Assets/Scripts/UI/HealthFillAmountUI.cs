using Mood.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Health Fill Amount UI")]
    [DisallowMultipleComponent]
    public sealed class HealthFillAmountUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Image DangerEffect;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private bool hideWhenEmpty;
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private string emptyText = "0 / 0";

        private void Reset()
        {
            AssignReferences();
            Refresh();
        }

        private void Awake()
        {
            AssignReferences();
            Refresh();
            SetDangerEffectActive(false);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (updateEveryFrame)
            {
                Refresh();
            }

            // 플레이어 체력이 30%이하면 
            if (playerHealth != null && playerHealth.CurrentHealth <= 30)
            {
                SetDangerEffectActive(true);
            }
            else
            {
                SetDangerEffectActive(false);
            }
        }

        public void Refresh()
        {
            if (fillImage == null && hpText == null)
            {
                return;
            }

            bool hasHealth = TryGetHealthInfo(out float healthRatio, out string healthText);
            if (!hasHealth)
            {
                if (fillImage != null)
                {
                    fillImage.fillAmount = 0f;
                    if (hideWhenEmpty)
                    {
                        fillImage.enabled = false;
                    }
                }

                if (hpText != null)
                {
                    hpText.text = emptyText;
                }

                return;
            }

            float clampedRatio = Mathf.Clamp01(healthRatio);
            if (fillImage != null)
            {
                fillImage.fillAmount = clampedRatio;
                if (hideWhenEmpty)
                {
                    fillImage.enabled = clampedRatio > 0f;
                }
            }

            if (hpText != null)
            {
                hpText.text = healthText;
            }
        }

        private void AssignReferences()
        {
            if (fillImage == null)
            {
                fillImage = GetComponent<Image>();
            }

            if (hpText == null)
            {
                hpText = GetComponentInChildren<TMP_Text>(true);
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }
        }

        private void SetDangerEffectActive(bool active)
        {
            if (DangerEffect != null)
            {
                DangerEffect.gameObject.SetActive(active);
            }
        }

        private bool TryGetHealthInfo(out float healthRatio, out string healthText)
        {
            if (playerHealth != null)
            {
                healthRatio = playerHealth.NormalizedHealth;
                healthText = $"{Mathf.CeilToInt(playerHealth.CurrentHealth)}";
                return true;
            }

            healthRatio = 0f;
            healthText = emptyText;
            return false;
        }
    }
}
