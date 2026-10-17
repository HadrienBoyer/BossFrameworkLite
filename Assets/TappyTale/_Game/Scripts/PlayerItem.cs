using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>
/// Holds the single item the player is currently carrying: parents it to <see cref="holdPoint"/>,
/// and exposes pick-up/use/drop operations consumed by <see cref="InteractableObject"/>,
/// <see cref="ItemLock"/>, and <see cref="ItemPlacer"/>.
/// </summary>
[MovedFrom(true, null, null, "PlayerItem")]
public class PlayerItem : MonoBehaviour
{
    [SerializeField] private string itemName;
    /// <summary>The item currently being held, or null if empty-handed.</summary>
    public PickableItem item;

    [Space(10)]
    /// <summary>Transform the held item is parented to and centered on.</summary>
    public Transform holdPoint;

    [Space(10)]
    /// <summary>Input action bound to the drop button.</summary>
    public InputActionReference drop;

    public void Awake()
    {
        if (drop != null) drop.action.Enable();
    }

    /// <summary>Name of the currently held item, or an empty string if none.</summary>
    public string ItemName { get => itemName; }

    /// <summary>Picks up <paramref name="_item"/> into the hold slot, dropping whatever was previously held first.</summary>
    public void AddItem(PickableItem _item)
    {
        if(item != null)
        {
            DropItem();
        }

        itemName = _item.itemName;
        item = _item;
        item.transform.SetParent(holdPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        Toolbox.UiManager.hud.DropControlStatus(true);

        if(item.gameObject.TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = false;
        }

        if(item.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

    }

    void Update()
    {
        if (drop != null && drop.action.WasPressedThisFrame())
        {
            DropItem();
        }
    }

    /// <summary>Consumes the currently held item (e.g. after unlocking or placing it) without dropping it back into the world.</summary>
    public void OnUsed()
    {
        if (item)
        {
            item = null;
            itemName = "";

            Toolbox.UiManager.hud.DropControlStatus(false);
        }
    }

    /// <summary>Drops the currently held item back into the world, if any.</summary>
    public void DropItem()
    {
        if (item)
        {
            item.Drop();

            item = null;
            itemName = "";

            Toolbox.UiManager.hud.DropControlStatus(false);
        }
    }
}
}
