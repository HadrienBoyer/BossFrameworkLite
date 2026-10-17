using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Object inspector popup: instantiates a copy of a given object into a turntable view so
/// the player can examine it up close, without touching the original in the scene.
/// </summary>
[MovedFrom(true, null, null, "ObjectInspectorListener")]
public class ObjectInspectorListener : MonoBehaviour
{
    /// <summary>Controls rotation/spin of the inspected object copy.</summary>
    public ObjectRotator rotator;
    /// <summary>Parent transform the inspected object copy is placed under.</summary>
    public Transform objParent;
    private GameObject curObject;

    /// <summary>Unity callback: clears the inspected object copy when this popup is disabled.</summary>
    public void OnDisable()
    {
        ClearObject();
    }

    /// <summary>Clears any previously inspected object, then instantiates a copy of <paramref name="_object"/> for inspection.</summary>
    public void SetObject(GameObject _object)
    {
        ClearObject();

        curObject = Instantiate(_object, objParent.position, Quaternion.identity);
        curObject.GetComponent<Collider>().enabled = false; // Disable collider for inspection

        curObject.transform.SetParent(objParent);
        curObject.transform.localPosition = Vector3.zero;
        curObject.transform.localRotation = Quaternion.identity;
        curObject.transform.localScale = Vector3.one;

        rotator.SetReferenceCamera(Camera.main);
        rotator.ResetRotation();
        rotator.StartAnim();
    }

    /// <summary>Destroys the currently inspected object copy, if any.</summary>
    public void ClearObject()
    {
        if(curObject != null)
        {
            Destroy(curObject);
            curObject = null;
        }
    }
}
}
