// 월드 공간 이펙트 공용 Additive — 하늘 레이저(SkyLaserFx)의 발사판·충전 구체·착탄 섬광 쿼드와 파티클에 쓴다.
//   색 = 텍스처(rgb × a) × 정점 색(파티클 색) × _Color × _Intensity. 검은 바탕 텍스처와 알파 텍스처 모두 받는다.
//   쿼드별 색/밝기는 SkyLaserFx가 MaterialPropertyBlock으로 _Color를 덮어쓴다. 블룸이 없는 게임이므로 밝기는 직접 올린다.
//   출력은 premultiplied alpha: _Opacity 0 = 순수 Additive, 올릴수록 밝은 배경을 덮어 색이 또렷해진다.
Shader "USW/Fx/AdditiveWorld"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 6)) = 1
        _Opacity ("Opacity (0 additive ~ 1 covers background)", Range(0, 1)) = 0
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
            Name "AdditiveWorld"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float mask = tex.a * input.color.a * _Color.a;
                float3 col = tex.rgb * mask * input.color.rgb * _Color.rgb * _Intensity;
                float cover = saturate(max(tex.r, max(tex.g, tex.b)) * mask * _Opacity);
                return half4(col, cover);
            }
            ENDHLSL
        }
    }
}
