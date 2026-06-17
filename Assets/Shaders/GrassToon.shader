Shader "Custom/URP/GrassToon"
{
    Properties
    {
        _BaseMap    ("Base Map (RGBA)", 2D) = "white" {}
        _BaseColor  ("Base Color", Color) = (1,1,1,1)
        _Cutoff     ("Alpha Cutoff", Range(0,1)) = 0.5

        _Steps      ("Light Steps", Range(1,8)) = 3
        _MinLight   ("Min Light", Range(0,1)) = 0.25

        _WindStrength ("Wind Strength", Range(0,0.5)) = 0.15
        _WindSpeed    ("Wind Speed", Range(0,5)) = 1.5
        _WindFrequency("Wind Frequency (XZ)", Vector) = (0.4, 0.0, 0.7, 0.0)

        _HabitatColor ("Habitat Color", Color) = (1,1,1,1)
        _HabitatStrength ("Habitat Strength", Range(0,1)) = 0
        _HabitatTintIntensity ("Habitat Tint Intensity", Range(0,1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Cutoff;
                float  _Steps;
                float  _MinLight;
                float  _WindStrength;
                float  _WindSpeed;
                float4 _WindFrequency;
                float4 _HabitatColor;
                float  _HabitatStrength;
                float  _HabitatTintIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS = IN.positionOS.xyz;

                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS    = TransformObjectToWorld(posOS);

                float heightMask = saturate(IN.uv.y);
                float t = _Time.y * _WindSpeed;

                float2 windDir = normalize(_WindFrequency.xz);
                float phase = dot(posWS.xz, windDir) + t;
                float sway = sin(phase) * _WindStrength * heightMask;

                posWS.xz += windDir * sway;

                OUT.positionWS = posWS;
                OUT.normalWS   = normalWS;

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float alpha = baseCol.a * _BaseColor.a;

                clip(alpha - _Cutoff);

                float3 albedo = baseCol.rgb * _BaseColor.rgb;

                float habitatMask = lerp(0.4, 1.0, saturate(IN.uv.y));
                float habitatBlend = saturate(_HabitatStrength * habitatMask * _HabitatTintIntensity);
                albedo = lerp(albedo, albedo * _HabitatColor.rgb, habitatBlend);

                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));

                float steps = max(_Steps, 1.0);
                float stepped = floor(NdotL * steps) / steps;

                float diff = lerp(_MinLight, 1.0, stepped);

                float3 ambient = SampleSH(normalWS);

                float3 color = albedo * (ambient + diff * mainLight.color * mainLight.shadowAttenuation);

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack Off
}