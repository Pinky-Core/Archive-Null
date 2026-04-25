Shader "ArchiveNull/RoomDissolve"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
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

            Cull Back
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
                float scatter = saturate((_DissolveAmount - 0.12) / 0.88);
                worldPos += worldNormal * ((noise - 0.5) * _FragmentJitter * scatter);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = worldPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float noise = PixelNoise(input.worldPos);
                clip(noise - _DissolveAmount);

                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * _BaseColor;
                half edge = 1.0 - smoothstep(_DissolveAmount, _DissolveAmount + _EdgeWidth, noise);
                half3 finalColor = baseCol.rgb + (_EdgeColor.rgb * edge * _EdgeEmission);

                return half4(finalColor, baseCol.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
