using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    [DisallowMultipleComponent]
    public sealed class BossDashExecutor : MonoBehaviour
    {
        [SerializeField] private BossDirector director;
        [SerializeField] private CharacterController characterController;
        [SerializeField, Min(0f)] private float defaultDistance = 4f;
        [SerializeField, Min(0.02f)] private float defaultDuration = 0.18f;

        private BossRuntime _boundRuntime;
        private BossTimelineActionExecutor _executor;
        private BossTimelineRunner _runner;
        private Vector3 _dashVelocity;
        private float _dashRemaining;

        public bool IsDashing => _dashRemaining > 0f;

        private void Awake()
        {
            if (director == null) director = GetComponentInParent<BossDirector>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt += TryBind;
            }
            TryBind();
        }

        private void Update()
        {
            if (_boundRuntime != director?.Runtime)
            {
                TryBind();
            }

            if (_dashRemaining <= 0f)
            {
                return;
            }

            if (director != null && director.Runtime != null &&
                (!director.Runtime.IsRunning || director.Runtime.Health.IsDead || director.Runtime.ReactionWindows.IsStaggered))
            {
                CancelDash();
                return;
            }

            float step = Mathf.Min(Time.deltaTime, _dashRemaining);
            _dashRemaining -= step;
            Vector3 delta = _dashVelocity * step;

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(delta);
            }
            else
            {
                transform.position += delta;
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt -= TryBind;
            }
            Unbind();
            CancelDash();
        }

        public void CancelDash()
        {
            _dashRemaining = 0f;
            _dashVelocity = Vector3.zero;
        }

        private void TryBind()
        {
            Unbind();
            _boundRuntime = director != null ? director.Runtime : null;
            if (_boundRuntime == null)
            {
                return;
            }

            _executor = _boundRuntime.ActionExecutor;
            _runner = _boundRuntime.Timeline;
            if (_executor != null)
            {
                _executor.DashRequested += Dash;
            }

            if (_runner != null)
            {
                _runner.Cancelled += OnAttackCancelled;
            }

            _boundRuntime.Stopped += CancelDash;
        }

        private void Unbind()
        {
            if (_executor != null)
            {
                _executor.DashRequested -= Dash;
                _executor = null;
            }

            if (_runner != null)
            {
                _runner.Cancelled -= OnAttackCancelled;
                _runner = null;
            }

            if (_boundRuntime != null)
            {
                _boundRuntime.Stopped -= CancelDash;
                _boundRuntime = null;
            }
        }

        private void OnAttackCancelled(BossAttackDefinition _) => CancelDash();

        private void Dash(AttackTimelineEntry entry)
        {
            Vector3 direction = director != null && director.Blackboard != null
                ? director.Blackboard.DirectionToTarget
                : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = transform.forward;
            }

            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            float distance = entry.Value > 0f ? entry.Value : defaultDistance;
            float duration = entry.Duration > 0f ? entry.Duration : defaultDuration;

            if (duration <= 0.01f)
            {
                Vector3 motion = direction * distance;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(motion);
                }
                else
                {
                    transform.position += motion;
                }
                CancelDash();
                return;
            }

            _dashRemaining = duration;
            _dashVelocity = direction * (distance / duration);
        }
    }
}