// 하늘 레이저(SkyLaserFx) 빔 본체 — 레퍼런스: design/레이저연출예시1.mp4, 레이저연출예시2.gif.
//   단위 쿼드(uv.x = 폭 방향, uv.y = 길이 방향, 0 = 착탄점 / 1 = 발사판)에 그린다.
//   가로 단면: 하얀 심지(_CoreWidth) + 색 번짐(_GlowPower). 게임에 블룸이 없으므로 번짐은 셰이더가 직접 만든다.
//   세로: _FlowTex 결이 발사판 → 착탄점 방향으로 흐르고(_FlowSpeed), 전체 밝기가 빠르게 떨린다(_Flicker).
//   _Length(월드 길이)·_Alpha·_Seed는 SkyLaserFx가 MaterialPropertyBlock으로 매 프레임 넣는다.
//   시간은 _Time.y(scaled) — 인게임 일시정지 시 함께 멈춘다.
//   출력은 premultiplied alpha: 밝은 하늘 배경에서도 빔이 보이도록 번짐이 _GlowOpacity만큼 배경을 덮는다 (0 = 순수 Additive).
Shader "USW/Fx/LaserBeam"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (0.25, 1, 0.85, 1)
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _Intensity ("Glow Intensity", Range(0, 4)) = 1.4
        _GlowOpacity ("Glow Opacity (0 additive ~ 1 covers background)", Range(0, 1)) = 0.55
        _CoreWidth ("Core Width (0~1 of half width)", Range(0.01, 1)) = 0.22
        _GlowPower ("Glow Falloff Power", Range(0.5, 8)) = 2.4
        _FlowTex ("Flow Texture", 2D) = "white" {}
        _FlowTiling ("Flow Tiling (per world unit)", Float) = 0.35
        _FlowSpeed ("Flow Speed (world units/s)", Float) = 9
        _FlowAmount ("Flow Amount", Range(0, 1)) = 0.55
        _Flicker ("Flicker Amount", Range(0, 1)) = 0.12
        _FlickerSpeed ("Flicker Speed", Float) = 55
        _TipSoft ("Tip Softness (world units)", Float) = 0.25
        _Length ("Length (world, set by script)", Float) = 5
        _Alpha ("Alpha (set by script)", Range(0, 1)) = 1
        _Seed ("Seed (set by script)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "LaserBeam"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_FlowTex);
            SAMPLER(sampler_FlowTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float4 _FlowTex_ST;
                float _Intensity, _GlowOpacity, _CoreWidth, _GlowPower;
                float _FlowTiling, _FlowSpeed, _FlowAmount;
                float _Flicker, _FlickerSpeed, _TipSoft;
                float _Length, _Alpha, _Seed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float x = abs(input.uv.x * 2.0 - 1.0);          // 0 = 중심선, 1 = 가장자리
                float along = input.uv.y * _Length;             // 착탄점에서의 월드 거리

                float glow = pow(saturate(1.0 - x), _GlowPower);
                float core = 1.0 - smoothstep(_CoreWidth * 0.45, _CoreWidth, x);

                // 결: 착탄점 방향으로 흐른다 (같은 무늬가 along 감소 방향으로 이동)
                float2 flowUv = float2(input.uv.x * 0.5 + _Seed * 0.37, along * _FlowTiling + t * _FlowSpeed * _FlowTiling);
                float flow = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, flowUv).r;
                float flowMul = lerp(1.0, 0.45 + flow * 1.3, _FlowAmount);

                float flick = 1.0 + _Flicker * sin(t * _FlickerSpeed + _Seed * 17.0) * sin(t * _FlickerSpeed * 0.37 + _Seed * 5.0);

                float tip = max(_TipSoft, 1e-3);
                float ends = smoothstep(0.0, tip, along) * smoothstep(0.0, tip, _Length - along);

                float3 col = _Color.rgb * (glow * flowMul * _Intensity) + _CoreColor.rgb * core * lerp(1.0, flowMul, 0.35);
                float fade = flick * ends * _Alpha;
                float cover = saturate(glow * _GlowOpacity + core) * ends * _Alpha;
                return half4(col * fade, cover);
            }
            ENDHLSL
        }
    }
}
