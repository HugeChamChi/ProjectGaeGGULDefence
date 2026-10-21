// 보스 HP바 보호막 띠 (실험실 시안 2026-10-22).
// HP 채움과 같은 알약 그림의 알파로 모양을 자르고, 그 안에 에너지 무늬·경계 발광·광택 띠를 그린다.
// _Style 0 유리(광택 띠가 지나감) / 1 육각 에너지막 / 2 사선 배리어. _Dissolve로 무늬 칸 단위로 깨져 사라진다.
// _BandU(띠 시작·끝 U)와 _Aspect(그림 폭/높이)는 UI_BossHpShield가 매 프레임 넣는다.
Shader "GGD/UI/BossShieldBand"
{
    Properties
    {
        [PerRendererData] _MainTex ("Bar Shape", 2D) = "white" {}
        _GlowColor ("Glow", Color) = (0.65,0.97,1,1)
        _Style ("Style 0 Glass 1 Hex 2 Stripe", Float) = 1
        _FillAlpha ("Fill Alpha", Range(0,1)) = 0.45
        _PatternScale ("Pattern Cells Per Bar Height", Float) = 3.2
        _PatternLine ("Pattern Line Width", Range(0.005,0.3)) = 0.06
        _PatternStrength ("Pattern Strength", Range(0,2)) = 0.7
        _Scroll ("Pattern Scroll (xy per sec)", Vector) = (0.25,0,0,0)
        _RimStrength ("Band Edge Rim", Range(0,3)) = 1.2
        _RimSharpness ("Band Edge Sharpness", Float) = 9
        _Gloss ("Top Gloss", Range(0,2)) = 0.5
        _GlossY ("Gloss Center/Width (UV y)", Vector) = (0.66,0.06,0,0)
        _Sweep ("Sweep Strength", Range(0,3)) = 0
        _SweepPeriod ("Sweep Period", Float) = 1.8
        _Shimmer ("Cell Shimmer", Range(0,1)) = 0.35
        _BandU ("Band U (start,end)", Vector) = (0,1,0,0)
        _Aspect ("Art Width / Height", Float) = 8.5
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _Style, _FillAlpha, _PatternScale, _PatternLine, _PatternStrength;
                float4 _Scroll;
                float _RimStrength, _RimSharpness, _Gloss;
                float4 _GlossY;
                float _Sweep, _SweepPeriod, _Shimmer;
                float4 _BandU;
                float _Aspect, _Dissolve;
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

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 뾰족 위 육각 격자: xy = 칸 중심 기준 좌표, zw = 칸 번호.
            float4 HexCoords(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 c = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(p - c.xy * s, p - (c.zw + 0.5) * s);
                return dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, c.xy) : float4(h.zw, c.zw + 9.43);
            }
            float HexEdge(float2 h)
            {
                h = abs(h);
                return 0.5 - max(dot(h, float2(0.5, 0.8660254)), h.x);
            }

            half4 frag(Varyings i):SV_Target
            {
                half shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float t = _Time.y;
                // 바 높이 단위 좌표 (가로가 늘어나도 무늬 비율 유지)
                float2 bar = float2(i.uv.x * _Aspect, i.uv.y);
                float2 p = bar * _PatternScale + _Scroll.xy * t;

                float pattern = 0;
                float2 cell;
                if (_Style < 0.5)
                {
                    // 유리: 사선으로 기운 큰 조각 (깨질 때만 보인다)
                    float2 q = float2(p.x + p.y * 0.6, p.y) * float2(0.9, 1.4);
                    cell = floor(q);
                }
                else if (_Style < 1.5)
                {
                    float4 h = HexCoords(p);
                    pattern = 1 - smoothstep(0.0, _PatternLine, HexEdge(h.xy));
                    cell = h.zw;
                }
                else
                {
                    float d = frac((p.x + p.y) * 0.5);
                    // 사선 띠: 폭 _PatternLine 만큼 부드러운 밝은 줄이 대각선으로 흐른다
                    pattern = smoothstep(0.5 - _PatternLine, 0.5 + _PatternLine, abs(d - 0.5) * 2);
                    cell = floor(float2((p.x + p.y) * 0.5, p.y * 2));
                }
                float rnd = Hash(cell);
                // 칸마다 천천히 숨쉬는 밝기
                float shimmer = _Shimmer * (0.5 + 0.5 * sin(t * 2.3 + rnd * 6.2831));

                // 띠 경계(시작·끝)에서 안쪽으로 빛이 번진다
                float dl = max(0, (i.uv.x - _BandU.x) * _Aspect);
                float dr = max(0, (_BandU.y - i.uv.x) * _Aspect);
                float rim = exp(-dl * _RimSharpness) + exp(-dr * _RimSharpness);

                float gloss = exp(-pow((i.uv.y - _GlossY.x) / max(0.001, _GlossY.y), 2));

                float sweep = 0;
                if (_Sweep > 0)
                {
                    float span = max(0.0001, _BandU.y - _BandU.x);
                    float head = _BandU.x - 0.15 * span + frac(t / max(0.1, _SweepPeriod)) * span * 1.6;
                    float d = (i.uv.x - head) * _Aspect + (i.uv.y - 0.5) * 0.8;
                    sweep = exp(-d * d * 18);
                }

                float glow = pattern * _PatternStrength * (0.65 + shimmer) + rim * _RimStrength + gloss * _Gloss + sweep * _Sweep;

                // 깨짐: 칸 순서가 무작위로 사라지고, 사라지기 직전 칸은 하얗게 달아오른다
                float alive = step(_Dissolve, rnd * 0.999);
                float hot = (1 - smoothstep(0, 0.18, rnd - _Dissolve)) * step(0.001, _Dissolve);

                half4 col;
                col.rgb = i.color.rgb * (0.7 + 0.3 * shimmer) + _GlowColor.rgb * glow + hot * 0.8;
                col.a = shape * i.color.a * saturate(_FillAlpha + glow * 0.6 + hot) * alive;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, i.localPosition) * step(i.localPosition, _ClipRect.zw);
                col.a *= inside.x * inside.y;
                #endif
                return col;
            }
            ENDHLSL
        }
    }
}
