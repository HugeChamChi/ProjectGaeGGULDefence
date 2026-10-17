Shader "GaeGGUL/CasualTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Cover 0-0.5 / Reveal 0.5-1", Range(0,1)) = 0
        _Color ("Cover", Color) = (1,.88,.55,1)
        _Accent ("Icon color", Color) = (.56,.32,.13,1)
        _Clock ("Unscaled animation clock", Float) = 0
        _Aspect ("Viewport aspect", Float) = 1.77778
        _Preview ("Preview backdrop", Float) = 0
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
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            float _Progress, _Clock, _Aspect, _Preview;
            float4 _Color, _Accent;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*float2(_Aspect,1);
                float mask=CasualCircleMask(i.uv,_Aspect,_Progress);
                // Twice as many icons per axis, moving three times faster in screen space.
                float2 tile=abs(frac(p*14-float2(_Clock*.60,_Clock*.60))-.5);
                float hex=max(tile.x*.866025+tile.y*.5,tile.y);
                float aa=max(fwidth(hex),.004);
                float pattern=1-smoothstep(.105-aa,.105+aa,hex);
                float3 color=lerp(_Color.rgb,_Accent.rgb,pattern*.09);
                if(_Preview>.5)
                {
                    float stripe=fmod(floor(i.uv.x*12)+floor(i.uv.y*8),2);
                    float3 back=lerp(float3(.24,.38,.35),float3(.28,.43,.39),stripe);
                    back=lerp(back,float3(.65,.77,.53),1-smoothstep(.13,.15,length(p-float2(.22,-.10))));
                    return float4(lerp(back,color,mask),1);
                }
                return float4(color,mask);
            }
            ENDHLSL
        }
    }
}
