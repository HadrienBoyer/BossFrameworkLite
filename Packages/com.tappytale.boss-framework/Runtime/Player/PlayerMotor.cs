using UnityEngine;

namespace TappyTale.BossFight.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController characterController;

        public CharacterController CharacterController => characterController;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public Transform Body => transform;

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        public void Move(Vector3 velocity, float deltaTime)
        {
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(velocity * deltaTime);
            }
            else
            {
                transform.position += velocity * deltaTime;
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled) characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (wasEnabled) characterController.enabled = true;
        }

        public void Face(Vector3 direction, float sharpness, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, blend);
        }
    }
}
