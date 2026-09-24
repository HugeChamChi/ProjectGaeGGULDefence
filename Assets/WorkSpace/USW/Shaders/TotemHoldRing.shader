Shader "GaeGGUL/Totem Hold Ring"
{
    // 토템을 누르고 있을 때 회전→이동 전환까지 남은 시간을 보여주는 원형 게이지.
    // 쿼드 UV(0~1) 중심 기준 극좌표로 그린다. 12시 방향에서 시계 방향으로 차오른다.
    // 동적 값(_Progress/_Armed/_Visibility/_Flash/_HoldRingTime)은 TotemHoldFeedback이 PropertyBlock으로 넣는다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Header(Shape)]
        _Radius ("Ring Radius (UV)", Range(0.2, 0.9)) = 0.68
        _Thickness ("Ring Thickness (UV)", Range(0.01, 0.3)) = 0.075
        [Header(Colors)]
        _TrackColor ("Track (Empty)", Color) = (1, 1, 1, 0.22)
        _FillColorStart ("Fill Start", Color) = (0.45, 0.85, 1, 0.9)
        _FillColorEnd ("Fill End", Color) = (0.75, 0.55, 1, 0.95)
        _GlassColor ("Inner Glass", Color) = (1, 1, 1, 0.07)
        _GlowColor ("Glow", Color) = (0.6, 0.85, 1, 0.55)
        _ArmedColor ("Armed (Full)", Color) = (1, 0.9, 0.45, 1)
        [Header(Orbit Dots When Full)]
        _DotCount ("Dot Count", Range(1, 8)) = 3
        _DotSpeed ("Dot Speed (Turns / Second)", Float) = 1.3
        _DotSize ("Dot Size (UV)", Range(0.005, 0.08)) = 0.042
        _DotOrbitOffset ("Dot Orbit Offset (UV)", Range(-0.1, 0.2)) = 0.075
        _TrailLength ("Trail Length (Turns)", Range(0, 0.5)) = 0.26
        [HideInInspector] _Progress ("Progress", Range(0, 1)) = 0
        [HideInInspector] _Armed ("Armed", Range(0, 1)) = 0
        [HideInInspector] _Visibility ("Visibility", Range(0, 1)) = 1
        [HideInInspector] _Flash ("Flash", Range(0, 1)) = 0
        [HideInInspector] _HoldRingTime ("Unscaled Time", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
                half4 _TrackColor;
                half4 _FillColorStart;
                half4 _FillColorEnd;
                half4 _GlassColor;
                half4 _GlowColor;
                half4 _ArmedColor;
                half4 _Color;
                float _Radius;
                float _Thickness;
                float _DotCount;
                float _DotSpeed;
                float _DotSize;
                float _DotOrbitOffset;
                float _TrailLength;
                float _Progress;
                float _Armed;
                float _Visibility;
                float _Flash;
                float _HoldRingTime;
            CBUFFER_END

            #define TAU 6.28318530718
            #define MAX_DOTS 8

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(position);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            // 0 at edge distance >= width, 1 inside; anti-aliased by screen derivative.
            float Band(float dist, float halfWidth, float aa)
            {
                return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, dist);
            }

            // Signed angular distance in turns (-0.5..0.5).
            float TurnDelta(float a, float b)
            {
                float d = a - b;
                return d - round(d);
            }

            half4 Over(half4 dst, half4 src)
            {
                half a = src.a + dst.a * (1 - src.a);
                half3 rgb = (src.rgb * src.a + dst.rgb * dst.a * (1 - src.a)) / max(a, 1e-4);
                return half4(rgb, a);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float aa = max(fwidth(r), 1e-4);
                // 12시 = 0, 시계 방향으로 증가하는 0~1 각도.
                float turn = frac(atan2(p.x, p.y) / TAU + 1.0);

                float halfT = _Thickness * 0.5;
                float ringDist = abs(r - _Radius);
                float ring = Band(ringDist, halfT, aa);

                half4 col = half4(0, 0, 0, 0);

                // 안쪽 반투명 유리 — 중심은 옅고 링 쪽으로 살짝 진해진다.
                float inner = 1.0 - smoothstep(_Radius - halfT - aa, _Radius - halfT + aa, r);
                half4 glass = _GlassColor;
                glass.a *= inner * lerp(0.4, 1.0, saturate(r / max(_Radius, 1e-3)));
                col = Over(col, glass);

                // 빈 트랙.
                half4 track = _TrackColor;
                track.a *= ring;
                col = Over(col, track);

                // 채워진 호 — 끝부분이 부드럽게 끊기도록 각도 방향도 AA.
                float progress = saturate(_Progress);
                float arcAA = max(fwidth(turn), 1e-4) * 1.5;
                float arc = progress >= 0.999 ? 1.0 : (1.0 - smoothstep(progress - arcAA, progress + arcAA, turn));
                arc *= step(1e-4, progress);
                half4 fill = lerp(_FillColorStart, _FillColorEnd, turn);
                fill.rgb = lerp(fill.rgb, _ArmedColor.rgb, _Armed);
                fill.a *= ring * arc;
                col = Over(col, fill);

                // 차오르는 머리 부분 하이라이트.
                float headTurn = abs(TurnDelta(turn, progress));
                float head = exp(-headTurn * headTurn * 900.0) * exp(-ringDist * ringDist / max(_Thickness * _Thickness, 1e-5) * 3.0);
                head *= step(1e-4, progress) * (1.0 - _Armed);
                col = Over(col, half4(1, 1, 1, saturate(head * 0.85)));

                // 바깥 글로우 — 가득 차면 맥동.
                float pulse = 0.5 + 0.5 * sin(_HoldRingTime * TAU * 1.6);
                float glowAmount = lerp(0.35 * progress, 0.7 + 0.3 * pulse, _Armed) + _Flash;
                float glowFall = exp(-max(ringDist - halfT, 0.0) * 18.0) * (1.0 - ring);
                half4 glow = half4(lerp(_GlowColor.rgb, _ArmedColor.rgb, _Armed), _GlowColor.a * glowFall * saturate(glowAmount));
                glow.a *= lerp(arc, 1.0, _Armed);
                col = Over(col, glow);

                // 가득 차면 링 바깥을 도는 점 + 꼬리.
                if (_Armed > 0.001)
                {
                    float orbitR = _Radius + _DotOrbitOffset;
                    float orbitDist = abs(r - orbitR);
                    float dots = 0.0;
                    int count = (int)clamp(_DotCount, 1.0, (float)MAX_DOTS);
                    [unroll(MAX_DOTS)]
                    for (int i = 0; i < MAX_DOTS; i++)
                    {
                        float active = step((float)i + 0.5, (float)count);
                        float dotTurn = frac(_HoldRingTime * _DotSpeed + (float)i / (float)count);
                        float d = TurnDelta(turn, dotTurn);
                        // 점: 호 길이로 환산한 거리.
                        float arcLen = d * TAU * orbitR;
                        float dd = length(float2(arcLen, orbitDist));
                        float dotMask = Band(dd, _DotSize, aa);
                        // 꼬리: 점 뒤쪽(d<0)으로만 점점 가늘고 옅어진다.
                        float behind = saturate(-d / max(_TrailLength, 1e-3));
                        float trail = (d < 0.0 && behind < 1.0) ? (1.0 - behind) : 0.0;
                        float trailWidth = _DotSize * lerp(0.7, 0.15, behind);
                        trail *= Band(orbitDist, trailWidth, aa) * 0.85;
                        dots = max(dots, max(dotMask, trail) * active);
                    }
                    half4 dotCol = half4(lerp(_ArmedColor.rgb, half3(1, 1, 1), 0.35), saturate(dots) * _Armed);
                    col = Over(col, dotCol);
                }

                // 가득 찬 순간 전체 번쩍.
                col.rgb = lerp(col.rgb, half3(1, 1, 1), saturate(_Flash) * 0.5 * ring);

                col *= input.color;
                col.a *= saturate(_Visibility);
                return col;
            }
            ENDHLSL
        }
    }
}
