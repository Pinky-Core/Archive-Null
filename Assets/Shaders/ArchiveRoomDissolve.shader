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
        }

        Cull Back
        ZWrite On

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _BaseColor;
        float _DissolveAmount;
        float _PixelScale;
        float _NoiseScale;
        float _EdgeWidth;
        fixed4 _EdgeColor;
        float _EdgeEmission;
        float _FragmentJitter;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

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

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float noise = PixelNoise(worldPos);
            float scatter = saturate((_DissolveAmount - 0.12) / 0.88);
            v.vertex.xyz += v.normal * ((noise - 0.5) * _FragmentJitter * scatter);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float noise = PixelNoise(IN.worldPos);
            clip(noise - _DissolveAmount);

            fixed4 baseCol = tex2D(_MainTex, IN.uv_MainTex) * _Color * _BaseColor;
            float edge = 1.0 - smoothstep(_DissolveAmount, _DissolveAmount + _EdgeWidth, noise);

            o.Albedo = baseCol.rgb;
            o.Alpha = baseCol.a;
            o.Smoothness = 0.18;
            o.Metallic = 0.0;
            o.Emission = _EdgeColor.rgb * edge * _EdgeEmission;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
