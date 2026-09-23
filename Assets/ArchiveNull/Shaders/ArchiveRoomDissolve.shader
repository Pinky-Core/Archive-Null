Shader "ArchiveNull/RoomDissolve"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _WaveOrigin ("Shared world origin", Vector) = (0, 0, 0, 0)
        _ReconstructionOrigin ("Wave origin", Vector) = (0, 0, 0, 0)
        _WaveRadius ("World radius", Float) = 10000
        _WaveWidth ("World edge width", Float) = 0.45
        _MemoryRadius ("Fixed memory radius (0 disables)", Float) = 0
        _BoundaryWidth ("Memory pixel border width", Float) = 0.65
        _PixelScale ("Pixel Dissolve Scale", Float) = 18
        _NoiseScale ("Noise Scale", Float) = 8
        _EdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.07
        _EdgeColor ("Edge Color", Color) = (0.05, 0.75, 1, 1)
        _EdgeEmission ("Edge Emission", Range(0, 8)) = 3
        _FragmentJitter ("Fragment Jitter", Range(0, 0.2)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            Blend One Zero
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _BaseColor;
                half _DissolveAmount;
                float4 _WaveOrigin;
                float4 _ReconstructionOrigin;
                float _WaveRadius;
                float _WaveWidth;
                float _MemoryRadius;
                float _BoundaryWidth;
                half _PixelScale;
                half _NoiseScale;
                half _EdgeWidth;
                half4 _EdgeColor;
                half _EdgeEmission;
                half _FragmentJitter;
            CBUFFER_END

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float PixelNoise(float3 worldPos)
            {
                float pixelScale = max(1.0, _PixelScale);
                float3 pixelCell = floor(worldPos * pixelScale) / pixelScale;
                float coarse = Hash(pixelCell * _NoiseScale);
                float fine = Hash(pixelCell * (_NoiseScale * 2.31) + 9.17);
                return saturate(coarse * 0.78 + fine * 0.22);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                float noise = PixelNoise(worldPos);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = worldPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float noise = PixelNoise(input.worldPos);
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                float front = _WaveRadius - distance(input.worldPos, _ReconstructionOrigin.xyz);
                float width = max(0.01, _WaveWidth);
                half alpha = smoothstep(-width, width, front + (noise - 0.5) * width);
                if (_DissolveAmount >= 0.9999) alpha = 0;
                if (_DissolveAmount <= 0.0001) alpha = 1;
                half edge = (1.0 - smoothstep(0.0, width * 2.0, abs(front))) * alpha;
                if (_MemoryRadius > 0)
                {
                    float inside = _MemoryRadius - distance(input.worldPos.xz, _WaveOrigin.xz);
                    clip(inside); // Hard cap: noise and proximity can never reveal beyond it.
                    float band = saturate(inside / max(0.05, _BoundaryWidth));
                    float cell = Hash(floor(input.worldPos * _PixelScale));
                    cell *= 0.65 + 0.35 * sin(_Time.y * 2.2 + cell * 19.0);
                    clip(band - cell * 0.95);
                    edge = max(edge, (1.0 - band) * alpha);
                }
                half3 finalColor = baseCol.rgb + (_EdgeColor.rgb * edge * _EdgeEmission);

                clip(alpha - 0.35);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
