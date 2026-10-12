// 튜토리얼 어둡게 처리 — 화면 전체를 덮는 한 장의 사각형에 둥근 구멍 여러 개를 뚫는다.
//   UV0에 오버레이 로컬 좌표를 넣고, 구멍(_Holes: xMin,yMin,xMax,yMax)까지의 둥근 사각형 거리로 투명도를 정한다.
//   구멍 바깥 _Feather 거리만큼 부드럽게 어두워져 대상이 네모 상자처럼 튀지 않는다. 구멍이 겹쳐도 두 번 어두워지지 않는다.
//   구멍 계산에서 먼 좌표(손가락 원형 표시)는 정점 색 그대로 그린다.
Shader "USW/UI/TutorialSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Float) = 24
        _Feather ("Feather", Float) = 20

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #include "UnityCG.cginc"

            #define MAX_HOLES 8

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 local : TEXCOORD0; };

            fixed4 _Color;
            float _Radius;
            float _Feather;
            float4 _Holes[MAX_HOLES];
            int _HoleCount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.local = v.uv;
                return o;
            }

            // 둥근 사각형 바깥까지의 거리(안쪽은 음수).
            float RoundedRectDistance(float2 p, float4 hole)
            {
                float2 center = (hole.xy + hole.zw) * 0.5;
                float2 half = max((hole.zw - hole.xy) * 0.5, 0.0001);
                float radius = min(_Radius, min(half.x, half.y));
                float2 q = abs(p - center) - half + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = i.color;
                float nearest = 1e6;
                for (int h = 0; h < MAX_HOLES; h++)
                {
                    if (h >= _HoleCount) break;
                    nearest = min(nearest, RoundedRectDistance(i.local, _Holes[h]));
                }
                color.a *= smoothstep(0.0, max(_Feather, 0.001), nearest);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
