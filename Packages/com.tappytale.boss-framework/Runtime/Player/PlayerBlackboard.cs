using UnityEngine;

namespace TappyTale.BossFight.Player
{
    public sealed class PlayerBlackboard
    {
        public PlayerState State { get; internal set; } = PlayerState.Airborne;
        public Vector2 MoveInput { get; internal set; }
        public Vector3 MoveDirection { get; internal set; }
        public Vector3 HorizontalVelocity { get; internal set; }
        public float VerticalVelocity { get; internal set; }
        public bool IsGrounded { get; internal set; }
        public bool IsInvulnerable { get; internal set; }
        public bool IsSprinting { get; internal set; }
        public bool IsWalking { get; internal set; }
        public float TimeSinceGrounded { get; internal set; }
        public float DashCooldownRemaining { get; internal set; }
        public float DodgeCooldownRemaining { get; internal set; }
        public float ParryCooldownRemaining { get; internal set; }
        public int AirJumpsRemaining { get; internal set; }

        public float CurrentHealth { get; internal set; } = 100f;
        public float MaxHealth { get; internal set; } = 100f;
        public float NormalizedHealth => MaxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / MaxHealth);
        public bool IsDead => CurrentHealth <= 0f || State == PlayerState.Dead;

        public bool IsDashing => State == PlayerState.Dashing;
        public bool IsDodging => State == PlayerState.Dodging;
        public bool IsAttacking => State == PlayerState.Attacking;
        public bool IsParrying => State == PlayerState.Parrying;
        public bool IsStaggered => State == PlayerState.Staggered;
        public bool IsAttackHitboxActive { get; internal set; }
        public bool IsParryWindowActive { get; internal set; }
        public bool CanCancelAttack { get; internal set; }
        public bool IsHeavyAttack { get; internal set; }
        public float ActiveAttackDamage { get; internal set; }
        public float ActiveAttackStagger { get; internal set; }

        public bool IsHardLocked { get; internal set; }
        public Transform LockedTargetTransform { get; internal set; }

        public void ResetState(PlayerDefinition definition)
        {
            float maxHp = definition != null ? Mathf.Max(1f, definition.MaxHealth) : 100f;
            MaxHealth = maxHp;
            CurrentHealth = maxHp;
            State = PlayerState.Grounded;
            MoveInput = Vector2.zero;
            MoveDirection = Vector3.zero;
            HorizontalVelocity = Vector3.zero;
            VerticalVelocity = 0f;
            IsGrounded = true;
            IsInvulnerable = false;
            IsSprinting = false;
            IsWalking = false;
            TimeSinceGrounded = 0f;
            DashCooldownRemaining = 0f;
            DodgeCooldownRemaining = 0f;
            ParryCooldownRemaining = 0f;
            AirJumpsRemaining = definition != null ? definition.AdditionalAirJumps : 1;
            IsAttackHitboxActive = false;
            IsParryWindowActive = false;
            CanCancelAttack = false;
            IsHeavyAttack = false;
            ActiveAttackDamage = 0f;
            ActiveAttackStagger = 0f;
        }
    }
}
