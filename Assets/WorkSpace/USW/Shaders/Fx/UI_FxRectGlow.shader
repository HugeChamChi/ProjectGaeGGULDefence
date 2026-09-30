// 레벨업 카드 이펙트 — 카드 모양(둥근 사각형)을 따라 빛나는 테두리 + 바깥 번짐 + 사선 빛 쓸기.
//   레퍼런스: design/연출예시4.mp4 (카드 등급 테두리 번쩍임), design/연출예시3.mp4 (선택 카드 빛 쓸기).
//   사용처: CardRevealFx(③ 카드 출력), 이후 ④ 카드 선택.
//   쿼드는 카드보다 크게(여백 포함) 두고, 카드 반크기(_BoxHalf)와 쿼드 크기(_QuadSize)를 픽셀 단위로 넘긴다 —
//   컴포넌트가 인스턴스 머티리얼에 카드 크기를 넣어 준다 (UV만으로는 가로세로 비율을 알 수 없으므로).
//   색 = 정점색(등급색), 알파 = 전체 밝기. 빛 쓸기는 흰색에 가깝게. 출력은 premultiplied Additive.
Shader "USW/UI/FxRectGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _QuadSize ("Quad Size (px)", Vector) = (1100, 400, 0, 0)
        _BoxHalf ("Card Half Size (px)", Vector) = (450, 90, 0, 0)
        _Radius ("Corner Radius (px)", Float) = 24

        [Header(Border and Glow)]
        _BorderWidth ("Border Width (px)", Float) = 10
        _BorderIntensity ("Border Intensity", Range(0, 4)) = 1.5
        _GlowWidth ("Outer Glow Width (px)", Float) = 60
        _GlowIntensity ("Outer Glow Intensity", Range(0, 4)) = 0.8
        _InnerFill ("Inner Fill", Range(0, 1)) = 0.1

        [Header(Sweep)]
        _SweepPos ("Sweep Position (0 left ~ 1 right)", Float) = -1
        _SweepWidth ("Sweep Width (0~1 of card)", Range(0.01, 0.5)) = 0.08
        _SweepSlant ("Sweep Slant", Range(-2, 2)) = 0.6
        _SweepIntensity ("Sweep Intensity", Range(0, 4)) = 0
        _SweepWhite ("Sweep Whiteness", Range(0, 1)) = 0.8

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
        Blend One One
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
            float4 _QuadSize, _BoxHalf;
            float _Radius;
            float _BorderWidth, _BorderIntensity, _GlowWidth, _GlowIntensity, _InnerFill;
            float _SweepPos, _SweepWidth, _SweepSlant, _SweepIntensity, _SweepWhite;

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

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * _QuadSize.xy;
                float2 hs = max(_BoxHalf.xy, 1.0);
                float r = min(_Radius, min(hs.x, hs.y));
                float d = RoundBox(p, hs, r);

                float bt = d / max(_BorderWidth, 0.5);
                float border = _BorderIntensity * exp(-bt * bt);
                float outer = d > 0.0 ? _GlowIntensity * exp(-d / max(_GlowWidth, 1.0)) : 0.0;
                float inside = 1.0 - smoothstep(-1.5, 1.5, d);
                float fill = _InnerFill * inside;

                // 사선 빛 쓸기: 카드 가로 0~1 좌표에 기울기를 더한 띠 (카드 안쪽에만)
                float u = (p.x + p.y * _SweepSlant) / (2.0 * hs.x) + 0.5;
                float st = (u - _SweepPos) / max(_SweepWidth, 1e-3);
                float sweep = _SweepIntensity * exp(-st * st) * inside;

                float glow = (border + outer + fill) * input.color.a;
                float3 rgb = input.color.rgb * glow + lerp(input.color.rgb, 1.0.xxx, _SweepWhite) * sweep * input.color.a;
                float a = saturate(glow + sweep * input.color.a);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
