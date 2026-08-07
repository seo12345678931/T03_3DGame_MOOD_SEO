using UnityEngine;

namespace Mood.Audio
{
    [AddComponentMenu("MOOD/Audio/BGM Trigger Zone")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BgmTriggerZone : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private bool triggerOnce = true;

        [Header("BGM")]
        [Tooltip("BgmManager의 Track Key와 동일하게 맞춰야 한다.")]
        [SerializeField] private string enterTrackKey;
        [Tooltip("트리거에서 벗어날 때 되돌릴 Track Key. 비워두면 유지한다.")]
        [SerializeField] private string exitTrackKey;
        [SerializeField] private bool immediateTransition;

        private bool hasTriggered;

        private void Reset()
        {
            Collider targetCollider = GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanHandleTrigger(other))
            {
                return;
            }

            if (triggerOnce && hasTriggered)
            {
                return;
            }

            hasTriggered = true;
            BgmManager.Instance?.PlayByKey(enterTrackKey, immediateTransition);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CanHandleTrigger(other))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(exitTrackKey))
            {
                return;
            }

            BgmManager.Instance?.PlayByKey(exitTrackKey, immediateTransition);
        }

        private bool CanHandleTrigger(Collider other)
        {
            if (other == null || string.IsNullOrWhiteSpace(requiredTag))
            {
                return false;
            }

            if (other.CompareTag(requiredTag))
            {
                return true;
            }

            Transform root = other.transform.root;
            return root != null && root.CompareTag(requiredTag);
        }
    }
}
