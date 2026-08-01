Shader "UI/WaveGlowTexture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Space(10)]
        [Header(Glow Color)]
        [HDR] _GlowColor ("발광 색상 (Glow Color)", Color) = (0.3, 0.9, 1.0, 1)
        _GlowIntensity ("발광 강도 (Glow Intensity)", Range(0, 1)) = 0.9

        [Space(10)]
        [Header(Wave Texture)]
        _WaveTex ("물결 텍스처 (Wave Texture, Alpha=Mask)", 2D) = "white" {}
        _WaveScrollSpeed ("스크롤 속도 (Scroll Speed)", Float) = 0.5

        [Space(10)]
        [Toggle] _WaveTexOnly ("물결 텍스처만 보기 (Wave Texture Only)", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

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

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
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
                float4 clipRect : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _GlowColor;
            float _GlowIntensity;

            sampler2D _WaveTex;
            float4 _WaveTex_ST;
            float _WaveScrollSpeed;
            float _WaveTexOnly;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);

                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.clipRect = _ClipRect;

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. 원본 텍스처 컬러 (Tint 포함, 실루엣 알파 포함)
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // 2. 물결 텍스처를 좌측으로 연속 스크롤하며 샘플링 (알파 채널을 마스크로 사용)
                float2 waveUV = TRANSFORM_TEX(i.texcoord, _WaveTex) + float2(_Time.y * _WaveScrollSpeed, 0);
                half waveMaskRaw = tex2D(_WaveTex, waveUV).a;

                // 3. 스프라이트 실루엣 내부로 제한 후 지정한 색으로 코팅
                half waveMask = waveMaskRaw * color.a;
                color.rgb = lerp(color.rgb, _GlowColor.rgb, waveMask * _GlowIntensity);

                // Wave Texture Only 켜면 합성 없이 물결 텍스처(마스크) 자체만 출력
                half4 waveOnlyColor = half4(_GlowColor.rgb, waveMaskRaw);
                color = lerp(color, waveOnlyColor, _WaveTexOnly);

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, i.clipRect);
                #endif

                return color;
            }
        ENDHLSL
        }
    }
}
