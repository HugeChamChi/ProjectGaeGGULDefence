Shader "GaeGGUL/Totem Range Stripes"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        [Toggle] _UseSpriteAlpha ("Use Sprite Alpha (Off For Invisible Cell Texture)", Float) = 0
        _FillColor ("Fill Color", Color) = (1, 0.45, 0.05, 0.12)
        _StripeColor ("Stripe Color", Color) = (1, 0.55, 0.05, 0.7)
        _StripeSpacing ("Stripe Spacing (World Units)", Float) = 0.3
        _StripeWidth ("Stripe Width", Range(0.02, 0.98)) = 0.4
        _StripeAngle ("Stripe Angle", Range(-180, 180)) = -45
        _ScrollSpeed ("Scroll Speed (Cycles / Second)", Float) = 0.5
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                half4 _StripeColor;
                half4 _Color;
                float _StripeSpacing;
                float _StripeWidth;
                float _StripeAngle;
                float _ScrollSpeed;
                float _UseSpriteAlpha;
            CBUFFER_END
            float _TotemRangeUnscaledTime;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(position);
                o.world = TransformObjectToWorld(position).xy;
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float angle = radians(_StripeAngle);
                float phase = dot(input.world, float2(cos(angle), sin(angle))) / max(_StripeSpacing, 0.001) - _TotemRangeUnscaledTime * _ScrollSpeed;
                float distance = abs(frac(phase) - 0.5);
                float aa = max(fwidth(phase), 0.001);
                float stripe = 1 - smoothstep(_StripeWidth * 0.5 - aa, _StripeWidth * 0.5 + aa, distance);
                half4 color = lerp(_FillColor, _StripeColor, stripe) * input.color;
                color.a *= lerp(1.0, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a, _UseSpriteAlpha);
                return color;
            }
            ENDHLSL
        }
    }
}
