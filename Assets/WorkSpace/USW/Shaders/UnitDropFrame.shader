Shader "GaeGGUL/Unit Drop Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FrameColor ("Frame Color", Color) = (0.2, 0.85, 1, 0.7)
        _CornerColor ("Corner Color", Color) = (0.65, 1, 1, 0.95)
        _LineWidth ("Line Width", Range(0.005, 0.08)) = 0.022
        _GlowWidth ("Glow Width", Range(0.01, 0.2)) = 0.07
        _Inset ("Frame Inset", Range(0.05, 0.25)) = 0.12
        _CornerLength ("Corner Length", Range(0.1, 0.6)) = 0.3
        _CornerTravel ("Corner Travel", Range(0, 0.12)) = 0.035
        _PulseSpeed ("Pulse Cycles Per Second", Range(0, 3)) = 0.8
        [HideInInspector] _CellRect ("Sprite Center And Half Size", Vector) = (0,0,0.5,0.5)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FrameColor;
                half4 _CornerColor;
                half4 _Color;
                float4 _CellRect;
                float _LineWidth, _GlowWidth, _Inset, _CornerLength, _CornerTravel, _PulseSpeed;
            CBUFFER_END
            float _UnitDropUnscaledTime;
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                o.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                o.local = (input.positionOS.xy - _CellRect.xy) / max(_CellRect.zw, 0.001);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = abs(input.local);
                float pulse = 0.5 + 0.5 * sin(_UnitDropUnscaledTime * _PulseSpeed * 6.2831853);
                float edge = 1 - _Inset;
                float aa = max(fwidth(max(p.x, p.y)), 0.001);
                float distance = abs(max(p.x, p.y) - edge);
                float frameLine = 1 - smoothstep(_LineWidth, _LineWidth + aa, distance);
                float glow = pow(saturate(1 - distance / max(_GlowWidth, 0.001)), 2) * 0.32;
                float frame = saturate(frameLine + glow) * lerp(0.55, 0.85, pulse);

                float cornerEdge = edge - pulse * _CornerTravel;
                float2 a = float2(cornerEdge - _CornerLength, cornerEdge);
                float2 b = float2(cornerEdge, cornerEdge);
                float2 nearest = float2(clamp(p.x, a.x, b.x), cornerEdge);
                float2 nearestVertical = float2(cornerEdge, clamp(p.y, a.x, b.x));
                float cornerDistance = min(length(p - nearest), length(p - nearestVertical));
                float corner = 1 - smoothstep(_LineWidth * 1.5, _LineWidth * 1.5 + aa, cornerDistance);
                float cornerGlow = pow(saturate(1 - cornerDistance / max(_GlowWidth, 0.001)), 2) * 0.4;
                float cornerAlpha = saturate(corner + cornerGlow) * _CornerColor.a;
                float frameAlpha = frame * _FrameColor.a;
                float alpha = max(frameAlpha, cornerAlpha);
                half3 rgb = lerp(_FrameColor.rgb, _CornerColor.rgb, cornerAlpha / max(frameAlpha + cornerAlpha, 0.001));
                return half4(rgb, alpha) * input.color;
            }
            ENDHLSL
        }
    }
}
