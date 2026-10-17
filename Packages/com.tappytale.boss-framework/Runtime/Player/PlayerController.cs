using System;
using System.Collections.Generic;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Projectiles;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerMotor))]
    public sealed class PlayerController : MonoBehaviour, IDamageReceiver, IBossDamageReceiver
    {
        [SerializeField] private PlayerDefinition definition;
        [SerializeField] private PlayerInputSource inputSource;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerMeleeHitbox meleeHitbox;
        [SerializeField] private PlayerFeedbackAdapter feedbackAdapter;
        [SerializeField] private BossDirector primaryBossDirector;

        private PlayerInputBuffer _inputBuffer;
        private PlayerDefinition _runtimeFallbackDefinition;
        private readonly Collider[] _parryOverlapBuffer = new Collider[16];
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _capturedInitialTransform;
        private ILockOnTarget _currentLockTarget;
        private bool _isHardLocked;

        public PlayerRuntime Runtime { get; private set; }
        public PlayerBlackboard Blackboard => Runtime?.Blackboard;
        public PlayerDefinition Definition => definition;
        public PlayerInputSource InputSource => inputSource;
        public BossDirector PrimaryBossDirector
        {
            get => primaryBossDirector;
            set => primaryBossDirector = value;
        }

        public DamageTeam Team => DamageTeam.Player;
        public bool IsAlive => Runtime != null && !Runtime.Blackboard.IsDead;

        public event Action ParrySucceeded;
        public event Action PerfectDodgeSucceeded;

        private void Reset()
        {
            motor = GetComponent<PlayerMotor>();
            inputSource = GetComponent<PlayerInputSource>();
            meleeHitbox = GetComponentInChildren<PlayerMeleeHitbox>();
            feedbackAdapter = GetComponent<PlayerFeedbackAdapter>();
        }

        private void Awake()
        {
            CaptureInitialTransform();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (inputSource == null) inputSource = GetComponent<PlayerInputSource>();
            if (meleeHitbox == null) meleeHitbox = GetComponentInChildren<PlayerMeleeHitbox>();
            if (feedbackAdapter == null) feedbackAdapter = GetComponent<PlayerFeedbackAdapter>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            BuildRuntime();
        }

        private void Update()
        {
            if (Runtime == null || inputSource == null)
            {
                return;
            }

            PlayerDefinition def = RuntimeDefinition;

            if (inputSource.JumpPressed)
            {
                _inputBuffer.BufferJump(def.JumpBufferTime);
            }

            if (inputSource.DashPressed)
            {
                _inputBuffer.BufferDash(def.DashBufferTime);
            }

            if (inputSource.LightAttackPressed)
            {
                _inputBuffer.BufferLightAttack(def.AttackBufferTime);
            }

            if (inputSource.HeavyAttackPressed)
            {
                _inputBuffer.BufferHeavyAttack(def.AttackBufferTime);
            }

            if (inputSource.ParryPressed)
            {
                _inputBuffer.BufferParry(def.ParryBufferTime);
            }

            if (inputSource.DodgePressed)
            {
                _inputBuffer.BufferDodge(def.DashBufferTime);
            }

            if (inputSource.LockOnPressed)
            {
                ToggleHardLock();
            }
            else if (Mathf.Abs(inputSource.SwitchTargetAxis) > 0.1f && _isHardLocked)
            {
                SwitchLockTarget(inputSource.SwitchTargetAxis);
            }

            UpdateLockTargetResolution();

            Runtime.Tick(inputSource.Move, inputSource.SprintHeld, inputSource.WalkHeld, Time.deltaTime);

            if (Runtime.Blackboard.IsParryWindowActive)
            {
                EvaluateActiveParryWindow();
            }

            if (Runtime.Blackboard.IsDashing || Runtime.Blackboard.IsDodging)
            {
                EvaluateActivePerfectDodge();
            }
        }

        private void OnDisable()
        {
            if (meleeHitbox != null)
            {
                meleeHitbox.Deactivate();
            }
        }

        private void OnDestroy()
        {
            UnbindRuntimeEvents();
            if (_runtimeFallbackDefinition != null)
            {
                Destroy(_runtimeFallbackDefinition);
            }

            Runtime = null;
        }

        public bool ReceiveDamage(DamageInfo damage)
        {
            if (Runtime == null) return false;
            if (damage.SourceTeam == DamageTeam.Player) return false;

            if (Runtime.Blackboard.IsParryWindowActive)
            {
                BossDirector boss = ResolveActiveBossDirector();
                if (boss != null && boss.TryParry())
                {
                    feedbackAdapter?.NotifyParrySuccess();
                    ParrySucceeded?.Invoke();
                    return false;
                }
            }

            if (Runtime.Blackboard.IsDashing || Runtime.Blackboard.IsDodging)
            {
                BossDirector boss = ResolveActiveBossDirector();
                if (boss != null && boss.TryPerfectDodge())
                {
                    feedbackAdapter?.NotifyPerfectDodgeSuccess();
                    PerfectDodgeSucceeded?.Invoke();
                    return false;
                }
            }

            return Runtime.ApplyDamage(damage);
        }

        public bool ReceiveDamage(float amount, GameObject source)
        {
            return ReceiveDamage(new DamageInfo(amount, source, DamageTeam.Boss));
        }

        public void ResetPlayer(bool restoreTransform = true)
        {
            if (restoreTransform && _capturedInitialTransform && motor != null)
            {
                motor.Teleport(_initialPosition, _initialRotation);
            }

            if (meleeHitbox != null)
            {
                meleeHitbox.Deactivate();
            }

            Runtime?.RestoreFullHealth();
        }

        public void SetInputSource(PlayerInputSource source)
        {
            inputSource = source;
        }

        public void SetCamera(Transform newCamera)
        {
            cameraTransform = newCamera;
            if (Runtime != null)
            {
                Runtime.SetCamera(newCamera);
            }
            else
            {
                BuildRuntime();
            }
        }

        public void SetDefinition(PlayerDefinition newDefinition)
        {
            definition = newDefinition;
            BuildRuntime();
        }

        public void SetDisabled(bool disabled)
        {
            if (disabled && meleeHitbox != null)
            {
                meleeHitbox.Deactivate();
            }
            Runtime?.SetDisabled(disabled);
        }

        public void ToggleHardLock()
        {
            if (_isHardLocked)
            {
                _isHardLocked = false;
                _currentLockTarget = null;
                Runtime?.SetLockTarget(null, false);
                return;
            }

            ILockOnTarget best = FindBestTarget(RuntimeDefinition.LockOnRange);
            if (best != null)
            {
                _currentLockTarget = best;
                _isHardLocked = true;
                Runtime?.SetLockTarget(best.AimTransform, true);
            }
        }

        private void UpdateLockTargetResolution()
        {
            PlayerDefinition def = RuntimeDefinition;
            if (_isHardLocked)
            {
                if (_currentLockTarget == null || !_currentLockTarget.IsTargetable || _currentLockTarget.AimTransform == null)
                {
                    _isHardLocked = false;
                    _currentLockTarget = FindBestTarget(def.LockOnRange);
                    _isHardLocked = _currentLockTarget != null;
                }
                else
                {
                    float dist = Vector3.Distance(transform.position, _currentLockTarget.AimTransform.position);
                    if (dist > def.LockOnBreakDistance)
                    {
                        _isHardLocked = false;
                        _currentLockTarget = null;
                    }
                }

                Runtime?.SetLockTarget(_currentLockTarget?.AimTransform, _isHardLocked);
                return;
            }

            if (def.AllowSoftLock)
            {
                ILockOnTarget soft = FindBestTarget(def.LockOnRange);
                _currentLockTarget = soft;
                Runtime?.SetLockTarget(soft?.AimTransform, false);
            }
            else
            {
                Runtime?.SetLockTarget(null, false);
            }
        }

        private ILockOnTarget FindBestTarget(float maxRange)
        {
            IReadOnlyList<ILockOnTarget> targets = LockOnTargetRegistry.ActiveTargets;
            ILockOnTarget best = null;
            int bestPriority = int.MinValue;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < targets.Count; i++)
            {
                ILockOnTarget candidate = targets[i];
                if (candidate == null || !candidate.IsTargetable || candidate.AimTransform == null)
                {
                    continue;
                }

                float distSq = (candidate.AimTransform.position - transform.position).sqrMagnitude;
                if (distSq > maxRange * maxRange)
                {
                    continue;
                }

                int priority = (int)candidate.Priority;
                if (priority > bestPriority || (priority == bestPriority && distSq < bestDistSq))
                {
                    best = candidate;
                    bestPriority = priority;
                    bestDistSq = distSq;
                }
            }

            if (best == null && primaryBossDirector != null && primaryBossDirector.IsAlive)
            {
                float dist = Vector3.Distance(transform.position, primaryBossDirector.transform.position);
                if (dist <= maxRange)
                {
                    LockOnTarget fallback = primaryBossDirector.GetComponent<LockOnTarget>();
                    if (fallback != null) return fallback;
                }
            }

            return best;
        }

        private void SwitchLockTarget(float directionSign)
        {
            IReadOnlyList<ILockOnTarget> targets = LockOnTargetRegistry.ActiveTargets;
            if (targets.Count <= 1 || _currentLockTarget == null || _currentLockTarget.AimTransform == null)
            {
                return;
            }

            Vector3 toCurrent = (_currentLockTarget.AimTransform.position - transform.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toCurrent).normalized;
            float maxRangeSq = RuntimeDefinition.LockOnRange * RuntimeDefinition.LockOnRange;

            ILockOnTarget best = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                ILockOnTarget candidate = targets[i];
                if (candidate == null || ReferenceEquals(candidate, _currentLockTarget) || !candidate.IsTargetable || candidate.AimTransform == null)
                {
                    continue;
                }

                Vector3 offset = candidate.AimTransform.position - transform.position;
                float distSq = offset.sqrMagnitude;
                if (distSq > maxRangeSq) continue;

                float side = Vector3.Dot(offset.normalized, right);
                if ((directionSign > 0f && side > 0.05f) || (directionSign < 0f && side < -0.05f))
                {
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = candidate;
                    }
                }
            }

            if (best != null)
            {
                _currentLockTarget = best;
                Runtime?.SetLockTarget(best.AimTransform, true);
            }
        }

        private void EvaluateActiveParryWindow()
        {
            BossDirector boss = ResolveActiveBossDirector();
            if (boss != null && boss.Blackboard != null && boss.Blackboard.IsParryWindowOpen)
            {
                float dist = Vector3.Distance(transform.position, boss.transform.position);
                if (dist <= RuntimeDefinition.ParryDeflectRadius * 1.5f && boss.TryParry())
                {
                    feedbackAdapter?.NotifyParrySuccess();
                    ParrySucceeded?.Invoke();
                    return;
                }
            }

            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 1f,
                RuntimeDefinition.ParryDeflectRadius,
                _parryOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _parryOverlapBuffer[i];
                if (col == null) continue;
                BossProjectile proj = col.GetComponentInParent<BossProjectile>();
                if (proj != null && proj.IsActive && proj.CanBeParried)
                {
                    Vector3 reflectDir = boss != null
                        ? (boss.transform.position - proj.transform.position).normalized
                        : transform.forward;
                    if (proj.TryDeflect(transform, reflectDir, DamageTeam.Player))
                    {
                        feedbackAdapter?.NotifyParrySuccess();
                        ParrySucceeded?.Invoke();
                        break;
                    }
                }
            }
        }

        private void EvaluateActivePerfectDodge()
        {
            BossDirector boss = ResolveActiveBossDirector();
            if (boss != null && boss.Blackboard != null && boss.Blackboard.IsPerfectDodgeWindowOpen)
            {
                if (boss.TryPerfectDodge())
                {
                    feedbackAdapter?.NotifyPerfectDodgeSuccess();
                    PerfectDodgeSucceeded?.Invoke();
                }
            }
        }

        private BossDirector ResolveActiveBossDirector()
        {
            if (primaryBossDirector != null)
            {
                return primaryBossDirector;
            }

            if (_currentLockTarget is LockOnTarget lockComponent && lockComponent.BossDirector != null)
            {
                return lockComponent.BossDirector;
            }

            if (Runtime?.Blackboard.LockedTargetTransform != null)
            {
                return Runtime.Blackboard.LockedTargetTransform.GetComponentInParent<BossDirector>();
            }

            return null;
        }

        private PlayerDefinition RuntimeDefinition
        {
            get
            {
                if (definition != null)
                {
                    return definition;
                }

                if (_runtimeFallbackDefinition == null)
                {
                    _runtimeFallbackDefinition = ScriptableObject.CreateInstance<PlayerDefinition>();
                    _runtimeFallbackDefinition.hideFlags = HideFlags.HideAndDontSave;
                }

                return _runtimeFallbackDefinition;
            }
        }

        private void CaptureInitialTransform()
        {
            if (_capturedInitialTransform) return;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _capturedInitialTransform = true;
        }

        private void BuildRuntime()
        {
            if (motor == null)
            {
                return;
            }

            UnbindRuntimeEvents();
            _inputBuffer = new PlayerInputBuffer();
            Runtime = new PlayerRuntime(RuntimeDefinition, motor, cameraTransform, _inputBuffer);
            Runtime.AttackHitboxOpened += OnAttackHitboxOpened;
            Runtime.AttackHitboxClosed += OnAttackHitboxClosed;
        }

        private void UnbindRuntimeEvents()
        {
            if (Runtime == null) return;
            Runtime.AttackHitboxOpened -= OnAttackHitboxOpened;
            Runtime.AttackHitboxClosed -= OnAttackHitboxClosed;
        }

        private void OnAttackHitboxOpened()
        {
            if (meleeHitbox != null) meleeHitbox.Activate();
        }

        private void OnAttackHitboxClosed()
        {
            if (meleeHitbox != null) meleeHitbox.Deactivate();
        }
    }
}
