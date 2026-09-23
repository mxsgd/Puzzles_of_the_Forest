Shader "Idle Forest/WindLeaves"
{
    // Screen-space breeze overlay: three parallax layers of procedurally drawn, tumbling leaves
    // carried along the wind direction, plus faint wind streaks. Everything swells and fades in
    // sweeping gusts so the breeze comes in waves. Drawn as a fullscreen triangle over the scene
    // (see WindLeavesVfx); no textures, no particles.
    Properties
    {
        [Header(Wind)]
        _WindAngle ("Wind Direction (degrees, 0 = to the right)", Range(0, 360)) = 200
        _WindSpeed ("Wind Speed", Range(0, 3)) = 0.7
        _GustFrequency ("Gust Frequency", Range(0.05, 1)) = 0.25
        _GustSharpness ("Gust Sharpness (higher = calmer gaps)", Range(0, 1)) = 0.5

        [Header(Leaves)]
        _LeafColor1 ("Leaf Color 1", Color) = (0.42, 0.66, 0.30, 1)
        _LeafColor2 ("Leaf Color 2", Color) = (0.70, 0.75, 0.30, 1)
        _LeafColor3 ("Leaf Color 3", Color) = (0.86, 0.58, 0.24, 1)
        _LeafDensity ("Leaf Density", Range(0, 1)) = 0.3
        _LeafSize ("Leaf Size", Range(0.4, 1.2)) = 0.9
        _LeafAlpha ("Leaf Opacity", Range(0, 1)) = 0.9
        _Tumble ("Tumble Amount", Range(0, 2)) = 1

        [Header(Wind Streaks)]
        _StreakAlpha ("Streak Opacity", Range(0, 0.5)) = 0.1
        _StreakDensity ("Streak Density", Range(2, 30)) = 9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WindLeaves"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WindAngle;
                float _WindSpeed;
                float _GustFrequency;
                float _GustSharpness;
                float4 _LeafColor1;
                float4 _LeafColor2;
                float4 _LeafColor3;
                float _LeafDensity;
                float _LeafSize;
                float _LeafAlpha;
                float _Tumble;
                float _StreakAlpha;
                float _StreakDensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ndc : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                OUT.ndc = IN.positionOS.xy;
                return OUT;
            }

            float hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }

            float2 hash22(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            float2 rot(float2 v, float a)
            {
                float c = cos(a), s = sin(a);
                return float2(c * v.x - s * v.y, s * v.x + c * v.y);
            }

            // Sweeping wave along the wind: 0 = calm, 1 = full gust.
            float Gust(float2 uv, float2 wind, float t, float phaseOffset)
            {
                float ph = dot(uv, wind) * 0.9 - t * _WindSpeed * 0.6 * _GustFrequency * 4.0 + phaseOffset;
                float w = 0.5 + 0.5 * sin(ph * 1.7) * cos(ph * 0.6 + 1.3);
                return smoothstep(_GustSharpness * 0.6, _GustSharpness * 0.6 + 0.4, w);
            }

            // One parallax layer of leaves. Returns premultiplied color + alpha.
            float4 LeafLayer(float2 uv, float2 wind, float scale, float speedMul, float seed, float t, float sizeMul)
            {
                float2 p = uv * scale - wind * (t * _WindSpeed * speedMul);
                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;

                float2 h  = hash22(cell + seed);
                float2 h2 = hash22(cell * 1.7 + seed + 13.1);

                float presence = step(1.0 - _LeafDensity, h2.x);
                float gust = Gust(uv, wind, t, seed);

                // Position inside the cell + gentle sway.
                float2 pos = (h - 0.5) * 0.2;
                pos += float2(sin(t * (0.8 + h2.y) + h.x * 6.2831),
                              cos(t * (1.1 + h.y) + h.y * 6.2831)) * 0.1;

                float2 d = f - pos;

                // Orientation: points along the wind, rocks back and forth, slowly spins.
                float ang = atan2(wind.y, wind.x)
                          + (sin(t * 1.3 + h2.y * 6.2831) * 0.7 + (h.x - 0.5) * 1.5) * _Tumble
                          + t * (h.y - 0.5) * 0.6 * _Tumble;
                d = rot(d, -ang);

                float len = lerp(0.12, 0.2, h2.y) * _LeafSize * sizeMul;
                float x = d.x / len * 0.5 + 0.5;   // 0..1 along the leaf
                float y = d.y / len;

                // Tumbling flip: leaf narrows as it turns edge-on.
                float flip = lerp(1.0, lerp(0.3, 1.0, abs(cos(t * 1.7 + h.x * 6.2831))), saturate(_Tumble));
                float halfW = 0.45 * pow(sin(3.14159 * saturate(x)), 0.85) * flip;

                float dist = abs(y) - halfW;
                float shape = (1.0 - smoothstep(0.0, 0.06, dist)) * step(0.0, x) * step(x, 1.0);

                // Colour: pick from palette per leaf, darker rim, lighter midrib.
                float pick = frac(h.x * 7.31 + h.y * 3.17);
                float3 col = pick < 0.5 ? _LeafColor1.rgb
                           : (pick < 0.8 ? _LeafColor2.rgb : _LeafColor3.rgb);
                float rib = 1.0 - smoothstep(0.0, 0.05, abs(y));
                col *= lerp(0.8, 1.05, saturate(1.0 - abs(y) / max(halfW, 0.01)));
                col = lerp(col, col * 1.35, rib * 0.5);

                float a = shape * presence * gust * _LeafAlpha;
                return float4(col * a, a);
            }

            float StreakLayer(float2 uv, float2 wind, float t, float density, float speedMul, float seed)
            {
                float2 perp = float2(-wind.y, wind.x);
                float2 q = float2(dot(uv, wind), dot(uv, perp));
                q.x -= t * _WindSpeed * 2.5 * speedMul;
                q.y += sin(q.x * 1.3 + t * 0.4 + seed) * 0.18;

                float row = floor(q.y * density);
                float rh = hash11(row + seed);
                float present = step(0.55, rh);

                // Thin line across the row, broken into long dashes.
                float lineY = abs(frac(q.y * density) - 0.5);
                float thin = 1.0 - smoothstep(0.0, 0.05, lineY);
                float along = frac(q.x * (0.15 + rh * 0.2) + rh * 17.0);
                float dash = smoothstep(0.0, 0.25, along) * (1.0 - smoothstep(0.35, 0.6, along));
                return thin * dash * present * Gust(uv, wind, t, seed * 2.0);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.ndc;
                uv.x *= _ScreenParams.x / _ScreenParams.y;

                float a = radians(_WindAngle);
                float2 wind = float2(cos(a), sin(a));
                float t = _Time.y;

                float4 outc = 0;

                // Wind streaks first (behind the leaves).
                float s = StreakLayer(uv, wind, t, _StreakDensity, 1.0, 3.0)
                        + StreakLayer(uv * 1.3, wind, t, _StreakDensity * 0.7, 0.7, 11.0) * 0.7;
                s = saturate(s) * _StreakAlpha;
                outc = float4(float3(1, 1, 0.95) * s, s);

                // Far -> near: small/slow to large/fast.
                float4 l;
                l = LeafLayer(uv, wind, 10.0, 0.7, 1.0, t, 1.0);
                outc = l + outc * (1.0 - l.a);
                l = LeafLayer(uv, wind, 7.0, 1.0, 5.0, t, 1.0);
                outc = l + outc * (1.0 - l.a);
                l = LeafLayer(uv, wind, 4.5, 1.4, 9.0, t, 1.0);
                outc = l + outc * (1.0 - l.a);

                return outc;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
