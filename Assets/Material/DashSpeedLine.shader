Shader "Mood/UI/Dash Speed Lines"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.95, 0.97, 1.0, 1.0)
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1

        _LaneCount ("Lane Count", Range(8, 220)) = 24
        _LaneJitter ("Lane Jitter", Range(0, 1)) = 0.04
        _LineSharpness ("Line Sharpness", Range(1, 40)) = 16
        _LineDensity ("Line Density", Range(0.1, 8)) = 0.45
        _ScrollSpeed ("Scroll Speed", Range(0, 16)) = 3.8

        _CenterCalmRadius ("Center Calm Radius", Range(0, 0.8)) = 0.58
        _CenterFadeRadius ("Center Fade Radius", Range(0.1, 1.2)) = 0.78
        _EdgeBoost ("Edge Boost", Range(0.5, 8)) = 2.4
        _EdgeFade ("Edge Fade", Range(0.8, 2.2)) = 1.55

        _WarpStrength ("Warp Strength", Range(0, 8)) = 1.1
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.4
        _LengthVariation ("Length Variation", Range(0, 1)) = 0.88
        _Brightness ("Brightness", Range(0, 5)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlowColor;
                float _Intensity;
                float _Opacity;
                float _Alpha;
                float _LaneCount;
                float _LaneJitter;
                float _LineSharpness;
                float _LineDensity;
                float _ScrollSpeed;
                float _CenterCalmRadius;
                float _CenterFadeRadius;
                float _EdgeBoost;
                float _EdgeFade;
                float _WarpStrength;
                float _NoiseStrength;
                float _LengthVariation;
                float _Brightness;
            CBUFFER_END

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453123);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453123);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv - 0.5;
                float rawRadius = length(centeredUv) * 2.0;
                float radius = saturate(rawRadius);
                float angle01 = atan2(centeredUv.y, centeredUv.x) * (1.0 / 6.28318530718) + 0.5;

                float centerFade = smoothstep(_CenterCalmRadius, _CenterFadeRadius, rawRadius);
                float edgeFade = 1.0 - smoothstep(_EdgeFade, _EdgeFade + 0.18, rawRadius);
                float edgeMask = saturate(centerFade * edgeFade);
                float edgeWeight = pow(saturate((rawRadius - _CenterCalmRadius) / max(0.0001, _EdgeFade - _CenterCalmRadius)), _EdgeBoost);

                float laneCoord = angle01 * _LaneCount;
                float laneIndex = floor(laneCoord);
                float laneFrac = frac(laneCoord) - 0.5;

                float laneSeedA = Hash11(laneIndex + 13.7);
                float laneSeedB = Hash11(laneIndex + 41.3);
                float laneSeedC = Hash11(laneIndex + 91.1);

                float laneWidth = lerp(0.012, 0.045, laneSeedA);
                float laneOffset = (laneSeedB - 0.5) * _LaneJitter;
                float laneMask = 1.0 - smoothstep(laneWidth, laneWidth + 0.01, abs(laneFrac + laneOffset));

                float edgePos = saturate((rawRadius - _CenterCalmRadius) / max(0.0001, _EdgeFade - _CenterCalmRadius));
                float stretchedPos = pow(edgePos, lerp(1.8, 0.42, saturate(_WarpStrength * 0.2)));
                float flow = _Time.y * _ScrollSpeed;

                float streakPhase = stretchedPos * lerp(0.3, 1.4, _LineDensity) - flow * lerp(0.16, 0.62, laneSeedA);
                float streakCell = floor(streakPhase);
                float streakFrac = frac(streakPhase);

                float streakSeedA = Hash21(float2(laneIndex + 1.2, streakCell + 9.3));
                float streakSeedB = Hash21(float2(laneIndex + 14.8, streakCell + 3.1));

                float streakLength = lerp(1.2, 2.35, streakSeedA * _LengthVariation + edgeWeight * (1.0 - _LengthVariation * 0.08));
                float head = smoothstep(0.0, 0.015, streakFrac);
                float tail = 1.0 - smoothstep(streakLength, streakLength + 0.3, streakFrac);
                float streakMask = saturate(max(head * tail, smoothstep(0.02, 0.18, edgePos)));

                float lineNoise = lerp(1.0, 0.65 + 0.35 * sin((angle01 * 120.0) + streakSeedB * 6.28318530718 + flow * 0.4), _NoiseStrength);
                float radialNoise = lerp(1.0, 0.7 + 0.3 * sin(rawRadius * 20.0 - flow * 0.6 + laneSeedC * 6.28318530718), _NoiseStrength * 0.8);
                float irregularity = saturate(lineNoise * radialNoise);

                float lineBody = laneMask * streakMask * irregularity * edgeMask;
                float lineCore = pow(saturate(lineBody), _LineSharpness);
                float lineSoft = pow(saturate(lineBody), max(1.0, _LineSharpness * 0.35));

                float rimGlow = pow(saturate(edgePos), 2.0) * (0.2 + 0.8 * lineSoft);
                float alpha = saturate(lineCore + lineSoft * 0.2 + rimGlow * 0.06);
                alpha *= saturate(max(_Intensity, max(_Opacity, _Alpha))) * _BaseColor.a;

                float3 color = _BaseColor.rgb * (lineCore * _Brightness + lineSoft * 0.18);
                color += _GlowColor.rgb * (lineSoft * 0.08 + rimGlow * 0.04);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
