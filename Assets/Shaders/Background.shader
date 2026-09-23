Shader "Idle Forest/Background"
{
    // Soft, slowly flowing background: a few iterations of domain warping (the same family of
    // technique as Balatro's paint swirl) produce a smooth scalar field that is mapped through a
    // gentle three-color gradient — no hard bands, no highlights.
    //
    // The pattern is sampled from the pixel's VIEW DIRECTION projected onto a unit-depth plane, like
    // a skybox: panning or zooming the camera does not move it at all (it's effectively infinitely
    // far away), while orbiting with Q/E turns it with the world. Drawn as a fullscreen triangle
    // pinned to the far plane (see BackgroundQuad), so it only shows where nothing else covers it.
    Properties
    {
        [Header(Palette)]
        _Color1 ("Main Color", Color) = (0.25, 0.38, 0.40, 1)
        _Color2 ("Secondary Color", Color) = (0.36, 0.33, 0.50, 1)
        _Color3 ("Dark Color", Color) = (0.10, 0.14, 0.20, 1)

        [Header(Motion)]
        _FlowSpeed ("Flow Speed", Range(0, 2)) = 0.25
        _WarpStrength ("Warp Strength", Range(0, 1)) = 0.35

        [Header(Look)]
        _PatternScale ("Pattern Scale (higher = finer)", Range(1, 30)) = 6
        _BandFrequency ("Color Band Frequency", Range(0.3, 5)) = 1.4
        _SecondaryMix ("Secondary Color Amount", Range(0, 1)) = 0.55
        _PixelSize ("Pixelate (pattern units, 0 = off)", Range(0, 0.2)) = 0
        _Vignette ("Vignette", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Background"
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #ifndef UNITY_RAW_FAR_CLIP_VALUE
                #if UNITY_REVERSED_Z
                    #define UNITY_RAW_FAR_CLIP_VALUE 0.0
                #else
                    #define UNITY_RAW_FAR_CLIP_VALUE 1.0
                #endif
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float _FlowSpeed;
                float _WarpStrength;
                float _PatternScale;
                float _BandFrequency;
                float _SecondaryMix;
                float _PixelSize;
                float _Vignette;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ndc : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Mesh vertices are already clip-space XY (fullscreen triangle); pin depth to the far plane.
                OUT.positionCS = float4(IN.positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
                OUT.ndc = IN.positionOS.xy;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // World-space view ray for this pixel (same projection the scene geometry uses, so
                // any platform-specific Y flip cancels out). Only the direction is used — never the
                // camera position — so translating the camera cannot move the pattern.
                float4x4 proj = GetViewToHClipMatrix();
                float3 viewDir = float3(IN.ndc.x / proj._m00, IN.ndc.y / proj._m11, -1.0);
                float3 dirWS = normalize(mul((float3x3)GetViewToWorldMatrix(), viewDir));

                // Project onto a unit-depth horizontal plane; the clamp keeps it finite near the horizon.
                float2 p = dirWS.xz / max(abs(dirWS.y), 0.15);
                if (_PixelSize > 0.0001)
                    p = floor(p / _PixelSize) * _PixelSize;

                float2 uv = p * _PatternScale;
                float speed = _Time.y * _FlowSpeed;

                float2 uv2 = float2(uv.x + uv.y, uv.x + uv.y);

                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    uv2 += sin(max(uv.x, uv.y)) + uv;
                    uv += _WarpStrength * float2(cos(5.1123314 + 0.353 * uv2.y + speed * 0.131121),
                                                  sin(uv2.x - 0.113 * speed));
                    uv -= _WarpStrength * 2.0 * (cos(uv.x + uv.y) - sin(uv.x * 0.711 - uv.y));
                }

                // Smooth scalar field -> two overlapping soft waves that blend the three colors.
                float field = length(uv) * 0.035 * _BandFrequency;
                float a = 0.5 + 0.5 * sin(field * 6.2831853);
                float b = 0.5 + 0.5 * sin(field * 3.8 + 2.0 + speed * 0.4);

                float3 col = lerp(_Color3.rgb, _Color1.rgb, smoothstep(0.0, 1.0, a));
                col = lerp(col, _Color2.rgb, smoothstep(0.2, 0.9, b) * _SecondaryMix);

                float2 sv = IN.ndc * 0.5;
                col *= 1.0 - _Vignette * saturate(dot(sv, sv) * 2.2);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
