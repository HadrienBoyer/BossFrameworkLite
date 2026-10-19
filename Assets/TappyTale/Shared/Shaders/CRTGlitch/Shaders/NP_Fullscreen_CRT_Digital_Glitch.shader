Shader "Hidden/TappyTale/NovaParis/Fullscreen CRT Digital Glitch"
{
    Properties
    {
        _EffectIntensity ("Effect Intensity", Range(0, 1)) = 1
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.22
        _ArtifactIntensity ("Digital Artifact Intensity", Range(0, 1)) = 0.28
        _GrainIntensity ("Grain Intensity", Range(0, 1)) = 0.16
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.22
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.05)) = 0.007
        _WetBlurMax ("Wet Blur Max", Range(0, 0.02)) = 0.006
        _WetDistortion ("Wet Distortion", Range(0, 0.05)) = 0.015
        _WetVignette ("Wet Vignette", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "FullscreenCRTDigitalGlitch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EffectIntensity;
            float _GlitchIntensity;
            float _ArtifactIntensity;
            float _GrainIntensity;
            float _ScanlineIntensity;
            float _ChromaticAberration;
            float _WetBlurMax;
            float _WetDistortion;
            float _WetVignette;
            float _CrtTime;
            float _RandomSeed;
            float _NPFullscreenCRTMoisture;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 BarrelWarp(float2 uv, float amount)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radius2 = dot(centered, centered);
                centered += centered * radius2 * amount;
                return centered * 0.5 + 0.5;
            }

            float4 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            float4 BlurSource(float2 uv, float radius)
            {
                if (radius <= 0.00001)
                {
                    return SampleSource(uv);
                }

                float2 texel = _BlitTexture_TexelSize.xy;
                float2 r = texel * radius;

                float4 col = SampleSource(uv) * 0.28;
                col += SampleSource(uv + float2( r.x, 0.0)) * 0.12;
                col += SampleSource(uv + float2(-r.x, 0.0)) * 0.12;
                col += SampleSource(uv + float2(0.0,  r.y)) * 0.12;
                col += SampleSource(uv + float2(0.0, -r.y)) * 0.12;
                col += SampleSource(uv + float2( r.x,  r.y)) * 0.06;
                col += SampleSource(uv + float2(-r.x,  r.y)) * 0.06;
                col += SampleSource(uv + float2( r.x, -r.y)) * 0.06;
                col += SampleSource(uv + float2(-r.x, -r.y)) * 0.06;
                return col;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;
                float time = _CrtTime + _RandomSeed * 19.19;
                float intensity = saturate(_EffectIntensity);
                float moisture = saturate(_NPFullscreenCRTMoisture);

                float2 warpedUv = BarrelWarp(uv, 0.035 * intensity);

                float row = floor(warpedUv.y * 72.0);
                float clock = floor(time * lerp(12.0, 36.0, _GlitchIntensity));
                float rowRnd = Hash21(float2(row, clock + _RandomSeed));
                float tearMask = step(1.0 - _GlitchIntensity * intensity, rowRnd);
                float tear = (Hash21(float2(rowRnd * 73.1, clock)) - 0.5) * 0.065 * tearMask * intensity;

                float blockDensity = lerp(18.0, 72.0, _ArtifactIntensity);
                float2 block = floor(warpedUv * float2(blockDensity, blockDensity * 0.56));
                float blockRnd = Hash21(block + floor(time * 18.0));
                float artifact = step(1.0 - _ArtifactIntensity * 0.32 * intensity, blockRnd);
                float blockShift = (Hash21(block.yx + clock) - 0.5) * 0.035 * artifact * intensity;

                float wetStreak = ValueNoise(float2(warpedUv.x * 26.0, warpedUv.y * 3.2 - time * 0.55));
                wetStreak = smoothstep(0.58, 0.96, wetStreak) * moisture;
                float wetRun = ValueNoise(float2(warpedUv.x * 9.0 + time * 0.08, warpedUv.y * 42.0));
                float2 wetOffset = float2(
                    (wetStreak - 0.5) * _WetDistortion * moisture,
                    -wetRun * _WetDistortion * moisture * 0.65
                );

                float fineJitter = (ValueNoise(float2(row * 0.21, time * 42.0)) - 0.5) * 0.008 * intensity;
                float2 glitchUv = warpedUv + float2(tear + blockShift + fineJitter, 0.0) + wetOffset;

                float rgbSplit = (_ChromaticAberration + artifact * 0.012) * intensity;
                rgbSplit += moisture * 0.0025;

                float blurRadius = moisture * _WetBlurMax * 900.0;
                float4 center = BlurSource(glitchUv, blurRadius);
                float4 red = BlurSource(glitchUv + float2(rgbSplit, 0.0), blurRadius);
                float4 blue = BlurSource(glitchUv - float2(rgbSplit, 0.0), blurRadius);

                float4 col = center;
                col.r = red.r;
                col.b = blue.b;

                float scan = sin((uv.y + time * 0.012) * 820.0 * 6.2831853);
                float scanFactor = lerp(1.0, lerp(0.72, 1.07, scan * 0.5 + 0.5), _ScanlineIntensity * intensity);
                col.rgb *= scanFactor;

                float interlace = step(0.5, frac(uv.y * 420.0 + floor(time * 12.0) * 0.5));
                col.rgb *= lerp(1.0, lerp(0.86, 1.0, interlace), 0.12 * intensity);

                float grain = Hash21(floor(uv * _ScreenParams.xy + time * 60.0));
                float softNoise = ValueNoise(uv * 360.0 + time * 5.0);
                float mixedNoise = lerp(softNoise, grain, 0.72);
                col.rgb += (mixedNoise - 0.5) * (_GrainIntensity + moisture * 0.08) * intensity;

                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float digitalFlash = artifact * Hash21(float2(clock, rowRnd)) * _ArtifactIntensity * 0.25 * intensity;
                col.rgb = lerp(col.rgb, float3(luma, luma * 1.12, luma * 0.88), 0.08 * intensity + digitalFlash);

                float2 centered = uv * 2.0 - 1.0;
                float vignette = saturate(1.0 - dot(centered, centered) * (0.22 + _WetVignette * moisture));
                col.rgb *= vignette;

                float wetHaze = moisture * 0.18;
                col.rgb = lerp(col.rgb, float3(0.72, 0.82, 0.86), wetHaze * wetStreak);
                col.rgb = saturate(col.rgb);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
