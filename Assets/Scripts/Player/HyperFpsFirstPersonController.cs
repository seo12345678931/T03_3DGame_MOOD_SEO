using System.Collections.Generic;
using Akila.FPSFramework;
using Mood.Audio;
using Mood.Input;
using Mood.Speed;
using UnityEngine;

namespace Mood.Player
{
    // CharacterController 기반 이동, 점프, 대시, 시점 회전을 처리한다.
    [AddComponentMenu("MOOD/Player/Hyper FPS First Person Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(InputManager))]
    public sealed class HyperFpsFirstPersonController : MonoBehaviour, ISpeedBoostReceiver
    {
        private sealed class ActiveSpeedBoost
        {
            public float MoveSpeedBonus;
            public float EndTime;
        }

        [Header("References")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private bool useCinemachineCamera = true;
        [SerializeField] private Transform facingReference;
        [SerializeField] private InputManager inputManager;

        [Header("Move")]
        [SerializeField, Min(0f)] private float moveSpeed = 9f;
        [SerializeField, Min(0f)] private float groundAcceleration = 80f;
        [SerializeField, Min(0f)] private float groundDeceleration = 90f;
        [SerializeField, Min(0f)] private float airAcceleration = 35f;
        [SerializeField, Min(0f)] private float airDeceleration = 15f;

        [Header("Jump")]
        [SerializeField, Min(0.01f)] private float jumpHeight = 1.6f;
        [SerializeField, Min(0f)] private float gravityMultiplier = 2.5f;
        [SerializeField, Min(0f)] private float groundedGravity = 5f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 40f;

        [Header("Dash")]
        [SerializeField, Min(0f)] private float dashSpeed = 22f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.14f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.35f;
        [SerializeField] private bool allowAirDash = true;
        [SerializeField, Min(0)] private int maxAirDashCount = 1;
        [SerializeField] private bool resetVerticalVelocityOnDash = true;
        [SerializeField, Range(0f, 1f)] private float dashExitSpeedMultiplier = 0.35f;

        [Header("Look")]
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 220f;
        [SerializeField, Range(-89f, 0f)] private float minPitch = -89f;
        [SerializeField, Range(0f, 89f)] private float maxPitch = 89f;
        
        [Header("Compass")]
        [SerializeField] private Compass compass; 

        [Header("Audio")]
        [SerializeField] private SfxPlayer movementSfxPlayer;
        [SerializeField] private AudioProfile[] footstepAudioProfiles;
        [SerializeField] private AudioProfile jumpAudioProfile;
        [SerializeField] private AudioProfile dashAudioProfile;
        [SerializeField] private AudioProfile landingAudioProfile;
        [SerializeField, Min(0.1f)] private float footstepDistance = 2.1f;
        [SerializeField, Min(0f)] private float minimumFootstepSpeed = 1.5f;
        [SerializeField, Min(0f)] private float minimumLandingSpeed = 6f;

        private CharacterController characterController;
        private readonly List<ActiveSpeedBoost> activeSpeedBoosts = new List<ActiveSpeedBoost>(4);
        private Vector2 moveInput;
        private Vector3 planarVelocity;
        private Vector3 dashVelocity;
        private float verticalVelocity;
        private float yaw;
        private float pitch;
        private float dashEndTime;
        private float nextDashTime;
        private int remainingAirDashCount;
        private bool isDashing;
        private float footstepDistanceProgress;

        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsDashing => isDashing;
        public Component Component => this;

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            inputManager = GetComponent<InputManager>();
            AssignCameraReferences();
            AssignAudioReferences();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputManager = inputManager != null ? inputManager : GetComponent<InputManager>();
            AssignCameraReferences();
            AssignAudioReferences();

            yaw = transform.eulerAngles.y;
            pitch = cameraRoot != null ? NormalizeAngle(cameraRoot.localEulerAngles.x) : 0f;
            remainingAirDashCount = maxAirDashCount;
            InitializeCompass();
        }

        private void OnEnable()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            if (useCinemachineCamera)
            {
                UpdateFacingFromCamera();
            }
        }

        private void Update()
        {
            if (inputManager == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            bool wasGrounded = characterController.isGrounded;
            float downwardImpactSpeed = !wasGrounded ? Mathf.Max(0f, -verticalVelocity) : 0f;
            CleanupExpiredSpeedBoosts();

            moveInput = inputManager.Move;

            if (!useCinemachineCamera)
            {
                UpdateLook(deltaTime);
            }

            if (inputManager.DashPressed)
            {
                TryStartDash(wasGrounded);
            }

            if (!isDashing && inputManager.JumpPressed && wasGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityMultiplier);
                movementSfxPlayer?.Play(jumpAudioProfile);
            }

            UpdateHorizontalVelocity(deltaTime, wasGrounded);
            UpdateVerticalVelocity(deltaTime, wasGrounded);

            CollisionFlags collisionFlags = characterController.Move(Velocity * deltaTime);
            bool isGroundedNow = (collisionFlags & CollisionFlags.Below) != 0;

            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if (isGroundedNow)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -groundedGravity;
                }

                remainingAirDashCount = maxAirDashCount;
            }

            UpdateMovementAudio(deltaTime, wasGrounded, isGroundedNow, downwardImpactSpeed);
        }

        private void AssignCameraReferences()
        {
            // 자식 카메라를 우선 쓰고 없으면 메인 카메라를 사용한다.
            if (cameraRoot == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                cameraRoot = childCamera != null ? childCamera.transform : Camera.main != null ? Camera.main.transform : null;
            }

            if (facingReference == null)
            {
                facingReference = cameraRoot;
            }
        }

        private void AssignAudioReferences()
        {
            if (movementSfxPlayer == null)
            {
                movementSfxPlayer = GetComponentInChildren<SfxPlayer>(true);
            }

            if (movementSfxPlayer == null)
            {
                GameObject sfxObject = new GameObject("Movement SFX Player");
                sfxObject.transform.SetParent(transform, false);
                movementSfxPlayer = sfxObject.AddComponent<SfxPlayer>();
            }
        }
        
        private void InitializeCompass()
        {
            if (compass == null)
            {
                compass = FindFirstObjectByType<Compass>();
            }

            if (compass != null)
            {
                compass.StartCompass(transform);
                return;
            }

            Debug.LogWarning($"[HyperFpsFirstPersonController:{name}] Compass reference is missing.", this);
        }

        private void UpdateLook(float deltaTime)
        {
            if (cameraRoot == null || inputManager == null)
            {
                return;
            }

            Vector2 lookInput = inputManager.Look;
            float lookScale = inputManager.IsUsingGamepadLook ? gamepadLookSpeed * deltaTime : mouseSensitivity;

            yaw += lookInput.x * lookScale;
            pitch -= lookInput.y * lookScale;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateFacingFromCamera()
        {
            Transform reference = facingReference != null ? facingReference : cameraRoot;

            if (reference == null)
            {
                return;
            }

            // 카메라 전방을 지면에 투영해 캐릭터 yaw만 따라가게 만든다.
            Vector3 flattenedForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (flattenedForward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(flattenedForward.normalized, Vector3.up);
            transform.rotation = targetRotation;
            yaw = transform.eulerAngles.y;
        }

        private void UpdateHorizontalVelocity(float deltaTime, bool wasGrounded)
        {
            if (isDashing)
            {
                if (Time.time >= dashEndTime)
                {
                    isDashing = false;
                    planarVelocity = dashVelocity * dashExitSpeedMultiplier;
                }
                else
                {
                    planarVelocity = dashVelocity;
                    return;
                }
            }

            Vector3 targetVelocity = GetMoveDirection(moveInput) * (moveSpeed * GetMoveSpeedMultiplier());
            bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
            float rate = hasMoveInput
                ? (wasGrounded ? groundAcceleration : airAcceleration)
                : (wasGrounded ? groundDeceleration : airDeceleration);

            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * deltaTime);
        }

        private void UpdateVerticalVelocity(float deltaTime, bool wasGrounded)
        {
            if (wasGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundedGravity;
                return;
            }

            verticalVelocity += Physics.gravity.y * gravityMultiplier * deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
        }

        private void TryStartDash(bool wasGrounded)
        {
            if (isDashing || Time.time < nextDashTime)
            {
                return;
            }

            if (!wasGrounded)
            {
                if (!allowAirDash || remainingAirDashCount <= 0)
                {
                    return;
                }

                remainingAirDashCount--;
            }

            // 대시 입력을 4방향으로 정리해 방향을 일관되게 맞춘다.
            Vector3 direction = GetDashDirection(moveInput);

            isDashing = true;
            dashVelocity = direction * dashSpeed;
            dashEndTime = Time.time + dashDuration;
            nextDashTime = Time.time + dashCooldown;
            movementSfxPlayer?.Play(dashAudioProfile);

            if (resetVerticalVelocityOnDash)
            {
                verticalVelocity = 0f;
            }
        }

        private Vector3 GetMoveDirection(Vector2 input)
        {
            Transform reference = useCinemachineCamera && facingReference != null ? facingReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            Vector3 direction = (forward * input.y) + (right * input.x);

            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private Vector3 GetDashDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f)
            {
                return GetMoveDirection(Vector2.up);
            }

            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x > 0f ? GetMoveDirection(Vector2.right) : GetMoveDirection(Vector2.left);
            }

            return input.y > 0f ? GetMoveDirection(Vector2.up) : GetMoveDirection(Vector2.down);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }

        private void UpdateMovementAudio(float deltaTime, bool wasGrounded, bool isGroundedNow, float downwardImpactSpeed)
        {
            if (movementSfxPlayer == null)
            {
                return;
            }

            if (!wasGrounded && isGroundedNow && downwardImpactSpeed >= minimumLandingSpeed)
            {
                movementSfxPlayer.Play(landingAudioProfile);
            }

            if (!isGroundedNow || isDashing)
            {
                footstepDistanceProgress = 0f;
                return;
            }

            float planarSpeed = new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
            bool canPlayFootstep = moveInput.sqrMagnitude > 0.01f && planarSpeed >= minimumFootstepSpeed;
            if (!canPlayFootstep)
            {
                footstepDistanceProgress = 0f;
                return;
            }

            footstepDistanceProgress += planarSpeed * deltaTime;
            if (footstepDistanceProgress < footstepDistance)
            {
                return;
            }

            footstepDistanceProgress = 0f;
            movementSfxPlayer.Play(GetRandomFootstepProfile());
        }

        private AudioProfile GetRandomFootstepProfile()
        {
            if (footstepAudioProfiles == null || footstepAudioProfiles.Length == 0)
            {
                return null;
            }

            int validProfileCount = 0;
            for (int index = 0; index < footstepAudioProfiles.Length; index++)
            {
                if (footstepAudioProfiles[index] != null)
                {
                    validProfileCount++;
                }
            }

            if (validProfileCount == 0)
            {
                return null;
            }

            int randomIndex = Random.Range(0, validProfileCount);
            for (int index = 0; index < footstepAudioProfiles.Length; index++)
            {
                AudioProfile audioProfile = footstepAudioProfiles[index];
                if (audioProfile == null)
                {
                    continue;
                }

                if (randomIndex == 0)
                {
                    return audioProfile;
                }

                randomIndex--;
            }

            return null;
        }

        public bool CanReceiveSpeedBoost(float moveSpeedBonus, float duration)
        {
            return moveSpeedBonus > 0f && duration > 0f;
        }

        public bool ReceiveSpeedBoost(float moveSpeedBonus, float duration, GameObject source)
        {
            if (!CanReceiveSpeedBoost(moveSpeedBonus, duration))
            {
                return false;
            }

            activeSpeedBoosts.Add(new ActiveSpeedBoost
            {
                MoveSpeedBonus = moveSpeedBonus,
                EndTime = Time.time + duration
            });

            return true;
        }

        private void CleanupExpiredSpeedBoosts()
        {
            for (int index = activeSpeedBoosts.Count - 1; index >= 0; index--)
            {
                if (activeSpeedBoosts[index].EndTime <= Time.time)
                {
                    activeSpeedBoosts.RemoveAt(index);
                }
            }
        }

        private float GetMoveSpeedMultiplier()
        {
            float totalMultiplier = 1f;

            for (int index = 0; index < activeSpeedBoosts.Count; index++)
            {
                totalMultiplier += activeSpeedBoosts[index].MoveSpeedBonus;
            }

            return totalMultiplier;
        }
    }
}
