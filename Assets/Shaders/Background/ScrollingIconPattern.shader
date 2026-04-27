Shader "Custom/ScrollingIconPattern"
{
    Properties
    {
        _IconTex ("Icon Texture", 2D) = "white" {}
        _IconColor ("Icon Tint", Color) = (1,1,1,1)
        
        _BgTex ("Background Texture", 2D) = "white" {}
        _BgColor ("Background Tint", Color) = (0.5, 0.5, 0.5, 1)

        _IconSize ("Icon Size (Pixels)", Float) = 50
        _IconTiling ("Icon Tiling (X, Y)", Vector) = (10, 10, 0, 0)
        _IconGap ("Icon Gap (X, Y)", Vector) = (20, 20, 0, 0)
        _IconRotation ("Icon Rotation (Deg)", Float) = 0
        _ScrollSpeed ("Scroll Speed (X, Y)", Vector) = (50, 0, 0, 0)
        
        _TargetAspect ("Target Aspect Ratio (e.g. 1.77)", Float) = 1.777
        [Toggle] _EnableStagger ("Enable Diagonal Layout", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _IconTex;
            sampler2D _BgTex;
            fixed4 _IconColor;
            fixed4 _BgColor;
            
            float _IconSize;
            float4 _IconTiling;
            float4 _IconGap;
            float _IconRotation;
            float4 _ScrollSpeed;
            float _TargetAspect;
            float _EnableStagger;

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. 타일링 기반 좌표 계산
                float2 pixelPos = i.uv * _IconTiling.xy * (_IconSize + _IconGap.xy);

                // 2. 배경 그리기
                fixed4 finalCol = tex2D(_BgTex, i.uv) * _BgColor;

                // 3. 아이콘 레이어 계산
                float speed = length(_ScrollSpeed.xy);
                float2 direction = normalize(float2(1, 1)); // 45도 대각선 방향으로 고정
                float2 scrolledPos = pixelPos + _Time.y * (direction * speed);
                float2 cellSize = _IconSize + _IconGap.xy;

                // 엇갈린 배치 (Diagonal Layout) 적용
                if (_EnableStagger > 0) {
                    float rowID = floor(scrolledPos.y / cellSize.y);
                    scrolledPos.x += frac(rowID * 0.5) > 0.1 ? cellSize.x * 0.5 : 0.0;
                }
                
                float2 localUV = frac(scrolledPos / cellSize); 
                float2 iconThreshold = _IconSize / cellSize;

                // 4. 아이콘 영역 판별
                if (localUV.x < iconThreshold.x && localUV.y < iconThreshold.y) {
                    float2 iconTexUV = localUV / iconThreshold;

                    // 아이콘 회전 적용
                    float rad = radians(_IconRotation);
                    float sinX = sin(rad);
                    float cosX = cos(rad);
                    float2x2 rotMat = float2x2(cosX, -sinX, sinX, cosX);
                    
                    // Center 기준으로 회전
                    iconTexUV = mul(rotMat, iconTexUV - 0.5) + 0.5;

                    if (iconTexUV.x >= 0 && iconTexUV.x <= 1 && iconTexUV.y >= 0 && iconTexUV.y <= 1) {
                        fixed4 iconCol = tex2D(_IconTex, iconTexUV) * _IconColor * i.color;
                        finalCol.rgb = lerp(finalCol.rgb, iconCol.rgb, iconCol.a);
                        finalCol.a = max(finalCol.a, iconCol.a);
                    }
                }

                return finalCol;
            }
            ENDHLSL
        }
    }
}
