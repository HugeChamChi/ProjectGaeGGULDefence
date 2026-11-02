Shader "USW/Fx/StatBuffBatch"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glow ("Glow", Float) = 0
        _Intensity ("Intensity", Float) = 1
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Glow, _Intensity, _Opacity;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = tex.a * input.color.a * _Color.a;
                half3 rgb = tex.rgb * input.color.rgb * _Color.rgb * alpha;
                half cover = saturate(max(tex.r, max(tex.g, tex.b)) * alpha * _Opacity);
                return half4(rgb * lerp(1, _Intensity, _Glow), lerp(alpha, cover, _Glow));
            }
            ENDHLSL
        }
    }
}
