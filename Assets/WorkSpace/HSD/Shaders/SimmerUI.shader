Shader "UI/Simmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _CycleTime ("Cycle Time", Float) = 3.0
        _Rotation ("Rotation", Float) = -35.0
        _WaveSpeed ("Wave Speed", Float) = -4.0
        _WaveScale ("Wave Width", Range(0.01, 1)) = 0.2
        _SimmerIntensity ("Simmer Intensity", Range(0, 1)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma shader_feature_local_fragment UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _CycleTime;
            float _Rotation;
            float _WaveSpeed;
            float _WaveScale;
            float _SimmerIntensity;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);

                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                o.color = v.color * _Color;
                return o;
            }

            float2 rotate(float2 uv, float rotation)
            {
                float rad = radians(rotation);
                float s = sin(rad);
                float c = cos(rad);
                float2x2 rotMat = float2x2(c, -s, s, c);
                return mul(rotMat, uv - 0.5) + 0.5;
            }

            float triangle_wave(float x)
            {
                return 2.0 * abs(frac(x + 0.5) - 0.5);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. 원본 텍스처 컬러 (Tint 포함)
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // 2. Simmer 라인 계산
                float t = _Time.y % _CycleTime;
                float2 rotatedUV = rotate(i.texcoord, _Rotation);
                
                // Based on graph: Saturate(TriangleWave(Saturate(rotatedUV.r + 1.4 + t * WaveSpeed)) + WaveScale) * Intensity
                float simmerInput = rotatedUV.x + 1.4 + t * _WaveSpeed;
                
                // Triangle Wave 기반으로 라인 생성
                float wave = triangle_wave(saturate(simmerInput));
                
                // Thresholding: WaveScale을 문턱값으로 사용하여 라인 이외의 영역은 0이 되도록 처리
                float simmerLine = saturate((wave - (1.0 - _WaveScale)) / max(0.01, _WaveScale));

                // 3. Simmer 효과를 RGB에 더함
                // _SimmerIntensity를 Simmer 라인의 Alpha(투명도)처럼 취급하여 곱함
                // 원본 color.a(텍스처 알파)를 곱해 이미지 영역 내로 제한
                float3 shimmerEffect = simmerLine * _SimmerIntensity * color.a;
                color.rgb += shimmerEffect;

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDHLSL
        }
    }
}
