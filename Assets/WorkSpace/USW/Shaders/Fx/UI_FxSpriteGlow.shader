// 레벨업 카드 이펙트 — 카드 테두리 스프라이트의 실루엣(알파)을 번지게 해 카드 모양 그대로 빛나게 한다.
//   레퍼런스: design/연출예시4.mp4 (카드 등급 테두리 번쩍임), design/연출예시3.mp4 (빛 쓸기).
//   사용처: CardRevealFx(③ 카드 출력). 사각형 근사(UI_FxRectGlow)는 카드 그림의 투명 여백·비스듬한 끝과 어긋나서 이 방식으로 바꿨다.
//   Image(Simple)에 카드와 같은 스프라이트를 넣고 영역을 여백만큼 키운 뒤, _Expand(= 쿼드 크기 / 카드 크기)로
//   UV를 되돌려 원래 크기의 실루엣을 샘플한다. 번짐은 12방향 × 3고리 샘플 평균 (짧은 연출용, 카드 3장).
//   아틀라스로 묶이지 않은 단일 스프라이트(UV 0~1) 전제. 출력은 premultiplied Additive, 색 = 정점색(등급색).
Shader "USW/UI/FxSpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Expand ("Expand (quad / card size)", Vector) = (1.3, 2, 0, 0)
        _RadiusUV ("Glow Radius (card UV)", Vector) = (0.06, 0.25, 0, 0)

        [Header(Glow)]
        _GlowIntensity ("Outer Glow Intensity", Range(0, 4)) = 1.2
        _GlowGain ("Outer Glow Gain", Range(0.5, 8)) = 2.5
        _BorderIntensity ("Inner Rim Intensity", Range(0, 4)) = 1.5
        _InnerFill ("Inner Fill", Range(0, 1)) = 0.1

        [Header(Sweep)]
        _SweepPos ("Sweep Position (0 left ~ 1 right)", Float) = -1
        _SweepWidth ("Sweep Width (0~1 of card)", Range(0.01, 0.5)) = 0.07
        _SweepSlant ("Sweep Slant", Range(-2, 2)) = 0.25
        _SweepIntensity ("Sweep Intensity", Range(0, 4)) = 0
        _SweepWhite ("Sweep Whiteness", Range(0, 1)) = 0.85

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

            sampler2D _MainTex;
            float4 _Color;
            float4 _Expand, _RadiusUV;
            float _GlowIntensity, _GlowGain, _BorderIntensity, _InnerFill;
            float _SweepPos, _SweepWidth, _SweepSlant, _SweepIntensity, _SweepWhite;

            // 카드 UV(0~1) 밖은 투명으로 본다
            float AlphaAt(float2 uv)
            {
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
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
                float2 uv = (input.uv - 0.5) * _Expand.xy + 0.5;   // 쿼드 UV → 카드 UV
                float a0 = AlphaAt(uv);

                // 12방향 × 3고리 평균 — 고리마다 각도를 엇갈려 계단 모양 윤곽이 겹쳐 보이지 않게 한다
                float outerSum = 0.0, innerSum = 0.0;
                [unroll] for (int i = 0; i < 12; i++)
                {
                    float ang = i * 0.52359878;
                    float2 d0 = float2(cos(ang), sin(ang)) * _RadiusUV.xy;
                    float2 d1 = float2(cos(ang + 0.2617994), sin(ang + 0.2617994)) * _RadiusUV.xy;
                    outerSum += AlphaAt(uv + d0) + AlphaAt(uv + d1 * 0.66);
                    innerSum += AlphaAt(uv + d0 * 0.33);
                }
                float blurOuter = outerSum / 24.0;
                float blurInner = innerSum / 12.0;

                float outer = saturate(max(blurOuter, blurInner) * _GlowGain) * (1.0 - a0);   // 카드 바깥 번짐
                float rim = a0 * saturate(1.0 - blurInner) * 2.0;                              // 카드 가장자리 안쪽 테두리
                float fill = a0 * _InnerFill;

                // 사선 빛 쓸기 (카드 실루엣 안쪽만)
                float u = uv.x + (uv.y - 0.5) * _SweepSlant;
                float st = (u - _SweepPos) / max(_SweepWidth, 1e-3);
                float sweep = _SweepIntensity * exp(-st * st) * a0;

                float glow = (outer * _GlowIntensity + rim * _BorderIntensity + fill) * input.color.a;
                float3 rgb = input.color.rgb * glow + lerp(input.color.rgb, 1.0.xxx, _SweepWhite) * sweep * input.color.a;
                float a = saturate(glow + sweep * input.color.a);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
