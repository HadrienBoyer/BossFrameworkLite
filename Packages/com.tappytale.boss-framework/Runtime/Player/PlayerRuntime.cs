using System;
using TappyTale.BossFight.Combat;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    public sealed class PlayerRuntime
    {
        private readonly PlayerDefinition _definition;
        private readonly PlayerMotor _motor;
        private Transform _camera;
        private readonly PlayerInputBuffer _buffer;

        private float _dashRemaining;
        private Vector3 _dashDirection;
        private bool _airDashConsumed;

        private float _dodgeRemaining;
        private Vector3 _dodgeDirection;

        private float _attackElapsed;
        private float _attackTotalDuration;
        private float _attackActiveStart;
        private float _attackActiveEnd;
        private float _attackCancelTime;
        private Vector3 _attackDirection;

        private float _parryElapsed;
        private float _parryTotalDuration;
        private float _parryActiveWindow;

        private float _staggerRemaining;
        private Vector3 _knockbackVelocity;
        private float _invulnerabilityRemaining;

        public PlayerRuntime(PlayerDefinition definition, PlayerMotor motor, Transform camera, PlayerInputBuffer buffer)
        {
            _definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _motor = motor != null ? motor : throw new ArgumentNullException(nameof(motor));
            _camera = camera;
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Blackboard = new PlayerBlackboard();
            Blackboard.ResetState(definition);
        }

        public PlayerBlackboard Blackboard { get; }
        public PlayerDefinition Definition => _definition;

        public event Action<PlayerState, PlayerState> StateChanged;
        public event Action Jumped;
        public event Action DashStarted;
        public event Action DashEnded;
        public event Action DodgeStarted;
        public event Action DodgeEnded;
        public event Action<bool> AttackStarted;
        public event Action AttackHitboxOpened;
        public event Action AttackHitboxClosed;
        public event Action AttackEnded;
        public event Action ParryStarted;
        public event Action ParryEnded;
        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action Staggered;
        public event Action Died;

        public void SetCamera(Transform cameraTransform)
        {
            _camera = cameraTransform;
        }

        public void Tick(Vector2 moveInput, float deltaTime)
        {
            Tick(moveInput, false, false, deltaTime);
        }

        public void Tick(Vector2 moveInput, bool sprintHeld, bool walkHeld, float deltaTime)
        {
            Blackboard.MoveInput = moveInput;
            Blackboard.IsSprinting = sprintHeld && !walkHeld && moveInput.sqrMagnitude > 0.01f;
            Blackboard.IsWalking = walkHeld && moveInput.sqrMagnitude > 0.01f;

            Blackboard.DashCooldownRemaining = Mathf.Max(0f, Blackboard.DashCooldownRemaining - deltaTime);
            Blackboard.DodgeCooldownRemaining = Mathf.Max(0f, Blackboard.DodgeCooldownRemaining - deltaTime);
            Blackboard.ParryCooldownRemaining = Mathf.Max(0f, Blackboard.ParryCooldownRemaining - deltaTime);

            if (_invulnerabilityRemaining > 0f)
            {
                _invulnerabilityRemaining = Mathf.Max(0f, _invulnerabilityRemaining - deltaTime);
            }

            if (Blackboard.State == PlayerState.Dead || Blackboard.State == PlayerState.Disabled)
            {
                Blackboard.IsInvulnerable = false;
                _buffer.Tick(deltaTime);
                return;
            }

            UpdateGrounding(deltaTime);
            Vector3 moveDirection = ResolveMoveDirection(moveInput);
            Blackboard.MoveDirection = moveDirection;

            if (Blackboard.State == PlayerState.Staggered)
            {
                TickStagger(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (Blackboard.State == PlayerState.Dashing)
            {
                TickDash(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (Blackboard.State == PlayerState.Dodging)
            {
                TickDodge(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (Blackboard.State == PlayerState.Parrying)
            {
                TickParry(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (Blackboard.State == PlayerState.Attacking)
            {
                if (Blackboard.CanCancelAttack && TryConsumeDefensiveOrDashAction(moveDirection))
                {
                    CancelActiveAttack();
                    _buffer.Tick(deltaTime);
                    UpdateInvulnerabilityFlag();
                    return;
                }

                TickAttack(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (TryConsumeDefensiveOrDashAction(moveDirection))
            {
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            if (TryBeginAttack(moveDirection))
            {
                TickAttack(deltaTime);
                _buffer.Tick(deltaTime);
                UpdateInvulnerabilityFlag();
                return;
            }

            TryJump();
            TickLocomotion(moveDirection, deltaTime);
            _buffer.Tick(deltaTime);
            UpdateInvulnerabilityFlag();
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (Blackboard.IsDead || Blackboard.State == PlayerState.Disabled || damage.Amount <= 0f)
            {
                return false;
            }

            if (damage.SourceTeam == DamageTeam.Player)
            {
                return false;
            }

            if (Blackboard.IsInvulnerable && !damage.IgnoreInvulnerability)
            {
                return false;
            }

            float previous = Blackboard.CurrentHealth;
            Blackboard.CurrentHealth = Mathf.Max(0f, previous - damage.Amount);
            HealthChanged?.Invoke(Blackboard.CurrentHealth, Blackboard.MaxHealth);
            Damaged?.Invoke(damage);

            if (Blackboard.CurrentHealth <= 0f)
            {
                ClearAllActiveActions();
                SetState(PlayerState.Dead);
                Died?.Invoke();
                return true;
            }

            _invulnerabilityRemaining = Mathf.Max(_invulnerabilityRemaining, _definition.HitInvulnerabilityDuration);
            if (_definition.StaggerDuration > 0f)
            {
                BeginStagger(damage.HitDirection);
            }

            UpdateInvulnerabilityFlag();
            return true;
        }

        public void RestoreFullHealth()
        {
            ClearAllActiveActions();
            Blackboard.ResetState(_definition);
            HealthChanged?.Invoke(Blackboard.CurrentHealth, Blackboard.MaxHealth);
        }

        public void SetDisabled(bool disabled)
        {
            if (disabled)
            {
                ClearAllActiveActions();
                SetState(PlayerState.Disabled);
            }
            else if (Blackboard.State == PlayerState.Disabled)
            {
                SetState(Blackboard.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
            }
        }

        public void SetLockTarget(Transform targetTransform, bool hardLocked)
        {
            Blackboard.LockedTargetTransform = targetTransform;
            Blackboard.IsHardLocked = hardLocked && targetTransform != null;
        }

        private bool TryConsumeDefensiveOrDashAction(Vector3 moveDirection)
        {
            if (_buffer.HasParry && CanParry())
            {
                _buffer.ConsumeParry();
                BeginParry();
                return true;
            }

            if (_buffer.HasDodge && CanDodge())
            {
                _buffer.ConsumeDodge();
                BeginDodge(moveDirection);
                return true;
            }

            if (_buffer.HasDash && CanDash())
            {
                _buffer.ConsumeDash();
                BeginDash(moveDirection);
                return true;
            }

            return false;
        }

        private void ClearAllActiveActions()
        {
            CancelActiveAttack();
            if (_dashRemaining > 0f)
            {
                _dashRemaining = 0f;
                DashEnded?.Invoke();
            }

            if (_dodgeRemaining > 0f)
            {
                _dodgeRemaining = 0f;
                DodgeEnded?.Invoke();
            }

            if (Blackboard.IsParryWindowActive || _parryElapsed < _parryTotalDuration)
            {
                Blackboard.IsParryWindowActive = false;
                _parryElapsed = _parryTotalDuration = 0f;
                ParryEnded?.Invoke();
            }

            _staggerRemaining = 0f;
            _invulnerabilityRemaining = 0f;
            Blackboard.IsInvulnerable = false;
            Blackboard.HorizontalVelocity = Vector3.zero;
            Blackboard.VerticalVelocity = 0f;
            _buffer.Clear();
        }

        private void UpdateInvulnerabilityFlag()
        {
            bool dashInvuln = Blackboard.State == PlayerState.Dashing && _definition.InvulnerableDuringDash;
            bool dodgeInvuln = Blackboard.State == PlayerState.Dodging && _definition.InvulnerableDuringDodge;
            Blackboard.IsInvulnerable = dashInvuln || dodgeInvuln || _invulnerabilityRemaining > 0f;
        }

        private void UpdateGrounding(float deltaTime)
        {
            Blackboard.IsGrounded = _motor.IsGrounded;
            if (Blackboard.IsGrounded)
            {
                Blackboard.TimeSinceGrounded = 0f;
                Blackboard.AirJumpsRemaining = _definition.AdditionalAirJumps;
                _airDashConsumed = false;
                if (Blackboard.VerticalVelocity < 0f)
                {
                    Blackboard.VerticalVelocity = -2f;
                }

                if (Blackboard.State == PlayerState.Airborne)
                {
                    SetState(PlayerState.Grounded);
                }
            }
            else
            {
                Blackboard.TimeSinceGrounded += deltaTime;
                if (Blackboard.State == PlayerState.Grounded)
                {
                    SetState(PlayerState.Airborne);
                }
            }
        }

        private void TickLocomotion(Vector3 direction, float deltaTime)
        {
            float desiredSpeed = _definition.MoveSpeed;
            if (Blackboard.IsSprinting)
            {
                desiredSpeed = _definition.SprintSpeed > 0f ? _definition.SprintSpeed : _definition.MoveSpeed * 1.35f;
            }
            else if (Blackboard.IsWalking)
            {
                desiredSpeed = _definition.WalkSpeed > 0f ? _definition.WalkSpeed : _definition.MoveSpeed * 0.55f;
            }

            if (Blackboard.IsHardLocked && Blackboard.LockedTargetTransform != null && !Blackboard.IsSprinting)
            {
                desiredSpeed *= Mathf.Clamp(_definition.StrafeSpeedMultiplier, 0.4f, 1.2f);
            }

            Vector3 targetVelocity = direction * desiredSpeed;
            float control = Blackboard.IsGrounded ? 1f : _definition.AirControl;
            float rate = targetVelocity.sqrMagnitude > Blackboard.HorizontalVelocity.sqrMagnitude
                ? _definition.Acceleration
                : _definition.Deceleration;
            Blackboard.HorizontalVelocity = Vector3.MoveTowards(
                Blackboard.HorizontalVelocity,
                targetVelocity,
                rate * control * deltaTime);

            Blackboard.VerticalVelocity += _definition.Gravity * deltaTime;
            Vector3 velocity = Blackboard.HorizontalVelocity + Vector3.up * Blackboard.VerticalVelocity;
            _motor.Move(velocity, deltaTime);

            if (Blackboard.IsHardLocked && Blackboard.LockedTargetTransform != null && !Blackboard.IsSprinting)
            {
                Vector3 toTarget = Blackboard.LockedTargetTransform.position - _motor.Body.position;
                _motor.Face(toTarget, _definition.RotationSharpness * 1.25f, deltaTime);
            }
            else
            {
                _motor.Face(direction, _definition.RotationSharpness, deltaTime);
            }
        }

        private void TryJump()
        {
            if (!_buffer.HasJump)
            {
                return;
            }

            bool canUseCoyote = Blackboard.IsGrounded || Blackboard.TimeSinceGrounded <= _definition.CoyoteTime;
            bool canUseAirJump = !canUseCoyote && Blackboard.AirJumpsRemaining > 0;
            if (!canUseCoyote && !canUseAirJump)
            {
                return;
            }

            _buffer.ConsumeJump();
            if (canUseAirJump)
            {
                Blackboard.AirJumpsRemaining--;
            }

            Blackboard.VerticalVelocity = Mathf.Sqrt(2f * _definition.JumpHeight * -_definition.Gravity);
            Blackboard.IsGrounded = false;
            Blackboard.TimeSinceGrounded = _definition.CoyoteTime + 1f;
            SetState(PlayerState.Airborne);
            Jumped?.Invoke();
        }

        private bool CanDash()
        {
            if (Blackboard.DashCooldownRemaining > 0f)
            {
                return false;
            }

            if (Blackboard.IsGrounded)
            {
                return true;
            }

            return _definition.AllowAirDash && !_airDashConsumed;
        }

        private void BeginDash(Vector3 moveDirection)
        {
            CancelActiveAttack();
            _dashDirection = moveDirection.sqrMagnitude > 0.0001f ? moveDirection : _motor.Body.forward;
            _dashDirection.y = 0f;
            _dashDirection.Normalize();
            _dashRemaining = _definition.DashDuration;
            Blackboard.HorizontalVelocity = Vector3.zero;
            Blackboard.VerticalVelocity = 0f;
            Blackboard.DashCooldownRemaining = _definition.DashCooldown;
            if (!Blackboard.IsGrounded)
            {
                _airDashConsumed = true;
            }

            SetState(PlayerState.Dashing);
            UpdateInvulnerabilityFlag();
            DashStarted?.Invoke();
            TickDash(0f);
        }

        private void TickDash(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                _dashRemaining -= deltaTime;
                _motor.Move(_dashDirection * _definition.DashSpeed, deltaTime);
                _motor.Face(_dashDirection, _definition.RotationSharpness * 2f, deltaTime);
            }

            if (_dashRemaining > 0f)
            {
                return;
            }

            SetState(_motor.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
            UpdateInvulnerabilityFlag();
            DashEnded?.Invoke();
        }

        private bool CanDodge()
        {
            return Blackboard.IsGrounded && Blackboard.DodgeCooldownRemaining <= 0f;
        }

        private void BeginDodge(Vector3 moveDirection)
        {
            CancelActiveAttack();
            _dodgeDirection = moveDirection.sqrMagnitude > 0.0001f ? moveDirection : -_motor.Body.forward;
            _dodgeDirection.y = 0f;
            _dodgeDirection.Normalize();
            _dodgeRemaining = _definition.DodgeDuration;
            Blackboard.HorizontalVelocity = Vector3.zero;
            Blackboard.VerticalVelocity = 0f;
            Blackboard.DodgeCooldownRemaining = _definition.DodgeCooldown;

            SetState(PlayerState.Dodging);
            UpdateInvulnerabilityFlag();
            DodgeStarted?.Invoke();
        }

        private void TickDodge(float deltaTime)
        {
            _dodgeRemaining -= deltaTime;
            _motor.Move(_dodgeDirection * _definition.DodgeSpeed + Vector3.up * (_definition.Gravity * 0.25f), deltaTime);
            if (_dodgeRemaining > 0f)
            {
                return;
            }

            SetState(_motor.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
            UpdateInvulnerabilityFlag();
            DodgeEnded?.Invoke();
        }

        private bool CanParry()
        {
            return Blackboard.IsGrounded && Blackboard.ParryCooldownRemaining <= 0f;
        }

        private void BeginParry()
        {
            CancelActiveAttack();
            _parryElapsed = 0f;
            _parryActiveWindow = Mathf.Max(0.05f, _definition.ParryWindowDuration);
            _parryTotalDuration = Mathf.Max(_parryActiveWindow, _definition.ParryDuration);
            Blackboard.ParryCooldownRemaining = _definition.ParryCooldown;
            Blackboard.HorizontalVelocity = Vector3.zero;
            Blackboard.IsParryWindowActive = true;

            if (Blackboard.LockedTargetTransform != null)
            {
                Vector3 toTarget = Blackboard.LockedTargetTransform.position - _motor.Body.position;
                _motor.Face(toTarget, 100f, 1f);
            }

            SetState(PlayerState.Parrying);
            ParryStarted?.Invoke();
        }

        private void TickParry(float deltaTime)
        {
            _parryElapsed += deltaTime;
            Blackboard.IsParryWindowActive = _parryElapsed <= _parryActiveWindow;
            Blackboard.VerticalVelocity += _definition.Gravity * deltaTime;
            _motor.Move(Vector3.up * Blackboard.VerticalVelocity, deltaTime);

            if (_parryElapsed < _parryTotalDuration)
            {
                return;
            }

            Blackboard.IsParryWindowActive = false;
            SetState(_motor.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
            ParryEnded?.Invoke();
        }

        private bool TryBeginAttack(Vector3 moveDirection)
        {
            bool heavy = false;
            if (_buffer.HasHeavyAttack)
            {
                _buffer.ConsumeHeavyAttack();
                heavy = true;
            }
            else if (_buffer.HasLightAttack)
            {
                _buffer.ConsumeLightAttack();
                heavy = false;
            }
            else
            {
                return false;
            }

            _attackElapsed = 0f;
            Blackboard.IsHeavyAttack = heavy;
            Blackboard.ActiveAttackDamage = heavy ? _definition.HeavyAttackDamage : _definition.LightAttackDamage;
            Blackboard.ActiveAttackStagger = heavy ? _definition.HeavyAttackStagger : _definition.LightAttackStagger;
            _attackTotalDuration = heavy ? _definition.HeavyAttackDuration : _definition.LightAttackDuration;
            _attackActiveStart = heavy ? _definition.HeavyAttackActiveStart : _definition.LightAttackActiveStart;
            _attackActiveEnd = heavy ? _definition.HeavyAttackActiveEnd : _definition.LightAttackActiveEnd;
            _attackCancelTime = heavy ? _definition.HeavyAttackCancelTime : _definition.LightAttackCancelTime;

            if (Blackboard.LockedTargetTransform != null)
            {
                Vector3 toTarget = Blackboard.LockedTargetTransform.position - _motor.Body.position;
                toTarget.y = 0f;
                _attackDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : _motor.Body.forward;
            }
            else
            {
                _attackDirection = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : _motor.Body.forward;
            }

            _attackDirection.y = 0f;
            _attackDirection.Normalize();
            _motor.Face(_attackDirection, 100f, 1f);

            Blackboard.IsAttackHitboxActive = false;
            Blackboard.CanCancelAttack = false;
            SetState(PlayerState.Attacking);
            AttackStarted?.Invoke(heavy);
            return true;
        }

        private void TickAttack(float deltaTime)
        {
            _attackElapsed += deltaTime;
            bool shouldBeActive = _attackElapsed >= _attackActiveStart && _attackElapsed <= _attackActiveEnd;
            if (shouldBeActive != Blackboard.IsAttackHitboxActive)
            {
                Blackboard.IsAttackHitboxActive = shouldBeActive;
                if (shouldBeActive)
                {
                    AttackHitboxOpened?.Invoke();
                }
                else
                {
                    AttackHitboxClosed?.Invoke();
                }
            }

            Blackboard.CanCancelAttack = _attackElapsed >= _attackCancelTime;

            float lungeFactor = _attackElapsed <= _attackActiveEnd ? 1f : 0.15f;
            Vector3 horizontal = _attackDirection * (_definition.AttackLungeSpeed * lungeFactor);
            Blackboard.VerticalVelocity += _definition.Gravity * deltaTime;
            _motor.Move(horizontal + Vector3.up * Blackboard.VerticalVelocity, deltaTime);

            if (_attackElapsed < _attackTotalDuration)
            {
                return;
            }

            CancelActiveAttack();
            SetState(_motor.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
            AttackEnded?.Invoke();
        }

        private void CancelActiveAttack()
        {
            if (Blackboard.IsAttackHitboxActive)
            {
                Blackboard.IsAttackHitboxActive = false;
                AttackHitboxClosed?.Invoke();
            }
            Blackboard.CanCancelAttack = false;
            Blackboard.ActiveAttackDamage = 0f;
            Blackboard.ActiveAttackStagger = 0f;
            _attackElapsed = 0f;
        }

        private void BeginStagger(Vector3 hitDirection)
        {
            ClearAllActiveActions();
            _staggerRemaining = Mathf.Max(0.05f, _definition.StaggerDuration);
            Vector3 dir = hitDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = -_motor.Body.forward;
            }
            _knockbackVelocity = dir.normalized * _definition.KnockbackSpeed;
            SetState(PlayerState.Staggered);
            Staggered?.Invoke();
        }

        private void TickStagger(float deltaTime)
        {
            _staggerRemaining -= deltaTime;
            _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, _definition.Deceleration * deltaTime);
            Blackboard.VerticalVelocity += _definition.Gravity * deltaTime;
            _motor.Move(_knockbackVelocity + Vector3.up * Blackboard.VerticalVelocity, deltaTime);

            if (_staggerRemaining > 0f)
            {
                return;
            }

            SetState(_motor.IsGrounded ? PlayerState.Grounded : PlayerState.Airborne);
        }

        private Vector3 ResolveMoveDirection(Vector2 input)
        {
            Vector3 forward = _camera != null ? _camera.forward : Vector3.forward;
            Vector3 right = _camera != null ? _camera.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 direction = forward * input.y + right * input.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void SetState(PlayerState next)
        {
            if (Blackboard.State == next)
            {
                return;
            }

            PlayerState previous = Blackboard.State;
            Blackboard.State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
