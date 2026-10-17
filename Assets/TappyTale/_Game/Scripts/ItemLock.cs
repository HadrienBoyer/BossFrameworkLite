using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>
/// Gates an interaction behind a specific named item: an <see cref="InteractableObject"/>
/// checks <see cref="IsRequiredItem"/> against the player's held item and calls
/// <see cref="Unlock"/> on a match (e.g. a locked door that needs a key).
/// </summary>
[MovedFrom(true, null, null, "ItemLock")]
public class ItemLock : MonoBehaviour
{
    [SerializeField] private string requiredItemName;
    [Tooltip("If assigned, the item used to unlock will be parented to this transform.")]
    [SerializeField] private Transform holdPoint;

    [Header("Sound")]
    public AudioClip unlockSound;

    [Header("Light")]
    public Color unlockedColor = Color.green;

    [Space(10)]
    /// <summary>Invoked after a successful <see cref="Unlock"/>.</summary>
    public UnityEvent onUnlock;

    /// <summary>Whether <paramref name="itemName"/> matches the item required to unlock this lock.</summary>
    public bool IsRequiredItem(string itemName)
    {
        return itemName.Equals(requiredItemName);
    }

    /// <summary>Kept for compatibility with existing UnityEvent wiring (e.g. prefab button OnClick hooks); intentionally empty since the volumetric line feature it drove was removed.</summary>
    public void ForcedUnlockState()
    {
        // Volumetric line removed; this method remains for compatibility if behavior is expanded later.
    }

    /// <summary>Unlocks with the given item: plays <see cref="unlockSound"/>, then either parents the item to <see cref="holdPoint"/> (if assigned) or destroys it, and fires <see cref="onUnlock"/>.</summary>
    public void Unlock(PickableItem _item)
    {
        if (unlockSound)
            AudioSource.PlayClipAtPoint(unlockSound, transform.position);

        if(holdPoint && _item)
        {
            _item.transform.SetParent(holdPoint);
            _item.transform.localPosition = Vector3.zero;
            _item.transform.localRotation = Quaternion.identity;

            if(_item.gameObject.TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false;
            }

            if(_item.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
            }
        }
        else
        {
            if(_item)
                Destroy(_item.gameObject);
        }

        onUnlock?.Invoke();

    }
}
}
