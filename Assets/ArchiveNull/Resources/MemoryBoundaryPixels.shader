Shader "ArchiveNull/MemoryBoundaryPixels"
{
    Properties { _Progress ("Rebuild progress", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 seed : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; half alpha : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Progress;
            CBUFFER_END
            V vert(A input)
            {
                V output;
                float phase = _Time.y * (0.6 + input.seed.x * 0.8) + input.seed.y * 1.73;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                p.y += sin(phase) * 0.22;
                float size = 0.018 + input.seed.x * 0.032;
                float3 view = TransformWorldToView(p);
                view.xy += input.uv * size;
                output.positionCS = mul(UNITY_MATRIX_P, float4(view, 1));
                output.alpha = smoothstep(0.85, 1.0, _Progress) * pow(saturate(sin(phase)), 2) * 0.8;
                return output;
            }
            half4 frag(V input) : SV_Target { return half4(0.12, 0.8, 1, input.alpha); }
            ENDHLSL
        }
    }
}
