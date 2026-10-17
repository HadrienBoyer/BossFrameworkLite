using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Fires a UnityEvent in response to a configurable trigger (enable/disable/start, a trigger/collision
/// hit, or an external interaction call), after an optional delay.
/// </summary>
[MovedFrom(true, null, null, "EventTriggerSystem")]
public class EventTriggerSystem : MonoBehaviour
{
    /// <summary>Which condition invokes <see cref="eventsToTrigger"/>.</summary>
    public enum Type {

        ON_ENABLE,
        ON_DISABLE,
        ON_START,
        ON_TRIGGER_ENTER,
        ON_COLLISION_ENTER,
        ON_INTERACTION
    }
    public Type type;
    /// <summary>Delay in seconds between the trigger condition firing and <see cref="eventsToTrigger"/> being invoked.</summary>
    public float eventTriggerDelay = 1;

    /// <summary>If true, <see cref="eventTriggerDelay"/> is overwritten with a random value in [<see cref="randomTriggerDelayMin"/>, <see cref="randomTriggerDelayMax"/>] on enable.</summary>
    public bool hasRandomTriggerDelay = false;
    public float randomTriggerDelayMin = 0.5f;
    public float randomTriggerDelayMax = 2f;

    [Header("On Hit related Values")]
    /// <summary>If true, any collider/collision triggers the event regardless of tag; otherwise only <see cref="triggerObjTag"/> matches trigger it.</summary>
    public bool triggerByAll = false;
    public string triggerObjTag = "Player";
    /// <summary>If true, the event only fires once; subsequent trigger/collision hits are ignored.</summary>
    public bool performActionOnce = true;
    /// <summary>If true, this GameObject is deactivated after the event fires.</summary>
    public bool disbaleObjOnAction = false;

    private bool actionDone = false;

    [Space(20)]
    public UnityEvent eventsToTrigger;

    private void OnEnable()
    {
        if(hasRandomTriggerDelay)
        {
            eventTriggerDelay = Random.Range(randomTriggerDelayMin, randomTriggerDelayMax);
        }

        if (type == Type.ON_ENABLE)
        {
            CallEvent();
        }
    }
    private void OnDisable()
    {
        if (type == Type.ON_DISABLE)
        {
            CallEvent();
        }
    }

    private void Start()
    {
        if (type == Type.ON_START) {

            CallEvent();
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (type != Type.ON_TRIGGER_ENTER)
            return;

        if (performActionOnce && actionDone) 
        {
            return;
        }

        if (triggerByAll)
        {
            CallEvent();
        }
        else {

            if (other.CompareTag(triggerObjTag)) {

                CallEvent();
            }
        }        
    }

    /// <summary>Call this from an external interaction source (e.g. an <see cref="InteractableObject"/>'s UnityEvent) to fire <see cref="eventsToTrigger"/> when <see cref="type"/> is <see cref="Type.ON_INTERACTION"/>.</summary>
    public void Interact()
    {
        if (type != Type.ON_INTERACTION)
            return;

        if (performActionOnce && actionDone)
        {
            return;
        }

        CallEvent();
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (type != Type.ON_COLLISION_ENTER)
            return;

        if (performActionOnce && actionDone)
        {
            return;
        }

        if (triggerByAll)
        {
            CallEvent();
        }
        else
        {

            if (collision.collider.CompareTag(triggerObjTag))
            {
                CallEvent();
            }
        }
    }

    void CallEvent() {

        SetActionDone();

        if (eventTriggerDelay <= 0)
        {
            EventAction();
        }
        else {

            StartCoroutine(CR_TriggerEvent());
        }
        
    }

    IEnumerator CR_TriggerEvent()
    {
        yield return new WaitForSeconds(eventTriggerDelay);
        EventAction();
    }

    void EventAction() {

        eventsToTrigger?.Invoke();


        if (disbaleObjOnAction)
            this.gameObject.SetActive(false);

    }

    void SetActionDone() {

        if (performActionOnce)
        {
            actionDone = true;
        }
    }
}
}
