using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Animated door with separate open/close/force-close actions, each with its own audio,
/// optional event delay, and UnityEvent hook. Requires an <see cref="Animator"/> with an
/// "Open" bool parameter (and a "ForceClose" trigger for <see cref="ForceClose"/>).
/// </summary>
[RequireComponent(typeof(AudioSource))]
[MovedFrom(true, null, null, "Door")]
public class Door : MonoBehaviour
{
    public enum DoorState { DEFAULT, OPEN, CLOSED }

    [SerializeField] private Animator anim;
    private AudioSource audioSource;

    [Header("Start")]
    /// <summary>State to force this door into on <see cref="Start"/>. DEFAULT leaves the Animator's own state untouched.</summary>
    public DoorState onStartState = DoorState.DEFAULT;

    [Header("Open")]
    /// <summary>If true, <see cref="OpenDoor"/> persists the open state to PlayerPrefs so it survives a reload (see <see cref="DoorStateKey"/>).</summary>
    public bool savePrefOnOpen = true;
    public AudioClip openDoor;
    /// <summary>Seconds to wait after opening before firing <see cref="onOpen"/>.</summary>
    public float onOpenEventDelay = 0f;
    public UnityEvent onOpen;

    [Header("Close")]
    /// <summary>If true, <see cref="CloseDoor"/> persists the closed state to PlayerPrefs so it survives a reload (see <see cref="DoorStateKey"/>).</summary>
    public bool savePrefOnClose = true;
    public AudioClip closeDoor;
    /// <summary>Seconds to wait after closing before firing <see cref="onClose"/>.</summary>
    public float onCloseEventDelay = 0f;
    public UnityEvent onClose;

    [Header("Force Close")]
    /// <summary>If true, <see cref="ForceClose"/> persists the closed state to PlayerPrefs so it survives a reload (see <see cref="DoorStateKey"/>).</summary>
    public bool savePrefOnForceClose = true;
    public AudioClip forceCloseDoor;
    /// <summary>Seconds to wait after force-closing before firing <see cref="onForceClose"/>.</summary>
    public float onForceCloseEventDelay = 0f;
    public UnityEvent onForceClose;

    /// <summary>PlayerPrefs key this door's open/closed state is saved under, unique per GameObject name.</summary>
    private string DoorStateKey => $"Door_{gameObject.name}_Open";


    private void Awake()
    {
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        // A previously saved state (from OpenDoor/CloseDoor/ForceClose with their
        // matching savePrefOn* flag enabled) takes priority over onStartState.
        if (PlayerPrefs.HasKey(DoorStateKey))
        {
            PrefLoadHandling(PlayerPrefs.GetInt(DoorStateKey), false);
            return;
        }

        switch (onStartState)
        {
            case DoorState.OPEN:
                OpenDoor();
                break;
            case DoorState.CLOSED:
                CloseDoor();
                break;
            case DoorState.DEFAULT:
            default:
                break;
        }

    }

    /// <summary>Plays the open animation/sound and fires <see cref="onOpen"/> (after <see cref="onOpenEventDelay"/>, if set). No-op if already open.</summary>
    [ContextMenu("Open Door")]
    public void OpenDoor() {

        if(anim && anim.GetBool("Open")) return; // If already open, do nothing

        if(openDoor && audioSource)
            audioSource.PlayOneShot(openDoor);
    
        if(anim)
            anim.SetBool("Open", true);

        if(TryGetComponent(out Collider col))
            col.enabled = true;

        if (savePrefOnOpen)
        {
            PlayerPrefs.SetInt(DoorStateKey, 1);
            PlayerPrefs.Save();
        }

        if (onOpenEventDelay > 0f)
            Invoke(nameof(InvokeOnOpen), onOpenEventDelay);
        else
            onOpen?.Invoke();
    }

    private void InvokeOnOpen()
    {
        onOpen?.Invoke();
    }

    /// <summary>Plays the close animation/sound and fires <see cref="onClose"/> (after <see cref="onCloseEventDelay"/>, if set). No-op if already closed.</summary>
    [ContextMenu("Close Door")]
    public void CloseDoor()
    {
        if(anim && anim.GetBool("Open") == false) return; // If already closed, do nothing

        if(closeDoor && audioSource)
            audioSource.PlayOneShot(closeDoor);

        if(anim)
            anim.SetBool("Open", false);

        if(TryGetComponent(out Collider col))
            col.enabled = true;

        if (savePrefOnClose)
        {
            PlayerPrefs.SetInt(DoorStateKey, 0);
            PlayerPrefs.Save();
        }

        if (onCloseEventDelay > 0f)
            Invoke(nameof(InvokeOnClose), onCloseEventDelay);
        else
            onClose?.Invoke();
    }

    private void InvokeOnClose()
    {
        onClose?.Invoke();
    }

    /// <summary>Immediately closes the door via the Animator's "ForceClose" trigger, e.g. to interrupt an in-progress open. Fires <see cref="onForceClose"/> (after <see cref="onForceCloseEventDelay"/>, if set).</summary>
    public void ForceClose()
    {
        if(forceCloseDoor && audioSource)
            audioSource.PlayOneShot(forceCloseDoor);

        if(anim)
            anim.SetTrigger("ForceClose");
        if(anim)
            anim.SetBool("Open", false);

        if(TryGetComponent(out Collider col))
            col.enabled = true;

        if (savePrefOnForceClose)
        {
            PlayerPrefs.SetInt(DoorStateKey, 0);
            PlayerPrefs.Save();
        }

        if (onForceCloseEventDelay > 0f)
            Invoke(nameof(InvokeOnForceClose), onForceCloseEventDelay);
        else
            onForceClose?.Invoke();
    }

    private void InvokeOnForceClose()
    {
        onForceClose?.Invoke();
    }

    /// <summary>Opens the door if closed, or closes it if open.</summary>
    public void ToggleDoor()
    {
        if (anim && anim.GetBool("Open"))
            CloseDoor();
        else
            OpenDoor();
    }

    /// <summary>Restores door state from a saved value without playing audio, e.g. right after a level load. <paramref name="_open"/> is 1 for open, 0 for closed; pass <paramref name="_callEvents"/> to still fire <see cref="onOpen"/>/<see cref="onClose"/>.</summary>
    public void PrefLoadHandling(int _open, bool _callEvents)
    {
        audioSource.enabled = false; // Disable audio during loading to prevent unwanted sounds
        
        if (_open == 1){

            if(anim)
                anim.SetBool("Open", true);

            if(_callEvents)                
                onOpen.Invoke();
        }
        else
        {
            if(anim)
                anim.SetBool("Open", false);

            if(_callEvents)                
                onClose.Invoke();
        }

        audioSource.enabled = true; // Re-enable audio after loading
        if(TryGetComponent(out Collider col))
            col.enabled = _open == 0; // Enable collider if door is closed, disable if open
    }
}
}
