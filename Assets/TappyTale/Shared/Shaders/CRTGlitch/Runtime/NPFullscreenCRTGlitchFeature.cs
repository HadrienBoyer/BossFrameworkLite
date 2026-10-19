using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TappyTale.NovaParis.VisualEffects
{
    public static class NPFullscreenCRTGlitchGlobals
    {
        public static readonly int Moisture = Shader.PropertyToID("_NPFullscreenCRTMoisture");
        public static readonly int CrtTime = Shader.PropertyToID("_CrtTime");
        public static readonly int RandomSeed = Shader.PropertyToID("_RandomSeed");
        public static readonly int EffectIntensity = Shader.PropertyToID("_EffectIntensity");
        public static readonly int GlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        public static readonly int ArtifactIntensity = Shader.PropertyToID("_ArtifactIntensity");
        public static readonly int GrainIntensity = Shader.PropertyToID("_GrainIntensity");
        public static readonly int ScanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        public static readonly int ChromaticAberration = Shader.PropertyToID("_ChromaticAberration");
        public static readonly int WetBlurMax = Shader.PropertyToID("_WetBlurMax");
        public static readonly int WetDistortion = Shader.PropertyToID("_WetDistortion");
        public static readonly int WetVignette = Shader.PropertyToID("_WetVignette");
    }

    public sealed class NPFullscreenCRTGlitchFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/TappyTale/NovaParis/Fullscreen CRT Digital Glitch";

        [Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Shader shader;
            public bool applyInSceneView;
            [Range(0f, 1f)] public float effectIntensity = 1f;
            [Range(0f, 1f)] public float glitchIntensity = 0.22f;
            [Range(0f, 1f)] public float artifactIntensity = 0.28f;
            [Range(0f, 1f)] public float grainIntensity = 0.16f;
            [Range(0f, 1f)] public float scanlineIntensity = 0.22f;
            [Range(0f, 0.05f)] public float chromaticAberration = 0.007f;
            [Range(0f, 0.02f)] public float wetBlurMax = 0.006f;
            [Range(0f, 0.05f)] public float wetDistortion = 0.015f;
            [Range(0f, 1f)] public float wetVignette = 0.3f;
            public float randomSeed = 0.3615f;
        }

        [SerializeField] private Settings settings = new();

        private Material material;
        private FullscreenPass fullscreenPass;

        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = Shader.Find(ShaderName);

            if (settings.shader != null && material == null)
            {
                material = CoreUtils.CreateEngineMaterial(settings.shader);
                material.name = "NP Fullscreen CRT Digital Glitch";
            }

            fullscreenPass ??= new FullscreenPass(settings);
            fullscreenPass.renderPassEvent = settings.renderPassEvent;
            fullscreenPass.SetMaterial(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null)
                Create();

            if (material == null || fullscreenPass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && !(settings.applyInSceneView && cameraType == CameraType.SceneView))
                return;

            fullscreenPass.renderPassEvent = settings.renderPassEvent;
            fullscreenPass.ConfigureInput(ScriptableRenderPassInput.Color);
            renderer.EnqueuePass(fullscreenPass);
        }

        protected override void Dispose(bool disposing)
        {
            fullscreenPass?.Dispose();
            fullscreenPass = null;
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class FullscreenPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private Material material;

            public FullscreenPass(Settings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler("NP Fullscreen CRT Digital Glitch");
            }

            public void SetMaterial(Material material)
            {
                this.material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                if (!resourceData.cameraColor.IsValid() || !resourceData.activeColorTexture.IsValid())
                    return;

                UpdateMaterial();

                TextureDesc descriptor = renderGraph.GetTextureDesc(resourceData.cameraColor);
                descriptor.name = "_NPFullscreenCRTGlitchSource";
                descriptor.clearBuffer = false;

                TextureHandle sourceCopy = renderGraph.CreateTexture(descriptor);
                renderGraph.AddBlitPass(
                    resourceData.activeColorTexture,
                    sourceCopy,
                    Vector2.one,
                    Vector2.zero,
                    passName: "Copy Color NP CRT Digital Glitch"
                );

                RenderGraphUtils.BlitMaterialParameters blitParameters = new(
                    sourceCopy,
                    resourceData.activeColorTexture,
                    material,
                    0
                );

                renderGraph.AddBlitPass(blitParameters, passName: "NP Fullscreen CRT Digital Glitch");
            }

            private void UpdateMaterial()
            {
                float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

                material.SetFloat(NPFullscreenCRTGlitchGlobals.CrtTime, now);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.RandomSeed, settings.randomSeed);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.EffectIntensity, settings.effectIntensity);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.GlitchIntensity, settings.glitchIntensity);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.ArtifactIntensity, settings.artifactIntensity);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.GrainIntensity, settings.grainIntensity);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.ScanlineIntensity, settings.scanlineIntensity);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.ChromaticAberration, settings.chromaticAberration);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.WetBlurMax, settings.wetBlurMax);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.WetDistortion, settings.wetDistortion);
                material.SetFloat(NPFullscreenCRTGlitchGlobals.WetVignette, settings.wetVignette);
            }

            public void Dispose()
            {
            }
        }
    }
}
