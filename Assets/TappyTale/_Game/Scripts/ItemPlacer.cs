using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>
/// A slot an <see cref="InteractableObject"/> can place a held <see cref="PickableItem"/> into
/// (and remove it from again), optionally checking the item against <see cref="acceptedItemName"/>
/// and swapping with whatever the player is currently holding.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[MovedFrom(true, null, null, "ItemPlacer")]
public class ItemPlacer : MonoBehaviour
{
    /// <summary>If true, logs place/remove/swap activity to the Console (useful while wiring up puzzles).</summary>
    public bool showDebugLogs = true;

    [SerializeField] private string acceptedItemName;
    [Tooltip("If assigned, the placed item will be parented to this transform.")]
    [SerializeField] private Transform holdPoint;

    [Header("Sound")]
    public AudioClip placeSound;
    public AudioClip removeSound;
    public AudioClip noItemSound; // interaction sound when player has no item to place or swap
    public bool use3DSound = false;

    [Space(10)]
    /// <summary>Invoked whenever any item is placed, regardless of whether it's the accepted item.</summary>
    public UnityEvent onPlaced;
    /// <summary>Invoked when the item placed matches <see cref="acceptedItemName"/> (see <see cref="IsCorrectItem"/>).</summary>
    public UnityEvent onCorrectItemPlaced;
    /// <summary>Invoked when an item is removed without a swap.</summary>
    public UnityEvent onRemoved;
    /// <summary>Invoked when the placed item is swapped for the item the player is holding.</summary>
    public UnityEvent onSwapped;
    /// <summary>Invoked when the previously-correct placed item is removed (via removal or swap).</summary>
    public UnityEvent onCorrectItemRemoved;

    private PickableItem placedItem;
    [SerializeField] private bool isCorrectItemPlaced;

    [Header("Force Correct Item")]
    /// <summary>Invoked by <see cref="SetForceCorrectItemState"/> when the correct state is forced without an actual item placement.</summary>
    public UnityEvent onForcedCorrectState;


    /// <summary>Whether <paramref name="itemName"/> matches <see cref="acceptedItemName"/> (case-insensitive). An empty <see cref="acceptedItemName"/> accepts anything.</summary>
    public bool IsCorrectItem(string itemName)
    {
        return string.IsNullOrEmpty(acceptedItemName) || itemName.Equals(acceptedItemName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether an item is currently placed on this slot.</summary>
    public bool IsPlaced => placedItem != null;
    /// <summary>The item currently placed on this slot, or null if empty.</summary>
    public PickableItem PlacedItem => placedItem;

    /// <summary>Places <paramref name="_item"/> into this slot, parenting it, disabling its physics, playing <see cref="placeSound"/>, and firing <see cref="onPlaced"/> plus <see cref="onCorrectItemPlaced"/> if it matches <see cref="acceptedItemName"/>.</summary>
    public void Place(PickableItem _item)
    {
        if (_item == null) return;

        placedItem = _item;


        Transform target = holdPoint != null ? holdPoint : transform;

        placedItem.transform.SetParent(target);
        placedItem.transform.localPosition = Vector3.zero;
        placedItem.transform.localRotation = Quaternion.identity;

        if (placedItem.gameObject.TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = false;
        }

        if (placedItem.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        if (placeSound)
        {
            if (use3DSound)
                AudioSource.PlayClipAtPoint(placeSound, transform.position);
            else
                GetComponent<AudioSource>().PlayOneShot(placeSound);
        }

        onPlaced?.Invoke();

        if (IsCorrectItem(_item.itemName))
        {
            if (showDebugLogs)
                Debug.Log($"Correct item {_item.itemName} placed on {gameObject.name}");

            isCorrectItemPlaced = true;
            onCorrectItemPlaced?.Invoke();
        }else
        {
            isCorrectItemPlaced = false;
            if (showDebugLogs)
                Debug.Log($"Incorrect item {_item.itemName} placed on {gameObject.name}");
        }
    }

    /// <summary>Removes the placed item, giving it to <paramref name="_playerItem"/>. If the player is already holding something, the two items are swapped instead.</summary>
    public void Remove(PlayerItem _playerItem)
    {
        if (placedItem == null) return;

        if (showDebugLogs)
            Debug.Log($"Removed {placedItem.gameObject.name} from {gameObject.name}");

        if (removeSound)
        {
            if (use3DSound)
                AudioSource.PlayClipAtPoint(removeSound, transform.position);
            else
                GetComponent<AudioSource>().PlayOneShot(removeSound);
        }

        if(isCorrectItemPlaced)
        {
            onCorrectItemRemoved?.Invoke();
        }

        if(_playerItem != null && _playerItem.item != null)
        {
            if (showDebugLogs)
                Debug.Log($"Player already holding {placedItem.gameObject.name}, need to swap");

            //Swap items if player is already holding something
            PickableItem tempItem = _playerItem.item;
            _playerItem.AddItem(placedItem);
            Place(tempItem);
            onSwapped?.Invoke();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"Removing item without swap, player now holding {placedItem.gameObject.name}");

            if (_playerItem != null)
                _playerItem.AddItem(placedItem);

            placedItem = null;
            isCorrectItemPlaced = false;

            onRemoved?.Invoke();
        }
    }

    /// <summary>Forces this slot into the "correct item placed" state without an actual placement, firing <see cref="onForcedCorrectState"/> (e.g. for a puzzle skip/debug flow).</summary>
    public void SetForceCorrectItemState()
    {
        if (showDebugLogs)
            Debug.Log($"Forcing correct item {acceptedItemName} on {gameObject.name}");

        isCorrectItemPlaced = true;
        onForcedCorrectState?.Invoke();
    }
}
}
