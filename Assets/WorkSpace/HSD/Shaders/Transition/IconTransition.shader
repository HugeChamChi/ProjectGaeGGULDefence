Shader "Custom/IconTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TransitionTex ("Transition Icon (Alpha)", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _GridSize ("Grid Size", Vector) = (20, 10, 0, 0)
        _IconMaxScale ("Icon Max Scale", Range(1, 5)) = 2.5
        _Color ("Transition Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _TransitionTex;
            float4 _MainTex_ST;
            float _Progress;
            float4 _GridSize;
            float _IconMaxScale;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 gridUV = i.uv * _GridSize.xy;
                float2 localUV = frac(gridUV);
                
                float mask = 0;
                float threshold = 0;

                if (_Progress < 0.5)
                {
                    // Phase 1: 채워지는 단계
                    float p = _Progress * 2.0; 
                    threshold = saturate(p * 1.8 - i.uv.x * 0.8);
                    
                    // 아이콘 스케일 계산 (Max Scale까지 확장)
                    float currentScale = threshold * _IconMaxScale;
                    
                    // 중앙 기준 UV 스케일링 (값이 커질수록 이미지가 커짐 = 샘플링 범위는 좁아짐)
                    float2 scaledUV = (localUV - 0.5) / max(currentScale, 0.0001) + 0.5;
                    
                    // 텍스처 경계 밖은 알파 0으로 처리 (Clamp와 유사)
                    if (all(scaledUV >= 0 && scaledUV <= 1))
                    {
                        mask = tex2D(_TransitionTex, scaledUV).a;
                    }
                }
                else
                {
                    // Phase 2: 사라지는 단계
                    float p = (_Progress - 0.5) * 2.0;
                    threshold = saturate(p * 1.8 - i.uv.x * 0.8);
                    
                    float currentScale = threshold * _IconMaxScale;
                    float2 scaledUV = (localUV - 0.5) / max(currentScale, 0.0001) + 0.5;
                    
                    if (all(scaledUV >= 0 && scaledUV <= 1))
                    {
                        mask = tex2D(_TransitionTex, scaledUV).a;
                    }
                    mask = 1.0 - mask;
                }

                fixed4 col = _Color;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }
}
