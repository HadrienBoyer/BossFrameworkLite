using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Applies physics force to this object's Rigidbody in response to collisions (from tagged colliders)
/// or explicit impact reports via <see cref="ReceiveImpact"/> (e.g. from a character controller).
/// Auto-adds a Rigidbody if none is assigned.
/// </summary>
[RequireComponent(typeof(Collider))]
[MovedFrom(true, null, null, "ImpactCollisionForce")]
public class ImpactCollisionForce : MonoBehaviour
{
    [Header("Collision Filter")]
    /// <summary>Only collisions from colliders with one of these tags apply force.</summary>
    [SerializeField] private List<string> collisionTags = new List<string> { "Player" };

    [Header("Force Settings")]
    /// <summary>Rigidbody force is applied to. Auto-assigned from this GameObject (adding one if missing) if left empty.</summary>
    [SerializeField] private Rigidbody forceRigidbody;
    [SerializeField] private float forceMultiplier = 1f;
    [SerializeField] private float maxForce = 12f;
    /// <summary>Force magnitude used when the incoming collision/impact has no usable velocity to derive strength from.</summary>
    [SerializeField] private float fallbackForce = 1f;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    private void Awake()
    {
        EnsureForceRigidbody();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasMatchingTag(collision.collider))
            return;

        EnsureForceRigidbody();
        if (forceRigidbody == null || forceRigidbody.isKinematic)
            return;

        Vector3 applicationPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : forceRigidbody.worldCenterOfMass;

        Vector3 forceDirection = collision.contactCount > 0
            ? -collision.GetContact(0).normal
            : (forceRigidbody.worldCenterOfMass - collision.transform.position).normalized;

        if (forceDirection.sqrMagnitude <= Mathf.Epsilon)
            return;

        float strength = collision.relativeVelocity.sqrMagnitude > Mathf.Epsilon
            ? Mathf.Clamp(collision.relativeVelocity.magnitude * forceMultiplier, 0f, maxForce)
            : Mathf.Clamp(fallbackForce * forceMultiplier, 0f, maxForce);

        if (strength <= 0f)
            return;

        Vector3 force = forceDirection.normalized * strength;
        forceRigidbody.AddForceAtPosition(force, applicationPoint, forceMode);
    }

    /// <summary>Applies force at <paramref name="hitPoint"/> away from <paramref name="hitNormal"/>, scaled by <paramref name="speed"/>. Intended for callers (e.g. a character controller) that detect impacts outside Unity's collision callbacks.</summary>
    public void ReceiveImpact(Vector3 hitNormal, Vector3 hitPoint, float speed)
    {
        EnsureForceRigidbody();
        if (forceRigidbody == null || forceRigidbody.isKinematic)
            return;

        float strength = Mathf.Clamp(speed * forceMultiplier, 0f, maxForce);
        if (strength <= 0f)
            strength = Mathf.Clamp(fallbackForce * forceMultiplier, 0f, maxForce);

        Vector3 force = (-hitNormal).normalized * strength;
        forceRigidbody.AddForceAtPosition(force, hitPoint, forceMode);
    }

    private bool HasMatchingTag(Collider otherCollider)
    {
        if (otherCollider == null || collisionTags == null || collisionTags.Count == 0)
            return false;

        for (int i = 0; i < collisionTags.Count; i++)
        {
            string tagName = collisionTags[i];
            if (!string.IsNullOrWhiteSpace(tagName) && otherCollider.CompareTag(tagName))
                return true;
        }

        return false;
    }

    private void EnsureForceRigidbody()
    {
        if (forceRigidbody != null)
            return;

        forceRigidbody = GetComponent<Rigidbody>();
        if (forceRigidbody == null)
            forceRigidbody = gameObject.AddComponent<Rigidbody>();
    }
}
}
