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

        _CrestFoamAmount ("Crest Foam Amount", Range(0.0, 1.0)) = 0.65
        _CrestFoamStart ("Crest Foam Start", Range(0.0, 1.0)) = 0.62
        _EdgeFoamStrength ("Edge Foam Strength", Range(0.0, 2.0)) = 0.9
        _CrestFoamScale ("Crest Foam Noise Scale", Range(0.05, 6.0)) = 0.9

        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.85
        _SpecularStrength ("Specular Strength", Range(0.0, 4.0)) = 1.25
        _FresnelPower ("Fresnel Power", Range(0.1, 8.0)) = 4.0
        _FresnelStrength ("Fresnel Strength", Range(0.0, 0.5)) = 0.06
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.92
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
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

            float _CrestFoamAmount;
            float _CrestFoamStart;
            float _EdgeFoamStrength;
            float _CrestFoamScale;

            float _Smoothness;
            float _SpecularStrength;
            float _FresnelPower;
            float _FresnelStrength;
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

            // Smooth value noise used to break up the crest foam line.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
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

                // Scattered foam patches on the surface.
                float2 foamUV = IN.worldPos.xz * _FoamScale + _Time.y * _FoamSpeed;
                float foam = FoamPattern(foamUV) * _FoamAmount;

                // Foam on wave crests: driven by wave height, broken up by noise.
                float2 crestUV = IN.worldPos.xz * _CrestFoamScale + _Time.y * _FoamSpeed * 0.5;
                float crestNoise = ValueNoise(crestUV) * 0.6 + ValueNoise(crestUV * 2.7) * 0.4;
                float crest = smoothstep(_CrestFoamStart, _CrestFoamStart + 0.25, IN.waveMask);
                float crestFoam = crest * smoothstep(0.35, 0.75, crestNoise) * _CrestFoamAmount;

                // Foam on tilted facets (steep wave sides) instead of a bright reflection.
                float steepness = 1.0 - saturate(n.y);
                float edgeFoam = smoothstep(0.15, 0.5, steepness) * _EdgeFoamStrength;
                edgeFoam *= smoothstep(0.25, 0.65, crestNoise);

                float totalFoam = saturate(foam + crestFoam + edgeFoam);

                float3 baseCol = lerp(_WaterColorDark.rgb, _WaterColor.rgb, IN.waveMask);
                // Keep fresnel subtle and only where there is no foam, so edges read as foam, not glare.
                baseCol += fresnel * _FresnelStrength * (1.0 - totalFoam);
                baseCol = lerp(baseCol, _FoamColor.rgb, totalFoam);

                // Foam is diffuse: kill the specular highlight where foam covers the surface.
                spec *= 1.0 - totalFoam;

                float3 lit = baseCol * (0.35 + NdotL * 0.65) + spec * mainLight.color.rgb;

                float alpha = saturate(_Alpha + totalFoam * (1.0 - _Alpha));
                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }
}
