Shader "GaeGGUL/Deertree/AppleTouchHighlight"
{
    // Touch hint for Deertree touch targets (apples and curse bindings).
    // Keeps the authored colours: only a soft brightness lift, a thin glint and a tap flash.
    Properties
    {
        [NoScaleOffset] _MainTex ("Spine Atlas", 2D) = "white" {}
        _Highlight ("Glint Colour", Color) = (1,.96,.86,1)
        _Strength ("Breathing Strength", Range(0,1)) = .18
        _Clock ("Combat Clock", Float) = 0
        _Phase ("Target Phase", Float) = 0
        _Hit ("Touch Response", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Highlight;
            float _Strength, _Clock, _Phase, _Hit;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float2 world:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }
            float4 frag(Output i):SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                // Black ink outlines stay black; feedback only touches the painted fill.
                float fill = smoothstep(.06, .22, dot(tex.rgb, float3(.299, .587, .114)));
                float breathing = .5 + .5 * sin(_Clock * 2.4 + _Phase);
                // Multiplicative lift keeps the original hue instead of tinting it.
                float lift = 1 + _Strength * .6 * breathing + _Hit * .35;
                // A narrow diagonal glint passes every ~2.6 s.
                float band = frac((i.world.x * .55 + i.world.y * .35) * .5 - _Clock * .38 + _Phase * .13);
                float glint = smoothstep(.0, .04, band) * (1 - smoothstep(.04, .09, band));
                float3 color = tex.rgb * lift + _Highlight.rgb * fill * (glint * .45 * _Strength * 4 + _Hit * .25);
                // The original alpha is the sole coverage mask: no circle or exterior badge.
                return float4(saturate(color) * tex.a * i.color.rgb, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
