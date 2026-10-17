using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Add this to the same GameObject as the CharacterController.
/// It forwards OnControllerColliderHit events to ImpactCollisionForce
/// on any object the character walks into.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[MovedFrom(true, null, null, "CharacterControllerImpact")]
public class CharacterControllerImpact : MonoBehaviour
{
    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        ImpactCollisionForce impactForce = hit.collider.GetComponent<ImpactCollisionForce>();
        if (impactForce == null)
            return;

        float speed = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;
        impactForce.ReceiveImpact(hit.normal, hit.point, speed);
    }
}
}
