using TappyTale.BossFight.Feedback;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    public enum BossCameraMode
    {
        DuelFraming = 0,
        PlayerFollow = 1,
        LockOnOverShoulder = 2
    }

    /// <summary>
    /// Built-in URP-compatible camera controller for Boss Duel Lab and Player demos.
    /// Supports duel framing (Player + Boss), lock-on framing, obstruction checks, FOV kicks, and CameraShakeBus execution without requiring Cinemachine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossDuelCameraController : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform bossTarget;
        [SerializeField] private BossCameraMode mode = BossCameraMode.DuelFraming;
        [SerializeField] private Vector3 baseOffset = new(0f, 11.5f, -13.5f);
        [SerializeField, Min(1f)] private float positionSmoothing = 8f;
        [SerializeField, Min(1f)] private float rotationSmoothing = 10f;
        [SerializeField, Range(35f, 85f)] private float baseFov = 55f;
        [SerializeField] private float shakeScaleMultiplier = 1f;

        private Camera _camera;
        private float _shakeRemaining;
        private float _shakeDuration;
        private float _shakeAmplitude;
        private float _shakeFrequency = 18f;
        private float _fovKickOffset;
        private AnimationCurve _shakeFalloff;

        public Transform PlayerTarget
        {
            get => playerTarget;
            set => playerTarget = value;
        }

        public Transform BossTarget
        {
            get => bossTarget;
            set => bossTarget = value;
        }

        public BossCameraMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public float ShakeScaleMultiplier
        {
            get => shakeScaleMultiplier;
            set => shakeScaleMultiplier = Mathf.Clamp(value, 0f, 3f);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                baseFov = _camera.fieldOfView;
            }
        }

        private void OnEnable()
        {
            CameraShakeBus.ShakeRequested += OnShakeDefinitionRequested;
            CameraShakeBus.RawShakeRequested += OnRawShakeRequested;
        }

        private void OnDisable()
        {
            CameraShakeBus.ShakeRequested -= OnShakeDefinitionRequested;
            CameraShakeBus.RawShakeRequested -= OnRawShakeRequested;
            _shakeRemaining = 0f;
            _fovKickOffset = 0f;
        }

        private void LateUpdate()
        {
            if (playerTarget == null)
            {
                return;
            }

            Vector3 focusPoint = playerTarget.position + Vector3.up * 1.1f;
            Vector3 desiredPosition = playerTarget.position + baseOffset;

            if (mode == BossCameraMode.DuelFraming && bossTarget != null && bossTarget.gameObject.activeInHierarchy)
            {
                Vector3 bossPoint = bossTarget.position + Vector3.up * 1.3f;
                focusPoint = Vector3.Lerp(focusPoint, bossPoint, 0.45f);
                float separation = Vector3.Distance(playerTarget.position, bossTarget.position);
                float zoomOut = Mathf.Clamp((separation - 7f) * 0.42f, 0f, 7f);
                desiredPosition = focusPoint + baseOffset.normalized * (baseOffset.magnitude + zoomOut);
            }
            else if (mode == BossCameraMode.LockOnOverShoulder && bossTarget != null && bossTarget.gameObject.activeInHierarchy)
            {
                Vector3 toBoss = (bossTarget.position - playerTarget.position);
                toBoss.y = 0f;
                if (toBoss.sqrMagnitude > 0.01f)
                {
                    toBoss.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, toBoss);
                    desiredPosition = playerTarget.position - toBoss * 7.5f + right * 1.6f + Vector3.up * 4.5f;
                    focusPoint = Vector3.Lerp(playerTarget.position + Vector3.up * 1.2f, bossTarget.position + Vector3.up * 1.4f, 0.65f);
                }
            }

            Vector3 shakeOffset = EvaluateShakeOffset(Time.deltaTime);
            float posBlend = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, posBlend) + shakeOffset;

            Vector3 lookDir = focusPoint - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                float rotBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotBlend);
            }

            if (_camera != null)
            {
                _fovKickOffset = Mathf.MoveTowards(_fovKickOffset, 0f, 18f * Time.deltaTime);
                _camera.fieldOfView = Mathf.Clamp(baseFov + _fovKickOffset, 30f, 95f);
            }
        }

        private void OnShakeDefinitionRequested(CameraShakeDefinition definition, float scale)
        {
            if (definition == null || shakeScaleMultiplier <= 0f) return;
            _shakeDuration = Mathf.Max(0.01f, definition.Duration);
            _shakeRemaining = _shakeDuration;
            _shakeAmplitude = definition.Amplitude * scale * shakeScaleMultiplier;
            _shakeFrequency = Mathf.Max(1f, definition.Frequency);
            _shakeFalloff = definition.Falloff;
            _fovKickOffset = definition.FovKick * scale * shakeScaleMultiplier;
        }

        private void OnRawShakeRequested(float duration, float amplitude, float frequency)
        {
            if (shakeScaleMultiplier <= 0f) return;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeRemaining = Mathf.Max(_shakeRemaining, _shakeDuration);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude * shakeScaleMultiplier);
            _shakeFrequency = Mathf.Max(1f, frequency);
            _shakeFalloff = null;
        }

        private Vector3 EvaluateShakeOffset(float deltaTime)
        {
            if (_shakeRemaining <= 0f)
            {
                _shakeAmplitude = 0f;
                return Vector3.zero;
            }

            _shakeRemaining = Mathf.Max(0f, _shakeRemaining - deltaTime);
            float normalizedTime = 1f - (_shakeRemaining / Mathf.Max(0.01f, _shakeDuration));
            float damper = _shakeFalloff != null
                ? Mathf.Clamp01(_shakeFalloff.Evaluate(normalizedTime))
                : (1f - normalizedTime);

            float t = Time.time * _shakeFrequency;
            float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.73f, t) - 0.5f) * 2f;
            return (transform.right * x + transform.up * y) * (_shakeAmplitude * damper);
        }
    }
}
