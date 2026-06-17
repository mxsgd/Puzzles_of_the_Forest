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

        _LightSteps ("Light Steps", Range(1,8)) = 3
        _LightSmoothness ("Light Band Softness", Range(0,1)) = 0.12
        _MinLight ("Min Light", Range(0,1)) = 0.3
        _ShadowTintStrength ("Shadow Tint Strength", Range(0,1)) = 0.15

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
                float _LightSteps;
                float _LightSmoothness;
                float _MinLight;
                float _ShadowTintStrength;
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

            float ComputeToonLight(float ndotl, float steps, float smoothness)
            {
                float safeSteps = max(steps, 1.0);
                float band = saturate(ndotl) * safeSteps;
                float steppedLow = floor(band) / safeSteps;
                float steppedHigh = min((floor(band) + 1.0) / safeSteps, 1.0);
                float bandT = frac(band);
                float soft = smoothstep(0.5 - smoothness * 0.5, 0.5 + smoothness * 0.5, bandT);
                return lerp(steppedLow, steppedHigh, soft * saturate(smoothness * 3.0));
            }

            float3 ApplyHabitatTint(float3 albedo, float habitatBlend, float lightTerm)
            {
                float3 habitatMul = lerp(1.0.xxx, _HabitatColor.rgb, habitatBlend * 0.85);
                float3 tinted = albedo * habitatMul;

                float lum = dot(tinted, float3(0.299, 0.587, 0.114));
                tinted = lerp(tinted, lum.xxx, habitatBlend * 0.12);

                float lightMask = lerp(0.45, 1.0, saturate(lightTerm));
                return lerp(albedo, tinted, habitatBlend * lightMask);
            }

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

                float vertexMask = saturate(input.mask);
                float mask = lerp(1.0, vertexMask, saturate(_UseVertexMask));
                float habitatBlend = saturate(_HabitatStrength * mask * _HabitatTintIntensity);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float stepped = ComputeToonLight(ndotl, _LightSteps, _LightSmoothness);
                float litTerm = lerp(_MinLight, 1.0, stepped) * mainLight.shadowAttenuation;

                float3 tintedAlbedo = ApplyHabitatTint(baseColor.rgb, habitatBlend, litTerm);

                float3 shadowTint = lerp(1.0.xxx, float3(0.84, 0.87, 0.93), saturate(_ShadowTintStrength));
                float3 ambient = SampleSH(normalWS) * tintedAlbedo;
                float upFactor = saturate(normalWS.y * 0.5 + 0.5);
                ambient *= lerp(1.0.xxx, _AmbientUpColor.rgb, upFactor * saturate(_AmbientUpStrength));

                float3 directLit = tintedAlbedo * mainLight.color * litTerm;
                float3 directShadow = tintedAlbedo * shadowTint * lerp(0.42, _MinLight, 0.5);
                float3 diffuse = ambient * (1.0 - litTerm * 0.2) + lerp(directShadow, directLit, saturate(litTerm));

                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), max(0.01, _RimPower));
                float rim = smoothstep(0.0, 1.0, fresnel) * saturate(_RimStrength);
                float3 rimLight = _RimColor.rgb * tintedAlbedo * rim * (0.2 + 0.8 * litTerm);

                return half4(diffuse + rimLight, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 instancedBaseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texSample.a * instancedBaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
