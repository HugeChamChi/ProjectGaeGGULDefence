Shader "Custom/CircleTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _GridSize ("Grid Size", Vector) = (20, 10, 0, 0)
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
            float4 _MainTex_ST;
            float _Progress;
            float4 _GridSize;
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
                float dist = distance(localUV, float2(0.5, 0.5));
                
                // Max distance from center to corner is sqrt(0.5^2 + 0.5^2) ≈ 0.707
                // Using 0.8 as a threshold to ensure full coverage
                float maxRadius = 0.8;
                float mask = 0;
                
                // Using a 'wave' effect from left to right
                // The multiplier 1.8 ensures that when progress is 1, the wave has passed the entire screen (0 to 1)
                if (_Progress < 0.5)
                {
                    // Phase 1: Circles fill from left to right (0.0 to 0.5)
                    float progress = _Progress * 2.0; 
                    float radius = saturate(progress * 1.8 - i.uv.x * 0.8);
                    mask = step(dist, radius * maxRadius);
                }
                else
                {
                    // Phase 2: Circles disappear from left to right (0.5 to 1.0)
                    float progress = (_Progress - 0.5) * 2.0;
                    float radius = saturate(progress * 1.8 - i.uv.x * 0.8);
                    // Invert the mask to make them "disappear"
                    mask = 1.0 - step(dist, radius * maxRadius);
                }

                fixed4 col = _Color;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }
}
