using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Central hub for the player GameObject: caches references to the movement/camera/item
/// components and drives the flashlight toggle input.
/// </summary>
[MovedFrom(true, null, null, "PlayerHandler")]
public class PlayerHandler : MonoBehaviour
{
    /// <summary>Whether a saved player position should be restored on Start.</summary>
    public bool loadPositionOnStart = true;

    [Header("Components")]
    [HideInInspector] public ThirdPersonController thirdPersonController;
    [HideInInspector] public CharacterController charController;
    [HideInInspector] public PlayerItem playerItem;
    /// <summary>The player's audio listener.</summary>
    public AudioListener listner;
    public CameraHandler cameraHandler;

    [Header("Audio Listener")]
    /// <summary>Delay before <see cref="EnableListener"/> re-enables the audio listener.</summary>
    [SerializeField] private float listenerEnableDelay = 0.5f;

    [Header("Player Accessories")]
    public bool allowFlashlight = true;
    public GameObject flashLight;

    [Space(10)]
    public InputActionReference flashLightInput;

    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        charController = GetComponent<CharacterController>();
        playerItem = GetComponent<PlayerItem>();

        if (flashLightInput != null) flashLightInput.action.Enable();
    }

    void Start()
    {

        if (flashLight != null) flashLight.SetActive(false);
        if(cameraHandler) cameraHandler.SetTarget(transform);

        // Listener starts disabled on the prefab to avoid briefly having two active
        // AudioListeners during a scene transition; re-enable it once things settle.
        if (listner) listner.enabled = false;
        Invoke(nameof(EnableListener), listenerEnableDelay);

        GameManager.playMode = PlayMode.GAMEMODE;
    }

    private void EnableListener()
    {
        if (listner) listner.enabled = true;
    }

    void Update()
    {
        if (flashLightInput != null && flashLightInput.action.WasPressedThisFrame())
        {
            if (GameManager.playMode == PlayMode.GAMEMODE)
                ToggleFlashLight();
        }
    }
    
    /// <summary>Parents the player under the given transform, keeping its world position/rotation.</summary>
    public void SetParent(Transform parent)
    {
        transform.SetParent(parent, true);
    }

    /// <summary>Detaches the player from its current parent, keeping its world position/rotation.</summary>
    public void RemoveParent()
    {
        transform.SetParent(null, true);
    }

    /// <summary>Toggles the flashlight GameObject on/off, if <see cref="allowFlashlight"/> is enabled.</summary>
    public void ToggleFlashLight()
    {
        if(allowFlashlight)
            flashLight.SetActive(!flashLight.activeSelf);
    }
}
}
