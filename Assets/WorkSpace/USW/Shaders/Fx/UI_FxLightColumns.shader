// 레벨업 등급 연출(TierRevealFx) — 레전더리 등급 상승 순간의 세로 빛기둥 (레퍼런스: design/연출예시1.mp4 1.0~1.6초).
//   가로를 _Lanes 칸으로 나누고 칸마다 해시로 기둥 유무/폭/밝기를 정한다. 아래가 밝고 위로 갈수록 옅어지며,
//   세로 방향으로 밝기 결이 위로 흘러 "솟아오르는" 느낌을 낸다. _Rise(0~1)로 기둥이 아래에서 위로 차오른다.
//   시간은 전역 _USWUnscaledTime (UnscaledShaderTime 컴포넌트가 공급) — timeScale=0에서도 흐른다. 없으면 _Time.y.
//   출력은 premultiplied Additive. 색은 Image.color, 전체 밝기는 알파.
Shader "USW/UI/FxLightColumns"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.7, 0.25, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Lanes ("Lanes", Range(2, 32)) = 9
        _Density ("Density (chance a lane has a column)", Range(0, 1)) = 0.7
        _WidthMin ("Width Min (lane 0~1)", Range(0.05, 1)) = 0.3
        _WidthMax ("Width Max (lane 0~1)", Range(0.05, 1)) = 0.9
        _BrightnessMin ("Per-column Brightness Min", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.5
        _VerticalFalloff ("Vertical Falloff Power", Range(0.1, 6)) = 1.3
        _Rise ("Rise (0 none, 1 full height)", Range(0, 1)) = 1
        _FlowSpeed ("Flow Speed", Float) = 0.8
        _FlowAmount ("Flow Amount", Range(0, 1)) = 0.35
        _Seed ("Seed", Float) = 1
        _Jitter ("Jitter (0 = even spacing/width, 1 = random)", Range(0, 1)) = 1

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
            float _Intensity, _Lanes, _Density, _WidthMin, _WidthMax, _BrightnessMin, _Softness;
            float _VerticalFalloff, _Rise, _FlowSpeed, _FlowAmount, _Seed, _Jitter;
            float _USWUnscaledTime; // 전역 — UnscaledShaderTime

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

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
                float t = _USWUnscaledTime > 0.0 ? _USWUnscaledTime : _Time.y;
                float n = max(floor(_Lanes), 1.0);
                float x = input.uv.x * n;
                float lane = floor(x);
                float f = x - lane;
                float id = lane + _Seed * 13.7;

                float h1 = Hash(id), h2 = Hash(id + 5.3), h3 = Hash(id + 9.1), h4 = Hash(id + 2.7);
                if (h4 > _Density) return 0;

                // 칸 안의 기둥: 중심 c, 반폭 w (칸 밖으로 넘치지 않게). _Jitter 0이면 칸 가운데·같은 폭 → 고른 간격
                float w = lerp((_WidthMin + _WidthMax) * 0.5, lerp(_WidthMin, _WidthMax, h2), _Jitter) * 0.5;
                float c = lerp(0.5, w + (1.0 - 2.0 * w) * h1, _Jitter);
                float band = smoothstep(0.0, _Softness, saturate(1.0 - abs(f - c) / max(w, 1e-3)));
                float bright = lerp(_BrightnessMin, 1.0, h3);

                // 아래 밝고 위로 옅어짐 + 기둥마다 높이가 조금 다르게 차오름
                float y = input.uv.y;
                float top = _Rise * lerp(1.0 - 0.3 * _Jitter, 1.0, h1);
                float vertical = pow(saturate(1.0 - y / max(top, 1e-3)), _VerticalFalloff) * step(y, top);
                float flow = 1.0 - _FlowAmount * (0.5 + 0.5 * sin((y * 6.0 - t * _FlowSpeed * 6.2831) + h2 * 6.2831));

                float v = band * bright * vertical * flow * input.color.a * _Intensity;
                return half4(input.color.rgb * v, v);
            }
            ENDHLSL
        }
    }
}
