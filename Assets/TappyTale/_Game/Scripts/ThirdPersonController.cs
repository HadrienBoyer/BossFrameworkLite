using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
    /// <summary>
    /// Main script for third-person movement using CharacterController.
    /// Movement, gravity, and rotation are handled in Update()
    /// to avoid camera jitter.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [MovedFrom(true, null, null, "ThirdPersonController")]
    public class ThirdPersonController : MonoBehaviour
    {
        PlayerHandler playerHandler;
        CharacterController cc;
        Animator animator;
        Camera mainCamera;

        [Header("Basic")]
        public float velocity = 5f;
        public float sprintAddition = 3.5f;
        public float jumpForce = 18f;
        public float jumpTime = 0.85f;
        public float gravity = 9.8f;

        [Header("Rotation")]
        public float rotationSpeed = 10f;
        public float aimRotationSpeed = 15f;

        [Header("Animator")]
        /// <summary>Animator layer index blended in while aiming (used for upper-body aim poses).</summary>
        public int aimLayerIndex = 1;

        [Header("Controls")]
        // Each input has a "canX" gate alongside its InputActionReference so a platform/mode
        // (e.g. cutscenes, joystick-only builds) can disable an action without unassigning it.
        public bool canMove = false;
        public InputActionReference move;
        public bool canCrouch = false;
        public InputActionReference crouch;
        public bool canJump = false;
        public InputActionReference jump;
        public bool canWalk = false;
        public InputActionReference walk;
        public bool canSprint = false;
        public InputActionReference sprint;
        public InputActionReference mouse;

        [Header("Joystick")]
        public bool useJoystick = false;
        public Joystick joystick;

        [Header("Aiming")]
        [SerializeField] private LayerMask raycastMask = ~0;
        /// <summary>GameObject moved to the aim raycast hit point while aiming (e.g. a reticle/target marker).</summary>
        [SerializeField] private GameObject target;

        [Header("Movement Smoothing")]
        /// <summary>When enabled, root motion drives the idle pose blend instead of code-driven animation.</summary>
        public bool allowIdleRootMotion = false;

        [SerializeField] private float moveValueSmoothTime = 0.1f;
        [SerializeField] private float velocityTransitionTime = 0.2f;

        [Header("Sprint Stamina")]
        public float fullSprintTime = 5f;
        /// <summary>UI element whose horizontal scale reflects remaining sprint stamina (0-1).</summary>
        public RectTransform sprintBarFill;
        float sprintStamina;
        bool sprintExhausted;

        // States
        bool isJumping;
        bool isWalking;
        bool isCrouching;
        [SerializeField] bool isAiming;
        [SerializeField] bool isAttacking;

        // Movement
        float jumpElapsedTime;
        float verticalVelocity;

        // Smooth movement values
        float currentMoveValue = 0f;
        float targetMoveValue = 0f;
        float currentVelocityMultiplier = 1f;
        float targetVelocityMultiplier = 1f;

        // Input
        /// <summary>Current horizontal movement input in [-1, 1] (from player input or <see cref="SetNavigationInput"/>).</summary>
        public float inputHorizontal;
        /// <summary>Current vertical movement input in [-1, 1] (from player input or <see cref="SetNavigationInput"/>).</summary>
        public float inputVertical;
        bool inputJump;
        bool inputWalk;
        bool inputSprint;
        bool inputCrouch;
        bool isSprinting;

        // Aiming
        Vector3 aimPos;

        // Animator params
        float animHorizontal;
        float animVertical;

        bool allowMovement = true;
        bool allowAimRotation = true;
        bool wasMovementDisabled = false;

        /// <summary>Whether movement (CharacterController.Move) is currently allowed.</summary>
        public bool AllowMovement { get => allowMovement; set => allowMovement = value; }
        /// <summary>Whether rotation toward the aim point or movement direction is currently allowed.</summary>
        public bool AllowAimRotation { get => allowAimRotation; set => allowAimRotation = value; }

        #region NAVMESH AGENTS RELATED
        [Header("Navigation Override")]
        [SerializeField] private bool isNavigationControlled = false;
        private float navigationHorizontal;
        private float navigationVertical;

        /// <summary>
        /// Feeds synthetic movement input (e.g. from a NavMeshAgent) in place of player input
        /// until <see cref="ClearNavigationInput"/> is called. See <see cref="NavMeshMovementController"/>.
        /// </summary>
        public void SetNavigationInput(float horizontal, float vertical)
        {
            isNavigationControlled = true;
            navigationHorizontal = Mathf.Clamp(horizontal, -1f, 1f);
            navigationVertical = Mathf.Clamp(vertical, -1f, 1f);
        }

        /// <summary>Stops using navigation-supplied input and returns control to normal player input.</summary>
        public void ClearNavigationInput()
        {
            isNavigationControlled = false;
            navigationHorizontal = 0f;
            navigationVertical = 0f;
        }

        #endregion

        private void Awake()
        {
            playerHandler = GetComponent<PlayerHandler>();
            cc = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            // Enable input actions when domain reload is disabled
            if (move != null) move.action.Enable();
            if (crouch != null) crouch.action.Enable();
            if (jump != null) jump.action.Enable();
            if (walk != null) walk.action.Enable();
            if (sprint != null) sprint.action.Enable();
            if (mouse != null) mouse.action.Enable();
        }

        void Start()
        {
            mainCamera = Camera.main;
            sprintStamina = fullSprintTime;

#if UNITY_ANDROID || UNITY_IOS
        useJoystick = true;
        if (joystick != null)
            joystick.gameObject.SetActive(true);
#endif
        }

        void Update()
        {
            if (GameManager.playMode == PlayMode.UIMODE || !AllowMovement)
            {
                inputHorizontal = 0f;
                inputVertical = 0f;

                animator.SetFloat("Move", 0f);
                animator.SetBool("Run", false);
                wasMovementDisabled = true;
                return;
            }

            // Reset smooth values when movement is re-enabled to prevent animation spike
            if (wasMovementDisabled)
            {
                currentMoveValue = 0f;
                currentVelocityMultiplier = 0f;
                animHorizontal = 0f;
                animVertical = 0f;
                wasMovementDisabled = false;
            }

            HandleAimingRaycast();

            ReadInput();
            HandleJumpRequest();
            HandleMovementAndRotation();
            AnimationHandling();
            HeadHittingDetect();

            // Update smooth movement values
            UpdateSmoothMovementValues();
        }

        void ReadInput()
        {
            if (isNavigationControlled)
            {
                // Use navigation input
                inputHorizontal = navigationHorizontal;
                inputVertical = navigationVertical;
            }
            else
            {
                if (canMove)
                {
                    Vector2 moveInput = move.action.ReadValue<Vector2>();
                    inputHorizontal = moveInput.x;
                    inputVertical = moveInput.y;

                    #region Clamp Input for Smooth Walking Transitions
                    if (inputHorizontal > inputVertical)
                    {

                        if (inputHorizontal > 0f && inputHorizontal <= 0.5f)
                            inputHorizontal = 0.5f;
                    }

                    if (inputVertical > inputHorizontal)
                    {
                        if (inputVertical > 0f && inputVertical <= 0.5f)
                            inputVertical = 0.5f;
                    }

                    // Handle negative values
                    if (Mathf.Abs(inputHorizontal) > Mathf.Abs(inputVertical))
                    {

                        if (inputHorizontal < 0f && inputHorizontal >= -0.5f)
                            inputHorizontal = -0.5f;
                    }

                    if (Mathf.Abs(inputVertical) > Mathf.Abs(inputHorizontal))
                    {
                        if (inputVertical < 0f && inputVertical >= -0.5f)
                            inputVertical = -0.5f;
                    }
                    #endregion
                }
                else
                {
                    inputHorizontal = 0f;
                    inputVertical = 0f;
                }
            }

            if (!allowMovement)
            {
                inputHorizontal = 0f;
                inputVertical = 0f;
            }

            // Calculate target move value based on input magnitude
            float inputMagnitude = new Vector2(inputHorizontal, inputVertical).magnitude;
            targetMoveValue = Mathf.Clamp01(inputMagnitude);

            // Snap to 0.5 or 1 based on input magnitude
            if (targetMoveValue > 0)
            {
                targetMoveValue = targetMoveValue <= 0.5f ? 0.5f : 1f;
            }

            if (canJump)
                inputJump = jump.action.WasPressedThisFrame();

            if (canWalk)
                inputWalk = walk.action.IsPressed();
            else
                inputWalk = false;

            isWalking = inputWalk;

            if (canSprint && sprint != null)
                inputSprint = sprint.action.IsPressed();
            else
                inputSprint = false;

            isSprinting = inputSprint && !isWalking && !isCrouching && !isAiming && !sprintExhausted;

            if (isSprinting)
            {
                sprintStamina -= Time.deltaTime;
                if (sprintStamina <= 0f)
                {
                    sprintStamina = 0f;
                    sprintExhausted = true;
                }
            }
            else
            {
                sprintStamina += Time.deltaTime;
                if (sprintStamina >= fullSprintTime)
                {
                    sprintStamina = fullSprintTime;
                    sprintExhausted = false;
                }
            }

            if (sprintBarFill != null)
                sprintBarFill.localScale = new Vector3(sprintStamina / fullSprintTime, 1f, 1f);

            if (canCrouch)
            {
                inputCrouch = crouch.action.WasPressedThisFrame();

                if (inputCrouch)
                    isCrouching = !isCrouching;
            }
        }

        void UpdateSmoothMovementValues()
        {
            // Smoothly interpolate move value
            currentMoveValue = Mathf.Lerp(currentMoveValue, targetMoveValue,
                Time.deltaTime / moveValueSmoothTime);

            // Calculate velocity multiplier based on current move value
            targetVelocityMultiplier = Mathf.Lerp(0f, 1f, currentMoveValue);

            // Smoothly interpolate velocity multiplier
            currentVelocityMultiplier = Mathf.Lerp(currentVelocityMultiplier, targetVelocityMultiplier,
                Time.deltaTime / velocityTransitionTime);

            // Ensure values don't get stuck at very small numbers
            if (targetMoveValue == 0f && currentMoveValue < 0.01f)
                currentMoveValue = 0f;

            if (targetVelocityMultiplier == 0f && currentVelocityMultiplier < 0.01f)
                currentVelocityMultiplier = 0f;
        }

        void HandleAimingRaycast()
        {
            if (!isAiming) return;

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastMask))
            {
                aimPos = hit.point;
                if (target != null)
                    target.transform.position = hit.point;
            }
        }

        void HandleJumpRequest()
        {
            if (inputJump && cc.isGrounded && !isCrouching)
            {
                isJumping = true;
                jumpElapsedTime = 0f;
            }
        }

        void HandleMovementAndRotation()
        {
            float velocityAddition = 0f;

            if (isSprinting)
                velocityAddition = sprintAddition;
            else if (isWalking)
                velocityAddition = -(velocity * 0.5f);

            if (isCrouching)
                velocityAddition = -(velocity * 0.75f);
            else if (isAiming)
                velocityAddition = -(velocity * 0.5f);

            Vector3 movementDirection;

            if (isAiming)
            {
                movementDirection = transform.forward * inputVertical + transform.right * inputHorizontal;
            }
            else
            {
                Vector3 forward = mainCamera.transform.forward;
                Vector3 right = mainCamera.transform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                movementDirection = forward * inputVertical + right * inputHorizontal;
            }

            if (movementDirection.magnitude > 1f)
                movementDirection.Normalize();

            // Apply smooth velocity multiplier
            float effectiveVelocity = (velocity + velocityAddition) * currentVelocityMultiplier;
            movementDirection *= effectiveVelocity;

            // Vertical movement
            if (cc.isGrounded)
            {
                if (!isJumping)
                    verticalVelocity = -2f;
            }

            if (isJumping)
            {
                verticalVelocity = Mathf.SmoothStep(
                    jumpForce,
                    jumpForce * 0.3f,
                    jumpElapsedTime / jumpTime
                );
                jumpElapsedTime += Time.deltaTime;

                if (jumpElapsedTime >= jumpTime)
                    isJumping = false;
            }

            verticalVelocity -= gravity * Time.deltaTime;

            // Rotation
            if (allowAimRotation)
            {
                if (isAiming)
                {
                    Vector3 dir = aimPos - transform.position;
                    dir.y = 0f;

                    if (dir != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir);
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            targetRot,
                            aimRotationSpeed * Time.deltaTime
                        );
                    }
                }
                else if (movementDirection.sqrMagnitude > 0.001f && currentMoveValue > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(movementDirection);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        rotationSpeed * Time.deltaTime
                    );
                }
            }

            if (allowMovement)
            {
                Vector3 finalMove =
                    movementDirection * Time.deltaTime +
                    Vector3.up * verticalVelocity * Time.deltaTime;

                cc.Move(finalMove);
            }
        }

        void AnimationHandling()
        {
            Vector3 relativeMovement;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            relativeMovement = camForward * inputVertical + camRight * inputHorizontal;
            relativeMovement = transform.InverseTransformDirection(relativeMovement);

            if (cc.isGrounded)
            {
                // Use the smoothed move value instead of raw input
                float relativeMagnitude = relativeMovement.magnitude;
                float smoothedMove = currentMoveValue;
                float animSmoothingFactor = Time.deltaTime / moveValueSmoothTime;

                // If there's input, use the smoothed value, otherwise use 0
                if (relativeMagnitude > 0.01f)
                {
                    // Apply smoothing to animator parameters based on currentMoveValue
                    animHorizontal = Mathf.Lerp(animHorizontal, relativeMovement.x * currentMoveValue, animSmoothingFactor);
                    animVertical = Mathf.Lerp(animVertical, relativeMovement.z * currentMoveValue, animSmoothingFactor);
                }
                else
                {
                    // Smoothly return to zero when no input
                    animHorizontal = Mathf.Lerp(animHorizontal, 0f, animSmoothingFactor);
                    animVertical = Mathf.Lerp(animVertical, 0f, animSmoothingFactor);
                }

                // Snap to zero when values are negligible to prevent drift
                if (Mathf.Abs(animHorizontal) < 0.001f) animHorizontal = 0f;
                if (Mathf.Abs(animVertical) < 0.001f) animVertical = 0f;

                float moveMagnitude = Mathf.Max(Mathf.Abs(animHorizontal), Mathf.Abs(animVertical));

                // Clamp animation to 0.5 when walking
                if (isWalking)
                    moveMagnitude = Mathf.Min(moveMagnitude, 0.5f);

                if (isSprinting && moveMagnitude > 0.01f)
                    moveMagnitude = 2f;

                animator.SetFloat("Move", moveMagnitude);
                animator.SetBool("Crouch", isCrouching);

                if (allowIdleRootMotion)
                    animator.applyRootMotion = moveMagnitude < 0.01f;

                animator.SetBool("Run", isSprinting);
            }

            animator.SetBool("Air", !cc.isGrounded);
        }

        void HeadHittingDetect()
        {
            float headHitDistance = 1.1f;
            Vector3 ccCenter = transform.TransformPoint(cc.center);
            float hitCalc = cc.height / 2f * headHitDistance;

            if (Physics.Raycast(ccCenter, Vector3.up, hitCalc))
            {
                jumpElapsedTime = 0f;
                isJumping = false;
            }
        }

        /// <summary>Enables or disables both movement and aim rotation in one call.</summary>
        public void MovementStatus(bool value)
        {
            AllowMovement = value;
            AllowAimRotation = value;
        }

        /// <summary>Directly sets the Animator's "Move" float, bypassing the normal input-driven blending.</summary>
        public void ForceMoveAnim(float _val)
        {

            animator.SetFloat("Move", _val);
        }

        /// <summary>Directly sets the Animator's "Fall_Forward" bool.</summary>
        public void Force_SetFrontFall(bool _val)
        {

            animator.SetBool("Fall_Forward", _val);
        }

        /// <summary>Directly sets the Animator's "Fall_Back" bool.</summary>
        public void Force_SetBackFall(bool _val)
        {

            animator.SetBool("Fall_Back", _val);
        }

        /// <summary>Overrides the move-value and velocity-transition smoothing times used by <see cref="Update"/>.</summary>
        public void SetSmoothTimes(float moveSmoothTime, float velocitySmoothTime)
        {
            moveValueSmoothTime = Mathf.Max(0.01f, moveSmoothTime);
            velocityTransitionTime = Mathf.Max(0.01f, velocitySmoothTime);
        }

        #region FALL FUNCTIONS
        /// <summary>
        /// Triggers the fall back animation and disables player movement.
        /// Moves backwards by 2 units over 0.5 seconds, then waits for delay.
        /// Can be called from Inspector.
        /// </summary>
        public void Fall_Back(float delay = 1f)
        {
            StartCoroutine(FallBackCoroutine(delay));
        }

        /// <summary>
        /// Triggers the fall forward animation and disables player movement.
        /// Moves forward by 2 units over 0.5 seconds, then waits for delay.
        /// Can be called from Inspector.
        /// </summary>
        public void Fall_Forward(float delay = 1f)
        {
            StartCoroutine(FallForwardCoroutine(delay));
        }

        private IEnumerator FallBackCoroutine(float delay = 1f)
        {
            // Set the Fall_Back animation
            animator.SetBool("Fall_Back", true);

            // Disable movement
            AllowMovement = false;

            // Apply backward movement over 0.5 seconds (negative forward = backward)
            float movementDuration = 1f;
            float elapsedTime = 0f;
            Vector3 totalMovement = transform.forward * -2f;  // Negative = backward

            while (elapsedTime < movementDuration)
            {
                elapsedTime += Time.deltaTime;
                Vector3 frameMovement = totalMovement * (Time.deltaTime / movementDuration);
                cc.Move(frameMovement);
                yield return null;
            }

            yield return new WaitForSeconds(delay);

            // Reset the Fall_Back animation
            animator.SetBool("Fall_Back", false);

            yield return new WaitForSeconds(3);
            ForceMoveAnim(0f);

            // Re-enable movement
            AllowMovement = true;
        }

        private IEnumerator FallForwardCoroutine(float delay = 1f)
        {
            // Set the Fall_Forward animation
            animator.SetBool("Fall_Forward", true);

            // Disable movement
            AllowMovement = false;

            // Apply forward movement over 0.5 seconds
            float movementDuration = 0.5f;
            float elapsedTime = 0f;
            Vector3 totalMovement = transform.forward * 2f;  // Positive = forward

            while (elapsedTime < movementDuration)
            {
                elapsedTime += Time.deltaTime;
                Vector3 frameMovement = totalMovement * (Time.deltaTime / movementDuration);
                cc.Move(frameMovement);
                yield return null;
            }

            yield return new WaitForSeconds(delay);

            // Reset the Fall_Forward animation
            animator.SetBool("Fall_Forward", false);

            yield return new WaitForSeconds(3);
            ForceMoveAnim(0f);

            // Re-enable movement
            AllowMovement = true;
        }

        /// <summary>
        /// Triggers the fall back animation without getting up automatically.
        /// Moves backwards by 2 units over 0.5 seconds, then waits for delay.
        /// Call GetUp_Back() separately to stand up.
        /// </summary>
        public void Fall_Back_NoGetUp(float delay = 1f)
        {
            StartCoroutine(FallBackNoGetUpCoroutine(delay));
        }

        /// <summary>
        /// Triggers the fall forward animation without getting up automatically.
        /// Moves forward by 2 units over 0.5 seconds, then waits for delay.
        /// Call GetUp_Forward() separately to stand up.
        /// </summary>
        public void Fall_Forward_NoGetUp(float delay = 1f)
        {
            StartCoroutine(FallForwardNoGetUpCoroutine(delay));
        }

        /// <summary>
        /// Gets up from a fall back animation.
        /// Resets the Fall_Back animation and re-enables movement after 3 seconds.
        /// </summary>
        public void GetUp_Back()
        {
            StartCoroutine(GetUpBackCoroutine());
        }

        /// <summary>
        /// Gets up from a fall forward animation.
        /// Resets the Fall_Forward animation and re-enables movement after 3 seconds.
        /// </summary>
        public void GetUp_Forward()
        {
            StartCoroutine(GetUpForwardCoroutine());
        }

        private IEnumerator FallBackNoGetUpCoroutine(float delay = 1f)
        {
            // Set the Fall_Back animation
            animator.SetBool("Fall_Back", true);

            // Disable movement
            AllowMovement = false;

            // Apply backward movement over 0.5 seconds (negative forward = backward)
            float movementDuration = 1f;
            float elapsedTime = 0f;
            Vector3 totalMovement = transform.forward * -2f;  // Negative = backward

            while (elapsedTime < movementDuration)
            {
                elapsedTime += Time.deltaTime;
                Vector3 frameMovement = totalMovement * (Time.deltaTime / movementDuration);
                cc.Move(frameMovement);
                yield return null;
            }

            yield return new WaitForSeconds(delay);
        }

        private IEnumerator FallForwardNoGetUpCoroutine(float delay = 1f)
        {
            // Set the Fall_Forward animation
            animator.SetBool("Fall_Forward", true);

            // Disable movement
            AllowMovement = false;

            // Apply forward movement over 0.5 seconds
            float movementDuration = 0.5f;
            float elapsedTime = 0f;
            Vector3 totalMovement = transform.forward * 2f;  // Positive = forward

            while (elapsedTime < movementDuration)
            {
                elapsedTime += Time.deltaTime;
                Vector3 frameMovement = totalMovement * (Time.deltaTime / movementDuration);
                cc.Move(frameMovement);
                yield return null;
            }

            yield return new WaitForSeconds(delay);
        }

        private IEnumerator GetUpBackCoroutine()
        {
            yield return new WaitForSeconds(3);

            // Reset the Fall_Back animation
            animator.SetBool("Fall_Back", false);
            ForceMoveAnim(0f);

            // Re-enable movement
            AllowMovement = true;
        }

        private IEnumerator GetUpForwardCoroutine()
        {
            yield return new WaitForSeconds(3);

            // Reset the Fall_Forward animation
            animator.SetBool("Fall_Forward", false);
            ForceMoveAnim(0f);

            yield return new WaitForSeconds(2);
            // Re-enable movement
            AllowMovement = true;
        }
        #endregion
    }
}
