// 보상 공개 연출(RewardRevealFx) 공용 원형 글로우 — 텍스처 없이 UV 반지름으로 모양을 만든다.
//   레퍼런스: design/연출예시1.mp4 (화면 섬광, 코어 글로우, 보케 원, 충격파 구체, 불씨 입자).
//   한 셰이더를 머티리얼 파라미터만 바꿔 여러 모양으로 쓴다:
//     코어 글로우  = Core 항만 (Hardness 1, Falloff 2 전후)
//     보케 원      = Core 항, Hardness 크게(6~) → 가장자리만 부드러운 원판
//     충격파 구체  = Inner + Fresnel(가장자리로 갈수록 밝음) + Rim(테두리 띠), Core 0
//   색 = _Color × 정점색(Image.color / 파티클 색), 알파가 모양. 출력은 premultiplied —
//   블렌드는 Additive(One One) 또는 Alpha(One OneMinusSrcAlpha)를 머티리얼에서 고른다.
Shader "USW/UI/FxGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 8)) = 1

        [Header(Core Glow)]
        _CoreIntensity ("Core Intensity", Range(0, 4)) = 1
        _CoreHardness ("Core Hardness (1 soft, 8 disc)", Range(0.5, 16)) = 1
        _CoreFalloff ("Core Falloff Power", Range(0.1, 8)) = 2
        _WhiteCore ("White Core Amount", Range(0, 4)) = 0
        _WhiteCorePower ("White Core Tightness", Range(0.5, 16)) = 4

        [Header(Bubble Sphere)]
        _InnerAlpha ("Inner Fill", Range(0, 1)) = 0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 4)) = 0
        _FresnelPower ("Fresnel Power", Range(0.5, 16)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 0
        _RimWidth ("Rim Width", Range(0.005, 0.5)) = 0.05
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimTint ("Rim Tint by Vertex Color (0 = Rim Color only)", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.02

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One = additive, OneMinusSrcAlpha = alpha)", Float) = 1

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
        Blend [_SrcBlend] [_DstBlend]
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
            float _CoreIntensity, _CoreHardness, _CoreFalloff, _WhiteCore, _WhiteCorePower;
            float _InnerAlpha, _FresnelIntensity, _FresnelPower, _RimIntensity, _RimWidth, _EdgeSoftness;
            float4 _RimColor;
            float _RimTint;

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
                float r = length(input.uv - 0.5) * 2.0;   // 0 중심 ~ 1 가장자리
                float inside = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, r);

                float core = _CoreIntensity * pow(saturate(1.0 - pow(saturate(r), _CoreHardness)), _CoreFalloff);
                float body = (_InnerAlpha + _FresnelIntensity * pow(saturate(r), _FresnelPower)) * inside;
                float rimT = (r - (1.0 - _RimWidth)) / _RimWidth;
                float rim = _RimIntensity * exp(-rimT * rimT) * inside;

                float3 rgb = input.color.rgb;
                rgb = lerp(rgb, 1.0.xxx, saturate(_WhiteCore * pow(saturate(1.0 - r), _WhiteCorePower)));
                float a = saturate(core + body) * input.color.a;
                float rimA = saturate(rim) * input.color.a;

                // premultiplied 출력 (Additive면 알파는 무시되고 rgb만 더해진다)
                // 테두리 색: _RimTint만큼 정점색(등급색 등)을 따라가게 해 한 머티리얼로 여러 색 구체를 만든다
                float3 rimRgb = lerp(_RimColor.rgb, input.color.rgb, _RimTint);
                float3 outRgb = (rgb * a + rimRgb * rimA) * _Intensity;
                float outA = saturate(a + rimA);
                return half4(outRgb, outA);
            }
            ENDHLSL
        }
    }
}
