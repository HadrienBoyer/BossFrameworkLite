using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;
using MSJTDCC.Tweening;

namespace MSJTDCC
{
/// <summary>
/// Lets the player drag-rotate and pinch/scroll-zoom this object (e.g. for item inspection), driven by
/// mouse, touch, or Input System actions, with an initial "present" tween via <see cref="StartAnim"/>.
/// </summary>
[MovedFrom(true, null, null, "ObjectRotator")]
public class ObjectRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float mobileRotationSpeed = 0.2f;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;
    /// <summary>If true (and <see cref="toggleOnClick"/> is false), rotation only applies while the click/touch is held.</summary>
    [SerializeField] private bool requireClick = true;
    /// <summary>If true, a click toggles rotation on/off instead of requiring it to be held.</summary>
    [SerializeField] private bool toggleOnClick = false;
    /// <summary>Set automatically on <see cref="Awake"/> from <see cref="Application.isMobilePlatform"/>; switches input handling to touch.</summary>
    [SerializeField] private bool mobileInputEnabled = false;
    private bool rotationEnabled = true;

    [Header("Clamp Settings (Optional)")]
    [SerializeField] private bool clampVerticalRotation = true;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Camera Settings")]
    /// <summary>Camera used to orient drag rotation relative to the view; defaults to <see cref="Camera.main"/> if unset.</summary>
    [SerializeField] private Camera referenceCamera;

    // Input references
    public InputActionReference rotateInput;
    public InputActionReference lookRotateInput;
    public InputActionReference clickInput;
    public InputActionReference zoomInput;

    // Rotation values
    private Vector2 currentMouseDelta = Vector2.zero;
    private Vector2 rotationSmoothVelocity;
    private float xRotation = 0f;
    private float yRotation = 0f;

    private bool enableInput = false;

    /// <summary>Seconds the <see cref="StartAnim"/> intro takes to scale up and rotate upright.</summary>
    private const float IntroDuration = 1f;
    private Coroutine introRoutine;
    private readonly List<TweenTrack> introTracks = new List<TweenTrack>();

        // Zoom (scale) settings
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float minScale = 0.1f;
    [SerializeField] private float maxScale = 3f;
    [SerializeField] private Transform zoomTargetParent; // optional; if null uses this transform

    /// <summary>Resets rotation and scale to identity/one.</summary>
    public void ResetRotation()
    {
        xRotation = 0f;
        yRotation = 0f;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    /// <summary>Overrides the camera used to orient drag rotation.</summary>
    public void SetReferenceCamera(Camera cam)
    {
        if (cam != null)
        {
            referenceCamera = cam;
        }
    }

    void Awake()
    {
        if (Application.isMobilePlatform)
            mobileInputEnabled = true;

        if(rotateInput != null) rotateInput.action.Enable();
        if (lookRotateInput != null) lookRotateInput.action.Enable();
        if (clickInput != null) clickInput.action.Enable();
        if (zoomInput != null) zoomInput.action.Enable();

        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    /// <summary>Plays an intro tween: scales up from half size and rotates upright from -90° pitch, disabling input until it completes.</summary>
    public void StartAnim()
    {
        enableInput = false;

        StopAnim();

        Vector3 startScale = Vector3.one * 0.5f;
        transform.localScale = startScale;
        xRotation = -90f;
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);

        if (!isActiveAndEnabled)
            return;

        introTracks.Clear();
        introTracks.Add(TweenTrack.Scale(transform, startScale, Vector3.one, IntroDuration, 0f, Ease.OutQuad));
        introTracks.Add(TweenTrack.Float(xRotation, 0f, SetVerticalRotation, IntroDuration, 0f, Ease.OutQuad));

        introRoutine = StartCoroutine(TweenGroup.Run(introTracks, 0f, false, 1, LoopType.Restart, OnIntroComplete));
    }

    /// <summary>Stops the <see cref="StartAnim"/> intro tween if it is still running.</summary>
    public void StopAnim()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }
    }

    private void SetVerticalRotation(float value)
    {
        xRotation = value;
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }

    private void OnIntroComplete()
    {
        introRoutine = null;
        enableInput = true;
    }

    private void OnDestroy()
    {
        StopAnim();
    }

    void Update()
    {
        if(!enableInput) return;

        HandleRotation();
        HandleZoom();

    }
    
    void HandleRotation()
    {
        bool allowRotation = true;

        bool clickPressed = false;
        bool clickHeld = false;

        if (mobileInputEnabled)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                // Block rotation when two fingers are active (pinch-to-zoom)
                bool twoFingers = touchscreen.touches.Count >= 2 &&
                                  touchscreen.touches[0].press.isPressed &&
                                  touchscreen.touches[1].press.isPressed;
                if (twoFingers) return;

                var touch = touchscreen.primaryTouch;
                clickPressed = touch.press.wasPressedThisFrame;
                clickHeld = touch.press.isPressed;
            }
        }

        if (!clickHeld)
        {
            if (clickInput != null)
            {
                clickPressed |= clickInput.action.triggered;
                clickHeld = clickInput.action.ReadValue<float>() > 0.5f;
            }
            else if (!mobileInputEnabled)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    clickPressed = mouse.leftButton.wasPressedThisFrame;
                    clickHeld = mouse.leftButton.isPressed;
                }
            }
        }

        if (toggleOnClick && clickPressed)
        {
            rotationEnabled = !rotationEnabled;
        }

        if (toggleOnClick)
        {
            allowRotation = rotationEnabled;
        }
        else if (requireClick)
        {
            allowRotation = clickHeld;
        }

        if (allowRotation)
        {
            bool useLook = PlayerPrefs.GetInt("lookRotateInput", 0) == 1;
            InputActionReference activeRotate = (useLook && lookRotateInput != null) ? lookRotateInput : rotateInput;
            Vector2 mouseDelta = Vector2.zero;

            if (mobileInputEnabled)
            {
                var touchscreen = Touchscreen.current;
                if (touchscreen != null)
                {
                    var touch = touchscreen.primaryTouch;
                    if (touch.press.isPressed)
                    {
                        mouseDelta = touch.delta.ReadValue();
                    }
                }
            }

            if (mouseDelta == Vector2.zero)
            {
                if (activeRotate != null)
                {
                    mouseDelta = activeRotate.action.ReadValue<Vector2>();
                }
                else if (Mouse.current != null)
                {
                    mouseDelta = Mouse.current.delta.ReadValue();
                }
            }

            // Apply smoothing
            currentMouseDelta = Vector2.SmoothDamp(
                currentMouseDelta, 
                mouseDelta, 
                ref rotationSmoothVelocity, 
                smoothTime
            );
            
            // Calculate rotation amounts
            float speed = mobileInputEnabled ? mobileRotationSpeed : rotationSpeed;
            float mouseX = currentMouseDelta.x * speed * Time.deltaTime;
            float mouseY = currentMouseDelta.y * speed * Time.deltaTime;
            
            // Apply inversion
            mouseX *= invertX ? -1f : 1f;
            mouseY *= invertY ? -1f : 1f;
            
            // Rotate relative to camera
            if (referenceCamera != null)
            {
                // Get camera's right and up vectors
                Vector3 cameraRight = referenceCamera.transform.right;
                Vector3 cameraUp = Vector3.up; // Use world up for horizontal rotation

                // Apply rotations relative to camera orientation
                transform.Rotate(cameraUp, mouseX, Space.World);

                // xRotation tracks accumulated vertical rotation since the last ResetRotation(),
                // so it can be clamped the same way as the no-camera fallback below.
                float verticalDelta = -mouseY;
                if (clampVerticalRotation)
                {
                    float previousX = xRotation;
                    xRotation = Mathf.Clamp(xRotation + verticalDelta, minVerticalAngle, maxVerticalAngle);
                    verticalDelta = xRotation - previousX;
                }
                else
                {
                    xRotation += verticalDelta;
                }
                transform.Rotate(cameraRight, verticalDelta, Space.World);
            }
            else
            {
                // Fallback to old behavior if no camera
                yRotation += mouseX;
                xRotation -= mouseY;
                if (clampVerticalRotation)
                    xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);
                transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            }
        }
        else if (referenceCamera == null)
        {
            // Apply rotation to object (fallback for no camera)
            transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
    }
    
    void HandleZoom()
    {
        float zoomValue = 0f;

        if (mobileInputEnabled)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                if (touches.Count >= 2)
                {
                    var touch1 = touches[0];
                    var touch2 = touches[1];

                    if (touch1.press.isPressed && touch2.press.isPressed)
                    {
                        Vector2 currentPos1 = touch1.position.ReadValue();
                        Vector2 currentPos2 = touch2.position.ReadValue();
                        Vector2 prevPos1 = currentPos1 - touch1.delta.ReadValue();
                        Vector2 prevPos2 = currentPos2 - touch2.delta.ReadValue();

                        float currentDistance = Vector2.Distance(currentPos1, currentPos2);
                        float previousDistance = Vector2.Distance(prevPos1, prevPos2);
                        zoomValue = currentDistance - previousDistance;
                    }
                }
            }
        }

        if (Mathf.Approximately(zoomValue, 0f))
        {
            if (zoomInput != null)
            {
                zoomValue = zoomInput.action.ReadValue<float>();
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    zoomValue = mouse.scroll.ReadValue().y;
                }
            }
        }

        if (Mathf.Approximately(zoomValue, 0f)) return;

        Transform target = zoomTargetParent != null ? zoomTargetParent : transform;

        float delta = zoomValue * zoomSpeed * Time.deltaTime;
        Vector3 scale = target.localScale;
        float uniform = Mathf.Clamp(scale.x + delta, minScale, maxScale);
        target.localScale = new Vector3(uniform, uniform, uniform);
    }

}
}