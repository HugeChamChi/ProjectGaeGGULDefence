Shader "GGD/UI/UnitSkillGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _FillColor ("Active Fill", Color) = (0.435,0.847,0.298,1)
        _TrackColor ("Empty Track", Color) = (0.161,0.227,0.188,1)
        _HighlightColor ("Top Highlight", Color) = (0.769,0.965,0.518,1)
        _HighlightHeight ("Highlight Height", Range(0,1)) = 0.3
        _PassiveColor ("Passive Bar", Color) = (0.30,0.33,0.34,1)
        _PassiveMarkColor ("Passive Dash", Color) = (0.60,0.63,0.63,1)
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor, _TrackColor, _HighlightColor, _PassiveColor, _PassiveMarkColor;
                half _HighlightHeight;
                float4 _ClipRect;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 state:TEXCOORD1; float2 border:TEXCOORD2; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 state:TEXCOORD1; float2 border:TEXCOORD2; float2 localPosition:TEXCOORD3; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.localPosition = v.positionOS.xy;
                o.color=v.color; o.uv=v.uv; o.state=v.state; o.border=v.border;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 result = i.color;
                // UV1.y: 0 = badge geometry; 1 = cooldown; 2 = passive. No material instances.
                if (i.state.y > 0.5)
                {
                    float2 innerUV = (i.uv-i.border) / max(1.0-2.0*i.border,0.001);
                    half inside = step(0,innerUV.x)*step(innerUV.x,1)*step(0,innerUV.y)*step(innerUV.y,1);
                    half filled = step(innerUV.x,saturate(i.state.x))*step(0.00001,i.state.x);
                    half shine = step(1.0-_HighlightHeight,innerUV.y);
                    half4 active = lerp(_TrackColor,lerp(_FillColor,_HighlightColor,shine),filled);
                    // A static dash distinguishes passive from an active skill that is currently empty.
                    half dash = step(abs(innerUV.x-.5),.08)*step(abs(innerUV.y-.5),.13);
                    half4 passive = lerp(_PassiveColor,_PassiveMarkColor,dash);
                    half4 surface = lerp(active,passive,step(1.5,i.state.y));
                    surface.a *= i.color.a;
                    result = lerp(i.color,surface,inside);
                }
                #ifdef UNITY_UI_CLIP_RECT
                float2 clipInside = step(_ClipRect.xy,i.localPosition)*step(i.localPosition,_ClipRect.zw);
                result.a *= clipInside.x*clipInside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-0.001);
                #endif
                return result;
            }
            ENDHLSL
        }
    }
}
