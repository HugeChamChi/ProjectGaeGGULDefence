Shader "UI/LiquidWaveFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)

        _Fill ("Fill Amount", Range(0, 1)) = 1
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.15)) = 0.02
        _WaveFrequency ("Wave Frequency", Range(0, 40)) = 6
        _WaveSpeed ("Wave Speed", Range(-10, 10)) = 2
        [HDR] _CrestColor ("Crest Highlight Color", Color) = (1,1,1,1)
        _CrestThickness ("Crest Thickness", Range(0, 0.05)) = 0.012

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [ToggleUnusedByRenderer] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float _Fill;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float4 _CrestColor;
            float _CrestThickness;

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
                half4 color = tex2D(_MainTex, input.uv) * input.color;

                // 목표 채움비(_Fill) 주변을 사인파로 흔들어 수면 경계선을 만든다.
                float waterLine = _Fill + sin(input.uv.x * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude;
                float dist = input.uv.y - waterLine;

                float underwater = step(dist, 0.0);
                color.a *= underwater;

                // 수면 경계에 얇은 하이라이트(포말) 선을 얹는다.
                float crest = 1.0 - smoothstep(0.0, max(_CrestThickness, 0.0001), abs(dist));
                color.rgb = lerp(color.rgb, _CrestColor.rgb, crest * _CrestColor.a * underwater);

                return color;
            }
            ENDHLSL
        }
    }
}
