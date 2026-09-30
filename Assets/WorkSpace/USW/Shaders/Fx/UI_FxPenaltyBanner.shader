// 패널티 연출 배너 — 둥근 사각형 판 + 흐르는 삼각형 무늬 + 테두리, 섬광(_Flash)·회색(_Gray) 상태.
//   레퍼런스: design/패널티연출.gif (배너 슬롯 → 회색 중간 문구 → 노란 섬광 → 결과). 레퍼런스의 해골 무늬 자리를
//   SPR_UI2100_CardFrame_Rare 머리띠처럼 살짝 비스듬한 삼각형 줄 무늬(SPR_UI1000_ButtonArrow_Off)로 바꾸고 줄 방향으로 끝없이 흐르게 했다.
//   무늬 배치(칸 = 아이콘 + 간격, 홀수 줄 반 칸 밀기, 방향 스크롤)는 HSD Custom/ScrollingIconPattern과 같은 방식.
//   그 셰이더를 쓰지 않은 이유: 아이콘 색을 곱해서 검은 삼각형 텍스처를 칠할 수 없고, 배너 모양으로 자르지 못하며, 시간이 _Time이라 timeScale 0에서 멈춘다.
//   아이콘 텍스처는 알파만 쓴다(모양 마스크) — 색은 _IconColor.
//   쿼드는 배너보다 크게(섬광 번짐 여백) 두고, 배너 반크기(_BoxHalf)와 쿼드 크기(_QuadSize)를 px로 넘긴다 (PenaltyRevealFx가 넣어 줌).
//   시간은 전역 _USWUnscaledTime (UnscaledShaderTime 컴포넌트가 공급) — 없으면 _Time.y. 출력은 premultiplied alpha.
Shader "USW/UI/FxPenaltyBanner"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _QuadSize ("Quad Size (px)", Vector) = (1100, 320, 0, 0)
        _BoxHalf ("Banner Half Size (px)", Vector) = (460, 80, 0, 0)
        _Radius ("Corner Radius (px)", Float) = 26

        [Header(Body)]
        _BgColor ("Background", Color) = (0.13, 0.12, 0.23, 0.96)
        _BorderColor ("Border", Color) = (0.62, 0.5, 1, 1)
        _BorderWidth ("Border Width (px)", Float) = 6
        _RimColor ("Outer Rim", Color) = (0.06, 0.05, 0.1, 1)
        _RimWidth ("Outer Rim Width (px)", Float) = 4

        [Header(Triangle Pattern)]
        _IconTex ("Icon (alpha = shape)", 2D) = "white" {}
        _IconColor ("Icon Color", Color) = (0.24, 0.21, 0.4, 1)
        _IconSize ("Icon Size (px)", Float) = 46
        _IconGap ("Icon Gap (px, X Y)", Vector) = (18, 8, 0, 0)
        _Stagger ("Stagger Odd Rows", Range(0, 1)) = 1
        _PatternAngle ("Row Angle (deg, - = down to the right)", Float) = -18
        _IconRotation ("Triangle Rotation (deg, on top of row angle, - = clockwise)", Float) = 0
        _ScrollSpeed ("Scroll Speed (px/s)", Float) = 40
        _ScrollDir ("Scroll Direction (screen, (1,1) = bottom-left to top-right)", Vector) = (1, 1, 0, 0)

        [Header(States)]
        _Gray ("Gray (0~1)", Range(0, 1)) = 0
        _GrayTint ("Gray Tint", Color) = (0.62, 0.62, 0.66, 1)
        _Flash ("Flash (0~1)", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 0.93, 0.45, 1)
        _GlowWidth ("Flash Outer Glow Width (px)", Float) = 40
        _GlowIntensity ("Flash Outer Glow Intensity", Range(0, 4)) = 1.2

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
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
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

            TEXTURE2D(_IconTex); SAMPLER(sampler_IconTex);
            float4 _Color;
            float4 _QuadSize, _BoxHalf;
            float _Radius;
            float4 _BgColor, _BorderColor, _RimColor;
            float _BorderWidth, _RimWidth;
            float4 _IconColor, _IconGap;
            float _IconSize, _Stagger, _PatternAngle, _IconRotation, _ScrollSpeed;
            float4 _ScrollDir;
            float _Gray, _Flash, _GlowWidth, _GlowIntensity;
            float4 _GrayTint, _FlashColor;
            float _USWUnscaledTime; // 전역 — UnscaledShaderTime

            // 둥근 사각형 부호거리 (px, 음수 = 안쪽)
            float RoundBox(float2 p, float2 hs, float r)
            {
                float2 q = abs(p) - hs + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
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

            // 삼각형 무늬 알파 (0~1). 화면 좌표를 흐름 방향으로 밀고(_ScrollDir, 기본 좌하단 → 우상단),
            // 줄 각도만큼 되돌려 무늬 좌표로 만든다 (줄이 가로가 되는 공간에서 칸을 나눔).
            float Pattern(float2 p, float t)
            {
                float2 dir = _ScrollDir.xy / max(length(_ScrollDir.xy), 1e-4);
                p -= dir * (t * _ScrollSpeed);
                float s, c;
                sincos(radians(-_PatternAngle), s, c);
                float2 q = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                float2 cell = max(_IconSize + _IconGap.xy, 1.0);
                float row = floor(q.y / cell.y);
                q.x += _Stagger * (abs(row) % 2.0) * cell.x * 0.5;
                float2 local = (frac(q / cell) - 0.5) * cell;       // 칸 중심 기준 px
                // 삼각형 자체 회전 (줄 기울기에 더해짐): 칸 중심 기준으로 샘플 좌표를 반대로 돌린다
                float rs, rc;
                sincos(radians(-_IconRotation), rs, rc);
                local = float2(rc * local.x - rs * local.y, rs * local.x + rc * local.y);
                float2 iuv = local / max(_IconSize, 1.0) + 0.5;
                float inside = step(0.0, iuv.x) * step(iuv.x, 1.0) * step(0.0, iuv.y) * step(iuv.y, 1.0);
                return SAMPLE_TEXTURE2D(_IconTex, sampler_IconTex, iuv).a * inside;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _USWUnscaledTime > 0.0 ? _USWUnscaledTime : _Time.y;
                float2 p = (input.uv - 0.5) * _QuadSize.xy;
                float2 hs = max(_BoxHalf.xy, 1.0);
                float r = min(_Radius, min(hs.x, hs.y));
                float d = RoundBox(p, hs, r);

                // 판: 바탕 + 무늬 → 테두리(안쪽 띠) → 바깥 어두운 테
                float icon = Pattern(p, t) * _IconColor.a;
                float3 col = lerp(_BgColor.rgb, _IconColor.rgb, icon);
                float bodyA = _BgColor.a;
                float border = smoothstep(-_BorderWidth - 1.0, -_BorderWidth + 1.0, d);
                col = lerp(col, _BorderColor.rgb, border);
                bodyA = lerp(bodyA, _BorderColor.a, border);
                float rim = smoothstep(-0.75, 0.75, d);
                col = lerp(col, _RimColor.rgb, rim);
                bodyA = lerp(bodyA, _RimColor.a, rim);
                float shape = 1.0 - smoothstep(_RimWidth - 0.75, _RimWidth + 0.75, d);

                // 회색 (중간 문구 단계)
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, lum * _GrayTint.rgb * 1.6, _Gray);

                // 섬광: 판 전체를 섬광색으로 + 바깥 번짐
                col = lerp(col, _FlashColor.rgb, _Flash);
                bodyA = lerp(bodyA, 1.0, _Flash);
                float outer = max(d - _RimWidth, 0.0);
                float glow = (1.0 - shape) * _Flash * _GlowIntensity * exp(-outer / max(_GlowWidth, 1.0));

                float a = input.color.a;
                float bodyAlpha = shape * bodyA * a;
                float3 rgb = col * bodyAlpha + _FlashColor.rgb * glow * a;
                return half4(rgb, saturate(bodyAlpha + glow * a * 0.5));
            }
            ENDHLSL
        }
    }
}
