// 용어 팝업(TermPopup) 배경 스냅숏 블러 — 팝업이 열릴 때 한 번만 찍은 화면을 Kawase 방식으로 흐린다.
//   Graphics.Blit 전용(카메라 렌더 밖, RT→RT). 패스마다 _Offset을 0,1,2,…로 올려 대각 4점을 평균한다.
//   1/4 해상도에서 4패스면 충분히 뭉개지고, 비용은 팝업을 열 때 1회뿐이다 (매 프레임 블러 아님).
//   Blit 호환성을 위해 UnityCG의 vert_img를 쓴다 (URP 프로젝트에서도 RT→RT Blit에 동작).
Shader "Hidden/USW/UI/SnapshotBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Offset ("Offset", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * (_Offset + 0.5);
                fixed4 c = tex2D(_MainTex, i.uv + float2( o.x,  o.y));
                c += tex2D(_MainTex, i.uv + float2(-o.x,  o.y));
                c += tex2D(_MainTex, i.uv + float2( o.x, -o.y));
                c += tex2D(_MainTex, i.uv + float2(-o.x, -o.y));
                c *= 0.25;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
