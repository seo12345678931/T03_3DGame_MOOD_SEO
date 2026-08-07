using UnityEngine;

namespace Mood.Stage
{
    [AddComponentMenu("MOOD/Stage/Stage Trigger")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private StageCameraPanEvent stageCameraPanEvent;

        [Header("Trigger")]
        [SerializeField] private LayerMask activatorLayers = ~0;
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private bool triggerOnce = true;

        private bool hasTriggered;

        private void Reset()
        {
            if (TryGetComponent(out Collider triggerCollider))
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered)
            {
                return;
            }

            if (!IsValidActivator(other))
            {
                return;
            }

            if (stageCameraPanEvent == null)
            {
                Debug.LogWarning($"[StageTrigger:{name}] StageCameraPanEvent reference is missing.", this);
                return;
            }

            if (!stageCameraPanEvent.Play())
            {
                return;
            }

            hasTriggered = true;
        }

        private bool IsValidActivator(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            int colliderLayerMask = 1 << other.gameObject.layer;
            if (activatorLayers.value != 0 && (activatorLayers.value & colliderLayerMask) == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(requiredTag))
            {
                return true;
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
