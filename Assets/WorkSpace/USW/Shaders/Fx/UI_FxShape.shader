// 만화풍 UI 이펙트 도형 — 외곽이 또렷한 도형 + 어두운 외곽선 + 흰 코어 (우리 게임의 두꺼운 외곽선 화풍에 맞춤).
//   레퍼런스: design/연출예시2.mp4 (날카로운 방사선·반짝이 별), 사용처: 레벨업 연출 (게이지 버스트, 등급 연출, 카드 이펙트).
//   _Shape  0 = 별 모양 폭발 (가시 _Spikes개, 가시마다 길이 무작위)
//           1 = 링 (반지름 _RingRadius, 두께 _Thickness — 퍼질 때 두께를 줄이면 얇아지며 사라진다)
//           2 = 4각 반짝이 별 (✦, _StarPinch가 작을수록 가늘고 뾰족)
//   색 = 정점색(Image.color / 파티클 색) × _Color, 코어는 _CoreColor, 외곽선은 _OutlineColor.
//   거리는 UV 반지름(0 중심 ~ 1 가장자리) 기준 근사 부호거리 — fwidth로 가장자리를 부드럽게(AA) 처리한다.
Shader "USW/UI/FxShape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(Starburst,0,Ring,1,Sparkle,2)] _Shape ("Shape", Float) = 0

        [Header(Starburst)]
        _Spikes ("Spikes", Range(3, 32)) = 12
        _InnerRadius ("Valley Radius", Range(0.05, 1)) = 0.45
        _SpikeMin ("Spike Length Min (0~1)", Range(0, 1)) = 0.6
        _Seed ("Seed", Float) = 1

        [Header(Ring)]
        _RingRadius ("Ring Radius", Range(0, 1)) = 0.85
        _Thickness ("Ring Thickness", Range(0, 1)) = 0.1

        [Header(Sparkle)]
        _StarPinch ("Star Pinch (0.3 thin ~ 1 diamond)", Range(0.2, 1)) = 0.45

        [Header(Outline and Core)]
        _OutlineColor ("Outline Color", Color) = (0.2, 0.1, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.3)) = 0.04
        _CoreColor ("Core Color", Color) = (1,1,1,1)
        _CoreSize ("Core Size (0~1 of shape)", Range(0, 1)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestingMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float _Shape;
            float _Spikes, _InnerRadius, _SpikeMin, _Seed;
            float _RingRadius, _Thickness;
            float _StarPinch;
            float4 _OutlineColor;
            float _OutlineWidth;
            float4 _CoreColor;
            float _CoreSize;

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            // 별 모양 폭발: 각도마다 가장자리 반지름 R(θ)을 구하고 d = r - R (음수 = 안쪽).
            // shapeSize = 그 방향의 도형 크기 (코어 크기 비율 계산용)
            float StarburstDist(float2 p, out float shapeSize)
            {
                float r = length(p);
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float n = max(floor(_Spikes), 3.0);
                float x = a * n;
                float id = floor(x);
                float f = x - id;
                // 골짜기(f=0,1)는 _InnerRadius, 가시 끝(f=0.5)은 가시마다 다른 길이
                float tip = lerp(_SpikeMin, 1.0, Hash(fmod(id, n) + _Seed * 17.0));
                float tri = 1.0 - abs(f * 2.0 - 1.0);
                float R = lerp(_InnerRadius, tip, tri);
                shapeSize = R;
                // 방사 방향 거리를 가시 기울기로 보정해 외곽선 두께를 대략 일정하게 만든다
                float slope = (tip - _InnerRadius) * 2.0 * n / 6.2831853 / max(r, 1e-3);
                return (r - R) / sqrt(1.0 + slope * slope);
            }

            float RingDist(float2 p, out float shapeSize)
            {
                shapeSize = _Thickness * 0.5;
                return abs(length(p) - _RingRadius) - _Thickness * 0.5;
            }

            // 4각 반짝이 별(✦): |x|^k + |y|^k = 1 (k < 1이면 가운데로 오목하게 좁아짐)
            float SparkleDist(float2 p, out float shapeSize)
            {
                float k = _StarPinch;
                float2 q = abs(p) + 1e-5;
                float v = pow(pow(q.x, k) + pow(q.y, k), 1.0 / k);
                shapeSize = 1.0;
                return v - 1.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float size;
                float d;
                if (_Shape < 0.5)      d = StarburstDist(p, size);
                else if (_Shape < 1.5) d = RingDist(p, size);
                else                   d = SparkleDist(p, size);

                float aa = max(fwidth(d), 1e-4);
                float inside = 1.0 - smoothstep(-aa, aa, d);                       // 채움 영역
                float fillArea = 1.0 - smoothstep(-aa, aa, d + _OutlineWidth);     // 외곽선 안쪽
                // 코어: 도형 크기 대비 안쪽 _CoreSize 비율 (링은 두께 중심선 쪽)
                float coreEdge = -size * (1.0 - _CoreSize);
                float core = _CoreSize > 0.0 ? 1.0 - smoothstep(-aa, aa, d - coreEdge) : 0.0;

                float3 rgb = lerp(_OutlineColor.rgb, input.color.rgb, fillArea);
                rgb = lerp(rgb, _CoreColor.rgb, core * _CoreColor.a * fillArea);
                float outlineAlpha = lerp(_OutlineColor.a, 1.0, fillArea);
                return half4(rgb, inside * outlineAlpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
