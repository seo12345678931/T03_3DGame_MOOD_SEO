using Mood.AI;
using Mood.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Boss Info UI")]
    [DisallowMultipleComponent]
    public sealed class BossInfoUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossNavMeshController bossController;
        [SerializeField] private EnemyHealth bossHealth;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private RectTransform visibilityRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Display")]
        [SerializeField] private bool autoFindBoss = true;
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private bool hideWhenBossMissing = true;
        [SerializeField] private bool hideWhenBossDead = true;
        [SerializeField] private string fallbackBossName = "BOSS";

        private void Reset()
        {
            AssignReferences();
            Refresh();
        }

        private void Awake()
        {
            AssignReferences();
            Refresh();
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
        }

        public void SetBoss(BossNavMeshController newBossController)
        {
            bossController = newBossController;
            bossHealth = bossController != null ? bossController.Health : null;
            Refresh();
        }

        public void Refresh()
        {
            AssignReferences();
            ResolveBossReferences();

            if (bossController == null || bossHealth == null)
            {
                ApplyMissingState();
                return;
            }

            if (hideWhenBossDead && (bossController.IsDead || bossHealth.IsDead))
            {
                ApplyHiddenState();
                return;
            }

            if (bossNameText != null)
            {
                bossNameText.text = ResolveBossName();
            }

            if (healthFillImage != null)
            {
                float maxHealth = Mathf.Max(0.0001f, bossHealth.MaxHealth);
                healthFillImage.fillAmount = Mathf.Clamp01(bossHealth.CurrentHealth / maxHealth);
            }

            SetVisible(true);
        }

        private void AssignReferences()
        {
            if (bossNameText == null)
            {
                bossNameText = GetComponentInChildren<TMP_Text>(true);
            }

            if (healthFillImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
                {
                    Image image = images[imageIndex];
                    if (image != null && image.type == Image.Type.Filled)
                    {
                        healthFillImage = image;
                        break;
                    }
                }
            }

            if (visibilityRoot == null || visibilityRoot == transform)
            {
                visibilityRoot = ResolveVisibilityRoot();
            }

            if (visibilityRoot == null)
            {
                visibilityRoot = transform as RectTransform;
            }

            if (canvasGroup == null || IsCanvasRootGroup(canvasGroup))
            {
                canvasGroup = visibilityRoot != null ? visibilityRoot.GetComponent<CanvasGroup>() : null;
                if (canvasGroup == null && visibilityRoot != null)
                {
                    canvasGroup = visibilityRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void ResolveBossReferences()
        {
            if (bossController == null && autoFindBoss)
            {
                bossController = FindAnyObjectByType<BossNavMeshController>();
            }

            if (bossHealth == null && bossController != null)
            {
                bossHealth = bossController.Health;
                if (bossHealth == null)
                {
                    bossHealth = bossController.GetComponent<EnemyHealth>();
                }
            }
        }

        private string ResolveBossName()
        {
            if (bossController != null && !string.IsNullOrWhiteSpace(bossController.BossDisplayName))
            {
                return bossController.BossDisplayName;
            }

            if (!string.IsNullOrWhiteSpace(fallbackBossName))
            {
                return fallbackBossName;
            }

            return bossController != null ? bossController.gameObject.name : "BOSS";
        }

        private void ApplyMissingState()
        {
            if (bossNameText != null)
            {
                bossNameText.text = fallbackBossName;
            }

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 0f;
            }

            SetVisible(!hideWhenBossMissing);
        }

        private void ApplyHiddenState()
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 0f;
            }

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }

            if (bossNameText != null)
            {
                bossNameText.enabled = visible;
            }

            if (healthFillImage != null)
            {
                healthFillImage.enabled = visible;
            }
        }

        private RectTransform ResolveVisibilityRoot()
        {
            if (bossNameText != null && healthFillImage != null)
            {
                Transform commonAncestor = FindCommonAncestor(bossNameText.transform, healthFillImage.transform);
                if (commonAncestor is RectTransform commonRectTransform && commonAncestor != transform)
                {
                    return commonRectTransform;
                }
            }

            if (bossNameText != null && bossNameText.transform.parent is RectTransform nameParent)
            {
                return nameParent.parent as RectTransform ?? nameParent;
            }

            return FindNamedChild("BOSS Info UI Group") ?? FindNamedChild("BOSS Info");
        }

        private RectTransform FindNamedChild(string targetName)
        {
            RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
            for (int rectTransformIndex = 0; rectTransformIndex < rectTransforms.Length; rectTransformIndex++)
            {
                RectTransform candidate = rectTransforms[rectTransformIndex];
                if (candidate != null && candidate != transform && candidate.name == targetName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Transform FindCommonAncestor(Transform first, Transform second)
        {
            if (first == null || second == null)
            {
                return null;
            }

            for (Transform firstCursor = first; firstCursor != null; firstCursor = firstCursor.parent)
            {
                for (Transform secondCursor = second; secondCursor != null; secondCursor = secondCursor.parent)
                {
                    if (firstCursor == secondCursor)
                    {
                        return firstCursor;
                    }
                }
            }

            return null;
        }

        private static bool IsCanvasRootGroup(CanvasGroup targetCanvasGroup)
        {
            return targetCanvasGroup != null && targetCanvasGroup.GetComponent<Canvas>() != null;
        }
    }
}
