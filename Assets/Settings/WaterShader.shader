Shader "Custom/LowPolyHexWater_MatchScreenshot"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.08, 0.62, 0.78, 1)
        _WaterColorDark ("Water Color Dark", Color) = (0.03, 0.28, 0.44, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 0.97, 1.0, 1)

        _WaveHeight ("Wave Height", Range(0.0, 1.0)) = 0.25
        _WaveScale ("Wave Scale", Range(0.05, 3.0)) = 0.9
        _WaveSpeed ("Wave Speed", Range(0.0, 4.0)) = 0.8
        _WaveChoppiness ("Wave Choppiness", Range(0.0, 1.5)) = 0.6
        _FacetSteps ("Facet Height Steps", Range(1, 12)) = 6

        _FoamScale ("Foam Scale", Range(0.0, 6.0)) = 1.2
        _FoamSpeed ("Foam Speed", Range(0.0, 3.0)) = 0.35
        _FoamAmount ("Foam Amount", Range(0.0, 1.0)) = 0.3

        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.85
        _SpecularStrength ("Specular Strength", Range(0.0, 4.0)) = 1.25
        _FresnelPower ("Fresnel Power", Range(0.1, 8.0)) = 4.0
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.92
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "UniversalForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float  waveMask    : TEXCOORD1;
            };

            float4 _WaterColor;
            float4 _WaterColorDark;
            float4 _FoamColor;

            float _WaveHeight;
            float _WaveScale;
            float _WaveSpeed;
            float _WaveChoppiness;
            float _FacetSteps;

            float _FoamScale;
            float _FoamSpeed;
            float _FoamAmount;

            float _Smoothness;
            float _SpecularStrength;
            float _FresnelPower;
            float _Alpha;

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float FacetedWave(float2 p, float t)
            {
                float2 p1 = p * _WaveScale;
                float2 p2 = p * (_WaveScale * 1.73);

                float w1 = sin(p1.x + t) * 0.65;
                float w2 = sin(p1.y * 1.22 - t * 1.15) * 0.45;
                float w3 = cos((p2.x + p2.y) * 0.7 + t * 0.65) * 0.35;
                float wave = w1 + w2 + w3;

                wave += sign(wave) * _WaveChoppiness * abs(wave) * 0.35;

                float steps = max(_FacetSteps, 1.0);
                wave = floor((wave * 0.5 + 0.5) * steps) / steps;
                wave = wave * 2.0 - 1.0;
                return wave;
            }

            float FoamPattern(float2 p)
            {
                float2 g = floor(p);
                float rnd = Hash21(g);
                return smoothstep(0.65, 1.0, rnd);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);

                float t = _Time.y * _WaveSpeed;
                float wave = FacetedWave(wp.xz, t);
                wp.y += wave * _WaveHeight;

                OUT.worldPos = wp;
                OUT.waveMask = saturate(wave * 0.5 + 0.5);
                OUT.positionHCS = TransformWorldToHClip(wp);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Flat-like normal from displaced geometry for polygonal shading.
                float3 dpdx = ddx(IN.worldPos);
                float3 dpdy = ddy(IN.worldPos);
                float3 n = normalize(cross(dpdy, dpdx));

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - IN.worldPos);
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(lightDir + viewDir);

                float NdotL = saturate(dot(n, lightDir));
                float NdotH = saturate(dot(n, halfDir));
                float specPow = lerp(8.0, 160.0, saturate(_Smoothness));
                float spec = pow(NdotH, specPow) * _SpecularStrength * NdotL;

                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), _FresnelPower);

                float2 foamUV = IN.worldPos.xz * _FoamScale + _Time.y * _FoamSpeed;
                float foam = FoamPattern(foamUV) * _FoamAmount;

                float3 baseCol = lerp(_WaterColorDark.rgb, _WaterColor.rgb, IN.waveMask);
                baseCol += fresnel * 0.22;
                baseCol = lerp(baseCol, _FoamColor.rgb, foam);

                float3 lit = baseCol * (0.35 + NdotL * 0.65) + spec * mainLight.color.rgb;

                return half4(lit, _Alpha);
            }
            ENDHLSL
        }
    }
}
