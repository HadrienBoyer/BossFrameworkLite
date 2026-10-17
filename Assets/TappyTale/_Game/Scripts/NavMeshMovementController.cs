using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>
/// Drives a <see cref="ThirdPersonController"/> toward a NavMeshAgent-computed path by
/// feeding it synthetic camera-relative movement input each frame, so NavMesh pathing and
/// manual player movement can share the same CharacterController-based locomotion.
/// </summary>
[MovedFrom(true, null, null, "NavMeshMovementController")]
public class NavMeshMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private ThirdPersonController thirdPersonController;
    /// <summary>The destination to navigate to. Assign directly or via <see cref="SetTarget"/>.</summary>
    [SerializeField] public Transform target;

    [Header("Settings")]
    [SerializeField] private float stoppingDistance = 1f;
    [SerializeField] private bool disablePlayerControl = true;

    private bool isNavigating = false;
    private bool wasPlayerControlEnabled = true;

    void Start()
    {
        // Get components if not assigned
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        
        if (thirdPersonController == null)
            thirdPersonController = GetComponent<ThirdPersonController>();

        // Configure NavMeshAgent
        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.updatePosition = false; // We'll use CharacterController for movement
            agent.updateRotation = false; // We'll handle rotation manually
        }
    }

    void Update()
    {
        if (!isNavigating || target == null || agent == null || thirdPersonController == null)
            return;

        // Update destination if target moved
        if (Vector3.Distance(agent.destination, target.position) > 0.1f)
        {
            agent.SetDestination(target.position);
        }

        // Get desired velocity from NavMeshAgent
        Vector3 desiredVelocity = agent.desiredVelocity;

        if (desiredVelocity.magnitude > 0.1f)
        {
            // Get camera directions (ThirdPersonController uses camera-relative input)
            Camera mainCamera = Camera.main;
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // Project desired velocity onto camera axes to get proper input values
            float horizontal = Vector3.Dot(desiredVelocity, camRight) / agent.speed;
            float vertical = Vector3.Dot(desiredVelocity, camForward) / agent.speed;

            // Clamp to valid input range
            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            vertical = Mathf.Clamp(vertical, -1f, 1f);

            // Send input to ThirdPersonController
            thirdPersonController.SetNavigationInput(horizontal, vertical);

            // Sync NavMeshAgent position with actual position
            agent.nextPosition = transform.position;
        }

        // Check if reached target
        if (HasReachedTarget())
        {
            StopNavigation();
        }
    }

    /// <summary>Sets the navigation target and immediately begins navigating to it.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        StartNavigation();
    }

    /// <summary>Sends the agent directly to a world position and begins navigating.</summary>
    public void MoveToPosition(Vector3 position)
    {
        if (agent != null)
        {
            agent.SetDestination(position);
            StartNavigation();
        }
    }

    /// <summary>Begins navigating toward <see cref="target"/>, optionally taking over player rotation control (see <see cref="disablePlayerControl"/>).</summary>
    public void StartNavigation()
    {
        if (target == null || agent == null || thirdPersonController == null)
        {
            Debug.LogWarning("Cannot start navigation: Missing target or components");
            return;
        }

        agent.nextPosition = transform.position;

        // Set destination
        agent.SetDestination(target.position);
        
        // Disable player control if needed (but keep movement enabled for navigation)
        if (disablePlayerControl)
        {
            wasPlayerControlEnabled = thirdPersonController.AllowMovement;
            // Keep movement enabled but control rotation ourselves
            thirdPersonController.AllowAimRotation = true; // Enable rotation so character turns while moving
        }

        isNavigating = true;
    }

    /// <summary>Stops navigation, clears the synthetic movement input, and restores player rotation control.</summary>
    public void StopNavigation()
    {
        if (!isNavigating)
            return;

        isNavigating = false;

        // Clear navigation input
        if (thirdPersonController != null)
        {
            thirdPersonController.ClearNavigationInput();
            thirdPersonController.ForceMoveAnim(0f);

            // Restore player control
            if (disablePlayerControl)
            {
                thirdPersonController.AllowAimRotation = wasPlayerControlEnabled;
            }
        }

        // Stop agent
        if (agent != null)
        {
            agent.ResetPath();
        }
    }

    /// <summary>True once the agent's remaining path distance is within its stopping distance.</summary>
    public bool HasReachedTarget()
    {
        if (agent == null || agent.pathPending)
            return false;

        return agent.remainingDistance <= agent.stoppingDistance;
    }

    /// <summary>True while navigation toward a target is in progress.</summary>
    public bool IsNavigating()
    {
        return isNavigating;
    }

    void OnDrawGizmos()
    {
        // Draw line to target
        if (target != null)
        {
            Gizmos.color = isNavigating ? Color.green : Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, stoppingDistance);
        }

        // Draw NavMesh path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.cyan;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
}

#if UNITY_EDITOR
/// <summary>Custom Inspector adding Start/Stop Navigation test buttons for use in Play Mode.</summary>
[CustomEditor(typeof(NavMeshMovementController))]
[MovedFrom(true, null, null, "NavMeshMovementControllerEditor")]
public class NavMeshMovementControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        NavMeshMovementController controller = (NavMeshMovementController)target;

        // Navigation controls
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Navigation Controls", EditorStyles.boldLabel);

        // Status display
        if (Application.isPlaying)
        {
            string status = controller.IsNavigating() ? "NAVIGATING" : "IDLE";
            EditorGUILayout.HelpBox($"Status: {status}", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();

        // Start button
        GUI.enabled = Application.isPlaying && controller.target != null && !controller.IsNavigating();
        if (GUILayout.Button("Start Navigation", GUILayout.Height(30)))
        {
            controller.StartNavigation();
        }

        // Stop button
        GUI.enabled = Application.isPlaying && controller.IsNavigating();
        if (GUILayout.Button("Stop Navigation", GUILayout.Height(30)))
        {
            controller.StopNavigation();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use navigation controls.", MessageType.Info);
        }
        else if (controller.target == null)
        {
            EditorGUILayout.HelpBox("Assign a Target to start navigation.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
}