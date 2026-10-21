Shader "GGD/UI/ChiefMonocleFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Button Texture", 2D) = "white" {}
        _SourceBorder ("Authored Blue Border", Color) = (0.3333333,0.4039216,0.8901961,1)
        _Gold ("Monocle Border", Color) = (1,0.78,0.27,1)
        _MatchInner ("Exact Border Tolerance (Linear)", Float) = 0.01
        _MatchOuter ("Border Edge Tolerance (Linear)", Float) = 0.08
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _SourceBorder, _Gold;
                half _MatchInner, _MatchOuter;
                float4 _ClipRect;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 localPosition:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.localPosition = v.positionOS.xy;
                o.color = v.color; o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 sampleColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Match this button artwork's blue rim before cooldown tint is applied.
                // The purple fill, dark shadow and separate portrait remain unchanged.
                half rim = 1 - smoothstep(_MatchInner, _MatchOuter, distance(sampleColor.rgb, _SourceBorder.rgb));
                half4 result = sampleColor * i.color;
                result.rgb = lerp(result.rgb, _Gold.rgb, rim);
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy,i.localPosition)*step(i.localPosition,_ClipRect.zw);
                result.a *= inside.x*inside.y;
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
