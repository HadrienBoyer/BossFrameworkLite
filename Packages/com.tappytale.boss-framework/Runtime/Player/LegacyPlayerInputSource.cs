using UnityEngine;

namespace TappyTale.BossFight.Player
{
    [DisallowMultipleComponent]
    public sealed class LegacyPlayerInputSource : PlayerInputSource
    {
        [Header("Movement Axes")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";

        [Header("Locomotion Keys")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode walkKey = KeyCode.LeftControl;

        [Header("Combat & Reaction Keys")]
        [SerializeField] private KeyCode lightAttackKey = KeyCode.J;
        [SerializeField] private KeyCode heavyAttackKey = KeyCode.K;
        [SerializeField] private KeyCode parryKey = KeyCode.F;
        [SerializeField] private KeyCode dodgeKey = KeyCode.L;
        [SerializeField] private bool allowMouseButtons = true;

        [Header("Lock-On & Utility Keys")]
        [SerializeField] private KeyCode lockOnKey = KeyCode.Tab;
        [SerializeField] private KeyCode switchTargetLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode switchTargetRightKey = KeyCode.E;
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        public override Vector2 Move => Vector2.ClampMagnitude(
            new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis)), 1f);

        public override bool JumpPressed => Input.GetKeyDown(jumpKey);
        public override bool DashPressed => Input.GetKeyDown(dashKey);
        public override bool SprintHeld => Input.GetKey(sprintKey);
        public override bool WalkHeld => Input.GetKey(walkKey);

        public override bool LightAttackPressed =>
            Input.GetKeyDown(lightAttackKey) || (allowMouseButtons && Input.GetMouseButtonDown(0));

        public override bool HeavyAttackPressed =>
            Input.GetKeyDown(heavyAttackKey) || Input.GetKeyDown(KeyCode.U);

        public override bool ParryPressed =>
            Input.GetKeyDown(parryKey) || (allowMouseButtons && Input.GetMouseButtonDown(1));

        public override bool DodgePressed =>
            Input.GetKeyDown(dodgeKey) || Input.GetKeyDown(KeyCode.C);

        public override bool LockOnPressed =>
            Input.GetKeyDown(lockOnKey) || (allowMouseButtons && Input.GetMouseButtonDown(2));

        public override float SwitchTargetAxis
        {
            get
            {
                if (Input.GetKeyDown(switchTargetLeftKey)) return -1f;
                if (Input.GetKeyDown(switchTargetRightKey)) return 1f;
                return 0f;
            }
        }

        public override bool RestartPressed => Input.GetKeyDown(restartKey);
    }
}
