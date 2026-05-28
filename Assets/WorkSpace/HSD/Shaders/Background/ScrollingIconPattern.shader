Shader "Custom/ScrollingIconPattern"
{
    Properties
    {
        [Header(Icon Settings)]
        _IconTex ("Icon Texture", 2D) = "white" {}
        _IconColor ("Icon Tint", Color) = (1,1,1,1)
        _IconSize ("Icon Size (Pixels)", Float) = 50
        _IconTiling ("Icon Tiling (X, Y)", Vector) = (10, 10, 0, 0)
        _IconGap ("Icon Gap (X, Y)", Vector) = (20, 20, 0, 0)
        _IconRotation ("Icon Rotation (Deg)", Float) = 0
        
        [Header(Background Settings)]
        _BgTex ("Background Texture", 2D) = "white" {}
        _BgColor ("Background Tint", Color) = (0.5, 0.5, 0.5, 1)

        [Header(Movement Settings)]
        _ScrollDirection ("Scroll Direction", Vector) = (1, 1, 0, 0)
        _ScrollSpeed ("Scroll Speed", Float) = 50
        [Toggle] _EnableStagger ("Enable Diagonal Layout", Float) = 1

        [Header(Stencil)]
        _Stencil ("Stencil ID", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        [Header(Other)]
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Shaders/Utils/ShaderUtils.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float2 scrollOffset : TEXCOORD1;
                float2 rotSinCos : TEXCOORD3;
            };

            sampler2D _IconTex;
            sampler2D _BgTex;
            fixed4 _IconColor;
            fixed4 _BgColor;
            
            float _IconSize;
            float4 _IconTiling;
            float4 _IconGap;
            float _IconRotation;
            float2 _ScrollDirection;
            float _ScrollSpeed;
            float _EnableStagger;

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                
                // Pre-calculate scroll offset in vertex shader
                o.scrollOffset = _Time.y * normalize(_ScrollDirection) * _ScrollSpeed;

                // Pre-calculate rotation sin/cos
                float rad = radians(_IconRotation);
                sincos(rad, o.rotSinCos.x, o.rotSinCos.y);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 cellSize = _IconSize + _IconGap.xy;
                
                // 1. Calculate base pixel position and apply scrolling
                float2 pixelPos = i.uv * _IconTiling.xy * cellSize + i.scrollOffset;

                // 2. Background Layer
                fixed4 finalCol = tex2D(_BgTex, i.uv) * _BgColor;

                // 3. Staggered layout calculation (Avoid IF)
                float rowID = floor(pixelPos.y / cellSize.y);
                float stagger = step(0.5, _EnableStagger) * step(0.1, frac(rowID * 0.5)) * (cellSize.x * 0.5);
                pixelPos.x += stagger;
                
                // 4. Icon Layer calculation
                float2 localUV = frac(pixelPos / cellSize); 
                float2 iconThreshold = _IconSize / cellSize;

                // Determine if we are inside the icon area (Avoid IF)
                float isInIconArea = step(localUV.x, iconThreshold.x) * step(localUV.y, iconThreshold.y);
                
                // Normalize local UV to [0, 1] for the icon texture
                float2 iconTexUV = localUV / iconThreshold;

                // Rotate Icon using pre-calculated sin/cos
                float s = i.rotSinCos.x;
                float c = i.rotSinCos.y;
                float2x2 rotMat = float2x2(c, -s, s, c);
                iconTexUV = mul(rotMat, iconTexUV - 0.5) + 0.5;

                // Check if rotated UV is within [0, 1] (Avoid IF)
                float isInRotatedIcon = step(0.0, iconTexUV.x) * step(iconTexUV.x, 1.0) * 
                                        step(0.0, iconTexUV.y) * step(iconTexUV.y, 1.0);
                
                float finalAlpha = isInIconArea * isInRotatedIcon;

                // Sampling and Blending (Only sample if possibly visible to save bandwidth if possible, 
                // but for mobile/modern GPUs sometimes it's better to avoid branching)
                // However, we can use the alpha to lerp.
                
                fixed4 iconCol = tex2D(_IconTex, iconTexUV) * _IconColor * i.color;
                
                // Combine layers
                float iconBlend = iconCol.a * finalAlpha;
                finalCol.rgb = lerp(finalCol.rgb, iconCol.rgb, iconBlend);
                finalCol.a = max(finalCol.a, iconBlend);

                return finalCol;
            }
            ENDHLSL
        }
    }
}
