// 레벨업 진입 전환 연출 — SPR_UI2100_ScreenBg 그림풍(플랫 주황 바탕 / 진한 사선 / 둥근 노란 캡슐+점)의 속도선이
//   우상단 → 좌하단으로 촘촘하게 쏟아지며 화면을 덮었다가(_WipeIn) 빠져나간다(_WipeOut). 레퍼런스: design/셰이더11예시.webm (밀도·속도감만 참고, 블러 없는 플랫 스타일).
//   4겹 레이어(원근): 먼 가는 진한 선 → 진한 띠 → 밝은 가는 선 → 앞쪽 노란 캡슐. 뒤 레이어일수록 느리다.
//   원본 이미지는 타일링이 안 되고 줄기가 그림에 박혀 있어 색만 따왔다 (_TextureBlend로 섞을 수는 있음, 텍스처 비율은 45도 보정에 사용).
//   시간은 전역 _USWUnscaledTime (UnscaledShaderTime 컴포넌트가 공급) — 레벨업 중 timeScale=0에서도 흐른다. 없으면 _Time.y.
//   레이어 파라미터: Params = (레인 폭, 굵기, 밀도 0~1, 속도 [화면 높이/초]), Shape = (길이 최소, 길이 최대, 주기 최소, 주기 최대)
Shader "USW/UI/DiagonalFlowBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Flow)]
        _FlowSpeed ("Flow Speed (global)", Range(0, 3)) = 1

        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.996, 0.533, 0.2, 1)
        _TextureBlend ("Texture Blend (0 = base only)", Range(0, 1)) = 0

        [Header(Layer 1 Far Dark Lines)]
        _L1Color ("Color", Color) = (0.996, 0.494, 0.133, 1)
        _L1Params ("Lane, Thick, Density, Speed", Vector) = (0.009, 0.0018, 0.85, 0.9)
        _L1Shape ("Len Min/Max, Period Min/Max", Vector) = (0.3, 1.0, 0.5, 1.1)

        [Header(Layer 2 Dark Bands)]
        _L2Color ("Color", Color) = (0.93, 0.44, 0.11, 1)
        _L2Params ("Lane, Thick, Density, Speed", Vector) = (0.038, 0.006, 0.65, 1.4)
        _L2Shape ("Len Min/Max, Period Min/Max", Vector) = (0.3, 1.1, 0.6, 1.4)

        [Header(Layer 3 Light Thin Lines)]
        _L3Color ("Color", Color) = (1.0, 0.66, 0.26, 1)
        _L3Params ("Lane, Thick, Density, Speed", Vector) = (0.013, 0.0024, 0.7, 2.0)
        _L3Shape ("Len Min/Max, Period Min/Max", Vector) = (0.15, 0.55, 0.4, 0.9)

        [Header(Layer 4 Yellow Capsules)]
        _L4Color ("Color", Color) = (0.996, 0.71, 0.275, 1)
        _L4Params ("Lane, Thick, Density, Speed", Vector) = (0.045, 0.009, 0.7, 2.8)
        _L4Shape ("Len Min/Max, Period Min/Max", Vector) = (0.12, 0.38, 0.5, 1.1)
        _DotChance ("Capsule Dot Chance", Range(0, 1)) = 0.55

        [Header(Transition Wipe TR to BL)]
        _WipeIn ("Wipe In (0 hidden, 1 covered)", Range(0, 1)) = 1
        _WipeOut ("Wipe Out (0 covered, 1 gone)", Range(0, 1)) = 0
        _WipeLaneWidth ("Edge Lane Width", Range(0.005, 0.2)) = 0.03
        _WipeJagged ("Edge Jaggedness", Range(0, 1)) = 0.35
        _WipeRim ("Edge Rim Width", Range(0, 0.2)) = 0.035
        _WipeRimColor ("Edge Rim Color", Color) = (0.996, 0.71, 0.275, 1)

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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _FlowSpeed;
            float4 _BaseColor;
            float _TextureBlend;

            float4 _L1Color, _L1Params, _L1Shape;
            float4 _L2Color, _L2Params, _L2Shape;
            float4 _L3Color, _L3Params, _L3Shape;
            float4 _L4Color, _L4Params, _L4Shape;
            float _DotChance;
            float _WipeIn, _WipeOut, _WipeLaneWidth, _WipeJagged, _WipeRim;
            float4 _WipeRimColor;

            float _USWUnscaledTime; // 전역 — UnscaledShaderTime

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            // 한 레이어의 줄기 마스크. u = 줄기 축(우상 방향) 좌표, v = 축에 수직 좌표 (화면 높이 = 1 단위).
            // 시간이 흐르면 패턴이 -u(좌하) 방향으로 이동한다. 캡슐은 [0, len] 구간, 점은 우상단 끝 앞에 놓인다.
            // 레인마다 굵기/길이/주기/속도를 랜덤으로 흔들어 레퍼런스처럼 불규칙한 속도선을 만든다.
            float Streaks(float u, float v, float t, float4 prm, float4 shape, float dotChance, float seed, float aa)
            {
                float laneW = prm.x;
                float lane = floor(v / laneW);
                if (Hash(lane + seed) > prm.z) return 0;
                float h2 = Hash(lane * 1.71 + seed + 3.1);
                float h3 = Hash(lane * 2.37 + seed + 7.7);
                float h4 = Hash(lane * 3.13 + seed + 1.3);
                float h5 = Hash(lane * 4.51 + seed + 5.9);

                float thick = prm.y * lerp(0.6, 1.4, h5);
                float across = (frac(v / laneW) - 0.5) * laneW + (h2 - 0.5) * max(laneW - thick * 2.0, 0.0);
                float period = lerp(shape.z, shape.w, h3);
                float len = min(lerp(shape.x, shape.y, h4), period * 0.85);
                float s = frac((u + t * prm.w * lerp(0.75, 1.3, h2) + h3 * 17.0) / period) * period;

                float along = min(max(s - len, 0.0), period - s);
                float mask = 1.0 - smoothstep(-aa, aa, length(float2(along, across)) - thick);

                if (h4 < dotChance)
                {
                    float r = thick * 1.25;
                    float center = len + thick * 2.2 + r;
                    mask = max(mask, 1.0 - smoothstep(-aa, aa, length(float2(s - center, across)) - r));
                }
                return saturate(mask);
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
                half4 tex = tex2D(_MainTex, input.uv);
                half4 col = half4(lerp(_BaseColor.rgb, tex.rgb, _TextureBlend), tex.a) * input.color;

                // 높이 기준 정규화 좌표 (텍스처 비율로 가로 보정 → 45도 유지)
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 p = float2(input.uv.x * aspect, input.uv.y);
                const float k = 0.70710678;
                float u = (p.x + p.y) * k;   // 우상 방향
                float v = (p.y - p.x) * k;   // 축에 수직
                float aa = max(fwidth(u), 1e-5) * 1.2;

                float t = (_USWUnscaledTime > 0.0 ? _USWUnscaledTime : _Time.y) * _FlowSpeed;

                col.rgb = lerp(col.rgb, _L1Color.rgb, Streaks(u, v, t, _L1Params, _L1Shape, 0.0, 11.0, aa) * _L1Color.a);
                col.rgb = lerp(col.rgb, _L2Color.rgb, Streaks(u, v, t, _L2Params, _L2Shape, 0.0, 97.0, aa) * _L2Color.a);
                col.rgb = lerp(col.rgb, _L3Color.rgb, Streaks(u, v, t, _L3Params, _L3Shape, 0.0, 29.0, aa) * _L3Color.a);
                col.rgb = lerp(col.rgb, _L4Color.rgb, Streaks(u, v, t, _L4Params, _L4Shape, _DotChance, 53.0, aa) * _L4Color.a);

                // 전환 와이프: 우상단 모서리에서의 거리 d를 레인마다 들쭉날쭉하게 흔들어 속도선 모양 경계를 만든다.
                //   In: 앞 경계가 좌하로 전진하며 덮는다 / Out: 뒤 경계가 같은 방향으로 따라가며 걷어낸다. 경계엔 노란 테두리.
                float uMax = (aspect + 1.0) * k;
                float jit = Hash(floor(v / _WipeLaneWidth) * 7.3 + 5.0) * _WipeJagged;
                float range = uMax + _WipeJagged + _WipeRim;
                float d = (uMax - u) + jit;
                float front = _WipeIn >= 1.0 ? 1e4 : _WipeIn * range;
                float back = _WipeOut <= 0.0 ? -1e4 : _WipeOut * range;
                float inside = smoothstep(-aa, aa, front - d) * smoothstep(-aa, aa, d - back);
                float rim = max((1.0 - smoothstep(_WipeRim - aa, _WipeRim + aa, front - d)),
                                (1.0 - smoothstep(_WipeRim - aa, _WipeRim + aa, d - back)));
                col.rgb = lerp(col.rgb, _WipeRimColor.rgb, rim * _WipeRimColor.a);
                col.a *= inside;
                return col;
            }
            ENDHLSL
        }
    }
}
