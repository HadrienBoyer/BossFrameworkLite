using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Plays footstep sounds timed to locomotion speed (read from the Animator's "Move" parameter),
/// selecting a per-surface sound set via a downward raycast from <see cref="groundCheck"/>.
/// </summary>
[MovedFrom(true, null, null, "AdvancedFootstepSystem")]
public class AdvancedFootstepSystem : MonoBehaviour
{
    /// <summary>A footstep sound set used when the ground raycast hits a collider tagged <see cref="tag"/>.</summary>
    [System.Serializable]
    public class SurfaceType
    {
        public string tag;
        public AudioClip[] footstepSounds;
        [Range(0f, 1f)] public float volume = 1f;
        public float minPitch = 0.9f;
        public float maxPitch = 1.1f;
    }

    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    /// <summary>Raycast origin used to detect the surface underfoot; should be placed near the character's feet.</summary>
    [SerializeField] private Transform groundCheck;

    [Header("Settings")]
    [SerializeField] private float stepDistance = 1.5f;
    /// <summary>Per-surface footstep sound sets, matched against the ground raycast hit's tag.</summary>
    [SerializeField] private SurfaceType[] surfaceTypes;
    /// <summary>Fallback footstep sounds used when the ground hit doesn't match any entry in <see cref="surfaceTypes"/>.</summary>
    [SerializeField] private AudioClip[] defaultFootsteps;

    [Header("Running/Walking")]
    /// <summary>Seconds between footsteps while walking (animator "Move" value at or below 0.5).</summary>
    [SerializeField] private float walkStepInterval = 0.5f;
    /// <summary>Seconds between footsteps while running (animator "Move" value between 0.5 and 2).</summary>
    [SerializeField] private float runStepInterval = 0.3f;
    /// <summary>Seconds between footsteps while sprinting (animator "Move" value of 2 or higher).</summary>
    [SerializeField] private float sprintStepInterval = 0.18f;

    private Vector3 lastPosition;
    private float stepTimer;
    private bool isWalking;

    /// <summary>Current locomotion speed read from the Animator's "Move" parameter each frame.</summary>
    public float moveVal = 0;
    
    
    private void Start()
    {
        lastPosition = transform.position;
    }
    
    private void Update()
    {
        // Get current move value from animator
        moveVal = animator.GetFloat("Move");
        
        if (moveVal == 0f)
        {            
            return;
        }

        // 0..0.5 = walk, 0.5..1 = run, 2 = sprint
        isWalking = moveVal <= 0.5f;
        
        // Only play footsteps if grounded and moving
        if (!controller.isGrounded || controller.velocity.magnitude < 0.1f)
        {
            return;
        }
        
        // Decrease step timer
        stepTimer -= Time.deltaTime;
        
        // Play footstep when timer runs out
        if (stepTimer <= 0f)
        {
            PlayFootstepBasedOnSurface();
            
            float stepInterval = moveVal >= 2f ? sprintStepInterval
                               : isWalking      ? walkStepInterval
                               :                  runStepInterval;
            stepTimer = stepInterval;
        }
    }
    
    private void PlayFootstepBasedOnSurface()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, out RaycastHit hit, 0.5f))
        {
            string surfaceTag = hit.collider.tag;
            SurfaceType surface = GetSurfaceType(surfaceTag);
            AudioClip[] clipsToPlay = surface != null ? surface.footstepSounds : defaultFootsteps;
            float surfaceVolume = surface != null ? surface.volume : 1f;

            if (clipsToPlay != null && clipsToPlay.Length > 0)
            {
                AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
                float minP = surface != null ? surface.minPitch : 0.9f;
                float maxP = surface != null ? surface.maxPitch : 1.1f;
                audioSource.pitch = Random.Range(minP, maxP);
                audioSource.PlayOneShot(clip, surfaceVolume * Random.Range(0.8f, 1.0f));
            }
        }
    }

    private SurfaceType GetSurfaceType(string surfaceTag)
    {
        foreach (SurfaceType surface in surfaceTypes)
        {
            if (surface.tag == surfaceTag)
                return surface;
        }
        return null;
    }
}
}