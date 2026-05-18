Shader "GaeGGUL/UI/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OuterWidth ("Outer Width", Range(0, 10)) = 1
        _InnerWidth ("Inner Width", Range(0, 10)) = 0
        
        [Header(Animation)]
        _AnimSpeed ("Animation Speed", Float) = 0
        _AnimIntensity ("Animation Intensity", Range(0, 1)) = 0.5

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OuterWidth;
            float _InnerWidth;
            float _AnimSpeed;
            float _AnimIntensity;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 mainColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                
                float2 texelSize = _MainTex_TexelSize.xy;
                
                // 애니메이션 펄스 계산
                float pulse = 1.0;
                if (_AnimSpeed > 0)
                {
                    pulse = lerp(1.0 - _AnimIntensity, 1.0, (sin(_Time.y * _AnimSpeed) + 1.0) * 0.5);
                }

                float outer = _OuterWidth * pulse;
                float inner = _InnerWidth * pulse;
                
                // 1. 바깥쪽 외곽선 감지 (Dilation: 주변에 알파가 있는가?)
                float maxAlpha = mainColor.a;
                maxAlpha = max(maxAlpha, tex2D(_MainTex, uv + float2(outer, 0) * texelSize).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, uv + float2(-outer, 0) * texelSize).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, uv + float2(0, outer) * texelSize).a);
                maxAlpha = max(maxAlpha, tex2D(_MainTex, uv + float2(0, -outer) * texelSize).a);

                // 2. 안쪽 외곽선 감지 (Erosion: 주변에 빈 공간이 있는가?)
                float minAlpha = mainColor.a;
                if (inner > 0)
                {
                    minAlpha = min(minAlpha, tex2D(_MainTex, uv + float2(inner, 0) * texelSize).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, uv + float2(-inner, 0) * texelSize).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, uv + float2(0, inner) * texelSize).a);
                    minAlpha = min(minAlpha, tex2D(_MainTex, uv + float2(0, -inner) * texelSize).a);
                }

                // 외곽선 영역 계산
                // 바깥쪽: 원본에는 없지만 확장된 영역에 있음
                // 안쪽: 원본에는 있지만 축소된 영역에는 없음
                float outerFactor = saturate(maxAlpha - mainColor.a);
                float innerFactor = saturate(mainColor.a - minAlpha);
                float outlineFactor = saturate(outerFactor + innerFactor);

                // 최종 색상 블렌딩
                fixed4 finalColor = lerp(mainColor, _OutlineColor, outlineFactor);
                
                // 알파 결정: 원본 알파 또는 외곽선 알파 중 큰 값 사용
                finalColor.a = max(mainColor.a, outlineFactor * _OutlineColor.a) * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
