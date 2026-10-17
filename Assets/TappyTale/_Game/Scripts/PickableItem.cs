using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{

/// <summary>
/// A world object that can be picked up into a <see cref="PlayerItem"/> hold slot.
/// Typically triggered via a sibling <see cref="InteractableObject"/> calling <see cref="Pick"/>.
/// </summary>
[MovedFrom(true, null, null, "PickableItem")]
public class PickableItem : MonoBehaviour
{
    /// <summary>Identifier compared against <see cref="ItemLock"/>/<see cref="ItemPlacer"/> accepted item names.</summary>
    public string itemName;

    /// <summary>True once this item has been picked up and is held by a player.</summary>
    public bool isPicked = false;

    [Header("Settings")]
    /// <summary>If true, this item's world position is saved to PlayerPrefs when it's disabled while dropped, and restored on <see cref="Start"/> — so a dropped item stays where it was left across sessions.</summary>
    public bool savePosition = true;

    [Header("Events")]
    /// <summary>Invoked when this item is picked up.</summary>
    public UnityEvent onPicked;

    [Header("Pick Limit")]
    /// <summary>If true, this item can be picked an unlimited number of times (ignores <see cref="pickCount"/>).</summary>
    public bool unlimited = true;
    /// <summary>Maximum number of times this item can be picked when <see cref="unlimited"/> is false.</summary>
    [Min(0)] public int pickCount = 0;

    private int picksTaken = 0;

    private void Start()
    {
        LoadPositionFromPrefs();
    }

    private void OnDisable()
    {
        SavePositionToPrefs();
    }

    /// <summary>Picks this item up into <paramref name="_player"/>'s hold slot, subject to <see cref="unlimited"/>/<see cref="pickCount"/>. No-ops if already picked or the pick limit is reached.</summary>
    public void Pick(PlayerItem _player)
    {
        if (isPicked || _player == null) return;
        if (!unlimited && picksTaken >= pickCount) return;

        _player.AddItem(this);
        isPicked = true;
        picksTaken++;
        onPicked?.Invoke();
    }

    /// <summary>Releases this item into the world: re-enables its physics/collider, unparents it, and re-enables interaction if it has an <see cref="InteractableObject"/>.</summary>
    public void Drop()
    {
        if(gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = false;
        }

        if(gameObject.TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = true;
        }

        transform.SetParent(null);

        isPicked = false;

        if(TryGetComponent<InteractableObject>(out InteractableObject interactable))
            interactable.EnableInteraction();
    }

    #region Prefs
    private string PositionKey => $"PickableItem_{itemName}_pos";

    private void SavePositionToPrefs()
    {
        if (!savePosition || isPicked) return;

        PlayerPrefs.SetString(PositionKey, Vector3ToString(transform.position));
        PlayerPrefs.Save();
    }

    private void LoadPositionFromPrefs()
    {
        if (!savePosition) return;

        if (PlayerPrefs.HasKey(PositionKey))
        {
            string posString = PlayerPrefs.GetString(PositionKey);
            Vector3 savedPos = StringToVector3(posString);
            transform.position = savedPos;
        }
    }

    private string Vector3ToString(Vector3 v)
    {
        return $"{v.x},{v.y},{v.z}";
    }

    private Vector3 StringToVector3(string s)
    {
        var parts = s.Split(',');
        if (parts.Length != 3) return Vector3.zero;
        float.TryParse(parts[0], out float x);
        float.TryParse(parts[1], out float y);
        float.TryParse(parts[2], out float z);
        return new Vector3(x, y, z);
    }
    #endregion
}
}
