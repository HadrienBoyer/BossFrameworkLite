Shader "TappyTale/NovaParis/CRT Glitch Noise"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(CRT)]
        _Curvature ("Curvature", Range(0, 1)) = 0.15
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.28
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.32
        _ScanlineDensity ("Scanline Density", Range(10, 1200)) = 420
        _ScanlineSpeed ("Scanline Speed", Range(-10, 10)) = 0.15
        _InterlaceIntensity ("Interlace Intensity", Range(0, 1)) = 0.12
        _InterlaceDensity ("Interlace Density", Range(10, 1200)) = 240
        _InterlaceSpeed ("Interlace Speed", Range(0, 30)) = 8

        [Header(Glitch)]
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.18
        _GlitchStrength ("Glitch Strength", Range(0, 0.25)) = 0.035
        _GlitchBlockDensity ("Glitch Block Density", Range(2, 120)) = 32
        _GlitchSpeed ("Glitch Speed", Range(0, 60)) = 16
        _HorizontalJitter ("Horizontal Jitter", Range(0, 0.08)) = 0.006
        _VerticalRoll ("Vertical Roll", Range(-1, 1)) = 0
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.05)) = 0.006

        [Header(Noise)]
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.12
        _NoiseScale ("Noise Scale", Range(8, 1200)) = 320
        _NoiseSpeed ("Noise Speed", Range(0, 80)) = 22

        [Header(Color)]
        _Brightness ("Brightness", Range(0, 3)) = 1
        _Contrast ("Contrast", Range(0, 3)) = 1.1
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
        _PhosphorTint ("Phosphor Tint", Color) = (0.75, 1, 0.82, 1)

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0

        [HideInInspector] _CrtTime ("CRT Time", Float) = 0
        [HideInInspector] _RandomSeed ("Random Seed", Float) = 0
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CRTGlitchNoise"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Curvature;
            float _VignetteIntensity;
            float _ScanlineIntensity;
            float _ScanlineDensity;
            float _ScanlineSpeed;
            float _InterlaceIntensity;
            float _InterlaceDensity;
            float _InterlaceSpeed;

            float _GlitchIntensity;
            float _GlitchStrength;
            float _GlitchBlockDensity;
            float _GlitchSpeed;
            float _HorizontalJitter;
            float _VerticalRoll;
            float _ChromaticAberration;

            float _NoiseIntensity;
            float _NoiseScale;
            float _NoiseSpeed;

            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Alpha;
            fixed4 _PhosphorTint;

            float _CrtTime;
            float _RandomSeed;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

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
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 ApplyCurvature(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                centered += centered * r2 * (_Curvature * 0.125);
                return centered * 0.5 + 0.5;
            }

            float IsInside01(float2 uv)
            {
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                return inside.x * inside.y;
            }

            fixed4 SampleMain(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _CrtTime + _RandomSeed * 13.37;

                float2 uv = i.uv;
                uv.y = frac(uv.y + _VerticalRoll * 0.08 * time);
                uv = ApplyCurvature(uv);

                float inside = IsInside01(uv);

                float row = floor(uv.y * max(1.0, _GlitchBlockDensity));
                float glitchClock = floor(time * max(0.001, _GlitchSpeed));
                float rowRandom = Hash21(float2(row, glitchClock));
                float glitchMask = step(1.0 - saturate(_GlitchIntensity), rowRandom);
                float blockOffset = (Hash21(float2(rowRandom * 71.19, glitchClock + _RandomSeed)) - 0.5) * 2.0;
                blockOffset *= _GlitchStrength * glitchMask;

                float jitterNoise = ValueNoise(float2(row * 0.17, time * 28.0));
                float fineJitter = (jitterNoise - 0.5) * 2.0 * _HorizontalJitter * (0.25 + _GlitchIntensity);

                float2 glitchUv = uv;
                glitchUv.x += blockOffset + fineJitter;

                float rgbSplit = _ChromaticAberration * (1.0 + glitchMask * 4.0);
                float2 splitOffset = float2(rgbSplit, 0.0);

                fixed4 centerSample = SampleMain(glitchUv);
                fixed4 redSample = SampleMain(glitchUv + splitOffset);
                fixed4 blueSample = SampleMain(glitchUv - splitOffset);

                fixed4 col = centerSample;
                col.r = redSample.r;
                col.b = blueSample.b;

                float scan = sin((uv.y + time * _ScanlineSpeed * 0.01) * _ScanlineDensity * 6.2831853);
                scan = 0.5 + 0.5 * scan;
                float scanFactor = lerp(1.0, lerp(0.62, 1.08, scan), _ScanlineIntensity);
                col.rgb *= scanFactor;

                float interlacePhase = floor(time * max(0.001, _InterlaceSpeed));
                float interlace = step(0.5, frac(uv.y * _InterlaceDensity + interlacePhase * 0.5));
                col.rgb *= lerp(1.0, lerp(0.82, 1.0, interlace), _InterlaceIntensity);

                float grain = Hash21(floor(uv * _NoiseScale + time * _NoiseSpeed));
                float softNoise = ValueNoise(uv * (_NoiseScale * 0.08) + time * (_NoiseSpeed * 0.05));
                float mixedNoise = lerp(softNoise, grain, 0.72);
                col.rgb += (mixedNoise - 0.5) * _NoiseIntensity;

                float2 centered = uv * 2.0 - 1.0;
                float vignette = saturate(1.0 - dot(centered, centered) * _VignetteIntensity);
                col.rgb *= vignette;

                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(float3(luma, luma, luma), col.rgb, _Saturation);
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                col.rgb *= _Brightness;
                col.rgb *= _PhosphorTint.rgb;

                col *= i.color;
                col.a *= _Alpha * inside;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
