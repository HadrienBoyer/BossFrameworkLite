using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TappyTale.NovaParis.VisualEffects
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("TappyTale/Nova Paris/Visual Effects/CRT Glitch Controller")]
    public sealed class NPCRTGlitchController : MonoBehaviour
    {
        private const string ShaderName = "TappyTale/NovaParis/CRT Glitch Noise";

        [Header("Targets")]
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Texture textureOverride;
        [SerializeField] private bool autoFindTarget = true;

        [Header("Material")]
        [SerializeField] private Shader crtShader;
        [SerializeField] private bool createMaterialInstance = true;
        [SerializeField] private bool applyInEditMode = true;
        [SerializeField] private bool uiUsesAlwaysZTest = true;
        [SerializeField] private CompareFunction worldZTest = CompareFunction.LessEqual;
        [SerializeField] private CullMode worldCullMode = CullMode.Off;

        [Header("Settings")]
        [SerializeField] private NPCRTGlitchPreset preset;
        [SerializeField] private bool copyPresetToLocalSettings;
        [SerializeField] private NPCRTGlitchSettings settings = new();

        [Header("Animation")]
        [SerializeField] private bool animate = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private float randomSeed = 0.3615f;

        [Header("Random Glitch Bursts")]
        [SerializeField] private bool randomBursts = true;
        [SerializeField, Min(0.05f)] private float minSecondsBetweenBursts = 1.2f;
        [SerializeField, Min(0.05f)] private float maxSecondsBetweenBursts = 4.0f;
        [SerializeField, Min(0.01f)] private float burstDuration = 0.14f;
        [SerializeField, Range(0f, 1f)] private float burstAmount = 0.75f;

        private Material runtimeMaterial;
        private Material previousGraphicMaterial;
        private Material previousRendererMaterial;
        private float startedAt;
        private float nextBurstAt;
        private float burstStartedAt;
        private bool isBursting;
        private System.Random burstRandom;

        public Material RuntimeMaterial => runtimeMaterial;
        public NPCRTGlitchSettings Settings => settings;

        private void Reset()
        {
            autoFindTarget = true;
            randomSeed = Mathf.Abs(transform.GetInstanceID() * 0.0001f) + 0.3615f;
            ResolveTargets();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            ResolveTargets();
            ApplyPresetIfRequested();
            CreateOrAssignMaterial();

            startedAt = GetNow();
            burstRandom = new System.Random(Mathf.RoundToInt(randomSeed * 100000f) ^ GetInstanceID());
            ScheduleNextBurst(startedAt);
            ApplyAllProperties(forceTexture: true);
        }

        private void OnDisable()
        {
            RestorePreviousMaterials();
            DestroyRuntimeMaterial();
        }

        private void OnValidate()
        {
            minSecondsBetweenBursts = Mathf.Max(0.05f, minSecondsBetweenBursts);
            maxSecondsBetweenBursts = Mathf.Max(minSecondsBetweenBursts, maxSecondsBetweenBursts);
            burstDuration = Mathf.Max(0.01f, burstDuration);

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                RestorePreviousMaterials();
                DestroyRuntimeMaterial();
                return;
            }

            ResolveTargets();
            ApplyPresetIfRequested();
            CreateOrAssignMaterial();
            ApplyAllProperties(forceTexture: true);
        }

        private void Update()
        {
            if (!animate && Application.isPlaying)
            {
                ApplyAllProperties(forceTexture: false);
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            ApplyAllProperties(forceTexture: false);
        }

        public void ApplyPreset(NPCRTGlitchPreset newPreset, bool copyToLocal)
        {
            preset = newPreset;
            copyPresetToLocalSettings = copyToLocal;
            ApplyPresetIfRequested();
            ApplyAllProperties(forceTexture: true);
        }

        public void TriggerBurst(float amount = 1f)
        {
            burstAmount = Mathf.Clamp01(amount);
            burstStartedAt = GetNow();
            isBursting = true;
        }

        public void SetTexture(Texture texture)
        {
            textureOverride = texture;
            ApplyAllProperties(forceTexture: true);
        }

        private void ResolveTargets()
        {
            if (!autoFindTarget)
            {
                return;
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }
        }

        private void ApplyPresetIfRequested()
        {
            if (preset == null || !copyPresetToLocalSettings)
            {
                return;
            }

            settings.CopyFrom(preset.Settings);
            copyPresetToLocalSettings = false;
        }

        private void CreateOrAssignMaterial()
        {
            if (targetGraphic == null && targetRenderer == null)
            {
                return;
            }

            if (crtShader == null)
            {
                crtShader = Shader.Find(ShaderName);
            }

            if (crtShader == null)
            {
                Debug.LogError($"[{nameof(NPCRTGlitchController)}] Shader not found: {ShaderName}", this);
                return;
            }

            if (runtimeMaterial != null && runtimeMaterial.shader != crtShader)
            {
                DestroyRuntimeMaterial();
            }

            if (runtimeMaterial == null)
            {
                Material source = FindSourceMaterial();
                runtimeMaterial = createMaterialInstance && source != null
                    ? new Material(source)
                    : new Material(crtShader);

                runtimeMaterial.shader = crtShader;
                runtimeMaterial.name = $"{ShaderName} Instance ({gameObject.name})";
                runtimeMaterial.hideFlags = HideFlags.DontSave;
            }

            AssignMaterialToTargets();
        }

        private Material FindSourceMaterial()
        {
            if (targetGraphic != null && targetGraphic.material != null)
            {
                return targetGraphic.material;
            }

            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                return targetRenderer.sharedMaterial;
            }

            return null;
        }

        private void AssignMaterialToTargets()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (targetGraphic != null && targetGraphic.material != runtimeMaterial)
            {
                if (previousGraphicMaterial == null)
                {
                    previousGraphicMaterial = targetGraphic.material;
                }

                targetGraphic.material = runtimeMaterial;
                targetGraphic.SetMaterialDirty();
            }

            if (targetRenderer != null && targetRenderer.sharedMaterial != runtimeMaterial)
            {
                if (previousRendererMaterial == null)
                {
                    previousRendererMaterial = targetRenderer.sharedMaterial;
                }

                targetRenderer.sharedMaterial = runtimeMaterial;
            }
        }

        private void RestorePreviousMaterials()
        {
            if (targetGraphic != null && targetGraphic.material == runtimeMaterial)
            {
                targetGraphic.material = previousGraphicMaterial;
                targetGraphic.SetMaterialDirty();
            }

            if (targetRenderer != null && targetRenderer.sharedMaterial == runtimeMaterial)
            {
                targetRenderer.sharedMaterial = previousRendererMaterial;
            }

            previousGraphicMaterial = null;
            previousRendererMaterial = null;
        }

        private void DestroyRuntimeMaterial()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }

            runtimeMaterial = null;
        }

        private void ApplyAllProperties(bool forceTexture)
        {
            if (runtimeMaterial == null)
            {
                CreateOrAssignMaterial();
            }

            if (runtimeMaterial == null)
            {
                return;
            }

            float now = GetNow();
            float effectTime = animate ? (now - startedAt) * timeScale : 0f;
            float burst01 = UpdateBurst(now);

            settings.ApplyTo(runtimeMaterial, burst01);
            runtimeMaterial.SetFloat(NPCRTGlitchShaderIds.CrtTime, effectTime);
            runtimeMaterial.SetFloat(NPCRTGlitchShaderIds.RandomSeed, randomSeed);
            runtimeMaterial.SetColor(NPCRTGlitchShaderIds.Color, Color.white);

            ApplyRenderState();

            if (forceTexture || textureOverride != null)
            {
                ApplyTexture();
            }

            if (targetGraphic != null)
            {
                targetGraphic.SetMaterialDirty();
            }
        }

        private void ApplyRenderState()
        {
            bool usesGraphicOnly = targetGraphic != null && targetRenderer == null;

            CompareFunction zTest = usesGraphicOnly && uiUsesAlwaysZTest
                ? CompareFunction.Always
                : worldZTest;

            runtimeMaterial.SetFloat(NPCRTGlitchShaderIds.ZTest, (float)zTest);
            runtimeMaterial.SetFloat(NPCRTGlitchShaderIds.Cull, (float)worldCullMode);
        }

        private void ApplyTexture()
        {
            if (textureOverride != null)
            {
                runtimeMaterial.SetTexture(NPCRTGlitchShaderIds.MainTex, textureOverride);
                return;
            }

            if (targetGraphic != null && targetGraphic.mainTexture != null)
            {
                runtimeMaterial.SetTexture(NPCRTGlitchShaderIds.MainTex, targetGraphic.mainTexture);
                return;
            }

            if (previousRendererMaterial != null && previousRendererMaterial.HasProperty(NPCRTGlitchShaderIds.MainTex))
            {
                runtimeMaterial.SetTexture(NPCRTGlitchShaderIds.MainTex, previousRendererMaterial.GetTexture(NPCRTGlitchShaderIds.MainTex));
            }
        }

        private float UpdateBurst(float now)
        {
            if (!randomBursts)
            {
                isBursting = false;
                return 0f;
            }

            if (!isBursting && now >= nextBurstAt)
            {
                burstStartedAt = now;
                isBursting = true;
            }

            if (!isBursting)
            {
                return 0f;
            }

            float normalizedAge = Mathf.Clamp01((now - burstStartedAt) / burstDuration);
            float shaped = Mathf.Sin(normalizedAge * Mathf.PI);

            if (normalizedAge >= 1f)
            {
                isBursting = false;
                ScheduleNextBurst(now);
            }

            return shaped * burstAmount;
        }

        private void ScheduleNextBurst(float now)
        {
            if (burstRandom == null)
            {
                burstRandom = new System.Random(Mathf.RoundToInt(randomSeed * 100000f) ^ GetInstanceID());
            }

            double random01 = burstRandom.NextDouble();
            float delay = Mathf.Lerp(minSecondsBetweenBursts, maxSecondsBetweenBursts, (float)random01);
            nextBurstAt = now + delay;
        }

        private float GetNow()
        {
            if (Application.isPlaying)
            {
                return useUnscaledTime ? Time.unscaledTime : Time.time;
            }

            return Time.realtimeSinceStartup;
        }
    }
}
