Shader "USW/UI/SortieOrganicFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Fill ("Fill", Range(0,1)) = 0.6
        _RippleAmplitude ("Surface ripple", Range(0,0.025)) = 0.003
        _CrestWidth ("Thin surface highlight", Range(0.001,0.025)) = 0.006
        _CrestStrength ("Surface highlight strength", Range(0,1)) = 0.45
        _Motes ("Small internal lights", Range(0,1)) = 0.2
        _MoteSize ("Internal light size", Range(0.4,2)) = 1
        _MoteDensity ("Internal light density", Range(0.5,2)) = 1
        _MoteSpeed ("Internal light rise speed", Range(0.1,2)) = 1
        _LargeBubbleChance ("Occasional large bubbles", Range(0,0.4)) = 0
        _LargeBubbleSize ("Large bubble size", Range(1,2.5)) = 1.55
        _LargeBubbleSpeed ("Large bubble rise speed", Range(0.1,2)) = 1.45
        _DepthTint ("Liquid depth shading", Range(0,0.3)) = 0
        _FoamHeight ("Effect above the fill surface", Range(0,0.07)) = 0.022
        _FoamStrength ("Surface foam opacity", Range(0,1)) = 0.85
        _FoamSpeed ("Surface foam motion", Range(0.1,3)) = 1
        _Cool ("Reference cyan", Range(0,1)) = 0
        _FlowTime ("Unscaled preview clock", Float) = 0
        _UvRect ("Sprite UV bounds", Vector) = (0,0,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
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
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float2 local : TEXCOORD1; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _TextureSampleAdd;
                float4 _ClipRect, _UvRect;
                float _Fill, _RippleAmplitude, _CrestWidth, _CrestStrength, _Motes, _Cool, _FlowTime;
                float _MoteSize, _MoteDensity, _MoteSpeed, _DepthTint;
                float _LargeBubbleChance, _LargeBubbleSize, _LargeBubbleSpeed;
                float _FoamHeight, _FoamStrength, _FoamSpeed;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.vertex.xyz);
                o.local = input.vertex.xy; o.uv = input.uv; o.color = input.color * _Color;
                return o;
            }
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34,456.21)); p += dot(p,p+45.32); return frac(p.x*p.y);
            }
            float noise21(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float bubbleShape(float2 q,float radius)
            {
                float distance=length(q);
                float bubbleAA=max(fwidth(distance),0.006);
                float disc=1-smoothstep(radius-bubbleAA,radius+bubbleAA,distance);
                float ring=disc*smoothstep(radius*.45,radius*.82,distance);
                float2 highlight=q-float2(-radius*.32,radius*.35);
                float glint=exp(-dot(highlight,highlight)/max(radius*radius*.1,.0001));
                return disc*.12+ring*.5+glint*.65;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv) + _TextureSampleAdd;
                float2 uv = (input.uv-_UvRect.xy) / max(_UvRect.zw-_UvRect.xy,0.0001);
                float t = _FlowTime;
                // Small continuous waves and a pixel-sized transition avoid the original hard step edge.
                float ripple = (noise21(float2(uv.x*8-t*.13,t*.19))-.5) * _RippleAmplitude;
                float d = input.uv.y - (_Fill+ripple);
                float aa = max(fwidth(d)*0.75,0.0001);
                float coverage = 1-smoothstep(-aa,aa,d);
                coverage *= step(0.0001,_Fill);
                coverage = lerp(coverage,1,step(0.9999,_Fill));
                float depth = max(-d,0);
                float rim = 1-smoothstep(0,max(_CrestWidth,aa),depth);
                float underRim = exp(-pow((depth-_CrestWidth*1.7)/max(_CrestWidth,aa),2));
                float activeSurface = step(0.0001,_Fill)*(1-step(0.9999,_Fill));
                half3 body = tex.rgb;
                float luminance = max(body.r,max(body.g,body.b));
                // A bright crest must not illuminate the sprite's translucent outer shadow.
                rim *= smoothstep(0.90,1.0,tex.a) * smoothstep(0.35,0.60,luminance);
                body = lerp(body,half3(0.04,0.70,0.92)*luminance,_Cool);
                body *= 1 - _DepthTint * saturate(depth * 2);
                body *= 1-underRim*activeSurface*0.055;
                half3 cap = lerp(body,half3(1,1,1),0.58);
                body = lerp(body,cap,rim*_CrestStrength*activeSurface);
                // Circular bubbles: a soft centre, rounded rim and small upper-left highlight.
                // Each column has its own upward speed. Bubbles wander without moving the whole layer.
                float lane=floor(uv.x*7*_MoteDensity);
                float laneSeed=hash21(float2(lane,13));
                float2 p=uv*float2(7,8)*_MoteDensity;
                p.y-=t*(.16+laneSeed*.22)*_MoteSpeed;
                p.y+=sin(t*.7+laneSeed*17)*.035;
                float2 cell = floor(p);
                float seed = hash21(cell);
                float2 q = frac(p)-0.5;
                q -= float2(seed-0.5,hash21(cell+8)-0.5)*0.4;
                q.x-=sin(t*(.7+seed*.8)+seed*27)*.065;
                float radius = (0.045+seed*0.035)*_MoteSize;
                radius*=1+smoothstep(.12,0,depth)*.22;
                q.y*=1+smoothstep(.04,0,depth)*.35;
                float spot = bubbleShape(q,radius)*step(0.28,seed);
                float twinkle = 0.72+0.28*sin(t*1.6+seed*31);
                float edge = smoothstep(0.05,0.17,min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
                // Independent sparse layer keeps C2's dense small bubbles and occasionally adds C3-sized ones.
                // Cell seeds stay fixed as they rise, so size/visibility never pop halfway through a bubble.
                float largeLane=floor(uv.x*7*1.05+23.7);
                float largeLaneSeed=hash21(float2(largeLane,57));
                float2 largeP=uv*float2(7,8)*1.05+float2(23.7,17.3);
                largeP.y-=t*(.19+largeLaneSeed*.2)*_LargeBubbleSpeed;
                float2 largeCell=floor(largeP);
                float largeSeed=hash21(largeCell+41);
                float2 largeQ=frac(largeP)-.5;
                largeQ-=float2(hash21(largeCell+9)-.5,hash21(largeCell+16)-.5)*.4;
                largeQ.x-=sin(t*(.55+largeSeed*.5)+largeSeed*37)*.07;
                float largeRadius=(.045+hash21(largeCell+71)*.035)*_LargeBubbleSize;
                largeRadius*=1+smoothstep(.15,0,depth)*.3;
                largeQ.y*=1+smoothstep(.045,0,depth)*.4;
                float largeSpot=bubbleShape(largeQ,largeRadius)*step(1-_LargeBubbleChance,largeSeed)*step(.0001,_LargeBubbleChance);
                float bubbles=saturate(spot*twinkle+largeSpot*.9)*_Motes;
                body = lerp(body,half3(1,1,0.9),bubbles*edge*smoothstep(0.001,0.015,depth));
                // The body remains clean; the reference's moving foam is a separate layer above it.
                float ft=t*_FoamSpeed;
                float broad=noise21(float2(uv.x*9-ft*.7,ft*.45));
                float fine=noise21(float2(uv.x*39+ft*1.4,ft*.8));
                // Local rising plumes, staggered in phase; a stable low foam bed stays visible.
                float plumeLane=floor(uv.x*18);
                float plumeSeed=hash21(float2(plumeLane,93));
                float life=frac(ft*(.24+plumeSeed*.24)+plumeSeed*13);
                float envelope=smoothstep(0,.22,life)*(1-smoothstep(.45,1,life));
                float bell=.5-.5*cos(frac(uv.x*18)*6.283185);
                float foamTop=_FoamHeight*(.18+pow(saturate(broad*.65+fine*.25),1.6)*.65+bell*envelope*.7);
                float foamAbove=1-smoothstep(foamTop-aa*2,foamTop+aa*2,d);
                float foamBelow=smoothstep(-_CrestWidth*2,-_CrestWidth*.2,d);
                float foam=foamAbove*foamBelow*_FoamStrength*activeSurface;
                // Short detached round droplets just above the foam, still contained by the button sprite.
                float2 fp=float2(uv.x*20-ft*.3,d/max(_FoamHeight,.0001)*2-ft*.45);
                float2 fc=floor(fp), fq=frac(fp)-.5;
                float drop=exp(-dot(fq,fq)*95)*step(.77,hash21(fc));
                float dropZone=smoothstep(0,_FoamHeight*.3,d)*(1-smoothstep(_FoamHeight*.5,_FoamHeight*1.6,d));
                foam=saturate(foam+drop*dropZone*_FoamStrength*activeSurface);
                float sideMask=smoothstep(.85,1,tex.a)*smoothstep(.35,.60,luminance);
                foam*=sideMask;
                half3 foamColor=lerp(body,half3(1,1,1),.44);
                float combined=coverage+foam*(1-coverage);
                half3 combinedColor=(body*coverage*(1-foam)+foamColor*foam)/max(combined,.0001);
                half4 result = half4(combinedColor,tex.a*combined)*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy,input.local)*step(input.local,_ClipRect.zw); result.a *= inside.x*inside.y;
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
