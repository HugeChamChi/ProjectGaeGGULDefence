// 시간 금고(9136) 유예 연출의 풀스크린 오버레이 UI 셰이더 (HTML 시안 C 시스템 오버라이드, 2026-11-05).
//   아래에서 위 순서로 겹친다: 붉은 틴트 → 가장자리 비네트 → 주사선 → 글리치 띠. _RectSize(px)는 TimeVaultFx가 매 프레임 넣는다.
//   출력은 premultiplied alpha (Image 정점 알파로 전체 페이드 가능).
Shader "USW/UI/TimeVaultOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused (Image)", 2D) = "white" {}
        _RectSize ("Rect Size (px)", Vector) = (1080, 1920, 0, 0)
        _Tint ("Tint (rgb, a = strength)", Color) = (1, 0.1, 0.08, 0)
        _VignetteColor ("Vignette Color", Color) = (0.7, 0, 0.04, 1)
        _Vignette ("Vignette Strength", Range(0, 1)) = 0
        _ScanColor ("Scan Color", Color) = (1, 0.2, 0.15, 1)
        _Scan ("Scan Strength", Range(0, 1)) = 0
        _ScanSpacing ("Scan Spacing (px)", Float) = 7
        _Glitch ("Glitch", Range(0, 1)) = 0
        _GlitchSeed ("Glitch Seed", Float) = 0
        _GlitchColorA ("Glitch Color A", Color) = (1, 0.18, 0.29, 1)
        _GlitchColorB ("Glitch Color B", Color) = (0.16, 1, 0.92, 1)

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

            float4 _RectSize, _Tint, _VignetteColor, _ScanColor, _GlitchColorA, _GlitchColorB;
            float _Vignette, _Scan, _ScanSpacing, _Glitch, _GlitchSeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            // premultiplied "over": 새 층(c, a)을 지금까지 쌓인 dst 위에 얹는다.
            void Over(inout float4 dst, float3 c, float a)
            {
                a = saturate(a);
                dst.rgb = c * a + dst.rgb * (1.0 - a);
                dst.a = a + dst.a * (1.0 - a);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 size = max(_RectSize.xy, 1.0);
                float2 p = uv * size;
                float4 col = 0;

                Over(col, _Tint.rgb, _Tint.a);

                float2 q = (uv - 0.5) * 2.0;
                float r = length(q * float2(1.0, 0.92));
                Over(col, _VignetteColor.rgb, smoothstep(0.55, 1.4, r) * _Vignette * _VignetteColor.a);

                float scanLine = step(0.5, frac(p.y / max(_ScanSpacing, 1.0)));
                Over(col, _ScanColor.rgb, scanLine * _Scan * _ScanColor.a);

                if (_Glitch > 0.0)
                {
                    float band = floor(uv.y * 34.0 + _GlitchSeed * 7.31);
                    float h = Hash(band + _GlitchSeed * 13.7);
                    if (h < _Glitch * 0.22)
                    {
                        float3 gc = Hash(band * 3.1 + _GlitchSeed) < 0.5 ? _GlitchColorA.rgb : _GlitchColorB.rgb;
                        Over(col, gc, 0.55 * _Glitch);
                    }
                }

                float a = input.color.a;
                return half4(col.rgb * a, col.a * a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
