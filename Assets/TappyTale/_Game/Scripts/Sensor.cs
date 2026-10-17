using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Trigger-volume sensor that tracks which tagged objects are currently inside it and
/// raises enter/leave events (plus first-in/last-out events) for use by puzzle logic.
/// </summary>
[MovedFrom(true, null, null, "Sensor")]
public class Sensor : MonoBehaviour
{
    [SerializeField] private bool isEnabled = true;

    /// <summary>UnityEvent variant that passes the GameObject which entered/left the sensor.</summary>
    [System.Serializable]
    public class UnityEventGameObject : UnityEvent<GameObject> { }


    [Header("Sensor Events")]
    public UnityEventGameObject onObjectEnter;
    public UnityEventGameObject onObjectLeave;
    /// <summary>Fires only when the object count goes from 0 to 1.</summary>
    public UnityEvent onFirstObjectEnter;
    /// <summary>Fires only when the object count drops from 1 to 0.</summary>
    public UnityEvent onLastObjectLeave;

    [Header("Sensor Settings")]
    [Tooltip("Objects with these tags will trigger the sensor")]
    public List<string> triggerTags = new List<string>();

    [Tooltip("If true, sensor will detect any object regardless of tag")]
    public bool detectAllTags = false;

    [Tooltip("If true, sensor triggers unlimited times")]
    public bool infiniteTrigger = true;
    [Tooltip("How many times the sensor can fire (ignored if Infinite Trigger is on)")]
    public int numberOfTimesToTrigger = 1;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    [Header("Debug")]
    [SerializeField] private List<GameObject> objectsInRange = new List<GameObject>();
    [SerializeField] private int objectCount = 0;
    [SerializeField] private int triggerCount = 0;

    /// <summary>Copy of the objects currently inside the sensor.</summary>
    public List<GameObject> ObjectsInRange => new List<GameObject>(objectsInRange);
    /// <summary>Number of objects currently inside the sensor.</summary>
    public int ObjectCount => objectCount;
    
    private Collider sensorCollider;
    
    private void Start()
    {
        sensorCollider = GetComponent<Collider>();
        if (sensorCollider == null)
        {
            Debug.LogError("Sensor requires a Collider component!");
            return;
        }
        
        if (!sensorCollider.isTrigger)
        {
            Debug.LogWarning("Sensor Collider should be set to 'Is Trigger' for proper functionality");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isEnabled) return;
        if (!IsValidObject(other.gameObject)) return;
        if (!infiniteTrigger && triggerCount >= numberOfTimesToTrigger) return;

        AddObject(other.gameObject);
        onObjectEnter?.Invoke(other.gameObject);
        triggerCount++;

        if (objectCount == 1)
            onFirstObjectEnter?.Invoke();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if(!isEnabled) return;

        if (!IsValidObject(other.gameObject)) return;
        
        RemoveObject(other.gameObject);
        onObjectLeave?.Invoke(other.gameObject);
        
        if (objectCount == 0)
        {
            onLastObjectLeave?.Invoke();
        }
    }
    
    private bool IsValidObject(GameObject obj)
    {
        if (detectAllTags) return true;
        
        foreach (string tag in triggerTags)
        {
            if (obj.CompareTag(tag))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void AddObject(GameObject obj)
    {
        if (!objectsInRange.Contains(obj))
        {
            objectsInRange.Add(obj);
            objectCount = objectsInRange.Count;
        }
    }
    
    private void RemoveObject(GameObject obj)
    {
        if (objectsInRange.Remove(obj))
        {
            objectCount = objectsInRange.Count;
        }
    }
    
    /// <summary>True if the given object is currently inside the sensor.</summary>
    public bool ContainsObject(GameObject obj)
    {
        return objectsInRange.Contains(obj);
    }

    /// <summary>True if any object currently inside the sensor has the given tag.</summary>
    public bool ContainsObjectWithTag(string tag)
    {
        foreach (GameObject obj in objectsInRange)
        {
            if (obj.CompareTag(tag))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Forgets all currently-tracked objects without firing any events.</summary>
    public void ClearSensor()
    {
        objectsInRange.Clear();
        objectCount = 0;
    }

    private void OnDestroy()
    {
        ClearSensor();
    }

    /// <summary>Stops the sensor from detecting trigger enter/exit until <see cref="EnableSensor"/> is called.</summary>
    public void DisableSensor()
    {
        isEnabled = false;
        sensorCollider.enabled = false;
    }

    /// <summary>Re-enables detection after <see cref="DisableSensor"/>.</summary>
    public void EnableSensor()
    {
        isEnabled = true;
        sensorCollider.enabled = true;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (sensorCollider == null)
            sensorCollider = GetComponent<Collider>();
        
        if (sensorCollider != null)
        {
            Gizmos.color = gizmoColor;
            if (sensorCollider is BoxCollider boxCollider)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            }
            else if (sensorCollider is SphereCollider sphereCollider)
            {
                Gizmos.DrawSphere(transform.position + sphereCollider.center, sphereCollider.radius);
            }
            else if (sensorCollider is CapsuleCollider capsuleCollider)
            {
                // Simplified visualization for capsule
                Vector3 center = transform.position + capsuleCollider.center;
                float radius = capsuleCollider.radius;
                float height = capsuleCollider.height;
                Gizmos.DrawSphere(center, radius);
                Gizmos.DrawSphere(center + Vector3.up * (height * 0.5f - radius), radius);
                Gizmos.DrawSphere(center - Vector3.up * (height * 0.5f - radius), radius);
            }
        }
    }
    #endif
}
}