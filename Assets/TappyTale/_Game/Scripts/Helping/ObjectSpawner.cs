using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEditor;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>A prefab entry in an <see cref="ObjectSpawner"/>'s pool, with a relative spawn weight and an optional per-prefab instance cap.</summary>
[System.Serializable]
[MovedFrom(true, null, null, "SpawnableObject")]
public class SpawnableObject
{
    public GameObject prefab;
    /// <summary>Relative chance of being picked in <see cref="ObjectSpawner.SpawnMode.WeightedRandom"/> mode (ignored by the other modes).</summary>
    [Range(0, 100)] public float weight = 1f;
    [Tooltip("Max instances of this specific prefab (-1 for unlimited)")]
    public int maxInstances = -1;

    [HideInInspector] public int currentInstances = 0;
}

/// <summary>
/// Spawns prefabs from a weighted pool on a timer/count/duration basis, with optional randomized
/// position/rotation and launch force. Supports sequential, random, and weighted-random selection,
/// per-prefab instance caps, and a "forced mode" pool swap (<see cref="SetForceCorrectItemState"/>)
/// for puzzle-style scripted overrides.
/// </summary>
[MovedFrom(true, null, null, "ObjectSpawner")]
public class ObjectSpawner : MonoBehaviour
{
    /// <summary>How the next prefab to spawn is chosen from <see cref="objectsToSpawn"/>.</summary>
    public enum SpawnMode { Sequential, Random, WeightedRandom }
    /// <summary>How spawning is bounded: a fixed count, unbounded, or for a fixed duration.</summary>
    public enum SpawnType { LimitedCount, Unlimited, Timed }

    [Header("Spawn Settings")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Random;
    [SerializeField] private SpawnType spawnType = SpawnType.LimitedCount;
    /// <summary>Delay between a spawn being triggered and the object actually being instantiated (spawn sound plays immediately, before this delay).</summary>
    [SerializeField] private float spawnDelay = 0f;

    [Header("Object Pool")]
    [SerializeField] private List<SpawnableObject> objectsToSpawn = new List<SpawnableObject>();

    [Header("Spawn Parameters")]
    /// <summary>Total objects to spawn when <see cref="spawnType"/> is LimitedCount.</summary>
    [SerializeField] private int spawnCount = 10;

    /// <summary>Total seconds to keep spawning when <see cref="spawnType"/> is Timed.</summary>
    [SerializeField] private float spawnDuration = 30f;

    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private bool spawnOnStart = true;
    /// <summary>If true, restarts spawning (after a 1s pause) once a LimitedCount or Timed run finishes.</summary>
    [SerializeField] private bool loopSpawning = false;

    [Header("Spawn Position")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private bool randomizePosition = false;
    /// <summary>Half-extents of the random position offset applied around <see cref="spawnPoint"/> when <see cref="randomizePosition"/> is enabled.</summary>
    [SerializeField] private Vector3 randomPositionRange = new Vector3(5, 0, 5);

    [Header("Spawn Rotation")]
    [SerializeField] private Vector3 initialRotation = Vector3.zero;
    /// <summary>If true, spawned objects get a fully random rotation instead of <see cref="initialRotation"/>.</summary>
    [SerializeField] private bool randomRotation = false;

    [Header("Spawn Force")]
    [SerializeField] private bool applyForceOnSpawn = false;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;
    [SerializeField] private Vector3 forceDirection = Vector3.up;
    [SerializeField] private float forceStrength = 10f;
    /// <summary>If true, force strength and direction are randomized (within a +/-30° cone) instead of using <see cref="forceStrength"/>/<see cref="forceDirection"/> directly.</summary>
    [SerializeField] private bool randomizeForce = false;
    [SerializeField] private float minForce = 5f;
    [SerializeField] private float maxForce = 15f;

    [Header("Parenting")]
    /// <summary>If set, spawned objects are parented here (takes priority over <see cref="parentToSpawner"/>).</summary>
    [SerializeField] private Transform parentTransform;
    /// <summary>If true (and <see cref="parentTransform"/> is unset), spawned objects are parented to this spawner.</summary>
    [SerializeField] private bool parentToSpawner = false;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnSound;

    [Header("Effect")]
    public ParticleSystem spawnEffect;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.green;

    [Header("Force Mode")]
    /// <summary>Replacement pool used by <see cref="SetForceCorrectItemState"/> to override <see cref="objectsToSpawn"/> (e.g. to force spawning the "correct" puzzle item).</summary>
    [SerializeField] private List<SpawnableObject> forcedModeObjectsToSpawn = new List<SpawnableObject>();
    /// <summary>Fires when <see cref="SetForceCorrectItemState"/> is called.</summary>
    public UnityEvent onForceModeActivated;


    // Runtime variables
    private int currentIndex = 0;
    private int totalSpawned = 0;
    private bool isSpawning = false;
    private Coroutine spawnCoroutine;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private float totalWeight = 0f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        CalculateTotalWeight();
        
        if (spawnOnStart)
        {
            StartSpawning();
        }
    }
    
    void CalculateTotalWeight()
    {
        totalWeight = 0f;
        foreach (var obj in objectsToSpawn)
        {
            if (obj.prefab != null)
            {
                totalWeight += obj.weight;
            }
        }
    }
    
    /// <summary>Begins spawning according to <see cref="SpawnType"/>. No-op if already spawning or the pool is empty.</summary>
    public void StartSpawning()
    {
        if (isSpawning || objectsToSpawn.Count == 0) return;
        
        StopAllCoroutines();
        
        switch (spawnType)
        {
            case SpawnType.LimitedCount:
                spawnCoroutine = StartCoroutine(SpawnLimitedCount());
                break;
            case SpawnType.Unlimited:
                spawnCoroutine = StartCoroutine(SpawnUnlimited());
                break;
            case SpawnType.Timed:
                spawnCoroutine = StartCoroutine(SpawnTimed());
                break;
        }
    }
    
    /// <summary>Stops the active spawn coroutine. Does not despawn already-spawned objects; use <see cref="ClearAllSpawned"/> for that.</summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        isSpawning = false;
    }

    /// <summary>Spawns one object immediately (subject to <see cref="spawnDelay"/>), independent of the running spawn loop.</summary>
    public void SpawnSingle()
    {
        if (objectsToSpawn.Count == 0) return;

        SpawnableObject objToSpawn = GetNextObjectToSpawn();
        if (objToSpawn != null)
        {
            SpawnObject(objToSpawn);

        }
    }

    /// <summary>Destroys every object this spawner has spawned and resets per-prefab instance counts and the total-spawned counter.</summary>
    public void ClearAllSpawned()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
        
        // Reset instance counts
        foreach (var spawnable in objectsToSpawn)
        {
            spawnable.currentInstances = 0;
        }
        
        totalSpawned = 0;
    }
    
    IEnumerator SpawnLimitedCount()
    {
        isSpawning = true;
        int count = 0;
        
        while (count < spawnCount)
        {
            SpawnableObject objToSpawn = GetNextObjectToSpawn();
            if (objToSpawn == null) break;
            
            SpawnObject(objToSpawn);
            count++;
            totalSpawned++;
            
            yield return new WaitForSeconds(spawnInterval);
        }
        
        isSpawning = false;
        
        if (loopSpawning)
        {
            yield return new WaitForSeconds(1f);
            StartSpawning();
        }
    }
    
    IEnumerator SpawnUnlimited()
    {
        isSpawning = true;
        
        while (true)
        {
            SpawnableObject objToSpawn = GetNextObjectToSpawn();
            if (objToSpawn != null)
            {
                SpawnObject(objToSpawn);
                totalSpawned++;
            }
            
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    IEnumerator SpawnTimed()
    {
        isSpawning = true;
        float timer = 0f;
        
        while (timer < spawnDuration)
        {
            SpawnableObject objToSpawn = GetNextObjectToSpawn();
            if (objToSpawn != null)
            {
                SpawnObject(objToSpawn);
                totalSpawned++;
            }
            
            yield return new WaitForSeconds(spawnInterval);
            timer += spawnInterval;
        }
        
        isSpawning = false;
        
        if (loopSpawning)
        {
            yield return new WaitForSeconds(1f);
            StartSpawning();
        }
    }
    
    SpawnableObject GetNextObjectToSpawn()
    {
        if (objectsToSpawn.Count == 0) return null;
        
        switch (spawnMode)
        {
            case SpawnMode.Sequential:
                return GetSequentialObject();
                
            case SpawnMode.Random:
                return GetRandomObject();
                
            case SpawnMode.WeightedRandom:
                return GetWeightedRandomObject();
                
            default:
                return objectsToSpawn[0];
        }
    }
    
    SpawnableObject GetSequentialObject()
    {
        // Find next available prefab in sequence
        for (int i = 0; i < objectsToSpawn.Count; i++)
        {
            int index = (currentIndex + i) % objectsToSpawn.Count;
            var spawnable = objectsToSpawn[index];
            
            if (spawnable.prefab != null && 
                (spawnable.maxInstances == -1 || spawnable.currentInstances < spawnable.maxInstances))
            {
                currentIndex = (index + 1) % objectsToSpawn.Count;
                return spawnable;
            }
        }
        return null;
    }
    
    SpawnableObject GetRandomObject()
    {
        List<SpawnableObject> availableObjects = new List<SpawnableObject>();
        
        foreach (var spawnable in objectsToSpawn)
        {
            if (spawnable.prefab != null && 
                (spawnable.maxInstances == -1 || spawnable.currentInstances < spawnable.maxInstances))
            {
                availableObjects.Add(spawnable);
            }
        }
        
        if (availableObjects.Count == 0) return null;
        
        int randomIndex = Random.Range(0, availableObjects.Count);
        return availableObjects[randomIndex];
    }
    
    SpawnableObject GetWeightedRandomObject()
    {
        List<SpawnableObject> availableObjects = new List<SpawnableObject>();
        float availableWeight = 0f;
        
        foreach (var spawnable in objectsToSpawn)
        {
            if (spawnable.prefab != null && 
                (spawnable.maxInstances == -1 || spawnable.currentInstances < spawnable.maxInstances))
            {
                availableObjects.Add(spawnable);
                availableWeight += spawnable.weight;
            }
        }
        
        if (availableObjects.Count == 0) return null;
        
        float randomValue = Random.Range(0f, availableWeight);
        float cumulativeWeight = 0f;
        
        foreach (var spawnable in availableObjects)
        {
            cumulativeWeight += spawnable.weight;
            if (randomValue <= cumulativeWeight)
            {
                return spawnable;
            }
        }
        
        return availableObjects[0];
    }
    
    void SpawnObject(SpawnableObject spawnableObj)
    {
        // Increment immediately to reserve the slot (prevents race condition during delay)
        spawnableObj.currentInstances++;
        StartCoroutine(SpawnObjectWithDelay(spawnableObj));
    }

    IEnumerator SpawnObjectWithDelay(SpawnableObject spawnableObj)
    {
        if (audioSource != null && spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        yield return new WaitForSeconds(spawnDelay);

        // Get spawn position
        Vector3 spawnPosition = GetSpawnPosition();
        
        // Get spawn rotation
        Quaternion spawnRotation = GetSpawnRotation();
        
        // Instantiate the object
        GameObject newObj = Instantiate(spawnableObj.prefab, spawnPosition, spawnRotation);
        
        // Instance count already incremented in SpawnObject() before delay
        // If spawn fails, decrement the counter to free the reserved slot
        if (newObj == null)
        {
            spawnableObj.currentInstances--;
            yield break;
        }
        
        // Set parent
        if (parentTransform != null)
        {
            newObj.transform.SetParent(parentTransform);
        }
        else if (parentToSpawner)
        {
            newObj.transform.SetParent(transform);
        }
        
        // Apply force if enabled
        if (applyForceOnSpawn)
        {
            ApplyForceToObject(newObj);
        }

        if (spawnEffect != null)
        {            
            spawnEffect.Play();
        }
        
        // Add to spawned objects list
        spawnedObjects.Add(newObj);
    }
    
    Vector3 GetSpawnPosition()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        position += spawnOffset;
        
        if (randomizePosition)
        {
            position += new Vector3(
                Random.Range(-randomPositionRange.x, randomPositionRange.x),
                Random.Range(-randomPositionRange.y, randomPositionRange.y),
                Random.Range(-randomPositionRange.z, randomPositionRange.z)
            );
        }
        
        return position;
    }
    
    Quaternion GetSpawnRotation()
    {
        if (randomRotation)
        {
            return Quaternion.Euler(
                Random.Range(0, 360),
                Random.Range(0, 360),
                Random.Range(0, 360)
            );
        }
        
        return Quaternion.Euler(initialRotation);
    }
    
    void ApplyForceToObject(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        
        Vector3 force = forceDirection.normalized;
        
        if (randomizeForce)
        {
            float strength = Random.Range(minForce, maxForce);
            force *= strength;
        }
        else
        {
            force *= forceStrength;
        }
        
        // Apply random direction variation
        if (randomizeForce)
        {
            force = Quaternion.Euler(
                Random.Range(-30, 30),
                Random.Range(-30, 30),
                Random.Range(-30, 30)
            ) * force;
        }
        
        rb.AddForce(force, forceMode);
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        Gizmos.color = gizmoColor;
        
        // Draw spawn point
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        spawnPos += spawnOffset;
        
        Gizmos.DrawWireSphere(spawnPos, 0.5f);
        Gizmos.DrawLine(spawnPos, spawnPos + Vector3.up * 1f);
        
        // Draw random position range
        if (randomizePosition)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawWireCube(spawnPos, randomPositionRange * 2);
        }
        
        // Draw force direction
        if (applyForceOnSpawn)
        {
            Gizmos.color = Color.red;
            Vector3 forceEnd = spawnPos + forceDirection.normalized * 2f;
            Gizmos.DrawLine(spawnPos, forceEnd);
            Gizmos.DrawSphere(forceEnd, 0.2f);
        }
    }

    /// <summary>Swaps the active spawn pool to <see cref="forcedModeObjectsToSpawn"/> and fires <see cref="onForceModeActivated"/>. Does not affect a spawn loop already in progress; new spawns after this call draw from the forced pool.</summary>
    public void SetForceCorrectItemState()
    {
        objectsToSpawn = forcedModeObjectsToSpawn;
        onForceModeActivated?.Invoke();
    }

    // Public properties for UI or other scripts
    /// <summary>Running count of objects spawned since the last <see cref="ClearAllSpawned"/> call.</summary>
    public int TotalSpawned => totalSpawned;
    /// <summary>True while a spawn coroutine (Limited/Unlimited/Timed) is active.</summary>
    public bool IsSpawning => isSpawning;
    /// <summary>All objects currently spawned and tracked by this spawner (cleared by <see cref="ClearAllSpawned"/>).</summary>
    public List<GameObject> SpawnedObjects => spawnedObjects;
    
    // Editor button
    [ContextMenu("Spawn Single Object")]
    void SpawnSingleEditor()
    {
        SpawnSingle();
    }
    
    [ContextMenu("Start Spawning")]
    void StartSpawningEditor()
    {
        StartSpawning();
    }
    
    [ContextMenu("Stop Spawning")]
    void StopSpawningEditor()
    {
        StopSpawning();
    }
    
    [ContextMenu("Clear All")]
    void ClearAllEditor()
    {
        ClearAllSpawned();
    }
}

// Conditional Hide Attribute for Editor
#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
[MovedFrom(true, null, null, "ConditionalHidePropertyDrawer")]
public class ConditionalHidePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);
        
        if (enabled)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);
        
        if (enabled)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
        else
        {
            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }
    
    private bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHAtt, SerializedProperty property)
    {
        SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(condHAtt.conditionalSourceField);
        
        if (sourcePropertyValue != null)
        {
            return CheckPropertyType(condHAtt, sourcePropertyValue);
        }
        return true;
    }
    
    private bool CheckPropertyType(ConditionalHideAttribute condHAtt, SerializedProperty sourcePropertyValue)
    {
        switch (sourcePropertyValue.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return sourcePropertyValue.boolValue == condHAtt.matchValueBool;
            case SerializedPropertyType.Enum:
                return sourcePropertyValue.enumValueIndex == (int)condHAtt.matchValueEnum;
            default:
                Debug.LogError("Data type of the property used for conditional hiding [" + 
                    sourcePropertyValue.propertyType + "] is currently not supported");
                return true;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Field)]
[MovedFrom(true, null, null, "ConditionalHideAttribute")]
public class ConditionalHideAttribute : PropertyAttribute
{
    public string conditionalSourceField = "";
    public bool matchValueBool;
    public object matchValueEnum;
    
    public ConditionalHideAttribute(string conditionalSourceField, bool matchValue)
    {
        this.conditionalSourceField = conditionalSourceField;
        this.matchValueBool = matchValue;
    }
    
    public ConditionalHideAttribute(string conditionalSourceField, object matchValue)
    {
        this.conditionalSourceField = conditionalSourceField;
        this.matchValueEnum = matchValue;
    }
}
#endif
}