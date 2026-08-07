using System.Collections.Generic;
using Mood.AI;
using Mood.Combat;
using UnityEngine;

namespace Mood.Obstacles
{
    [AddComponentMenu("MOOD/Obstacles/Enemy Passable Obstacle Trigger")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyPassableObstacleTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Collider solidCollider;

        [Header("Filtering")]
        [SerializeField] private LayerMask enemyLayers = 1 << 17;
        [SerializeField] private string enemyTag = "Enemy";

        private readonly Dictionary<Collider, int> ignoreCounts = new Dictionary<Collider, int>();
        private Collider triggerCollider;

        private void Reset()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            if (solidCollider == null)
            {
                solidCollider = FindSolidCollider();
            }
        }

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }

            if (solidCollider == null)
            {
                solidCollider = FindSolidCollider();
            }
        }

        private void OnDisable()
        {
            RestoreIgnoredCollisions();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!ShouldPass(other))
            {
                return;
            }

            SetIgnored(other.transform.root, true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!ShouldPass(other))
            {
                return;
            }

            SetIgnored(other.transform.root, false);
        }

        private bool ShouldPass(Collider other)
        {
            if (other == null || solidCollider == null)
            {
                return false;
            }

            int otherLayerMask = 1 << other.gameObject.layer;
            if ((enemyLayers.value & otherLayerMask) != 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(enemyTag) && other.CompareTag(enemyTag))
            {
                return true;
            }

            if (other.GetComponentInParent<EnemyNavMeshController>() != null)
            {
                return true;
            }

            return other.GetComponentInParent<EnemyHealth>() != null;
        }

        private void SetIgnored(Transform root, bool ignore)
        {
            if (root == null || solidCollider == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider targetCollider = colliders[index];
                if (targetCollider == null || targetCollider == solidCollider || targetCollider == triggerCollider)
                {
                    continue;
                }

                if (ignore)
                {
                    AddIgnore(targetCollider);
                }
                else
                {
                    RemoveIgnore(targetCollider);
                }
            }
        }

        private void AddIgnore(Collider targetCollider)
        {
            if (ignoreCounts.TryGetValue(targetCollider, out int currentCount))
            {
                ignoreCounts[targetCollider] = currentCount + 1;
                return;
            }

            ignoreCounts[targetCollider] = 1;
            Physics.IgnoreCollision(solidCollider, targetCollider, true);
        }

        private void RemoveIgnore(Collider targetCollider)
        {
            if (!ignoreCounts.TryGetValue(targetCollider, out int currentCount))
            {
                return;
            }

            if (currentCount > 1)
            {
                ignoreCounts[targetCollider] = currentCount - 1;
                return;
            }

            ignoreCounts.Remove(targetCollider);
            if (targetCollider != null && solidCollider != null)
            {
                Physics.IgnoreCollision(solidCollider, targetCollider, false);
            }
        }

        private void RestoreIgnoredCollisions()
        {
            if (solidCollider == null)
            {
                ignoreCounts.Clear();
                return;
            }

            foreach (KeyValuePair<Collider, int> pair in ignoreCounts)
            {
                if (pair.Key != null)
                {
                    Physics.IgnoreCollision(solidCollider, pair.Key, false);
                }
            }

            ignoreCounts.Clear();
        }

        private Collider FindSolidCollider()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                Collider[] colliders = current.GetComponents<Collider>();
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider candidate = colliders[index];
                    if (candidate != null && !candidate.isTrigger)
                    {
                        return candidate;
                    }
                }

                current = current.parent;
            }

            return null;
        }
    }
}
