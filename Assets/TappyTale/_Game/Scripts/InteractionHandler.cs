using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>
/// Detects the nearest <see cref="InteractableObject"/> within a sphere around this
/// transform and routes interact/hold input to it.
/// </summary>
[MovedFrom(true, null, null, "InteractionHandler")]
public class InteractionHandler : MonoBehaviour
{
    /// <summary>If true, draws the detection sphere as a wire gizmo in the Scene view (editor only).</summary>
    public bool drawGizmo = false;

    /// <summary>Radius of the sphere used to detect nearby interactables.</summary>
    [SerializeField] private float radius = 2f;
    /// <summary>Local offset from this transform's position used as the center of the detection sphere.</summary>
    [SerializeField] private Vector3 offset;

    [Header("Interact")]
    [Space(5)]
    /// <summary>The closest detected interactable, or null if none is currently in range.</summary>
    [SerializeField] private InteractableObject closesInteractableItem;
    /// <summary>Collider of the last-checked closest item; used to avoid reprocessing when it hasn't changed.</summary>
    [SerializeField] private Collider lastClosestInteractableItemCollider;

    private bool _isHolding = false;

    float dTime = 0;
    /// <summary>Interval in seconds between interactable detection sweeps (avoids checking every frame).</summary>
    [SerializeField] private float detectItemCheckDelay = 0.3f;

    [Space(5)]
    /// <summary>Layer mask used when sphere-casting for interactable colliders.</summary>
    [SerializeField] private LayerMask interactDetectionLayer;

    [Header("Control")]
    /// <summary>Input action bound to the interact/hold button.</summary>
    public InputActionReference interact;

    private void Awake() {

        if(interact) interact.action.Enable();
    }

    private void Start()
    {
        dTime = detectItemCheckDelay;
    }

    void Update()
    {
        dTime -= Time.deltaTime;

        if (dTime < 0)
        {
            DetectInteractableObjectsInSphere();
            dTime = detectItemCheckDelay;
        }

        if (interact.action.WasPressedThisFrame())
            InteractWithClosesItem();

        if (interact.action.WasReleasedThisFrame() && _isHolding)
            CancelHold();
    }

    #region INTERACT
    private void DetectInteractableObjectsInSphere()
    {
        // Perform the sphere detection
        Collider[] hitColliders = Physics.OverlapSphere(
            transform.position + offset,
            radius,
            interactDetectionLayer
        );

        //Find the closest item and update it in variable
        if (hitColliders.Length > 0)
        {
            float closesDist = Mathf.Infinity;
            Collider closestItem = null;

            foreach (var item in hitColliders)
            {
                // Use the collider's closest point for a more accurate distance
                Vector3 closestPoint = item.ClosestPoint(this.transform.position);
                float dist = Vector3.Distance(this.transform.position, closestPoint);
                if (dist < closesDist)
                {
                    closesDist = dist;
                    closestItem = item;
                }
            }

            if (lastClosestInteractableItemCollider != closestItem) {

                // Disable previous indicated interactable (if any)
                if (closesInteractableItem)
                {
                    RemoveClosestItem();
                }

                // Try to find an InteractableObject on the collider or a parent
                if (closestItem != null)
                {
                    InteractableObject itemI = closestItem.GetComponentInParent<InteractableObject>();
                    if (itemI != null)
                    {
                        closesInteractableItem = itemI;
                        closesInteractableItem.IndicationStatus(true);
                    }
                    // even if there's no InteractableObject, remember the collider as lastClosest
                    lastClosestInteractableItemCollider = closestItem;
                }
            }
        }
        else {

            if (closesInteractableItem)
            {
                RemoveClosestItem();
                lastClosestInteractableItemCollider = null;
            }
        }
    }

    /// <summary>Interacts with the closest detected interactable, if any.</summary>
    public void Interact() => InteractWithClosesItem();

    private void InteractWithClosesItem()
    {
        if (closesInteractableItem == null || GameManager.playMode == PlayMode.UIMODE)
            return;

        if (closesInteractableItem.interactType == InteractType.HOLD)
        {
            _isHolding = true;
            closesInteractableItem.onHoldStart?.Invoke();
        }
        else
        {
            closesInteractableItem.Interact(this.GetComponent<PlayerHandler>());
        }
    }

    private void CancelHold()
    {
        _isHolding = false;
        if (closesInteractableItem != null)
            closesInteractableItem.onHoldCancelled?.Invoke();
    }

    /// <summary>The interactable currently closest to and detected by this handler, or null if none.</summary>
    public InteractableObject ClosestInteractable => closesInteractableItem;

    /// <summary>Clears the currently tracked closest interactable, forcing re-detection on the next sweep.</summary>
    public void ForceRefresh()
    {
        RemoveClosestItem();
        lastClosestInteractableItemCollider = null;
    }

    void RemoveClosestItem()
    {
        if (_isHolding)
            CancelHold();

        closesInteractableItem.IndicationStatus(false);
        closesInteractableItem = null;
    }

    #endregion

#if UNITY_EDITOR
    // Visualize the sphere in the editor
    private void OnDrawGizmosSelected()
    {
        if (drawGizmo) {

            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position + offset, radius);
        }
    }
#endif
}
}
