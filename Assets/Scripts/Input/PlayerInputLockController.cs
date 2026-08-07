using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mood.Input
{
    [AddComponentMenu("MOOD/Input/Player Input Lock Controller")]
    [DisallowMultipleComponent]
    public sealed class PlayerInputLockController : MonoBehaviour, IPlayerInputLock
    {
        [Header("References")]
        [SerializeField] private InputManager inputManager;
        [SerializeField] private PlayerInput playerInput;

        [Header("Optional Disable Targets")]
        [SerializeField] private bool disablePlayerInputComponent;
        [SerializeField] private CinemachineInputAxisController[] cameraInputControllers;
        [SerializeField] private Behaviour[] extraBehavioursToDisable;

        private readonly HashSet<object> lockOwners = new HashSet<object>();
        private readonly Dictionary<Behaviour, bool> behaviourStates = new Dictionary<Behaviour, bool>();
        private bool wasPlayerInputEnabled;

        public bool IsInputLocked => lockOwners.Count > 0;

        private void Reset()
        {
            inputManager = GetComponent<InputManager>();
            playerInput = GetComponent<PlayerInput>();
            cameraInputControllers = GetComponentsInChildren<CinemachineInputAxisController>(true);
        }

        private void Awake()
        {
            inputManager = inputManager != null ? inputManager : GetComponent<InputManager>();
            playerInput = playerInput != null ? playerInput : GetComponent<PlayerInput>();
            if (cameraInputControllers == null || cameraInputControllers.Length == 0)
            {
                cameraInputControllers = GetComponentsInChildren<CinemachineInputAxisController>(true);
            }
        }

        private void OnDisable()
        {
            if (!IsInputLocked)
            {
                return;
            }

            lockOwners.Clear();
            ApplyLockState(false);
        }

        public bool TryLockInput(object owner)
        {
            object resolvedOwner = owner ?? this;
            bool wasUnlocked = lockOwners.Count == 0;
            bool added = lockOwners.Add(resolvedOwner);

            if (!added)
            {
                return true;
            }

            if (wasUnlocked)
            {
                ApplyLockState(true);
            }

            return true;
        }

        public void UnlockInput(object owner)
        {
            object resolvedOwner = owner ?? this;
            if (!lockOwners.Remove(resolvedOwner))
            {
                return;
            }

            if (lockOwners.Count == 0)
            {
                ApplyLockState(false);
            }
        }

        private void ApplyLockState(bool locked)
        {
            if (inputManager != null)
            {
                inputManager.SetInputLocked(locked);
            }

            if (disablePlayerInputComponent && playerInput != null)
            {
                if (locked)
                {
                    wasPlayerInputEnabled = playerInput.enabled;
                    playerInput.enabled = false;
                }
                else
                {
                    playerInput.enabled = wasPlayerInputEnabled;
                }
            }

            SetBehavioursLocked(cameraInputControllers, locked);
            SetBehavioursLocked(extraBehavioursToDisable, locked);
        }

        private void SetBehavioursLocked(Behaviour[] targets, bool locked)
        {
            if (targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                Behaviour behaviour = targets[index];
                if (behaviour == null)
                {
                    continue;
                }

                if (locked)
                {
                    if (!behaviourStates.ContainsKey(behaviour))
                    {
                        behaviourStates[behaviour] = behaviour.enabled;
                    }

                    behaviour.enabled = false;
                    continue;
                }

                if (!behaviourStates.TryGetValue(behaviour, out bool wasEnabled))
                {
                    continue;
                }

                behaviour.enabled = wasEnabled;
                behaviourStates.Remove(behaviour);
            }
        }
    }
}
