using UnityEngine;

namespace TappyTale.NovaParis.VisualEffects
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TappyTale/Nova Paris/Visual Effects/Fullscreen Sprint Moisture Controller")]
    public sealed class NPFullscreenSprintMoistureController : MonoBehaviour
    {
        [Header("Sprint Detection")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private string forwardAxis = "Vertical";
        [SerializeField, Range(0f, 1f)] private float minimumForwardInput = 0.2f;
        [SerializeField] private bool requireGrounded = false;

        [Header("Moisture")]
        [SerializeField, Range(0f, 1f)] private float maxMoisture = 1f;
        [SerializeField, Min(0.01f)] private float buildUpSpeed = 0.85f;
        [SerializeField, Min(0.01f)] private float recoverySpeed = 5.5f;
        [SerializeField] private bool resetOnDisable = true;

        private float currentMoisture;

        public float CurrentMoisture => currentMoisture;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            ApplyGlobalMoisture();
        }

        private void Update()
        {
            bool sprinting = IsSprinting();
            float target = sprinting ? maxMoisture : 0f;
            float speed = sprinting ? buildUpSpeed : recoverySpeed;

            currentMoisture = Mathf.MoveTowards(
                currentMoisture,
                target,
                speed * Time.deltaTime
            );

            ApplyGlobalMoisture();
        }

        private void OnDisable()
        {
            if (!resetOnDisable)
                return;

            currentMoisture = 0f;
            ApplyGlobalMoisture();
        }

        private bool IsSprinting()
        {
            if (!Input.GetKey(sprintKey))
                return false;

            if (requireGrounded && characterController != null && !characterController.isGrounded)
                return false;

            float forward = 0f;

            if (!string.IsNullOrEmpty(forwardAxis))
                forward = Input.GetAxisRaw(forwardAxis);

            return forward > minimumForwardInput;
        }

        private void ApplyGlobalMoisture()
        {
            Shader.SetGlobalFloat(NPFullscreenCRTGlitchGlobals.Moisture, currentMoisture);
        }
    }
}
