using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Plays collision-impact and continuous rolling sounds for a physics-driven object, with
/// velocity-scaled pitch/volume and optional per-surface (tag-matched) sound overrides.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
[MovedFrom(true, null, null, "MoveableObjectSoundHandler")]
public class MoveableObjectSoundHandler : MonoBehaviour
{
    [Header("Collision Sound Settings")]
    [SerializeField] private AudioClip[] collisionSounds;
    [SerializeField] private AudioClip[] rollingSounds;
    [SerializeField] private AudioClip slidingSound;

    [Header("Velocity-based Sound Settings")]
    [SerializeField] private float minCollisionVelocity = 1f;
    [SerializeField] private float maxCollisionVelocity = 10f;
    [SerializeField] private float minRollingVelocity = 0.5f;
    [SerializeField] private float maxRollingVelocity = 5f;

    [Header("Pitch & Volume Settings")]
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;
    [SerializeField] private float minVolume = 0.3f;
    [SerializeField] private float maxVolume = 1f;

    [Header("Rolling Sound Settings")]
    [SerializeField] private bool enableRollingSounds = true;
    /// <summary>Seconds between rolling-sound plays while the object keeps rolling.</summary>
    [SerializeField] private float rollingSoundInterval = 0.15f;
    [SerializeField] private float rollingStopThreshold = 0.1f;

    [Header("Audio Mixing")]
    [SerializeField] private AudioMixerGroup collisionMixerGroup;
    [SerializeField] private AudioMixerGroup rollingMixerGroup;

    [Header("Surface Materials (Optional)")]
    /// <summary>Per-tag sound/pitch/volume overrides, matched against the collided object's tag; falls back to <see cref="collisionSounds"/>/<see cref="rollingSounds"/> when no match is found.</summary>
    [SerializeField] private SurfaceMaterial[] surfaceMaterials;

    [Header("Debug")]
    /// <summary>Enables gizmo velocity visualization in the Scene view while selected.</summary>
    [SerializeField] private bool showDebugInfo = false;

    // Components
    private Rigidbody rb;
    private AudioSource collisionAudioSource;
    private AudioSource rollingAudioSource;

    // State variables
    private bool isRolling = false;
    private Coroutine rollingCoroutine;
    private float lastCollisionTime;
    private const float COLLISION_COOLDOWN = 0.1f;

    /// <summary>A tag-matched override of collision/rolling sounds and pitch/volume multipliers for a specific surface.</summary>
    [System.Serializable]
    public class SurfaceMaterial
    {
        public string materialTag = "Default";
        public AudioClip[] collisionClips;
        public AudioClip rollingClip;
        [Range(0f, 1f)] public float volumeMultiplier = 1f;
        [Range(0.5f, 2f)] public float pitchMultiplier = 1f;
    }
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Set up primary collision audio source
        collisionAudioSource = GetComponent<AudioSource>();
        collisionAudioSource.playOnAwake = false;
        collisionAudioSource.spatialBlend = 1f; // Full 3D
        collisionAudioSource.minDistance = 1f;
        collisionAudioSource.maxDistance = 50f;
        
        if (collisionMixerGroup != null)
            collisionAudioSource.outputAudioMixerGroup = collisionMixerGroup;
        
        // Create secondary audio source for rolling sounds
        rollingAudioSource = gameObject.AddComponent<AudioSource>();
        rollingAudioSource.playOnAwake = false;
        rollingAudioSource.spatialBlend = 1f;
        rollingAudioSource.loop = false;
        rollingAudioSource.minDistance = 1f;
        rollingAudioSource.maxDistance = 30f;
        
        if (rollingMixerGroup != null)
            rollingAudioSource.outputAudioMixerGroup = rollingMixerGroup;
    }
    
    void Update()
    {
        HandleRollingSounds();
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastCollisionTime < COLLISION_COOLDOWN)
            return;
            
        lastCollisionTime = Time.time;
        
        // Calculate collision velocity magnitude
        float collisionVelocity = collision.relativeVelocity.magnitude;
        
        // Only play sound if velocity exceeds minimum threshold
        if (collisionVelocity < minCollisionVelocity)
            return;
        
        // Get surface material info
        SurfaceMaterial material = GetSurfaceMaterial(collision.gameObject.tag);
        
        // Play collision sound with velocity-based parameters
        PlayCollisionSound(collisionVelocity, material, collision.contacts[0].point);

        // Debug visualization
        if (showDebugInfo)
        {
            Debug.DrawRay(collision.contacts[0].point, collision.contacts[0].normal * 2, Color.red, 2f);
        }
    }
    
    void HandleRollingSounds()
    {
        if (!enableRollingSounds || rollingSounds.Length == 0)
            return;
        
        float currentSpeed = rb.linearVelocity.magnitude;
        bool shouldBeRolling = currentSpeed > minRollingVelocity;
        
        // Start rolling sound
        if (shouldBeRolling && !isRolling)
        {
            StartRollingSounds();
        }
        // Stop rolling sound
        else if (!shouldBeRolling && isRolling)
        {
            StopRollingSounds();
        }
        
        // Adjust rolling sound based on speed while rolling
        if (isRolling)
        {
            AdjustRollingSound(currentSpeed);
        }
    }
    
    void StartRollingSounds()
    {
        isRolling = true;
        
        if (rollingCoroutine != null)
            StopCoroutine(rollingCoroutine);
            
        rollingCoroutine = StartCoroutine(PlayRollingSoundsRoutine());
    }
    
    void StopRollingSounds()
    {
        isRolling = false;
        
        if (rollingCoroutine != null)
        {
            StopCoroutine(rollingCoroutine);
            rollingCoroutine = null;
        }
        
        // Fade out rolling sound
        StartCoroutine(FadeOutAudioSource(rollingAudioSource, 0.5f));
    }
    
    IEnumerator PlayRollingSoundsRoutine()
    {
        while (isRolling)
        {
            float currentSpeed = rb.linearVelocity.magnitude;
            
            if (currentSpeed > minRollingVelocity)
            {
                float speedRatio = Mathf.Clamp01((currentSpeed - minRollingVelocity) / 
                                               (maxRollingVelocity - minRollingVelocity));
                
                PlayRollingSound(speedRatio);
            }
            
            yield return new WaitForSeconds(rollingSoundInterval);
        }
    }
    
    void PlayCollisionSound(float velocity, SurfaceMaterial material, Vector3 position)
    {
        if (collisionSounds.Length == 0 && 
            (material == null || material.collisionClips.Length == 0))
            return;
        
        // Select audio clip
        AudioClip clipToPlay;
        float volumeMultiplier = 1f;
        float pitchMultiplier = 1f;
        
        if (material != null && material.collisionClips.Length > 0)
        {
            clipToPlay = material.collisionClips[Random.Range(0, material.collisionClips.Length)];
            volumeMultiplier = material.volumeMultiplier;
            pitchMultiplier = material.pitchMultiplier;
        }
        else
        {
            clipToPlay = collisionSounds[Random.Range(0, collisionSounds.Length)];
        }
        
        // Calculate velocity-based parameters
        float velocityRatio = Mathf.Clamp01((velocity - minCollisionVelocity) / 
                                          (maxCollisionVelocity - minCollisionVelocity));
        
        // Calculate volume based on velocity
        float volume = Mathf.Lerp(minVolume, maxVolume, velocityRatio) * volumeMultiplier;
        
        // Calculate pitch based on velocity
        float pitch = Mathf.Lerp(minPitch, maxPitch, velocityRatio) * pitchMultiplier;
        
        // Add some randomness to pitch
        pitch *= Random.Range(0.95f, 1.05f);
        
        // Configure audio source
        collisionAudioSource.clip = clipToPlay;
        collisionAudioSource.volume = volume;
        collisionAudioSource.pitch = pitch;
        
        // Optional: Play at collision point instead of object center
        if (Vector3.Distance(position, transform.position) > 1f)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, position, volume);
        }
        else
        {
            collisionAudioSource.Play();
        }
    }
    
    void PlayRollingSound(float speedRatio)
    {
        if (rollingSounds.Length == 0)
            return;
        
        // Select random rolling sound
        AudioClip clip = rollingSounds[Random.Range(0, rollingSounds.Length)];
        
        // Calculate volume based on speed
        float volume = Mathf.Lerp(minVolume * 0.5f, maxVolume * 0.7f, speedRatio);
        
        // Calculate pitch based on speed (higher speed = higher pitch)
        float pitch = Mathf.Lerp(minPitch * 0.9f, maxPitch * 1.1f, speedRatio);
        
        // Add slight randomness
        pitch *= Random.Range(0.98f, 1.02f);
        
        // Play the sound
        rollingAudioSource.clip = clip;
        rollingAudioSource.volume = volume;
        rollingAudioSource.pitch = pitch;
        
        if (!rollingAudioSource.isPlaying)
            rollingAudioSource.Play();
    }
    
    void AdjustRollingSound(float currentSpeed)
    {
        if (!rollingAudioSource.isPlaying)
            return;
        
        float speedRatio = Mathf.Clamp01((currentSpeed - minRollingVelocity) / 
                                       (maxRollingVelocity - minRollingVelocity));
        
        // Smoothly adjust volume and pitch
        rollingAudioSource.volume = Mathf.Lerp(rollingAudioSource.volume, 
            Mathf.Lerp(minVolume * 0.5f, maxVolume * 0.7f, speedRatio), 
            Time.deltaTime * 5f);
        
        rollingAudioSource.pitch = Mathf.Lerp(rollingAudioSource.pitch,
            Mathf.Lerp(minPitch * 0.9f, maxPitch * 1.1f, speedRatio),
            Time.deltaTime * 5f);
    }
    
    SurfaceMaterial GetSurfaceMaterial(string tag)
    {
        if (surfaceMaterials == null || surfaceMaterials.Length == 0)
            return null;
        
        foreach (var material in surfaceMaterials)
        {
            if (material.materialTag == tag)
                return material;
        }
        
        return null;
    }
    
    IEnumerator FadeOutAudioSource(AudioSource audioSource, float duration)
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / duration);
            yield return null;
        }
        
        audioSource.Stop();
        audioSource.volume = startVolume;
    }
    
    /// <summary>Manually plays a specific collision clip, scaling volume/pitch as if it happened at the given velocity.</summary>
    public void PlayCustomCollisionSound(AudioClip clip, float velocity)
    {
        float velocityRatio = Mathf.Clamp01((velocity - minCollisionVelocity) / 
                                          (maxCollisionVelocity - minCollisionVelocity));
        
        collisionAudioSource.clip = clip;
        collisionAudioSource.volume = Mathf.Lerp(minVolume, maxVolume, velocityRatio);
        collisionAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, velocityRatio);
        collisionAudioSource.Play();
    }
    
    /// <summary>Replaces the collision sound pool at runtime.</summary>
    public void SetCollisionSounds(AudioClip[] newSounds)
    {
        collisionSounds = newSounds;
    }

    /// <summary>Replaces the rolling sound pool at runtime.</summary>
    public void SetRollingSounds(AudioClip[] newSounds)
    {
        rollingSounds = newSounds;
    }

    // Optional: Visual feedback for collisions
    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;
        
        // Draw velocity vector
        if (rb != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2);
            
            // Draw min/max velocity thresholds
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, minCollisionVelocity * 0.1f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, maxCollisionVelocity * 0.1f);
        }
    }
}
}