using TappyTale.BossFight.Player;
using UnityEngine;
#if TAPPYTALE_INPUTSYSTEM_AVAILABLE
using UnityEngine.InputSystem;
#endif

namespace TappyTale.BossFight.InputSystem
{
    /// <summary>
    /// Optional Unity Input System adapter implementing PlayerInputSource.
    /// Compiled in an isolated assembly only when com.unity.inputsystem is present.
    /// Supports both explicit InputActionReferences and zero-config Keyboard/Mouse/Gamepad fallback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputSystemPlayerInputSource : PlayerInputSource
    {
#if TAPPYTALE_INPUTSYSTEM_AVAILABLE
        [Header("Optional Input Action References")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference dashAction;
        [SerializeField] private InputActionReference sprintAction;
        [SerializeField] private InputActionReference lightAttackAction;
        [SerializeField] private InputActionReference heavyAttackAction;
        [SerializeField] private InputActionReference parryAction;
        [SerializeField] private InputActionReference dodgeAction;
        [SerializeField] private InputActionReference lockOnAction;

        private void OnEnable()
        {
            moveAction?.action?.Enable();
            jumpAction?.action?.Enable();
            dashAction?.action?.Enable();
            sprintAction?.action?.Enable();
            lightAttackAction?.action?.Enable();
            heavyAttackAction?.action?.Enable();
            parryAction?.action?.Enable();
            dodgeAction?.action?.Enable();
            lockOnAction?.action?.Enable();
        }

        public override Vector2 Move
        {
            get
            {
                if (moveAction != null && moveAction.action != null)
                {
                    return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
                }

                Vector2 move = Vector2.zero;
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                }

                Gamepad pad = Gamepad.current;
                if (pad != null && move.sqrMagnitude < 0.01f)
                {
                    move = pad.leftStick.ReadValue();
                }

                return Vector2.ClampMagnitude(move, 1f);
            }
        }

        public override bool JumpPressed =>
            (jumpAction?.action != null && jumpAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        public override bool DashPressed =>
            (dashAction?.action != null && dashAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);

        public override bool SprintHeld =>
            (sprintAction?.action != null && sprintAction.action.IsPressed()) ||
            (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
            (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);

        public override bool WalkHeld =>
            Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        public override bool LightAttackPressed =>
            (lightAttackAction?.action != null && lightAttackAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

        public override bool HeavyAttackPressed =>
            (heavyAttackAction?.action != null && heavyAttackAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        public override bool ParryPressed =>
            (parryAction?.action != null && parryAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);

        public override bool DodgePressed =>
            (dodgeAction?.action != null && dodgeAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        public override bool LockOnPressed =>
            (lockOnAction?.action != null && lockOnAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightStickButton.wasPressedThisFrame);

        public override float SwitchTargetAxis
        {
            get
            {
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.qKey.wasPressedThisFrame) return -1f;
                    if (kb.eKey.wasPressedThisFrame) return 1f;
                }
                return 0f;
            }
        }

        public override bool RestartPressed =>
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        public override Vector2 Move => Vector2.zero;
        public override bool JumpPressed => false;
        public override bool DashPressed => false;
        public override bool SprintHeld => false;
        public override bool WalkHeld => false;
        public override bool LightAttackPressed => false;
        public override bool HeavyAttackPressed => false;
        public override bool ParryPressed => false;
        public override bool DodgePressed => false;
        public override bool LockOnPressed => false;
        public override float SwitchTargetAxis => 0f;
        public override bool RestartPressed => false;
#endif
    }
}
