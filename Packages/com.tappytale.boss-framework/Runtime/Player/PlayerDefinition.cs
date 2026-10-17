using TappyTale.BossFight.Feedback;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Player Definition", fileName = "Player_")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "starter_player";
        [SerializeField] private string displayName = "Duelist Player";
        [SerializeField] private Color themeColor = new(0.2f, 0.72f, 0.95f, 1f);

        [Header("Locomotion")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0f)] private float moveSpeed = 8f;
        [SerializeField, Min(0f)] private float sprintSpeed = 11.5f;
        [SerializeField, Min(0f)] private float acceleration = 55f;
        [SerializeField, Min(0f)] private float deceleration = 70f;
        [SerializeField, Min(0f)] private float airControl = 0.8f;
        [SerializeField, Min(0f)] private float rotationSharpness = 24f;

        [Header("Jump")]
        [SerializeField] private float gravity = -32f;
        [SerializeField, Min(0f)] private float jumpHeight = 2.2f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0)] private int additionalAirJumps = 1;

        [Header("Dash")]
        [SerializeField, Min(0f)] private float dashSpeed = 20f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.35f;
        [SerializeField, Min(0f)] private float dashBufferTime = 0.15f;
        [SerializeField] private bool allowAirDash = true;
        [SerializeField] private bool invulnerableDuringDash = true;

        [Header("Vitality & Defense")]
        [SerializeField, Min(1f)] private float maxHealth = 120f;
        [SerializeField, Min(0f)] private float hitInvulnerabilityDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float staggerDuration = 0.28f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 6f;

        [Header("Combat - Light & Heavy Attacks")]
        [SerializeField, Min(1f)] private float lightAttackDamage = 22f;
        [SerializeField, Min(0f)] private float lightAttackStagger = 18f;
        [SerializeField, Min(0.05f)] private float lightAttackDuration = 0.36f;
        [SerializeField, Min(0f)] private float lightAttackActiveStart = 0.08f;
        [SerializeField, Min(0.02f)] private float lightAttackActiveEnd = 0.22f;
        [SerializeField, Min(0.05f)] private float lightAttackCancelTime = 0.24f;

        [SerializeField, Min(1f)] private float heavyAttackDamage = 45f;
        [SerializeField, Min(0f)] private float heavyAttackStagger = 42f;
        [SerializeField, Min(0.1f)] private float heavyAttackDuration = 0.62f;
        [SerializeField, Min(0f)] private float heavyAttackActiveStart = 0.18f;
        [SerializeField, Min(0.05f)] private float heavyAttackActiveEnd = 0.38f;
        [SerializeField, Min(0.1f)] private float heavyAttackCancelTime = 0.45f;
        [SerializeField, Min(0f)] private float attackLungeSpeed = 5.5f;
        [SerializeField, Min(0.5f)] private float attackRange = 2.4f;
        [SerializeField, Min(0.02f)] private float attackBufferTime = 0.16f;

        [Header("Parry & Dodge Reactions")]
        [SerializeField, Min(0.05f)] private float parryWindowDuration = 0.24f;
        [SerializeField, Min(0.05f)] private float parryDuration = 0.38f;
        [SerializeField, Min(0f)] private float parryCooldown = 0.45f;
        [SerializeField, Min(0.02f)] private float parryBufferTime = 0.14f;
        [SerializeField, Min(1f)] private float parryDeflectRadius = 3.2f;

        [SerializeField, Min(0f)] private float dodgeSpeed = 16f;
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.24f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.35f;
        [SerializeField] private bool invulnerableDuringDodge = true;

        [Header("Lock-On & Strafe")]
        [SerializeField, Min(1f)] private float lockOnRange = 24f;
        [SerializeField, Min(2f)] private float lockOnBreakDistance = 30f;
        [SerializeField] private bool allowSoftLock = true;
        [SerializeField, Range(0.4f, 1.2f)] private float strafeSpeedMultiplier = 0.88f;

        [Header("Animator Clips (Optional - Used by Animator Builder)")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip runClip;
        [SerializeField] private AnimationClip sprintClip;
        [SerializeField] private AnimationClip strafeForwardClip;
        [SerializeField] private AnimationClip strafeBackClip;
        [SerializeField] private AnimationClip strafeLeftClip;
        [SerializeField] private AnimationClip strafeRightClip;
        [SerializeField] private AnimationClip jumpClip;
        [SerializeField] private AnimationClip dashClip;
        [SerializeField] private AnimationClip lightAttackClip;
        [SerializeField] private AnimationClip heavyAttackClip;
        [SerializeField] private AnimationClip parryClip;
        [SerializeField] private AnimationClip staggerClip;
        [SerializeField] private AnimationClip deathClip;

        [Header("Feedback Payloads (Optional)")]
        [SerializeField] private AudioCueDefinition attackAudioCue;
        [SerializeField] private AudioCueDefinition parryAudioCue;
        [SerializeField] private AudioCueDefinition hitAudioCue;
        [SerializeField] private VfxDefinition attackVfx;
        [SerializeField] private VfxDefinition parryVfx;
        [SerializeField] private CameraShakeDefinition hitCameraShake;
        [SerializeField, Range(0f, 0.15f)] private float parryHitstopDuration = 0.05f;
        [SerializeField] private bool spawnGhostTrailOnDash = true;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public Color ThemeColor => themeColor;

        public float WalkSpeed => walkSpeed;
        public float MoveSpeed => moveSpeed;
        public float SprintSpeed => sprintSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float AirControl => airControl;
        public float RotationSharpness => rotationSharpness;

        public float Gravity => gravity;
        public float JumpHeight => jumpHeight;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public int AdditionalAirJumps => additionalAirJumps;

        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public float DashBufferTime => dashBufferTime;
        public bool AllowAirDash => allowAirDash;
        public bool InvulnerableDuringDash => invulnerableDuringDash;

        public float MaxHealth => maxHealth;
        public float HitInvulnerabilityDuration => hitInvulnerabilityDuration;
        public float StaggerDuration => staggerDuration;
        public float KnockbackSpeed => knockbackSpeed;

        public float LightAttackDamage => lightAttackDamage;
        public float LightAttackStagger => lightAttackStagger;
        public float LightAttackDuration => lightAttackDuration;
        public float LightAttackActiveStart => lightAttackActiveStart;
        public float LightAttackActiveEnd => lightAttackActiveEnd;
        public float LightAttackCancelTime => lightAttackCancelTime;

        public float HeavyAttackDamage => heavyAttackDamage;
        public float HeavyAttackStagger => heavyAttackStagger;
        public float HeavyAttackDuration => heavyAttackDuration;
        public float HeavyAttackActiveStart => heavyAttackActiveStart;
        public float HeavyAttackActiveEnd => heavyAttackActiveEnd;
        public float HeavyAttackCancelTime => heavyAttackCancelTime;
        public float AttackLungeSpeed => attackLungeSpeed;
        public float AttackRange => attackRange;
        public float AttackBufferTime => attackBufferTime;

        public float ParryWindowDuration => parryWindowDuration;
        public float ParryDuration => parryDuration;
        public float ParryCooldown => parryCooldown;
        public float ParryBufferTime => parryBufferTime;
        public float ParryDeflectRadius => parryDeflectRadius;

        public float DodgeSpeed => dodgeSpeed;
        public float DodgeDuration => dodgeDuration;
        public float DodgeCooldown => dodgeCooldown;
        public bool InvulnerableDuringDodge => invulnerableDuringDodge;

        public float LockOnRange => lockOnRange;
        public float LockOnBreakDistance => lockOnBreakDistance;
        public bool AllowSoftLock => allowSoftLock;
        public float StrafeSpeedMultiplier => strafeSpeedMultiplier;

        public RuntimeAnimatorController AnimatorController => animatorController;
        public AnimationClip IdleClip => idleClip;
        public AnimationClip WalkClip => walkClip;
        public AnimationClip RunClip => runClip;
        public AnimationClip SprintClip => sprintClip;
        public AnimationClip StrafeForwardClip => strafeForwardClip;
        public AnimationClip StrafeBackClip => strafeBackClip;
        public AnimationClip StrafeLeftClip => strafeLeftClip;
        public AnimationClip StrafeRightClip => strafeRightClip;
        public AnimationClip JumpClip => jumpClip;
        public AnimationClip DashClip => dashClip;
        public AnimationClip LightAttackClip => lightAttackClip;
        public AnimationClip HeavyAttackClip => heavyAttackClip;
        public AnimationClip ParryClip => parryClip;
        public AnimationClip StaggerClip => staggerClip;
        public AnimationClip DeathClip => deathClip;

        public AudioCueDefinition AttackAudioCue => attackAudioCue;
        public AudioCueDefinition ParryAudioCue => parryAudioCue;
        public AudioCueDefinition HitAudioCue => hitAudioCue;
        public VfxDefinition AttackVfx => attackVfx;
        public VfxDefinition ParryVfx => parryVfx;
        public CameraShakeDefinition HitCameraShake => hitCameraShake;
        public float ParryHitstopDuration => parryHitstopDuration;
        public bool SpawnGhostTrailOnDash => spawnGhostTrailOnDash;
    }
}
