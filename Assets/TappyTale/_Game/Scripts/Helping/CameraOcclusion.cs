using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Fades out renderers that block the line of sight between this camera and <see cref="target"/>,
/// by swapping in transparent material instances and lerping their alpha while occluding.
/// </summary>
[MovedFrom(true, null, null, "CameraOcclusion")]
public class CameraOcclusion : MonoBehaviour
{
    [Header("Target")]
    /// <summary>Usually the player or camera focus point; occlusion is tested along the line from this transform to the target.</summary>
    [SerializeField] private Transform target;

    [Header("Occlusion")]
    [Tooltip("Layers that should be dimmed when blocking the view")]
    [SerializeField] private LayerMask occlusionLayers;

    [Tooltip("Alpha applied to occluding objects")]
    [Range(0f, 1f)]
    [SerializeField] private float occludedAlpha = 0.5f;

    [Tooltip("Lerp speed for alpha transition")]
    [SerializeField] private float fadeSpeed = 10f;

    [Tooltip("Ignore these renderers (e.g., UI, particles)")]
    [SerializeField] private LayerMask ignoreLayers;

    // Stores renderers and their materials
    private class RendererState
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] modifiedMaterials;
        public float currentAlpha = 1f;
        public bool isModified = false;
    }

    private readonly Dictionary<Renderer, RendererState> rendererStates = new();
    private readonly List<Renderer> currentFrameHits = new();

    [Header("Debug")]
    [Tooltip("Colliders currently hit by the occlusion ray (updated every frame in Play mode)")]
    [SerializeField] private List<string> hittingColliderLog = new();

    private void LateUpdate()
    {
        HandleOcclusion();
        UpdateAlphaTransitions();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void HandleOcclusion()
    {
        if (!target) return;

        currentFrameHits.Clear();
        hittingColliderLog.Clear();

        Vector3 camPos = transform.position;
        Vector3 targetPos = target.position;
        Vector3 direction = targetPos - camPos;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(
            camPos,
            direction.normalized,
            distance,
            occlusionLayers
        );

        foreach (RaycastHit hit in hits)
        {
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (!renderer || ((1 << renderer.gameObject.layer) & ignoreLayers) != 0)
                continue;

            currentFrameHits.Add(renderer);
            hittingColliderLog.Add(hit.collider.name);

            if (!rendererStates.ContainsKey(renderer))
            {
                // Store original state
                var state = new RendererState
                {
                    renderer = renderer,
                    originalMaterials = renderer.sharedMaterials,
                    modifiedMaterials = new Material[renderer.sharedMaterials.Length],
                    currentAlpha = 1f
                };

                // Create modified material instances
                for (int i = 0; i < state.originalMaterials.Length; i++)
                {
                    if (state.originalMaterials[i] != null)
                    {
                        state.modifiedMaterials[i] = new Material(state.originalMaterials[i]);
                        SetupMaterialForTransparency(state.modifiedMaterials[i]);
                    }
                }

                rendererStates.Add(renderer, state);
            }

            // Apply modified materials
            var stateToModify = rendererStates[renderer];
            if (!stateToModify.isModified)
            {
                stateToModify.isModified = true;
                renderer.materials = stateToModify.modifiedMaterials;
            }
        }

        // Restore renderers no longer occluding
        List<Renderer> toRemove = new();
        foreach (var kvp in rendererStates)
        {
            if (!currentFrameHits.Contains(kvp.Key))
            {
                kvp.Value.isModified = false;
                kvp.Value.renderer.materials = kvp.Value.originalMaterials;
                toRemove.Add(kvp.Key);
            }
        }

        foreach (Renderer renderer in toRemove)
        {
            // Clean up created materials
            if (rendererStates.TryGetValue(renderer, out var state))
            {
                foreach (var mat in state.modifiedMaterials)
                {
                    if (mat != null && Application.isPlaying)
                        Destroy(mat);
                }
            }
            rendererStates.Remove(renderer);
        }
    }

    private void UpdateAlphaTransitions()
    {
        foreach (var kvp in rendererStates)
        {
            var state = kvp.Value;
            float targetAlpha = state.isModified ? occludedAlpha : 1f;

            // Smooth transition
            state.currentAlpha = Mathf.Lerp(state.currentAlpha, targetAlpha,
                fadeSpeed * Time.deltaTime);

            // Apply alpha to all materials
            for (int i = 0; i < state.modifiedMaterials.Length; i++)
            {
                if (state.modifiedMaterials[i] != null)
                {
                    Color color = state.modifiedMaterials[i].color;
                    color.a = state.currentAlpha;
                    state.modifiedMaterials[i].color = color;
                }
            }
        }
    }

    private void SetupMaterialForTransparency(Material mat)
    {
        if (mat == null) return;

        // Force material to use transparency
        if (mat.HasProperty("_Surface")) // URP Lit shader
        {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetFloat("_ZWrite", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000; // Transparent queue

            // Update shader keywords
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_ALPHAMODULATE_ON");
        }
        else if (mat.HasProperty("_Mode")) // Built-in Standard shader
        {
            mat.SetFloat("_Mode", 3f); // 3 = Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;

            // Update shader keywords
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        // Force update material properties
        mat.enableInstancing = false; // Disable instancing for dynamic changes
    }

    private void RestoreAll()
    {
        foreach (var kvp in rendererStates)
        {
            var state = kvp.Value;

            if (state.renderer != null)
            {
                state.renderer.materials = state.originalMaterials;
            }

            // Clean up created materials
            foreach (var mat in state.modifiedMaterials)
            {
                if (mat != null && Application.isPlaying)
                    Destroy(mat);
            }
        }

        rendererStates.Clear();
        currentFrameHits.Clear();
    }

    // For debugging
    private void OnDrawGizmosSelected()
    {
        if (!target) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, target.position);
    }
}
}