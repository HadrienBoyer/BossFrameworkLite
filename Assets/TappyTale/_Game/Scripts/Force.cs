using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>Applies an impulse force to a child Rigidbody, either on demand (<see cref="ApplyForce"/>) or using the configured default direction/multiplier (<see cref="ApplyDefaultForce"/>).</summary>
[MovedFrom(true, null, null, "Force")]
public class Force : MonoBehaviour
{
    private Rigidbody rb;


    [Header("Default Settings")]
    /// <summary>Direction used by <see cref="ApplyDefaultForce"/>. Also overwritten with the last force vector applied via <see cref="ApplyForce"/>.</summary>
    public Vector3 direction = new Vector3(0, 1, -1);
    public float forceMultiplier = 1f;
    /// <summary>If true, <paramref name="dir"/> in <see cref="ApplyForce"/> is treated as local space and converted to world space before applying.</summary>
    public bool useLocalSpace = false;

    void Awake()
    {
        rb = GetComponentInChildren<Rigidbody>();
    }

    /// <summary>Applies an impulse force of <paramref name="dir"/> * <paramref name="multiplier"/> to the Rigidbody found under this GameObject.</summary>
    public void ApplyForce(Vector3 dir, float multiplier)
    {
        if (rb != null)
        {
            Vector3 force = dir * multiplier;
            direction = force;

            Vector3 worldForce = useLocalSpace ? transform.TransformDirection(force) : force;
            rb.AddForce(worldForce, ForceMode.Impulse);
        }
    }

    /// <summary>Applies force using the configured <see cref="direction"/> and <see cref="forceMultiplier"/>.</summary>
    public void ApplyDefaultForce()
    {
        ApplyForce(direction, forceMultiplier);
    }
}

#if UNITY_EDITOR
/// <summary>Custom inspector for <see cref="Force"/> adding a button to trigger <see cref="Force.ApplyDefaultForce"/> from the Editor.</summary>
[CustomEditor(typeof(Force))]
[MovedFrom(true, null, null, "ForceEditor")]
public class ForceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Force force = (Force)target;

        if (GUILayout.Button("Apply Default Force"))
        {
            force.ApplyDefaultForce();
        }
    }
}
#endif
}
