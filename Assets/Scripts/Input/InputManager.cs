using Mood.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mood.Input
{
    // PlayerInput �׼� ������ ���� �Է��� ��ũ��Ʈ���� �д´�.
    [AddComponentMenu("MOOD/Input/Input Manager")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class InputManager : MonoBehaviour
    {
        [Header("Action Names")]
        [SerializeField] private string[] moveActionNames = { "Move" };
        [SerializeField] private string[] lookActionNames = { "Look" };
        [SerializeField] private string[] jumpActionNames = { "Jump" };
        [SerializeField] private string[] dashActionNames = { "Dash", "Sprint", "TacticalSprint" };
        [SerializeField] private string[] fireActionNames = { "Attack", "Fire" };
        [SerializeField] private string[] aimActionNames = { "Aim", "ADS" };
        [SerializeField] private string[] reloadActionNames = { "Reload" };
        [SerializeField] private string[] interactActionNames = { "Interact", "Intract" };
        [SerializeField] private string[] previousActionNames = { "Previous" };
        [SerializeField] private string[] nextActionNames = { "Next" };
        [SerializeField] private string[] throwActionNames = { "Throw", "Grenade" };
        [SerializeField] private string[] pauseActionNames = { "Pause", "Menu", "Cancel" };
        [SerializeField] private string[] scoreboardActionNames = { "Scoreboard", "Tab" };

        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction dashAction;
        private InputAction fireAction;
        private InputAction aimAction;
        private InputAction reloadAction;
        private InputAction interactAction;
        private InputAction previousAction;
        private InputAction nextAction;
        private InputAction throwAction;
        private InputAction pauseAction;
        private InputAction scoreboardAction;
        private bool isInputLocked;

        public Vector2 Move => isInputLocked ? Vector2.zero : (moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 Look => isInputLocked ? Vector2.zero : (lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero);
        public bool JumpPressed => !isInputLocked && jumpAction != null && jumpAction.WasPressedThisFrame();
        public bool DashPressed => !isInputLocked && dashAction != null && dashAction.WasPressedThisFrame();
        public bool FirePressed => !isInputLocked && fireAction != null && fireAction.WasPressedThisFrame();
        public bool FireHeld => !isInputLocked && fireAction != null && fireAction.IsPressed();
        public bool AimHeld => !isInputLocked && aimAction != null && aimAction.IsPressed();
        public bool ReloadPressed => !isInputLocked && reloadAction != null && reloadAction.WasPressedThisFrame();
        public bool InteractPressed => !isInputLocked && interactAction != null && interactAction.WasPressedThisFrame();
        public bool PreviousPressed => !isInputLocked && previousAction != null && previousAction.WasPressedThisFrame();
        public bool NextPressed => !isInputLocked && nextAction != null && nextAction.WasPressedThisFrame();
        public bool ThrowPressed => !isInputLocked && ((throwAction != null && throwAction.WasPressedThisFrame()) || ReadThrowPressed());
        public int WeaponSlotPressed => isInputLocked ? -1 : ReadWeaponSlotPressed();
        public bool IsUsingGamepadLook => !isInputLocked && lookAction != null && lookAction.activeControl?.device is Gamepad;
        public bool PausePressed =>
            (pauseAction != null && pauseAction.WasPressedThisFrame()) || ReadPausePressed();
        public bool ScoreboardHeld =>
            !isInputLocked && ((scoreboardAction != null && scoreboardAction.IsPressed()) || ReadScoreboardHeld());
        public bool IsInputLocked => isInputLocked;
        public PlayerInput PlayerInput => playerInput;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            CacheActions();
        }

        private void OnEnable()
        {
            CacheActions();
        }

        private void CacheActions()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            // �׼� �̸� �ĺ��� ������� �õ��ؼ� �ٸ� ���� �̸��� ����Ѵ�.
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            moveAction = FindAction(actions, moveActionNames);
            lookAction = FindAction(actions, lookActionNames);
            jumpAction = FindAction(actions, jumpActionNames);
            dashAction = FindAction(actions, dashActionNames);
            fireAction = FindAction(actions, fireActionNames);
            aimAction = FindAction(actions, aimActionNames);
            reloadAction = FindAction(actions, reloadActionNames);
            interactAction = FindAction(actions, interactActionNames);
            previousAction = FindAction(actions, previousActionNames);
            nextAction = FindAction(actions, nextActionNames);
            throwAction = FindAction(actions, throwActionNames);
            pauseAction = FindAction(actions, pauseActionNames);
            scoreboardAction = FindAction(actions, scoreboardActionNames);
        }

        private static InputAction FindAction(InputActionAsset asset, string[] actionNames)
        {
            if (asset == null || actionNames == null)
            {
                return null;
            }

            foreach (string actionName in actionNames)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                InputAction action = asset.FindAction(actionName, false);
                if (action != null)
                {
                    return action;
                }
            }

            return null;
        }

        private static int ReadWeaponSlotPressed()
        {
            // ���� Ű�� ���� �׼� ���� ���� �о� ���� ��ȯ�� ����Ѵ�.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                return 0;
            }

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                return 1;
            }

            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                return 2;
            }

            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                return 3;
            }

            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
            {
                return 4;
            }

            return -1;
        }

        private static bool ReadThrowPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.gKey.wasPressedThisFrame;
        }
        
        private static bool ReadPausePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
        
        private static bool ReadScoreboardHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.isPressed;
        }

        public void SetInputLocked(bool locked)
        {
            isInputLocked = locked;
        }
    }
}
