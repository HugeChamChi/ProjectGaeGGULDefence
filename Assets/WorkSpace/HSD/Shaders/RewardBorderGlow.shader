Shader "UI/RewardBorderGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Space(10)]
        [Header(Border Settings)]
        _BorderWidth ("테두리 두께 (Border Width)", Range(0.0, 0.5)) = 0.05
        
        [Space(10)]
        [Header(Line Settings)]
        [HDR] _LineColor ("선 밝기/색상 (Line Color)", Color) = (1, 0.8, 0.2, 1)
        _LineSpeed ("이동 속도 (Speed)", Float) = 1.0
        
        [Toggle(USE_STRAIGHT_LINE)] _UseStraightLine ("꼬리 없는 일자선 사용 (Straight Line)", Float) = 0
        _LineWidth ("선 두께 (Line Width)", Range(0.001, 0.2)) = 0.05
        _LineTail ("꼬리 길이 (체크 해제시)", Range(0.1, 20.0)) = 5.0

        // --- UI Default Properties ---
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature USE_STRAIGHT_LINE
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            float _BorderWidth;
            
            fixed4 _LineColor;
            float _LineSpeed;
            float _LineTail;
            float _LineWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 기본 UI 텍스처 색상
                half4 mainColor = tex2D(_MainTex, IN.texcoord) * IN.color;

                // UV를 중앙이 (0,0)이 되도록 이동 (-0.5 ~ 0.5)
                float2 uv = IN.texcoord - 0.5;
                
                // 1. 테두리 마스크 생성 (사각형 기준)
                float distX = abs(uv.x);
                float distY = abs(uv.y);
                float maxDist = max(distX, distY);
                
                // 테두리 영역에만 값이 1.0이 되도록 설정
                float borderMask = step(0.5 - _BorderWidth, maxDist) * step(maxDist, 0.5);

                // 2. 이동하는 선 효과 계산
                // 각도를 계산하고 0~1 사이로 정규화
                float angle = atan2(uv.y, uv.x);
                float normalizedAngle = (angle / 3.14159265359) * 0.5 + 0.5;
                
                // ★ * 2.0 을 곱해주어 정확히 반대편에도 동일한 효과가 나오도록 생성!
                float sweep = frac(normalizedAngle * 2.0 + _Time.y * _LineSpeed);

                float lineEffect = 0;

                #if USE_STRAIGHT_LINE
                    // 끝이 각지지 않게 부드러운 일자선(Smooth Line)을 사용
                    float distFromCenter = min(sweep, 1.0 - sweep);
                    // 선의 중앙(0)부터 _LineWidth*0.5 까지는 진하고, 그 이후 _LineWidth 까지 부드럽게 사라집니다.
                    lineEffect = smoothstep(_LineWidth, _LineWidth * 0.5, distFromCenter);
                #else
                    // 자연스럽게 사라지는 꼬리 효과 사용 (값 조절 가능)
                    lineEffect = pow(sweep, _LineTail);
                #endif

                // 3. 마스크와 색상 조합
                float finalAlpha = lineEffect * borderMask;
                
                // 선에 HDR 밝기나 색상 적용
                fixed4 effectColor = _LineColor;
                effectColor.a *= finalAlpha;

                // 기존 이미지 위에 이펙트를 자연스럽게 알파 블렌딩
                fixed4 result;
                result.rgb = lerp(mainColor.rgb, effectColor.rgb, effectColor.a);
                result.a = max(mainColor.a, effectColor.a);
                
                return result;
            }
            ENDCG
        }
    }
}
