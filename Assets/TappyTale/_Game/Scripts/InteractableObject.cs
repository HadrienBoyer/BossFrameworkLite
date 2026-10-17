using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>Whether an <see cref="InteractableObject"/> currently accepts player interaction.</summary>
public enum InteractionState { Enabled, Disabled }
/// <summary>Whether interacting requires a single tap or the interact button must be held.</summary>
public enum InteractType { TAP, HOLD }

/// <summary>
/// Base component for anything the player can interact with. Handles the interact prompt,
/// interaction sound, and one of three optional interaction modes: item lock (<see cref="itemLock"/>),
/// item placement (<see cref="itemPlacer"/>), or picking up (via a sibling <see cref="PickableItem"/>).
/// If none of those are configured, interacting simply invokes <see cref="onInteraction"/>.
/// </summary>
[MovedFrom(true, null, null, "InteractableObject")]
public class InteractableObject : MonoBehaviour
{
    [SerializeField] private InteractionState currentState = InteractionState.Enabled;
    /// <summary>Whether interacting with this object requires a tap or a held press.</summary>
    public InteractType interactType = InteractType.TAP;

    private CameraHandler cameraHandler;

    [Space(10)]
    [SerializeField] private TMP_Text interactMsgTxt;
    [SerializeField] private string interactMsg = "Interact";
    [Space(10)]
    [SerializeField] private AudioClip interactSound;
    [SerializeField] private bool use3DSound = false;

    [Header("Indication")]
    [SerializeField] private bool show3DIndication = true;
    [SerializeField] private GameObject indicationObj;


    [Header("Msg")]
    [SerializeField] private string msgTitle = "Msg";
    [TextArea(3, 8)]
    [SerializeField] private string msgDesc = "Msg";

    [Header("Additional Features")]
    [Tooltip("If this object requires an item to unlock, assign the ItemLock scriptable object here.")]
    public ItemLock itemLock;
    [Tooltip("On interaction, an item can be placed on this object. Assign the ItemPlacer component here if needed.")]
    public ItemPlacer itemPlacer;

    //Pick Handling
    private bool isPickableInteractable = false;
    [SerializeField] private PickableItem pickableItem;

    [Space(10)]
    /// <summary>Invoked on a standard interaction, i.e. when neither <see cref="itemLock"/>, <see cref="itemPlacer"/>, nor a sibling <see cref="PickableItem"/> handled it.</summary>
    public UnityEvent onInteraction;
    /// <summary>Invoked when interaction is disabled via <see cref="DisableInteraction"/>.</summary>
    public UnityEvent onDisableInteraction;
    /// <summary>Invoked when interaction is enabled via <see cref="EnableInteraction"/>.</summary>
    public UnityEvent onEnableInteraction;

    [Header("Interaction Delay")]
    /// <summary>If true, interaction is briefly disabled for <see cref="interactionDelay"/> seconds after each use.</summary>
    public bool hasInteractionDelay = true;
    [SerializeField] private float interactionDelay = 1.0f;
    private bool canInteract = true;

    [Header("Hold Settings")]
    /// <summary>Invoked when a HOLD-type interaction begins (interact button pressed).</summary>
    public UnityEvent onHoldStart;
    /// <summary>Invoked when a HOLD-type interaction is released before completion.</summary>
    public UnityEvent onHoldCancelled;

    /// <summary>Message shown to the player while this object is indicated as interactable.</summary>
    public string InteractMsg { get => interactMsg;}

    private void Awake() {

        if (TryGetComponent<PickableItem>(out pickableItem))
        {
            isPickableInteractable = true;
        }
    }

    private void Start()
    {
        cameraHandler = FindFirstObjectByType<CameraHandler>();

        if (indicationObj)
            indicationObj.SetActive(false);

        if(interactMsgTxt)
            interactMsgTxt.gameObject.SetActive(false);

        if (currentState == InteractionState.Disabled)
            DisableInteraction();
        else
            EnableInteraction();
    }

    /// <summary>
    /// Runs this object's interaction: plays the interact sound, then dispatches to whichever
    /// of item-lock, item-placer, or pickable handling is configured, falling back to
    /// <see cref="onInteraction"/> if none apply. No-ops while an interaction delay is active.
    /// </summary>
    public void Interact(PlayerHandler _player) {

        if (!canInteract) return;

        if(interactSound)
        {
            if (use3DSound)
                AudioSource.PlayClipAtPoint(interactSound, transform.position);
            else
                Toolbox.Soundmanager.PlaySound(interactSound);
        }

        if(itemLock)
        {
            if(itemLock.IsRequiredItem(_player.playerItem.ItemName))
            {
                StartInteractionDelay();
                itemLock.Unlock(_player.playerItem.item);
                _player.playerItem.OnUsed();
            }
            else
            {
                StartInteractionDelay();
                onInteraction?.Invoke();
            }
        }
        else if(itemPlacer != null)
        {
            if(itemPlacer.IsPlaced)
            {
                StartInteractionDelay();
                itemPlacer.Remove(_player.playerItem);
            }
            else if (_player.playerItem.item != null)
            {
                StartInteractionDelay();
                itemPlacer.Place(_player.playerItem.item);
                _player.playerItem.OnUsed();
            }
            else
            {
                Toolbox.Soundmanager.PlaySound(itemPlacer.noItemSound); // Play no item sound if player has no item to place
            }
        }
        else
        {
            if(isPickableInteractable)
            {
                StartInteractionDelay();
                IndicationStatus(false);
                pickableItem.Pick(_player.playerItem);
            }
            else
            {
                StartInteractionDelay();
                onInteraction?.Invoke();
            }
        }
    }

    private void StartInteractionDelay()
    {
        if (!hasInteractionDelay) return;
        canInteract = false;
        DisableInteraction();
        Invoke(nameof(EnableInteractionWithFlag), interactionDelay);
    }

    /// <summary>Shows or hides the interact prompt (3D indicator object, HUD message text, and HUD prompt) for this object.</summary>
    public void IndicationStatus(bool _val)
    {
        if(_val)
            Toolbox.UiManager.hud.ShowInteractMsg("Interact");
        else
            Toolbox.UiManager.hud.HideInteractMsg();

        if(show3DIndication)
        {
            if (indicationObj)
            {
                indicationObj.SetActive(_val);
            }

            if (interactMsgTxt)
            {
                interactMsgTxt.text = InteractMsg;
                interactMsgTxt.gameObject.SetActive(_val);

            }
        }

    }

    /// <summary>Shows the message box popup configured via <see cref="msgTitle"/>/<see cref="msgDesc"/>.</summary>
    public void ShowMessage()
    {
        Toolbox.UiManager.popups.ShowMessageBox(msgTitle, msgDesc);
    }

    /// <summary>Shows the object inspector popup focused on this GameObject.</summary>
    public void InspectObject()
    {
        Toolbox.UiManager.popups.ShowObjectInspector(gameObject);
    }

    /// <summary>Switches the camera into inspect mode focused on the given world point.</summary>
    public void InspectWorldPoint(Transform point)
    {
        if (cameraHandler) cameraHandler.WorldCameraInspect(point);
    }

    /// <summary>Enables interaction: re-enables the collider and fires <see cref="onEnableInteraction"/>.</summary>
    public void EnableInteraction()
    {
        CancelInvoke(nameof(EnableInteractionWithFlag));
        currentState = InteractionState.Enabled;
        canInteract = true;
        GetComponent<Collider>().enabled = true;
        onEnableInteraction?.Invoke();
    }

    /// <summary>Disables interaction: hides indicators, disables the collider, and fires <see cref="onDisableInteraction"/>.</summary>
    public void DisableInteraction()
    {
        currentState = InteractionState.Disabled;
        CancelInvoke(nameof(EnableInteractionWithFlag));

        if(indicationObj)indicationObj.SetActive(false);

        if(interactMsgTxt)interactMsgTxt.gameObject.SetActive(false);

        GetComponent<Collider>().enabled = false;
        onDisableInteraction?.Invoke();
    }

    private void EnableInteractionWithFlag()
    {
        if(TryGetComponent<PickableItem>(out PickableItem col))
        {
            if(col.isPicked)
                return;
        }

        EnableInteraction();
        canInteract = true;
    }
}

#if UNITY_EDITOR
/// <summary>Custom Inspector for <see cref="InteractableObject"/> that only shows hold-related event fields when <see cref="InteractType.HOLD"/> is selected, and adds a play-mode "Interact" test button.</summary>
[UnityEditor.CustomEditor(typeof(InteractableObject))]
[MovedFrom(true, null, null, "InteractableObjectEditor")]
public class InteractableObjectEditor : UnityEditor.Editor
{
    private UnityEditor.SerializedProperty _onHoldStart;
    private UnityEditor.SerializedProperty _onHoldCancelled;

    private void OnEnable()
    {
        _onHoldStart     = serializedObject.FindProperty("onHoldStart");
        _onHoldCancelled = serializedObject.FindProperty("onHoldCancelled");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InteractableObject io = (InteractableObject)target;

        // Draw everything except the hold fields
        UnityEditor.EditorGUI.BeginDisabledGroup(false);
        DrawPropertiesExcluding(serializedObject, "holdDuration", "onHoldStart", "onHoldComplete", "onHoldCancelled");
        UnityEditor.EditorGUI.EndDisabledGroup();

        // Show hold settings only when HOLD type is selected
        if (io.interactType == InteractType.HOLD)
        {
            UnityEditor.EditorGUILayout.Space(4);
            UnityEditor.EditorGUILayout.LabelField("Hold Settings", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(_onHoldStart);

            UnityEditor.EditorGUILayout.PropertyField(_onHoldCancelled);
        }

        serializedObject.ApplyModifiedProperties();

        if (!Application.isPlaying) return;

        UnityEditor.EditorGUILayout.Space(6);
        UnityEditor.EditorGUILayout.LabelField("Test Events", UnityEditor.EditorStyles.boldLabel);

        if (GUILayout.Button("Interact"))
            io.onInteraction?.Invoke();
    }
}
#endif
}
