// 보상 공개 연출(RewardRevealFx) 뒤 방사형 광선 — 쿼드 중심에서 사방으로 뻗는 빛줄기가 천천히 회전한다.
//   레퍼런스: design/연출예시1.mp4 (상자 뒤 보라/금색 광선, 공개 후에도 은은하게 유지).
//   각도를 _RayCount 구간으로 나누고 구간마다 해시로 빛줄기 위치/폭/밝기/유무를 정한다 → 불규칙한 광선.
//   2겹(서로 반대 방향 회전, 다른 개수)을 더해 겹치며 흐르는 느낌을 낸다. 밝기는 중심에서 멀어질수록 감쇠.
//   시간은 전역 _USWUnscaledTime (UnscaledShaderTime 컴포넌트가 공급) — timeScale=0에서도 흐른다. 없으면 _Time.y.
//   출력은 premultiplied Additive. 전체 밝기/페이드는 Image.color 알파로 조절한다.
Shader "USW/UI/FxGodRays"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.6, 0.3, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1

        [Header(Layer A)]
        _CountA ("Ray Count", Range(1, 32)) = 9
        _WidthA ("Width Min/Max (sector 0~0.5)", Vector) = (0.08, 0.3, 0, 0)
        _DensityA ("Density (chance a sector has a ray)", Range(0, 1)) = 0.8
        _SpeedA ("Rotation Speed (turns/sec)", Float) = 0.015
        _SeedA ("Seed", Float) = 1

        [Header(Layer B)]
        _CountB ("Ray Count", Range(1, 32)) = 13
        _WidthB ("Width Min/Max (sector 0~0.5)", Vector) = (0.05, 0.2, 0, 0)
        _DensityB ("Density", Range(0, 1)) = 0.6
        _SpeedB ("Rotation Speed (turns/sec)", Float) = -0.01
        _SeedB ("Seed", Float) = 7
        _LayerBWeight ("Layer B Weight", Range(0, 1)) = 0.6

        [Header(Shape)]
        _BrightnessMin ("Per-ray Brightness Min", Range(0, 1)) = 0.35
        _Softness ("Ray Edge Softness (0 hard, 1 soft)", Range(0.05, 1)) = 0.8
        _Pulse ("Per-ray Pulse Amount", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _InnerRadius ("Inner Fade Radius", Range(0, 0.5)) = 0.03
        _RadialFalloff ("Radial Falloff Power", Range(0.1, 6)) = 1.4
        _CenterGlow ("Center Glow", Range(0, 2)) = 0.3
        _LowerDim ("Lower Half Dim (0 none, 1 hidden)", Range(0, 1)) = 0

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
            float _Intensity;
            float _CountA, _DensityA, _SpeedA, _SeedA;
            float4 _WidthA;
            float _CountB, _DensityB, _SpeedB, _SeedB, _LayerBWeight;
            float4 _WidthB;
            float _BrightnessMin, _Softness, _Pulse, _PulseSpeed;
            float _InnerRadius, _RadialFalloff, _CenterGlow, _LowerDim;

            float _USWUnscaledTime; // 전역 — UnscaledShaderTime

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            // a: 0~1 각도(한 바퀴), 구간마다 빛줄기 하나 (없을 수도 있음). 빛줄기는 구간 안에만 있어 이음매가 없다.
            float RayLayer(float a, float t, float count, float2 widthRange, float density, float speed, float seed)
            {
                float n = max(floor(count), 1.0);
                float x = frac(a + t * speed) * n;
                float sector = floor(x);
                float f = x - sector;
                float id = sector + seed * 31.7;

                float h1 = Hash(id);
                float h2 = Hash(id + 17.3);
                float h3 = Hash(id + 41.9);
                float h4 = Hash(id + 73.1);
                if (h4 > density) return 0;

                float w = min(lerp(widthRange.x, widthRange.y, h2), 0.5);
                float c = w + (1.0 - 2.0 * w) * h1;
                float d = saturate(1.0 - abs(f - c) / w);
                // 가장자리 부드러움: Softness 1이면 삼각형 → smoothstep, 작을수록 판판한 띠
                float edge = smoothstep(0.0, _Softness, d);
                float bright = lerp(_BrightnessMin, 1.0, h3);
                bright *= 1.0 - _Pulse * (0.5 + 0.5 * sin(t * _PulseSpeed + h1 * 6.2831));
                return edge * bright;
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
                float r = length(p);
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float t = _USWUnscaledTime > 0.0 ? _USWUnscaledTime : _Time.y;

                float rays = RayLayer(a, t, _CountA, _WidthA.xy, _DensityA, _SpeedA, _SeedA);
                rays += _LayerBWeight * RayLayer(a, t, _CountB, _WidthB.xy, _DensityB, _SpeedB, _SeedB);

                float radial = pow(saturate(1.0 - r), _RadialFalloff) * smoothstep(0.0, _InnerRadius + 1e-4, r);
                // 레퍼런스 광선은 주로 위쪽으로 뻗는다 (아래는 배너/카드에 가려 거의 안 보임)
                radial *= lerp(1.0 - _LowerDim, 1.0, smoothstep(-0.35, 0.15, p.y));
                float glow = _CenterGlow * pow(saturate(1.0 - r * 2.5), 2.0);
                float v = (saturate(rays) * radial + glow) * input.color.a * _Intensity;
                return half4(input.color.rgb * v, v);
            }
            ENDHLSL
        }
    }
}
