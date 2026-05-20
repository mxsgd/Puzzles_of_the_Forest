Shader "Custom/URP/FoliageBillboard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BackFaceTint ("Back Face Tint", Color) = (0.92, 1, 0.92, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45

        [Header("Lighting")]
        _MinLight ("Min Light", Range(0, 1)) = 0.22

        [Header("Billboard")]
        [Toggle] _FullBillboard ("Full Camera Billboard", Float) = 1
        [Tooltip("Pivot w object space (zwykle dol karty).")]
        _PivotOS ("Pivot OS", Vector) = (0, 0, 0, 0)

        [Header("Wind")]
        _WindStrength ("Wind Strength", Range(0, 0.6)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.2
        _WindFrequency ("Wind Direction XZ", Vector) = (0.45, 0, 0.75, 0)
        _WindGust ("Wind Gust", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FoliageBillboardCommon.hlsl"

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
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                FoliageVertexInput vin;
                vin.positionOS = input.positionOS.xyz;
                vin.normalOS = input.normalOS;
                vin.uv = input.uv;

                FoliageVertexOutput vout = FoliageVertex(vin);

                output.positionWS = vout.positionWS;
                output.normalWS = vout.normalWS;
                output.positionCS = TransformWorldToHClip(vout.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float alpha = tex.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                float3 normalWS = normalize(input.normalWS);
                if (facing < 0)
                    normalWS = -normalWS;

                float3 albedo = tex.rgb * _BaseColor.rgb;
                if (facing < 0)
                    albedo *= _BackFaceTint.rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float lit = max(_MinLight, ndotl * mainLight.shadowAttenuation);

                float3 ambient = SampleSH(normalWS);
                float3 finalColor = albedo * (ambient + mainLight.color * lit);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "FoliageBillboardCommon.hlsl"

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
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                FoliageVertexInput vin;
                vin.positionOS = input.positionOS.xyz;
                vin.normalOS = input.normalOS;
                vin.uv = input.uv;

                FoliageVertexOutput vout = FoliageVertex(vin);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(vout.positionWS, normalWS, _MainLightPosition.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
