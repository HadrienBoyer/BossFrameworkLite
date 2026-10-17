using UnityEngine;

namespace TappyTale.BossFight.Player
{
    /// <summary>
    /// Drives Unity Animator parameters from PlayerRuntime state & velocity.
    /// Operates safely without errors when no Animator or Controller is assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorAdapter : MonoBehaviour
    {
        public static readonly int SpeedHash = Animator.StringToHash("Speed");
        public static readonly int MoveXHash = Animator.StringToHash("MoveX");
        public static readonly int MoveYHash = Animator.StringToHash("MoveY");
        public static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
        public static readonly int LockedOnHash = Animator.StringToHash("IsLockedOn");
        public static readonly int DashingHash = Animator.StringToHash("IsDashing");
        public static readonly int DeadHash = Animator.StringToHash("IsDead");
        public static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
        public static readonly int DashTriggerHash = Animator.StringToHash("Dash");
        public static readonly int LightAttackTriggerHash = Animator.StringToHash("LightAttack");
        public static readonly int HeavyAttackTriggerHash = Animator.StringToHash("HeavyAttack");
        public static readonly int ParryTriggerHash = Animator.StringToHash("Parry");
        public static readonly int StaggerTriggerHash = Animator.StringToHash("Stagger");

        [SerializeField] private PlayerController controller;
        [SerializeField] private Animator animator;

        private PlayerRuntime _boundRuntime;

        public Animator Animator => animator;
        public bool HasValidAnimator => animator != null && animator.runtimeAnimatorController != null;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (_boundRuntime != controller?.Runtime)
            {
                TryBind();
            }

            if (!HasValidAnimator || controller == null || controller.Blackboard == null)
            {
                return;
            }

            PlayerBlackboard bb = controller.Blackboard;
            Vector3 localVel = transform.InverseTransformDirection(bb.HorizontalVelocity);
            float maxRefSpeed = controller.Definition != null && controller.Definition.MoveSpeed > 0.01f
                ? controller.Definition.MoveSpeed
                : 8f;

            animator.SetFloat(SpeedHash, bb.HorizontalVelocity.magnitude / maxRefSpeed);
            animator.SetFloat(MoveXHash, Mathf.Clamp(localVel.x / maxRefSpeed, -1f, 1f));
            animator.SetFloat(MoveYHash, Mathf.Clamp(localVel.z / maxRefSpeed, -1f, 1f));
            animator.SetBool(GroundedHash, bb.IsGrounded);
            animator.SetBool(LockedOnHash, bb.IsHardLocked);
            animator.SetBool(DashingHash, bb.IsDashing || bb.IsDodging);
            animator.SetBool(DeadHash, bb.IsDead);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void TryBind()
        {
            Unbind();
            if (controller != null && controller.Definition != null && animator != null &&
                animator.runtimeAnimatorController == null && controller.Definition.AnimatorController != null)
            {
                animator.runtimeAnimatorController = controller.Definition.AnimatorController;
            }

            _boundRuntime = controller != null ? controller.Runtime : null;
            if (_boundRuntime == null) return;

            _boundRuntime.Jumped += OnJumped;
            _boundRuntime.DashStarted += OnDashStarted;
            _boundRuntime.DodgeStarted += OnDashStarted;
            _boundRuntime.AttackStarted += OnAttackStarted;
            _boundRuntime.ParryStarted += OnParryStarted;
            _boundRuntime.Staggered += OnStaggered;
        }

        private void Unbind()
        {
            if (_boundRuntime == null) return;
            _boundRuntime.Jumped -= OnJumped;
            _boundRuntime.DashStarted -= OnDashStarted;
            _boundRuntime.DodgeStarted -= OnDashStarted;
            _boundRuntime.AttackStarted -= OnAttackStarted;
            _boundRuntime.ParryStarted -= OnParryStarted;
            _boundRuntime.Staggered -= OnStaggered;
            _boundRuntime = null;
        }

        private void OnJumped()
        {
            if (HasValidAnimator) animator.SetTrigger(JumpTriggerHash);
        }

        private void OnDashStarted()
        {
            if (HasValidAnimator) animator.SetTrigger(DashTriggerHash);
        }

        private void OnAttackStarted(bool isHeavy)
        {
            if (HasValidAnimator)
            {
                animator.SetTrigger(isHeavy ? HeavyAttackTriggerHash : LightAttackTriggerHash);
            }
        }

        private void OnParryStarted()
        {
            if (HasValidAnimator) animator.SetTrigger(ParryTriggerHash);
        }

        private void OnStaggered()
        {
            if (HasValidAnimator) animator.SetTrigger(StaggerTriggerHash);
        }
    }
}
