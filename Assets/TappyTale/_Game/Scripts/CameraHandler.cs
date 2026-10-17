using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Third-person follow camera: smoothed distance/offset follow, scroll/pinch zoom,
/// wall collision + avoidance, screen shake/impulse, cutscenes, and a world-inspect mode.
/// </summary>
[MovedFrom(true, null, null, "CameraHandler")]
public class CameraHandler : MonoBehaviour
{
    /// <summary>The transform the camera follows. Set via <see cref="SetTarget"/>.</summary>
    [SerializeField] private Transform target;

    [Header("Starting Distance from Target")]
    /// <summary>Camera position offset from the target used only for the initial placement in <see cref="Start"/>/<see cref="SetTarget"/>.</summary>
    public Vector3 initialOffset = new Vector3(0f, 2f, -8f);

    [Header("Distance")]
    [SerializeField] private float distance = 8f;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothSpeed = 8f;

    [Header("Zoom")]
    /// <summary>When true, zoom follows <see cref="zoomAction"/> input; when false, distance is driven toward <see cref="forcedZoomValue"/> instead.</summary>
    public bool allowZoom = true;
    [SerializeField] private float curZoomVal = 2f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 15f;
    /// <summary>Target distance used while <see cref="allowZoom"/> is false (set via <see cref="ForceZoomValue"/>/<see cref="ForceZoomFar"/>).</summary>
    [SerializeField] private float forcedZoomValue = 8f;
    [SerializeField] private InputActionReference zoomAction; // float axis (e.g. scroll wheel, pinch)

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers; // Layers that block camera
    [SerializeField] private float collisionBuffer = 0.2f; // Small offset so camera doesn't sit exactly on wall

    [Header("Wall Avoidance")]
    [SerializeField] private bool enableWallAvoidance = true;
    [SerializeField] private float wallDetectionDistance = 2f; // How far to raycast for wall detection
    [SerializeField] private float wallAvoidanceOffset = 1f; // How much to move camera when near wall
    [SerializeField] private float wallAvoidanceSmoothTime = 0.2f; // Smoothing for wall avoidance offset
    [SerializeField] private LayerMask wallLayers; // Layers to detect as walls

    [Header("Camera Shake")]
    [SerializeField] private float shakeFrequency = 25f;
    [SerializeField] private float traumaDecay = 1.5f;
    [SerializeField] private float maxShakeRotation = 5f;
    [SerializeField] private float maxShakePosition = 0.3f;

    [Header("World Inspect")]
    /// <summary>Seconds for the camera to transition to/from an inspected object via <see cref="WorldCameraInspect"/>.</summary>
    public float inspectDuration = 1f;

    [Header("CutScene")]
    [SerializeField] private float cutSceneTransitionDuration = 2f;

    private Vector3 positionVelocity;
    private Vector3 followDirection;
    private float trauma = 0f;
    private Vector3 shakeOffset;
    private Vector3 impulseOffset;
    private bool isImpulsing = false;
    bool isFirst = true;
    private Vector3 wallAvoidanceCurrentOffset;
    private Vector3 wallAvoidanceVelocity;

    private bool isInspecting = false;
    private Coroutine inspectCoroutine;
    private Vector3 savedFollowDirection;
    private float savedDistance;
    private Vector3 savedOffset;
    private Vector3 playerPosAtInspect;

    // CutScene variables
    private bool isInCutScene = false;
    private Coroutine cutSceneCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine impulseCoroutine;
    private Vector3 cutSceneSavedFollowDirection;
    private float cutSceneSavedDistance;
    private Vector3 cutSceneSavedOffset;
    private Vector3 cutSceneSavedCameraPos;
    private Quaternion cutSceneSavedCameraRot;
    private float cutSceneDuration = 2f;

    private void OnEnable()
    {
        if (zoomAction != null) zoomAction.action.Enable();
    }

    private void Start()
    {
        forcedZoomValue = Mathf.Clamp(distance, minDistance, maxDistance);

        if (!target) return;

        transform.position = target.position + initialOffset;
        transform.LookAt(target.position);

        // Cache initial direction ONCE
        followDirection = (transform.position - target.position).normalized;

        SetDefaultResumeZoom();
    }

    /// <summary>Sets the follow target. On the first call, also snaps the camera to <see cref="initialOffset"/> from it.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target && isFirst)
        {
            transform.position = target.position + initialOffset;
            transform.LookAt(target.position);
            followDirection = (transform.position - target.position).normalized;
            isFirst = false;
        }
    }

    private void OnDisable()
    {
        if (zoomAction != null) zoomAction.action.Disable();
    }

    private void LateUpdate()
    {
        if (!target) return;
        if (isInspecting) return;
        if (isInCutScene) return;

        HandleZoom();
        UpdateShake();

        // Calculate wall avoidance offset
        Vector3 wallAvoidanceOffset = CalculateWallAvoidance();

        // Desired position based on distance
        Vector3 desiredPosition = target.position + offset + followDirection * distance + wallAvoidanceOffset;

        // Check for collision between target and desired camera position
        RaycastHit hit;
        if (Physics.Raycast(target.position + offset, followDirection, out hit, distance, collisionLayers))
        {
            // If hit, move camera to hit point minus buffer
            float adjustedDistance = hit.distance - collisionBuffer;
            desiredPosition = target.position + offset + followDirection * Mathf.Max(adjustedDistance, minDistance);
        }

        // Apply shake and impulse offsets
        desiredPosition += shakeOffset + impulseOffset;

        // Smooth movement
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        // Smooth rotation with shake
        Quaternion targetRotation = Quaternion.LookRotation(
            target.position - transform.position
        );

        // Apply shake rotation
        float shake = trauma * trauma;
        float rotX = maxShakeRotation * shake * Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) * 2f - maxShakeRotation * shake;
        float rotY = maxShakeRotation * shake * Mathf.PerlinNoise(0f, Time.time * shakeFrequency) * 2f - maxShakeRotation * shake;
        float rotZ = maxShakeRotation * shake * Mathf.PerlinNoise(Time.time * shakeFrequency, Time.time * shakeFrequency) * 2f - maxShakeRotation * shake;

        targetRotation *= Quaternion.Euler(rotX, rotY, rotZ);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    /// <summary>Forces the zoom out toward <see cref="maxDistance"/>, then re-enables zoom input after a 2 second delay.</summary>
    [ContextMenu("Force Zoom Default")]
    public void SetDefaultResumeZoom()
    {
        forcedZoomValue = maxDistance - 1;

        Invoke(nameof(AllowZoomInput), 2f);
    }

    /// <summary>Re-enables player zoom input (see <see cref="allowZoom"/>).</summary>
    public void AllowZoomInput()
    {
        allowZoom = true;
    }

    /// <summary>Disables player zoom input; distance will hold at whatever <see cref="forcedZoomValue"/> is.</summary>
    public void DisableZoomInput()
    {
        allowZoom = false;
    }

    /// <summary>Disables zoom input and smoothly moves the camera distance to the given value (clamped to min/max).</summary>
    public void ForceZoomValue(float value)
    {
        allowZoom = false;
        forcedZoomValue = Mathf.Clamp(value, minDistance, maxDistance);
    }

    /// <summary>Disables zoom input and moves the camera distance out to <see cref="maxDistance"/>.</summary>
    [ContextMenu("Force Zoom Far")]
    public void ForceZoomFar()
    {
        allowZoom = false;
        forcedZoomValue = Mathf.Clamp(maxDistance, minDistance, maxDistance);
    }

    private void HandleZoom()
    {
        if(GameManager.playMode == PlayMode.UIMODE) return; // Disable zoom in UI mode

        if (allowZoom)
        {
            curZoomVal = zoomAction != null ? zoomAction.action.ReadValue<float>() : 0f;
            distance -= curZoomVal * zoomSpeed * Time.deltaTime;
        }
        else
        {
            distance = Mathf.MoveTowards(distance, forcedZoomValue, zoomSpeed * Time.deltaTime);
        }

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private void UpdateShake()
    {
        if (trauma > 0f)
        {
            trauma -= traumaDecay * Time.deltaTime;
            trauma = Mathf.Clamp01(trauma);

            float shake = trauma * trauma;
            shakeOffset.x = maxShakePosition * shake * (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) * 2f - 1f);
            shakeOffset.y = maxShakePosition * shake * (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) * 2f - 1f);
            shakeOffset.z = maxShakePosition * shake * (Mathf.PerlinNoise(Time.time * shakeFrequency, Time.time * shakeFrequency) * 2f - 1f);
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
    }

    private Vector3 CalculateWallAvoidance()
    {
        if (!enableWallAvoidance || !target)
        {
            wallAvoidanceCurrentOffset = Vector3.SmoothDamp(wallAvoidanceCurrentOffset, Vector3.zero, ref wallAvoidanceVelocity, wallAvoidanceSmoothTime);
            return wallAvoidanceCurrentOffset;
        }

        Vector3 cameraRight = transform.right;
        Vector3 rayOrigin = transform.position;
        Vector3 targetOffset = Vector3.zero;

        bool hitRight = false;
        bool hitLeft = false;

        // Raycast to the right
        RaycastHit rightHit;
        if (Physics.Raycast(rayOrigin, cameraRight, out rightHit, wallDetectionDistance, wallLayers))
        {
            hitRight = true;
            Debug.DrawRay(rayOrigin, cameraRight * rightHit.distance, Color.red);
        }
        else
        {
            Debug.DrawRay(rayOrigin, cameraRight * wallDetectionDistance, Color.green);
        }

        // Raycast to the left
        RaycastHit leftHit;
        if (Physics.Raycast(rayOrigin, -cameraRight, out leftHit, wallDetectionDistance, wallLayers))
        {
            hitLeft = true;
            Debug.DrawRay(rayOrigin, -cameraRight * leftHit.distance, Color.red);
        }
        else
        {
            Debug.DrawRay(rayOrigin, -cameraRight * wallDetectionDistance, Color.green);
        }

        // Apply offset proportional to proximity so there's no binary snap that causes oscillation.
        // At wallDetectionDistance the strength is 0; at distance 0 it's full wallAvoidanceOffset.
        float rightStrength = hitRight ? (1f - rightHit.distance / wallDetectionDistance) : 0f;
        float leftStrength  = hitLeft  ? (1f - leftHit.distance  / wallDetectionDistance) : 0f;

        if (hitRight && !hitLeft)
        {
            targetOffset = -cameraRight * (wallAvoidanceOffset * rightStrength);
        }
        else if (hitLeft && !hitRight)
        {
            targetOffset = cameraRight * (wallAvoidanceOffset * leftStrength);
        }
        else if (hitRight && hitLeft)
        {
            // Net push away from whichever wall is closer
            targetOffset = cameraRight * (wallAvoidanceOffset * (leftStrength - rightStrength));
        }

        // Smooth the offset transition
        wallAvoidanceCurrentOffset = Vector3.SmoothDamp(wallAvoidanceCurrentOffset, targetOffset, ref wallAvoidanceVelocity, wallAvoidanceSmoothTime);
        return wallAvoidanceCurrentOffset;
    }

    /// <summary>Starts a decaying screen-shake effect that ramps trauma down from <paramref name="intensity"/> over <paramref name="duration"/> seconds.</summary>
    public void ShakeCamera(float intensity, float duration)
    {
        if (isInCutScene) return; // CutScene has priority
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
    }

    /// <summary>Applies a one-shot positional kick in <paramref name="direction"/> that eases back to zero over <paramref name="returnTime"/> seconds.</summary>
    public void ImpulseCamera(Vector3 direction, float strength, float returnTime)
    {
        if (isInCutScene) return; // CutScene has priority
        if (!isImpulsing)
        {
            impulseCoroutine = StartCoroutine(ImpulseCoroutine(direction.normalized * strength, returnTime));
        }
    }

    /// <summary>Adds to the current shake trauma value (clamped to [0, 1]); higher trauma produces stronger shake.</summary>
    public void AddTrauma(float amount)
    {
        if (isInCutScene) return; // CutScene has priority
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = elapsed / duration;
            float currentIntensity = intensity * (1f - normalizedTime);
            trauma = Mathf.Clamp01(currentIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }
        trauma = 0f;
    }

    private IEnumerator ImpulseCoroutine(Vector3 impulse, float returnTime)
    {
        isImpulsing = true;
        impulseOffset = impulse;

        float elapsed = 0f;
        while (elapsed < returnTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnTime;
            impulseOffset = Vector3.Lerp(impulse, Vector3.zero, t);
            yield return null;
        }

        impulseOffset = Vector3.zero;
        isImpulsing = false;
    }

    /// <summary>Disables camera-vs-world collision checks (camera can clip through geometry).</summary>
    public void SetCollisionLayerToNone()
    {
        collisionLayers = 0;
    }

    /// <summary>Sets collision layers to layer 6 (the project's convention for wall geometry).</summary>
    public void SetCollisionLayerToWalls()
    {
        collisionLayers = 1 << 6;
    }

    /// <summary>Sets just the vertical component of the follow <see cref="offset"/>.</summary>
    public void Cam_SetTopOffset(float yOffset)
    {
        offset.y = yOffset;
    }

    /// <summary>Replaces the follow offset entirely.</summary>
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }

    /// <summary>Interpolates the camera to <paramref name="cutSceneTarget"/>'s position/rotation and holds it there until <see cref="EndCutScene"/> is called. Cancels any active shake/impulse.</summary>
    public void StartCutScene(Transform cutSceneTarget, float duration = 2f)
    {
        // Stop lower-priority camera effects
        if (shakeCoroutine != null) { StopCoroutine(shakeCoroutine); shakeCoroutine = null; }
        if (impulseCoroutine != null) { StopCoroutine(impulseCoroutine); impulseCoroutine = null; }
        trauma = 0f;
        shakeOffset = Vector3.zero;
        impulseOffset = Vector3.zero;
        isImpulsing = false;

        if (cutSceneCoroutine != null) StopCoroutine(cutSceneCoroutine);
        cutSceneCoroutine = StartCoroutine(CutSceneCoroutine(cutSceneTarget, duration));
    }

    /// <summary>UnityEvent-friendly wrapper for <see cref="StartCutScene"/> using <see cref="cutSceneTransitionDuration"/>.</summary>
    public void StartCutSceneFromEvent(Transform cutSceneTarget)
    {
        if (cutSceneTarget == null)
        {
            Debug.LogWarning("CutScene Target is not assigned in CameraHandler!", this);
            return;
        }
        StartCutScene(cutSceneTarget, cutSceneTransitionDuration);
    }

    /// <summary>Stops the active cutscene, restores the camera to its pre-cutscene state, and tells <see cref="Toolbox.UiManager"/> to go back a menu.</summary>
    public void EndCutScene()
    {
        if (cutSceneCoroutine != null) StopCoroutine(cutSceneCoroutine);
            CancelCutScene();

        Toolbox.UiManager.GoBack();
    }

    /// <summary>Toggles a focused "inspect" transition to <paramref name="point"/>; calling again while inspecting cancels it. Auto-cancels if the follow target moves.</summary>
    public void WorldCameraInspect(Transform point)
    {
        if (isInspecting)
        {
            CancelInspect();
            return;
        }
        if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
        inspectCoroutine = StartCoroutine(InspectCoroutine(point));
    }

    /// <summary>Cancels an in-progress <see cref="WorldCameraInspect"/> and restores the previous follow state.</summary>
    public void CancelInspect()
    {
        if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
        EndInspect();
    }

    private void EndInspect()
    {
        isInspecting = false;
        followDirection = savedFollowDirection;
        distance = savedDistance;
        offset = savedOffset;
        positionVelocity = Vector3.zero;
        inspectCoroutine = null;
    }

    private void CancelCutScene()
    {
        isInCutScene = false;
        followDirection = cutSceneSavedFollowDirection;
        distance = cutSceneSavedDistance;
        offset = cutSceneSavedOffset;
        positionVelocity = Vector3.zero;
        cutSceneCoroutine = null;
    }

    private IEnumerator CutSceneCoroutine(Transform cutSceneTarget, float duration)
    {
        if (cutSceneTarget == null)
            yield break;

        // Save current camera state
        cutSceneSavedFollowDirection = followDirection;
        cutSceneSavedDistance = distance;
        cutSceneSavedOffset = offset;
        cutSceneSavedCameraPos = transform.position;
        cutSceneSavedCameraRot = transform.rotation;

        isInCutScene = true;

        // Store target position and rotation at the start
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = cutSceneTarget.position;
        Quaternion targetRot = cutSceneTarget.rotation;
        float elapsed = 0f;

        // Interpolate to target
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // Snap to final position
        transform.position = targetPos;
        transform.rotation = targetRot;

        // Hold at the cutscene target until EndCutScene() is explicitly called.
        // EndCutScene() stops this coroutine and calls CancelCutScene() to restore the camera.
        while (true)
            yield return null;
    }

    private IEnumerator InspectCoroutine(Transform point)
    {
        savedFollowDirection = followDirection;
        savedDistance = distance;
        savedOffset = offset;
        playerPosAtInspect = target ? target.position : Vector3.zero;

        isInspecting = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < inspectDuration)
        {
            if (target && Vector3.Distance(target.position, playerPosAtInspect) > 0.1f)
            {
                EndInspect();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / inspectDuration));
            transform.position = Vector3.Lerp(startPos, point.position, t);
            transform.rotation = Quaternion.Slerp(startRot, point.rotation, t);
            yield return null;
        }

        while (target && Vector3.Distance(target.position, playerPosAtInspect) <= 0.1f)
            yield return null;

        EndInspect();
    }
}

#if UNITY_EDITOR
/// <summary>Custom Inspector adding Play Mode test buttons for shake/impulse/trauma.</summary>
[UnityEditor.CustomEditor(typeof(CameraHandler))]
[MovedFrom(true, null, null, "CameraHandlerEditor")]
public class CameraHandlerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.LabelField("Test Camera Shake", UnityEditor.EditorStyles.boldLabel);

        CameraHandler handler = (CameraHandler)target;

        if (UnityEngine.GUILayout.Button("Shake (Intensity)") && Application.isPlaying)
        {
            handler.ShakeCamera(0.8f, 5f);
        }

        if (UnityEngine.GUILayout.Button("Impulse (Upward)") && Application.isPlaying)
        {
            handler.ImpulseCamera(UnityEngine.Vector3.up, 1f, 0.5f);
        }

        if (UnityEngine.GUILayout.Button("Add Trauma") && Application.isPlaying)
        {
            handler.AddTrauma(0.5f);
        }
    }
}
#endif
}
