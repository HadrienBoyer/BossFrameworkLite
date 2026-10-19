using System;
using UnityEngine;

namespace TappyTale.NovaParis.VisualEffects
{
    [Serializable]
    public sealed class NPCRTGlitchSettings
    {
        [Header("CRT")]
        [Range(0f, 1f)] public float curvature = 0.15f;
        [Range(0f, 1f)] public float vignetteIntensity = 0.28f;
        [Range(0f, 1f)] public float scanlineIntensity = 0.32f;
        [Range(10f, 1200f)] public float scanlineDensity = 420f;
        [Range(-10f, 10f)] public float scanlineSpeed = 0.15f;
        [Range(0f, 1f)] public float interlaceIntensity = 0.12f;
        [Range(10f, 1200f)] public float interlaceDensity = 240f;
        [Range(0f, 30f)] public float interlaceSpeed = 8f;

        [Header("Glitch")]
        [Range(0f, 1f)] public float glitchIntensity = 0.18f;
        [Range(0f, 0.25f)] public float glitchStrength = 0.035f;
        [Range(2f, 120f)] public float glitchBlockDensity = 32f;
        [Range(0f, 60f)] public float glitchSpeed = 16f;
        [Range(0f, 0.08f)] public float horizontalJitter = 0.006f;
        [Range(-1f, 1f)] public float verticalRoll = 0f;
        [Range(0f, 0.05f)] public float chromaticAberration = 0.006f;

        [Header("Noise")]
        [Range(0f, 1f)] public float noiseIntensity = 0.12f;
        [Range(8f, 1200f)] public float noiseScale = 320f;
        [Range(0f, 80f)] public float noiseSpeed = 22f;

        [Header("Color")]
        [Range(0f, 3f)] public float brightness = 1f;
        [Range(0f, 3f)] public float contrast = 1.1f;
        [Range(0f, 2f)] public float saturation = 1f;
        [Range(0f, 1f)] public float alpha = 1f;
        public Color phosphorTint = new(0.75f, 1f, 0.82f, 1f);

        public void ApplyTo(Material material, float burst01 = 0f)
        {
            if (material == null)
            {
                return;
            }

            burst01 = Mathf.Clamp01(burst01);

            material.SetFloat(NPCRTGlitchShaderIds.Curvature, curvature);
            material.SetFloat(NPCRTGlitchShaderIds.VignetteIntensity, vignetteIntensity);
            material.SetFloat(NPCRTGlitchShaderIds.ScanlineIntensity, scanlineIntensity);
            material.SetFloat(NPCRTGlitchShaderIds.ScanlineDensity, scanlineDensity);
            material.SetFloat(NPCRTGlitchShaderIds.ScanlineSpeed, scanlineSpeed);
            material.SetFloat(NPCRTGlitchShaderIds.InterlaceIntensity, interlaceIntensity);
            material.SetFloat(NPCRTGlitchShaderIds.InterlaceDensity, interlaceDensity);
            material.SetFloat(NPCRTGlitchShaderIds.InterlaceSpeed, interlaceSpeed);

            float burstGlitchIntensity = Mathf.Clamp01(glitchIntensity + burst01 * 0.55f);
            float burstGlitchStrength = Mathf.Clamp(glitchStrength + burst01 * 0.06f, 0f, 0.25f);
            float burstChromaticAberration = Mathf.Clamp(chromaticAberration + burst01 * 0.018f, 0f, 0.05f);
            float burstHorizontalJitter = Mathf.Clamp(horizontalJitter + burst01 * 0.02f, 0f, 0.08f);
            float burstNoiseIntensity = Mathf.Clamp01(noiseIntensity + burst01 * 0.25f);

            material.SetFloat(NPCRTGlitchShaderIds.GlitchIntensity, burstGlitchIntensity);
            material.SetFloat(NPCRTGlitchShaderIds.GlitchStrength, burstGlitchStrength);
            material.SetFloat(NPCRTGlitchShaderIds.GlitchBlockDensity, glitchBlockDensity);
            material.SetFloat(NPCRTGlitchShaderIds.GlitchSpeed, glitchSpeed);
            material.SetFloat(NPCRTGlitchShaderIds.HorizontalJitter, burstHorizontalJitter);
            material.SetFloat(NPCRTGlitchShaderIds.VerticalRoll, verticalRoll);
            material.SetFloat(NPCRTGlitchShaderIds.ChromaticAberration, burstChromaticAberration);

            material.SetFloat(NPCRTGlitchShaderIds.NoiseIntensity, burstNoiseIntensity);
            material.SetFloat(NPCRTGlitchShaderIds.NoiseScale, noiseScale);
            material.SetFloat(NPCRTGlitchShaderIds.NoiseSpeed, noiseSpeed);

            material.SetFloat(NPCRTGlitchShaderIds.Brightness, brightness);
            material.SetFloat(NPCRTGlitchShaderIds.Contrast, contrast);
            material.SetFloat(NPCRTGlitchShaderIds.Saturation, saturation);
            material.SetFloat(NPCRTGlitchShaderIds.Alpha, alpha);
            material.SetColor(NPCRTGlitchShaderIds.PhosphorTint, phosphorTint);
        }

        public void CopyFrom(NPCRTGlitchSettings other)
        {
            if (other == null)
            {
                return;
            }

            curvature = other.curvature;
            vignetteIntensity = other.vignetteIntensity;
            scanlineIntensity = other.scanlineIntensity;
            scanlineDensity = other.scanlineDensity;
            scanlineSpeed = other.scanlineSpeed;
            interlaceIntensity = other.interlaceIntensity;
            interlaceDensity = other.interlaceDensity;
            interlaceSpeed = other.interlaceSpeed;

            glitchIntensity = other.glitchIntensity;
            glitchStrength = other.glitchStrength;
            glitchBlockDensity = other.glitchBlockDensity;
            glitchSpeed = other.glitchSpeed;
            horizontalJitter = other.horizontalJitter;
            verticalRoll = other.verticalRoll;
            chromaticAberration = other.chromaticAberration;

            noiseIntensity = other.noiseIntensity;
            noiseScale = other.noiseScale;
            noiseSpeed = other.noiseSpeed;

            brightness = other.brightness;
            contrast = other.contrast;
            saturation = other.saturation;
            alpha = other.alpha;
            phosphorTint = other.phosphorTint;
        }
    }

    public static class NPCRTGlitchShaderIds
    {
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int CrtTime = Shader.PropertyToID("_CrtTime");
        public static readonly int RandomSeed = Shader.PropertyToID("_RandomSeed");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int Cull = Shader.PropertyToID("_Cull");

        public static readonly int Curvature = Shader.PropertyToID("_Curvature");
        public static readonly int VignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
        public static readonly int ScanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        public static readonly int ScanlineDensity = Shader.PropertyToID("_ScanlineDensity");
        public static readonly int ScanlineSpeed = Shader.PropertyToID("_ScanlineSpeed");
        public static readonly int InterlaceIntensity = Shader.PropertyToID("_InterlaceIntensity");
        public static readonly int InterlaceDensity = Shader.PropertyToID("_InterlaceDensity");
        public static readonly int InterlaceSpeed = Shader.PropertyToID("_InterlaceSpeed");

        public static readonly int GlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        public static readonly int GlitchStrength = Shader.PropertyToID("_GlitchStrength");
        public static readonly int GlitchBlockDensity = Shader.PropertyToID("_GlitchBlockDensity");
        public static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        public static readonly int HorizontalJitter = Shader.PropertyToID("_HorizontalJitter");
        public static readonly int VerticalRoll = Shader.PropertyToID("_VerticalRoll");
        public static readonly int ChromaticAberration = Shader.PropertyToID("_ChromaticAberration");

        public static readonly int NoiseIntensity = Shader.PropertyToID("_NoiseIntensity");
        public static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        public static readonly int NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");

        public static readonly int Brightness = Shader.PropertyToID("_Brightness");
        public static readonly int Contrast = Shader.PropertyToID("_Contrast");
        public static readonly int Saturation = Shader.PropertyToID("_Saturation");
        public static readonly int Alpha = Shader.PropertyToID("_Alpha");
        public static readonly int PhosphorTint = Shader.PropertyToID("_PhosphorTint");
    }
}
