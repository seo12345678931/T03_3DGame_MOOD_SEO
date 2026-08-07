using System.Collections.Generic;
using Mood.Player;
using UnityEngine;

namespace Mood.Effects
{
    [AddComponentMenu("MOOD/Effects/Visible Ground Particle Projector")]
    [DisallowMultipleComponent]
    public sealed class VisibleGroundParticleProjector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform[] particleAnchors;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private LayerMask roofMask;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Min(1f)] private float rayDistance = 120f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;
        [SerializeField, Min(0f)] private float roofCheckStartOffset = 0.05f;
        [SerializeField, Min(0f)] private float roofCheckDistance = 50f;
        [SerializeField] private bool alignToGroundNormal;

        [Header("Viewport Sampling")]
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField, Min(1)] private int rows = 3;
        [SerializeField, Range(0f, 1f)] private float viewportMinX = 0.15f;
        [SerializeField, Range(0f, 1f)] private float viewportMaxX = 0.85f;
        [SerializeField, Range(0f, 1f)] private float viewportMinY = 0.05f;
        [SerializeField, Range(0f, 1f)] private float viewportMaxY = 0.45f;

        [Header("Update")]
        [SerializeField, Min(0f)] private float refreshInterval = 0.05f;
        [SerializeField] private bool hideAnchorWithoutGroundHit = true;
        [SerializeField] private bool keepLastValidPositionOnMiss = true;

        private readonly List<Transform> cachedAnchors = new List<Transform>(16);
        private readonly Dictionary<Transform, bool> anchorVisibilityStates = new Dictionary<Transform, bool>(16);
        private readonly Dictionary<Transform, Vector3> lastValidPositions = new Dictionary<Transform, Vector3>(16);
        private float nextRefreshTime;

        private void Reset()
        {
            AssignReferences();
            CacheAnchorsIfNeeded();
        }

        private void Awake()
        {
            AssignReferences();
            CacheAnchorsIfNeeded();
        }

        private void LateUpdate()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + refreshInterval;

            if (targetCamera == null)
            {
                AssignReferences();
                if (targetCamera == null)
                {
                    return;
                }
            }

            CacheAnchorsIfNeeded();
            if (cachedAnchors.Count == 0)
            {
                return;
            }

            int sampleCount = Mathf.Min(cachedAnchors.Count, Mathf.Max(1, columns * rows));
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Transform anchor = cachedAnchors[sampleIndex];
                if (anchor == null)
                {
                    continue;
                }

                Vector2 viewportPoint = GetViewportPoint(sampleIndex);
                Ray ray = targetCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));

                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundLayers, triggerInteraction) &&
                    !IsBlockedByRoof(hit.point))
                {
                    Vector3 position = hit.point + (hit.normal * surfaceOffset);
                    anchor.position = position;
                    lastValidPositions[anchor] = position;

                    if (alignToGroundNormal)
                    {
                        Vector3 projectedForward = Vector3.ProjectOnPlane(targetCamera.transform.forward, hit.normal);
                        if (projectedForward.sqrMagnitude <= Mathf.Epsilon)
                        {
                            projectedForward = Vector3.ProjectOnPlane(targetCamera.transform.up, hit.normal);
                        }

                        if (projectedForward.sqrMagnitude > Mathf.Epsilon)
                        {
                            anchor.rotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);
                        }
                    }

                    SetAnchorVisible(anchor, true);

                    continue;
                }

                if (hideAnchorWithoutGroundHit)
                {
                    if (keepLastValidPositionOnMiss && lastValidPositions.TryGetValue(anchor, out Vector3 lastValidPosition))
                    {
                        anchor.position = lastValidPosition;
                    }

                    SetAnchorVisible(anchor, false);
                }
            }

            for (int index = sampleCount; index < cachedAnchors.Count; index++)
            {
                Transform anchor = cachedAnchors[index];
                if (anchor == null || !hideAnchorWithoutGroundHit)
                {
                    continue;
                }

                SetAnchorVisible(anchor, false);
            }
        }

        private void AssignReferences()
        {
            if (targetCamera == null)
            {
                HyperFpsFirstPersonController playerController = FindFirstObjectByType<HyperFpsFirstPersonController>();
                if (playerController != null)
                {
                    targetCamera = playerController.GetComponentInChildren<Camera>();
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void CacheAnchorsIfNeeded()
        {
            cachedAnchors.Clear();

            if (particleAnchors != null && particleAnchors.Length > 0)
            {
                for (int index = 0; index < particleAnchors.Length; index++)
                {
                    Transform anchor = particleAnchors[index];
                    if (anchor != null)
                    {
                        cachedAnchors.Add(anchor);
                    }
                }
            }

            if (cachedAnchors.Count > 0)
            {
                return;
            }

            ParticleSystem[] childParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            HashSet<Transform> uniqueAnchors = new HashSet<Transform>();
            for (int index = 0; index < childParticleSystems.Length; index++)
            {
                ParticleSystem particleSystem = childParticleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                Transform anchor = particleSystem.transform;
                if (anchor == transform || !uniqueAnchors.Add(anchor))
                {
                    continue;
                }

                cachedAnchors.Add(anchor);
            }

            if (cachedAnchors.Count == 0)
            {
                cachedAnchors.Add(transform);
            }

            for (int index = cachedAnchors.Count - 1; index >= 0; index--)
            {
                Transform anchor = cachedAnchors[index];
                if (anchor == null)
                {
                    cachedAnchors.RemoveAt(index);
                    continue;
                }

                if (!lastValidPositions.ContainsKey(anchor))
                {
                    lastValidPositions.Add(anchor, anchor.position);
                }
            }
        }

        private bool IsBlockedByRoof(Vector3 groundPoint)
        {
            if (roofMask.value == 0 || roofCheckDistance <= 0f)
            {
                return false;
            }

            Vector3 origin = groundPoint + Vector3.up * roofCheckStartOffset;
            return Physics.Raycast(origin, Vector3.up, roofCheckDistance, roofMask, triggerInteraction);
        }

        private void SetAnchorVisible(Transform anchor, bool visible)
        {
            if (anchor == null)
            {
                return;
            }

            if (anchorVisibilityStates.TryGetValue(anchor, out bool currentVisible) && currentVisible == visible)
            {
                return;
            }

            anchorVisibilityStates[anchor] = visible;

            ParticleSystem[] particleSystems = anchor.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                if (visible)
                {
                    particleSystem.gameObject.SetActive(true);
                    if (!particleSystem.isPlaying)
                    {
                        particleSystem.Play(true);
                    }

                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private Vector2 GetViewportPoint(int sampleIndex)
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            int columnIndex = sampleIndex % safeColumns;
            int rowIndex = sampleIndex / safeColumns;

            if (rowIndex >= safeRows)
            {
                rowIndex = safeRows - 1;
            }

            float xT = safeColumns == 1 ? 0.5f : columnIndex / (float)(safeColumns - 1);
            float yT = safeRows == 1 ? 0.5f : rowIndex / (float)(safeRows - 1);

            float viewportX = Mathf.Lerp(viewportMinX, viewportMaxX, xT);
            float viewportY = Mathf.Lerp(viewportMinY, viewportMaxY, yT);
            return new Vector2(viewportX, viewportY);
        }
    }
}
