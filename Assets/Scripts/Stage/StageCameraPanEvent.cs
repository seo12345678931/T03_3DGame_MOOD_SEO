using System.Collections;
using System.Collections.Generic;
using Mood.Input;
using Unity.Cinemachine;
using UnityEngine;

namespace Mood.Stage
{
    [AddComponentMenu("MOOD/Stage/Stage Camera Pan Event")]
    [DisallowMultipleComponent]
    public sealed class StageCameraPanEvent : MonoBehaviour
    {
        private struct HiddenObjectState
        {
            public GameObject Target;
            public bool WasActive;
        }

        private struct HiddenBehaviourState
        {
            public Behaviour Target;
            public bool WasEnabled;
        }

        [Header("Camera References")]
        [SerializeField] private CinemachineCamera playerCamera;
        [SerializeField] private CinemachineCamera stageCamera;
        [SerializeField] private CinemachineBrain targetBrain;
        [SerializeField] private MonoBehaviour inputLockSource;

        [Header("Visibility")]
        [Tooltip("Stage 카메라 연출 중 비활성화할 오브젝트(예: 플레이어 손, HUD 루트).")]
        [SerializeField] private GameObject[] hiddenObjectsDuringEvent;
        [Tooltip("Stage 카메라 연출 중 비활성화할 컴포넌트(예: 개별 UI 스크립트, Canvas, Renderer).")]
        [SerializeField] private Behaviour[] hiddenBehavioursDuringEvent;

        [Header("Path")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField, Min(0.01f)] private float totalPanDuration = 2.2f;
        [SerializeField, Min(0f)] private float finalHoldDuration = 0.5f;
        [SerializeField] private AnimationCurve panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool snapToFirstWaypointBeforeBlend = true;

        [Header("Priority Switching")]
        [SerializeField, Min(1)] private int stageCameraPriorityBoost = 10;
        [SerializeField] private bool waitForBlendBeforePan = true;

        [Header("Blend Override")]
        [SerializeField] private bool overrideBrainBlend = true;
        [SerializeField, Min(0f)] private float cameraBlendDuration = 0.35f;
        [SerializeField] private CinemachineBlendDefinition.Styles cameraBlendStyle =
            CinemachineBlendDefinition.Styles.EaseInOut;

        [Header("Gameplay Freeze")]
        [SerializeField] private bool freezeGameplayDuringEvent = true;
        [SerializeField, Range(0f, 1f)] private float frozenTimeScale = 0f;
        [SerializeField] private bool pauseAudioListener;

        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime = true;

        private Coroutine playCoroutine;
        private IPlayerInputLock inputLock;
        private bool isPlaying;
        private int originalPlayerPriority;
        private int originalStagePriority;
        private CinemachineBlendDefinition originalBlend;
        private bool cachedBlend;
        private float originalTimeScale = 1f;
        private bool originalAudioPause;
        private bool originalBrainIgnoreTimeScale;
        private bool cachedTimeState;
        private readonly List<HiddenObjectState> hiddenObjectStates = new List<HiddenObjectState>(4);
        private readonly List<HiddenBehaviourState> hiddenBehaviourStates = new List<HiddenBehaviourState>(4);

        public bool IsPlaying => isPlaying;

        private void Reset()
        {
            targetBrain = FindFirstObjectByType<CinemachineBrain>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            if (isPlaying)
            {
                RestoreRuntimeState();
            }
        }

        public bool Play()
        {
            if (isPlaying)
            {
                return false;
            }

            ResolveReferences();
            if (!ValidateSetup())
            {
                return false;
            }

            playCoroutine = StartCoroutine(PlayRoutine());
            return true;
        }

        public void PlayFromEvent()
        {
            Play();
        }

        private IEnumerator PlayRoutine()
        {
            isPlaying = true;

            if (inputLock != null)
            {
                inputLock.TryLockInput(this);
            }

            CacheCameraState();
            ApplyBlendOverride();
            ApplyGameplayFreeze();
            ApplyVisibilityOverride();

            if (snapToFirstWaypointBeforeBlend)
            {
                ApplyCameraPose(waypoints[0].position, waypoints[0].rotation);
            }

            SetStageCameraLive();

            if (waitForBlendBeforePan && cameraBlendDuration > 0f)
            {
                yield return WaitForSeconds(cameraBlendDuration);
            }

            yield return PanAlongWaypoints();

            if (finalHoldDuration > 0f)
            {
                yield return WaitForSeconds(finalHoldDuration);
            }

            SetPlayerCameraLive();

            if (cameraBlendDuration > 0f)
            {
                yield return WaitForSeconds(cameraBlendDuration);
            }

            RestoreRuntimeState();
        }

        private void ResolveReferences()
        {
            if (targetBrain == null)
            {
                targetBrain = FindFirstObjectByType<CinemachineBrain>();
            }

            if (playerCamera == null || playerCamera == stageCamera)
            {
                playerCamera = ResolvePlayerCameraCandidate();
            }

            if (inputLockSource == null)
            {
                inputLockSource = FindFirstObjectByType<PlayerInputLockController>();
            }

            inputLock = inputLockSource as IPlayerInputLock;
            if (inputLock == null && inputLockSource != null)
            {
                Debug.LogWarning($"[StageCameraPanEvent:{name}] Input lock source does not implement IPlayerInputLock.", this);
            }
        }

        private CinemachineCamera ResolvePlayerCameraCandidate()
        {
            CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            CinemachineCamera bestCandidate = null;
            int bestPriority = int.MinValue;

            for (int index = 0; index < cameras.Length; index++)
            {
                CinemachineCamera candidate = cameras[index];
                if (candidate == null || candidate == stageCamera)
                {
                    continue;
                }

                int candidatePriority = candidate.Priority.Value;
                bool isBetterCandidate = bestCandidate == null || candidatePriority > bestPriority;
                if (!isBetterCandidate)
                {
                    continue;
                }

                bestCandidate = candidate;
                bestPriority = candidatePriority;
            }

            return bestCandidate;
        }

        private bool ValidateSetup()
        {
            if (playerCamera == null)
            {
                Debug.LogWarning($"[StageCameraPanEvent:{name}] Player camera is not assigned.", this);
                return false;
            }

            if (stageCamera == null)
            {
                Debug.LogWarning($"[StageCameraPanEvent:{name}] Stage camera is not assigned.", this);
                return false;
            }

            if (playerCamera == stageCamera)
            {
                Debug.LogWarning($"[StageCameraPanEvent:{name}] Player camera and stage camera must be different.", this);
                return false;
            }

            if (waypoints == null || waypoints.Length == 0)
            {
                Debug.LogWarning($"[StageCameraPanEvent:{name}] At least one waypoint is required.", this);
                return false;
            }

            for (int index = 0; index < waypoints.Length; index++)
            {
                if (waypoints[index] != null)
                {
                    continue;
                }

                Debug.LogWarning($"[StageCameraPanEvent:{name}] Waypoint index {index} is null.", this);
                return false;
            }

            return true;
        }

        private void CacheCameraState()
        {
            originalPlayerPriority = playerCamera.Priority.Value;
            originalStagePriority = stageCamera.Priority.Value;
        }

        private void ApplyBlendOverride()
        {
            if (!overrideBrainBlend || targetBrain == null)
            {
                cachedBlend = false;
                return;
            }

            originalBlend = targetBrain.DefaultBlend;
            targetBrain.DefaultBlend = new CinemachineBlendDefinition(cameraBlendStyle, cameraBlendDuration);
            cachedBlend = true;
        }

        private void ApplyGameplayFreeze()
        {
            if (!freezeGameplayDuringEvent)
            {
                cachedTimeState = false;
                return;
            }

            originalTimeScale = Time.timeScale;
            originalAudioPause = AudioListener.pause;
            cachedTimeState = true;

            if (targetBrain != null)
            {
                originalBrainIgnoreTimeScale = targetBrain.IgnoreTimeScale;
                targetBrain.IgnoreTimeScale = true;
            }

            Time.timeScale = frozenTimeScale;

            if (pauseAudioListener)
            {
                AudioListener.pause = true;
            }
        }

        private void ApplyVisibilityOverride()
        {
            hiddenObjectStates.Clear();
            hiddenBehaviourStates.Clear();

            // 연출 카메라가 활성화되는 동안 플레이어 손/전투 UI가 겹쳐 보이지 않도록 상태를 캐시한 뒤 숨긴다.
            if (hiddenObjectsDuringEvent != null)
            {
                for (int index = 0; index < hiddenObjectsDuringEvent.Length; index++)
                {
                    GameObject target = hiddenObjectsDuringEvent[index];
                    if (target == null)
                    {
                        continue;
                    }

                    hiddenObjectStates.Add(new HiddenObjectState
                    {
                        Target = target,
                        WasActive = target.activeSelf
                    });

                    if (target.activeSelf)
                    {
                        target.SetActive(false);
                    }
                }
            }

            if (hiddenBehavioursDuringEvent != null)
            {
                for (int index = 0; index < hiddenBehavioursDuringEvent.Length; index++)
                {
                    Behaviour target = hiddenBehavioursDuringEvent[index];
                    if (target == null)
                    {
                        continue;
                    }

                    hiddenBehaviourStates.Add(new HiddenBehaviourState
                    {
                        Target = target,
                        WasEnabled = target.enabled
                    });

                    if (target.enabled)
                    {
                        target.enabled = false;
                    }
                }
            }
        }

        private void SetStageCameraLive()
        {
            int livePriority = Mathf.Max(originalPlayerPriority, originalStagePriority) + stageCameraPriorityBoost;
            stageCamera.Priority = livePriority;
            playerCamera.Priority = originalPlayerPriority;
        }

        private void SetPlayerCameraLive()
        {
            playerCamera.Priority = Mathf.Max(originalPlayerPriority, originalStagePriority) + stageCameraPriorityBoost;
            stageCamera.Priority = originalStagePriority;
        }

        private IEnumerator PanAlongWaypoints()
        {
            if (snapToFirstWaypointBeforeBlend && waypoints.Length == 1)
            {
                yield break;
            }

            float totalDistance = CalculateTotalDistance();
            int segmentCount = snapToFirstWaypointBeforeBlend ? waypoints.Length - 1 : waypoints.Length;

            if (segmentCount <= 0)
            {
                yield break;
            }

            for (int index = 0; index < segmentCount; index++)
            {
                Vector3 fromPosition;
                Quaternion fromRotation;
                Vector3 toPosition;
                Quaternion toRotation;

                if (snapToFirstWaypointBeforeBlend)
                {
                    fromPosition = waypoints[index].position;
                    fromRotation = waypoints[index].rotation;
                    toPosition = waypoints[index + 1].position;
                    toRotation = waypoints[index + 1].rotation;
                }
                else if (index == 0)
                {
                    fromPosition = stageCamera.transform.position;
                    fromRotation = stageCamera.transform.rotation;
                    toPosition = waypoints[0].position;
                    toRotation = waypoints[0].rotation;
                }
                else
                {
                    fromPosition = waypoints[index - 1].position;
                    fromRotation = waypoints[index - 1].rotation;
                    toPosition = waypoints[index].position;
                    toRotation = waypoints[index].rotation;
                }

                float segmentDuration = ResolveSegmentDuration(index, segmentCount, totalDistance);

                if (segmentDuration <= 0f)
                {
                    ApplyCameraPose(toPosition, toRotation);
                    continue;
                }

                float elapsed = 0f;
                while (elapsed < segmentDuration)
                {
                    elapsed += GetDeltaTime();
                    float normalizedTime = Mathf.Clamp01(elapsed / segmentDuration);
                    float easedTime = panCurve != null ? panCurve.Evaluate(normalizedTime) : normalizedTime;

                    Vector3 position = Vector3.LerpUnclamped(fromPosition, toPosition, easedTime);
                    Quaternion rotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, easedTime);
                    ApplyCameraPose(position, rotation);
                    yield return null;
                }

                ApplyCameraPose(toPosition, toRotation);
            }
        }

        private float CalculateTotalDistance()
        {
            float distance = 0f;
            if (!snapToFirstWaypointBeforeBlend && waypoints.Length > 0)
            {
                distance += Vector3.Distance(stageCamera.transform.position, waypoints[0].position);
            }

            for (int index = 0; index < waypoints.Length - 1; index++)
            {
                distance += Vector3.Distance(waypoints[index].position, waypoints[index + 1].position);
            }

            return distance;
        }

        private float ResolveSegmentDuration(int segmentIndex, int segmentCount, float totalDistance)
        {
            if (segmentCount <= 0)
            {
                return 0f;
            }

            if (totalDistance <= 0.0001f)
            {
                return totalPanDuration / segmentCount;
            }

            float segmentDistance;
            if (snapToFirstWaypointBeforeBlend)
            {
                segmentDistance = Vector3.Distance(waypoints[segmentIndex].position, waypoints[segmentIndex + 1].position);
            }
            else if (segmentIndex == 0)
            {
                segmentDistance = Vector3.Distance(stageCamera.transform.position, waypoints[0].position);
            }
            else
            {
                segmentDistance = Vector3.Distance(waypoints[segmentIndex - 1].position, waypoints[segmentIndex].position);
            }

            return totalPanDuration * (segmentDistance / totalDistance);
        }

        private void ApplyCameraPose(Vector3 position, Quaternion rotation)
        {
            stageCamera.transform.SetPositionAndRotation(position, rotation);
            stageCamera.ForceCameraPosition(position, rotation);
        }

        private IEnumerator WaitForSeconds(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                yield return null;
            }
        }

        private float GetDeltaTime()
        {
            bool shouldUseUnscaledTime = useUnscaledTime || (freezeGameplayDuringEvent && Mathf.Approximately(Time.timeScale, 0f));
            return shouldUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private void RestoreRuntimeState()
        {
            playerCamera.Priority = originalPlayerPriority;
            stageCamera.Priority = originalStagePriority;
            RestoreVisibilityOverride();

            if (cachedBlend && targetBrain != null)
            {
                targetBrain.DefaultBlend = originalBlend;
            }

            if (cachedTimeState)
            {
                Time.timeScale = originalTimeScale;
                AudioListener.pause = originalAudioPause;

                if (targetBrain != null)
                {
                    targetBrain.IgnoreTimeScale = originalBrainIgnoreTimeScale;
                }
            }

            if (inputLock != null)
            {
                inputLock.UnlockInput(this);
            }

            cachedBlend = false;
            cachedTimeState = false;
            isPlaying = false;
            playCoroutine = null;
        }

        private void RestoreVisibilityOverride()
        {
            for (int index = 0; index < hiddenObjectStates.Count; index++)
            {
                HiddenObjectState state = hiddenObjectStates[index];
                if (state.Target == null)
                {
                    continue;
                }

                state.Target.SetActive(state.WasActive);
            }

            for (int index = 0; index < hiddenBehaviourStates.Count; index++)
            {
                HiddenBehaviourState state = hiddenBehaviourStates[index];
                if (state.Target == null)
                {
                    continue;
                }

                state.Target.enabled = state.WasEnabled;
            }

            hiddenObjectStates.Clear();
            hiddenBehaviourStates.Clear();
        }
    }
}
