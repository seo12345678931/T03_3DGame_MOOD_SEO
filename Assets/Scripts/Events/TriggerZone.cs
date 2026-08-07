using System;
using Mood.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Mood.Events
{
    [AddComponentMenu("MOOD/Events/Trigger Zone")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TriggerZone : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private LayerMask activatorLayers = ~0;
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private WaveManager waveManager;

        [Header("UI")]
        [SerializeField] private string enterMessage;
        [SerializeField] private string unlockedMessage;
        [SerializeField] private ZoneAnnouncementUI announcementUI;
        [SerializeField, Min(0f)] private float unlockEventDelay;

        [Header("Events")]
        [SerializeField] private UnityEvent onTriggered;
        [SerializeField] private UnityEvent onUnlocked;

        private bool hasTriggered;
        private bool hasUnlocked;
        private bool isSubscribedToWaveManager;
        private Coroutine unlockEventRoutine;

        public bool HasTriggered => hasTriggered;
        public bool HasUnlocked => hasUnlocked;
        public WaveManager WaveManager => waveManager;

        private void Reset()
        {
            if (TryGetComponent(out Collider triggerCollider))
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromWaveManager();

            if (unlockEventRoutine != null)
            {
                StopCoroutine(unlockEventRoutine);
                unlockEventRoutine = null;
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

            Activate();
        }

        public void Activate()
        {
            if (triggerOnce && hasTriggered)
            {
                return;
            }

            hasTriggered = true;
            ShowEnterMessage();
            onTriggered?.Invoke();

            if (waveManager == null)
            {
                Unlock();
                return;
            }

            SubscribeToWaveManager();

            if (waveManager.IsSequenceCompleted(this))
            {
                Unlock();
                return;
            }

            if (waveManager.IsSequenceRunning(this))
            {
                return;
            }

            if (!waveManager.TriggerWaveSequence(this))
            {
                Debug.LogWarning($"[TriggerZone:{name}] Failed to start a wave sequence from {waveManager.name}.", this);
                Unlock();
            }
        }

        private void SubscribeToWaveManager()
        {
            if (waveManager == null || isSubscribedToWaveManager)
            {
                return;
            }

            waveManager.TriggeredSequenceCompleted += HandleWaveSequenceCompleted;
            isSubscribedToWaveManager = true;
        }

        private void UnsubscribeFromWaveManager()
        {
            if (waveManager == null || !isSubscribedToWaveManager)
            {
                return;
            }

            waveManager.TriggeredSequenceCompleted -= HandleWaveSequenceCompleted;
            isSubscribedToWaveManager = false;
        }

        private void HandleWaveSequenceCompleted(WaveManager completedWaveManager, TriggerZone completedTriggerZone)
        {
            if (completedWaveManager != waveManager || completedTriggerZone != this)
            {
                return;
            }

            Unlock();
        }

        private void Unlock()
        {
            if (hasUnlocked)
            {
                return;
            }

            hasUnlocked = true;
            UnsubscribeFromWaveManager();
            ShowAnnouncement(unlockedMessage);

            if (unlockEventRoutine != null)
            {
                StopCoroutine(unlockEventRoutine);
            }

            unlockEventRoutine = StartCoroutine(InvokeUnlockEvents());
        }

        private System.Collections.IEnumerator InvokeUnlockEvents()
        {
            if (unlockEventDelay > 0f)
            {
                yield return new WaitForSeconds(unlockEventDelay);
            }

            unlockEventRoutine = null;
            onUnlocked?.Invoke();
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

        private void ShowEnterMessage()
        {
            ShowAnnouncement(enterMessage);
        }

        private void ShowAnnouncement(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ZoneAnnouncementUI resolvedAnnouncementUI = ResolveAnnouncementUI();

            if (resolvedAnnouncementUI != null)
            {
                EnsureAnnouncementUIIsReady(resolvedAnnouncementUI);
                resolvedAnnouncementUI.ShowMessage(message);
                return;
            }

            Debug.LogWarning($"[TriggerZone:{name}] UI message is set, but no ZoneAnnouncementUI was found in the scene.", this);
        }

        private ZoneAnnouncementUI ResolveAnnouncementUI()
        {
            if (announcementUI != null)
            {
                return announcementUI;
            }

            ZoneAnnouncementUI resolvedAnnouncementUI = FindFirstObjectByType<ZoneAnnouncementUI>();
            if (resolvedAnnouncementUI == null)
            {
                ZoneAnnouncementUI[] announcementUIs = Resources.FindObjectsOfTypeAll<ZoneAnnouncementUI>();
                for (int index = 0; index < announcementUIs.Length; index++)
                {
                    ZoneAnnouncementUI candidate = announcementUIs[index];
                    if (candidate == null || !candidate.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    resolvedAnnouncementUI = candidate;
                    if (candidate.isActiveAndEnabled)
                    {
                        break;
                    }
                }
            }

            announcementUI = resolvedAnnouncementUI;
            return announcementUI;
        }

        private static void EnsureAnnouncementUIIsReady(ZoneAnnouncementUI resolvedAnnouncementUI)
        {
            if (resolvedAnnouncementUI == null)
            {
                return;
            }

            Transform current = resolvedAnnouncementUI.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }

                current = current.parent;
            }

            if (!resolvedAnnouncementUI.enabled)
            {
                resolvedAnnouncementUI.enabled = true;
            }
        }
    }
}
