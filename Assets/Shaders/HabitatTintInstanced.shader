Shader "Custom/URP/HabitatTintInstanced"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map (Atlas)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color (Instanced)", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _HabitatColor ("Habitat Color", Color) = (1,1,1,1)
        _HabitatStrength ("Habitat Strength", Range(0,1)) = 0
        _HabitatTintIntensity ("Habitat Tint Intensity", Range(0,1)) = 0.45
        _UseVertexMask ("Use Vertex Mask", Range(0,1)) = 0

        // Stylized lighting controls (subtle defaults, all optional)
        _RimColor ("Rim Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.08
        _RimPower ("Rim Power", Range(0.5,8)) = 2.5
        _AmbientUpStrength ("Ambient Upward Shaping", Range(0,1)) = 0.18
        _AmbientUpColor ("Ambient Up Color", Color) = (1.0, 0.96, 0.9, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
                float4 _HabitatColor;
                float _HabitatStrength;
                float _HabitatTintIntensity;
                float _UseVertexMask;
                float4 _RimColor;
                float _RimStrength;
                float _RimPower;
                float _AmbientUpStrength;
                float4 _AmbientUpColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float mask : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.mask = saturate(input.color.g);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 instancedBaseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 baseColor = texSample * instancedBaseColor;
                clip(baseColor.a - _Cutoff);

                // If vertex colors are missing/zero on mesh, allow full habitat tint fallback.
                float vertexMask = saturate(input.mask);
                float mask = lerp(1.0, vertexMask, saturate(_UseVertexMask));
                float habitatBlend = saturate(_HabitatStrength * mask * _HabitatTintIntensity);
                float3 habitatTinted = baseColor.rgb * _HabitatColor.rgb;
                float3 tintedAlbedo = lerp(baseColor.rgb, habitatTinted, habitatBlend);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float litFactor = ndotl * mainLight.shadowAttenuation;
                float3 diffuse = tintedAlbedo * mainLight.color * litFactor;

                // --- Painterly ambient shaping ---
                // Keep SH as base, then add subtle upward hemisphere warmth from normal.y.
                float3 ambient = SampleSH(normalWS) * tintedAlbedo;
                float upFactor = saturate(normalWS.y * 0.5 + 0.5);
                ambient *= lerp(1.0.xxx, _AmbientUpColor.rgb, upFactor * saturate(_AmbientUpStrength));

                // --- Soft rim/fresnel ---
                // Wide, diffused rim for close-up readability without hard outlines.
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), max(0.01, _RimPower));
                float rim = smoothstep(0.0, 1.0, fresnel) * saturate(_RimStrength);
                float3 rimLight = _RimColor.rgb * tintedAlbedo * rim * (0.25 + 0.75 * litFactor);

                float3 finalRgb = diffuse + ambient;
                finalRgb += rimLight;

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
}
