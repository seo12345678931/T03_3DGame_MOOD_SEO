using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-115)] // Before input system
public class DynamicNavMeshUpdater : MonoBehaviour {
    [Header("Agent Tracking")] [SerializeField, Tooltip("The agent whose position will be used to update the NavMesh")]
    NavMeshAgent trackedAgent;

    [SerializeField, Range(0.01f, 1f), Tooltip("Quantization factor for position updates (lower = more frequent updates)")]
    float quantizationFactor = 0.1f;

    [Header("Update Policy")]
    [SerializeField, Min(0.05f), Tooltip("Minimum delay between dynamic NavMesh rebuild requests")]
    float minUpdateInterval = 0.25f;

    [SerializeField, Tooltip("Track the agent's vertical movement as well. Usually this should stay disabled.")]
    bool followVerticalPosition;

    [SerializeField, Tooltip("Additional local-space offset for the tracked NavMesh volume")]
    Vector3 localCenterOffset;

    NavMeshSurface surface;
    Vector3 volumeSize;
    Vector3 pendingCenter;
    float baseCenterY;
    float nextUpdateTime;
    bool hasPendingUpdate;
    AsyncOperation currentUpdateOperation;

    void Awake() {
        surface = GetComponent<NavMeshSurface>();
    }

    void OnEnable() {
        if (!TryInitialize()) {
            enabled = false;
            return;
        }

        volumeSize = surface.size;
        baseCenterY = surface.center.y;
        pendingCenter = GetQuantizedCenter();
        surface.center = pendingCenter;
        surface.BuildNavMesh();
    }

    void Update() {
        if (surface == null || trackedAgent == null) return;

        if (currentUpdateOperation != null && currentUpdateOperation.isDone) {
            currentUpdateOperation = null;
        }

        var updatedCenter = GetQuantizedCenter();

        if (pendingCenter != updatedCenter) {
            pendingCenter = updatedCenter;
            hasPendingUpdate = true;
        }

        if (surface.size != volumeSize) {
            volumeSize = surface.size;
            hasPendingUpdate = true;
        }

        if (!hasPendingUpdate) return;
        if (currentUpdateOperation != null) return;
        if (Time.unscaledTime < nextUpdateTime) return;

        surface.center = pendingCenter;
        nextUpdateTime = Time.unscaledTime + minUpdateInterval;
        hasPendingUpdate = false;

        if (surface.navMeshData == null) {
            surface.BuildNavMesh();
            return;
        }

        currentUpdateOperation = surface.UpdateNavMesh(surface.navMeshData);
    }

    Vector3 GetQuantizedCenter() {
        Vector3 step = GetQuantizationStep();
        Vector3 targetWorldPosition = trackedAgent.transform.position;
        Vector3 quantizedWorldPosition = targetWorldPosition.Quantize(step);
        Vector3 localCenter = transform.InverseTransformPoint(quantizedWorldPosition);

        if (!followVerticalPosition) {
            localCenter.y = baseCenterY;
        }

        return localCenter + localCenterOffset;
    }

    Vector3 GetQuantizationStep() {
        return new Vector3(
            Mathf.Max(0.01f, surface.size.x * quantizationFactor),
            Mathf.Max(0.01f, surface.size.y * quantizationFactor),
            Mathf.Max(0.01f, surface.size.z * quantizationFactor)
        );
    }

    bool TryInitialize() {
        surface ??= GetComponent<NavMeshSurface>();

        if (surface == null) {
            Debug.LogWarning($"[{nameof(DynamicNavMeshUpdater)}] Missing {nameof(NavMeshSurface)} on {name}.", this);
            return false;
        }

        if (trackedAgent == null) {
            Debug.LogWarning($"[{nameof(DynamicNavMeshUpdater)}] Missing tracked agent on {name}.", this);
            return false;
        }

        return true;
    }

    void OnDrawGizmosSelected() {
        if (surface == null) surface = GetComponent<NavMeshSurface>();
        if (surface == null) return;

        Vector3 worldCenter = transform.TransformPoint(surface.center);
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, transform.lossyScale);
        Gizmos.DrawCube(Vector3.zero, surface.size);
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, surface.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}

public static class Vector3Extensions {
    public static Vector3 Quantize(this Vector3 position, Vector3 quantization) {
        return Vector3.Scale(
            quantization,
            new Vector3(
                Mathf.Floor(position.x / quantization.x),
                Mathf.Floor(position.y / quantization.y),
                Mathf.Floor(position.z / quantization.z)
            ));
    }
}
