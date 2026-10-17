Shader "GaeGGUL/UI/TransitionUnitSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Silhouette", Color) = (.24,.17,.12,1)
        _Progress ("Transition", Float) = .5
        _Aspect ("Aspect", Float) = .5625
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CasualCircleWipe.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 screen:TEXCOORD1; float4 color:COLOR; };
            sampler2D _MainTex;
            float4 _Color;
            float _Progress, _Aspect;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.screen=ComputeScreenPos(o.vertex); o.color=v.color; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.screen.xy/i.screen.w;
                float a=tex2D(_MainTex,i.uv).a*i.color.a;
                a*=CasualCircleMask(uv,_Aspect,_Progress);
                return float4(_Color.rgb*i.color.rgb,a*_Color.a);
            }
            ENDHLSL
        }
    }
}
