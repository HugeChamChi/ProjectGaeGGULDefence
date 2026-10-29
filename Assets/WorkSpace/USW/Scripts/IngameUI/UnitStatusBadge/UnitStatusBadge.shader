Shader "GGD/UI/UnitStatusBadge"
{
    // Every color, size and timing comes from UnitStatusBadgeSettings through vertex data or globals
    // set by UnitStatusBadgeOverlay; the material carries only the standard UI stencil state.
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ClipRect;
            CBUFFER_END
            // Globals from UnitStatusBadgeOverlay.
            float _UnitStatusTime;          // unscaled seconds
            float4 _UnitStatusTiming;       // x blink seconds, y full pulse period, z full pulse strength, w glint period
            half4 _UnitStatusOutline;
            half4 _UnitStatusFill;
            half4 _UnitStatusFull;
            half4 _UnitStatusTrack;
            half4 _UnitStatusPassiveTrack;
            half4 _UnitStatusPassiveStripe;
            half4 _UnitStatusGlint;
            float4 _UnitStatusEffect;       // x effect cycle seconds, y sweep seconds

            // UV0: xy shape space (tag half-size or bar half-height = 1), z kind (1 tag, 2 bar), w bar aspect / tag glint (1 + phase seed, 0 = off)
            // UV1: x charge, y state (0 charging, 1 passive, 2 full), z blink start, w tag rim width / bar segments
            // UV2: x outline width, y tag charge mode (0 none, 1 border, 2 fill), z tag symbol (1-4 number, 5 star, 6 outlined star),
            //      w border width or fill strength (tag) / segment gap half-width (bar)
            // COLOR tag plate, UV3 rgb tag number + rim, UV3.w tag effect (0 none, 1 sweep, 2 sparkle, 3 shimmer, 4 border run) + phase seed
            // (normal/tangent are transformed by the canvas, so no colors there)
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float2 localPosition : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.localPosition = v.positionOS.xy;
                o.color = v.color;
                o.uv0 = v.uv0; o.uv1 = v.uv1; o.uv2 = v.uv2; o.uv3 = v.uv3;
                return o;
            }

            float Inside(float d, float aa) { return 1.0 - smoothstep(-aa, aa, d); }

            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Rounded stroke digits 1-4 in a box of about +-0.42 x +-0.62.
            float sdDigit(float2 p, int n)
            {
                float d = 1e3;
                if (n == 1)
                {
                    d = min(d, sdSegment(p, float2(0.06, -0.6), float2(0.06, 0.6)));
                    d = min(d, sdSegment(p, float2(0.06, 0.6), float2(-0.26, 0.36)));
                }
                else if (n == 2)
                {
                    d = min(d, sdSegment(p, float2(-0.34, 0.36), float2(-0.16, 0.58)));
                    d = min(d, sdSegment(p, float2(-0.16, 0.58), float2(0.16, 0.58)));
                    d = min(d, sdSegment(p, float2(0.16, 0.58), float2(0.34, 0.4)));
                    d = min(d, sdSegment(p, float2(0.34, 0.4), float2(0.34, 0.2)));
                    d = min(d, sdSegment(p, float2(0.34, 0.2), float2(-0.36, -0.6)));
                    d = min(d, sdSegment(p, float2(-0.36, -0.6), float2(0.38, -0.6)));
                }
                else if (n == 3)
                {
                    d = min(d, sdSegment(p, float2(-0.34, 0.6), float2(0.32, 0.6)));
                    d = min(d, sdSegment(p, float2(0.32, 0.6), float2(-0.02, 0.1)));
                    d = min(d, sdSegment(p, float2(-0.02, 0.1), float2(0.16, 0.08)));
                    d = min(d, sdSegment(p, float2(0.16, 0.08), float2(0.36, -0.12)));
                    d = min(d, sdSegment(p, float2(0.36, -0.12), float2(0.36, -0.38)));
                    d = min(d, sdSegment(p, float2(0.36, -0.38), float2(0.14, -0.6)));
                    d = min(d, sdSegment(p, float2(0.14, -0.6), float2(-0.36, -0.6)));
                }
                else
                {
                    d = min(d, sdSegment(p, float2(0.18, -0.6), float2(0.18, 0.6)));
                    d = min(d, sdSegment(p, float2(0.18, 0.6), float2(-0.4, -0.22)));
                    d = min(d, sdSegment(p, float2(-0.4, -0.22), float2(0.4, -0.22)));
                }
                return d - 0.13;
            }

            float sdStar5(float2 p, float r, float rf)
            {
                const float2 k1 = float2(0.809016994375, -0.587785252292);
                const float2 k2 = float2(-k1.x, k1.y);
                p.x = abs(p.x);
                p -= 2.0 * max(dot(k1, p), 0.0) * k1;
                p -= 2.0 * max(dot(k2, p), 0.0) * k2;
                p.x = abs(p.x);
                p.y -= r;
                float2 ba = rf * float2(-k1.y, k1.x) - float2(0, 1);
                float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
                return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
            }

            // Tag symbol: numbers 1-4, 5 filled star, 6 outlined star.
            float sdSymbol(float2 p, int n)
            {
                if (n == 5) return sdStar5(p + float2(0.0, 0.05), 0.78, 0.48) - 0.04;
                if (n == 6) return abs(sdStar5(p + float2(0.0, 0.05), 0.72, 0.48) - 0.02) - 0.08;
                return sdDigit(p, n);
            }

            // Four-point glint at c, twinkling once per period.
            float Glint(float2 p, float2 c, float size, float period, float seed, float aa)
            {
                float2 q = abs(p - c) / size;
                float shape = Inside(min(q.x * 6.0 + q.y, q.y * 6.0 + q.x) * 0.5 - 0.17, aa * 3.0 / size);
                float phase = frac(_UnitStatusTime / max(period, 0.01) + seed);
                return shape * pow(saturate(sin(phase * 6.2831853)), 6.0);
            }

            half3 Hue(float h) { return saturate(abs(frac(h + float3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0) - 1.0); }

            // Final-tier effects: returns the color after the effect and raises alpha where light leaves the tag.
            half3 TagEffect(int effect, float seed, float2 p, float d, float symbolMask, float aa, half3 col, inout float alpha)
            {
                float period = max(_UnitStatusEffect.x, 0.05);
                float body = Inside(d, aa);
                if (effect == 1)
                {
                    float ph = fmod(_UnitStatusTime + seed * period, period);
                    float pos = lerp(-2.0, 2.0, saturate(ph / max(_UnitStatusEffect.y, 0.01)));
                    float band = Inside(abs((p.x + p.y) * 0.70710678 - pos) - 0.14, 0.14) * step(ph, _UnitStatusEffect.y) * body;
                    col = lerp(col, half3(1, 1, 0.94), band * 0.75);
                }
                else if (effect == 2)
                {
                    float s = Glint(p, float2(-0.72, 0.62), 0.55, period * 0.55, seed, aa)
                            + Glint(p, float2(0.74, 0.40), 0.5, period * 0.55, seed + 0.27, aa)
                            + Glint(p, float2(-0.60, -0.70), 0.45, period * 0.55, seed + 0.55, aa)
                            + Glint(p, float2(0.70, -0.72), 0.55, period * 0.55, seed + 0.8, aa);
                    s = saturate(s);
                    col = lerp(col, _UnitStatusGlint.rgb, s);
                    alpha = max(alpha, s);
                }
                else if (effect == 3)
                {
                    half3 sheen = Hue(frac((p.x + p.y) * 0.3 - _UnitStatusTime / (period * 1.5) + seed));
                    // Kept faint so the star still reads as gold.
                    col = lerp(col, col * (0.75 + sheen * 0.5), 0.35 * symbolMask);
                }
                else if (effect == 4)
                {
                    float head = frac(_UnitStatusTime / (period * 0.8) + seed);
                    float a = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    float behind = frac(head - a);
                    float trail = saturate(1.0 - behind / 0.14) * Inside(abs(d + 0.07) - 0.11, aa);
                    col = lerp(col, half3(1, 0.97, 0.82), trail * 0.95);
                }
                return col;
            }

            // 1 at the blink start, fading to 0 over the blink time.
            float Blink(float start)
            {
                float since = _UnitStatusTime - start;
                return since >= 0.0 ? saturate(1.0 - since / max(_UnitStatusTiming.x, 0.001)) : 0.0;
            }

            // Charge color for the current state; passive shows a muted diagonal hatch.
            half3 ChargeColor(float state, float filled, float2 p, float stripeScale)
            {
                if (state > 0.5 && state < 1.5)
                    return lerp(_UnitStatusPassiveTrack.rgb, _UnitStatusPassiveStripe.rgb, step(0.5, frac((p.x - p.y) * stripeScale)));
                half3 fill = _UnitStatusFill.rgb;
                if (state > 1.5)
                {
                    float pulse = 0.5 + 0.5 * sin(_UnitStatusTime * 6.2831853 / max(_UnitStatusTiming.y, 0.01));
                    fill = lerp(_UnitStatusFull.rgb, half3(1, 1, 1), _UnitStatusTiming.z * pulse);
                }
                return lerp(_UnitStatusTrack.rgb, fill, filled);
            }

            half4 Tag(Varyings i, float2 p)
            {
                float d = sdRoundBox(p, float2(1.0, 1.0), 0.38);
                float aa = max(fwidth(d), 1e-4);
                float outW = i.uv2.x, rimW = i.uv1.w;
                float charge = i.uv1.x, state = i.uv1.y;
                int mode = (int)round(i.uv2.y);
                float alpha = Inside(d - outW, aa);
                float body = Inside(d, aa);
                half3 plate = i.color.rgb;
                half3 number = i.uv3.rgb;

                half3 inside = plate;
                if (mode == 2)
                {
                    // Fill: charge rises from the bottom behind the number, softened by the fill strength.
                    float level = charge <= 0.0001 ? -9.0 : lerp(-1.0, 1.0, charge);
                    float filled = state > 0.5 && state < 1.5 ? 1.0 : Inside(p.y - level, aa);
                    half3 c = ChargeColor(state, 1.0, p, 2.2);
                    inside = lerp(plate, lerp(plate, c, i.uv2.w), filled);
                }
                half3 col = lerp(_UnitStatusOutline.rgb, inside, body);
                if (rimW > 0.0) col = lerp(col, number, body - Inside(d + rimW, aa));
                if (mode == 1)
                {
                    // Border: inner edge band fills clockwise from 12 o'clock.
                    float band = body - Inside(d + i.uv2.w, aa);
                    float t = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    float filled = charge >= 0.9999 ? 1.0 : Inside(t - charge, 0.004) * step(0.0001, charge);
                    half3 c = state > 0.5 && state < 1.5
                        ? lerp(_UnitStatusPassiveTrack.rgb, _UnitStatusOutline.rgb, step(0.55, frac(t * 12.0)))
                        : ChargeColor(state, filled, p, 1.0);
                    col = lerp(col, c, band);
                }

                float dd = sdSymbol(p, (int)round(i.uv2.z));
                float daa = max(fwidth(dd), 1e-4);
                float symbolMask = Inside(dd, daa);
                if (mode == 2) col = lerp(col, _UnitStatusOutline.rgb, Inside(dd - 0.09, daa));
                col = lerp(col, number, symbolMask);
                int effect = (int)floor(i.uv3.w + 0.001);
                if (effect > 0) col = TagEffect(effect, frac(i.uv3.w), p, d, symbolMask, aa, col, alpha);
                if (mode > 0) col = lerp(col, half3(1, 1, 1), Blink(i.uv1.z) * 0.6 * body);
                if (i.uv0.w > 0.5)
                {
                    // Occasional corner glint; the seed staggers badges.
                    float g = Glint(p, float2(0.95, 0.95), 1.0, _UnitStatusTiming.w, frac(i.uv0.w), aa);
                    col = lerp(col, _UnitStatusGlint.rgb, g);
                    alpha = max(alpha, g);
                }
                return half4(col, alpha);
            }

            half4 Bar(Varyings i, float2 p)
            {
                float aspect = i.uv0.w, outW = i.uv2.x;
                float d = sdRoundBox(p, float2(aspect, 1.0), 0.75);
                float aa = max(fwidth(d), 1e-4);
                float body = Inside(d, aa);
                float alpha = Inside(d - outW, aa);
                float charge = i.uv1.x, state = i.uv1.y;
                float edge = charge * 2.0 * aspect - (p.x + aspect);
                float filled = charge >= 0.9999 ? 1.0 : Inside(-edge, aa) * step(0.0001, charge);
                half3 surface = ChargeColor(state, filled, p, 0.32);
                float segments = round(i.uv1.w);
                if (segments > 1.5)
                {
                    // Divisions: dark gaps at each internal boundary.
                    float x = (p.x + aspect) / (2.0 * aspect) * segments;
                    float k = round(x);
                    float gap = abs(x - k) / segments * 2.0 * aspect;
                    float cut = step(0.5, k) * step(k, segments - 0.5) * Inside(gap - i.uv2.w, aa);
                    surface = lerp(surface, _UnitStatusOutline.rgb, cut);
                }
                half3 col = lerp(_UnitStatusOutline.rgb, surface, body);
                col = lerp(col, half3(1, 1, 1), Blink(i.uv1.z) * 0.8 * body);
                return half4(col, alpha);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv0.xy;
                half4 result = round(i.uv0.z) < 1.5 ? Tag(i, p) : Bar(i, p);
                result.a *= i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                float2 clipInside = step(_ClipRect.xy, i.localPosition) * step(i.localPosition, _ClipRect.zw);
                result.a *= clipInside.x * clipInside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDHLSL
        }
    }
}
