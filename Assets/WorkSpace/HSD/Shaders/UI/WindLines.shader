Shader "UI/WindLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        
        _Speed ("Speed", Range(0, 10)) = 2.0
        _Density ("Density", Range(1, 100)) = 20.0
        _LineLength ("Line Length", Range(0, 1)) = 0.5
        _LineThickness ("Line Thickness", Range(0, 1)) = 0.1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [ToggleUnusedByRenderer] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
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
        ZTest [unity_GUIZTestingMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "../../../../Shaders/Utils/NoiseUtils.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Speed;
            float _Density;
            float _LineLength;
            float _LineThickness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                
                // Determine the row
                float rowIdx = floor(uv.y * _Density);
                float rowFrac = frac(uv.y * _Density);
                
                // Random values per row
                float randOffset = Hash11(rowIdx);
                float randSpeed = Hash11(rowIdx + 0.5) * 0.5 + 0.5; // Speed multiplier between 0.5 and 1.0
                float randLengthMultiplier = Hash11(rowIdx + 0.2) * 0.5 + 0.5;

                // Scrolling logic
                float xPos = uv.x;
                xPos -= _Time.y * _Speed * randSpeed;
                xPos += randOffset;
                
                float lineVal = frac(xPos);
                
                // Line length and thickness masks
                float lengthMask = step(lineVal, _LineLength * randLengthMultiplier);
                
                // Center the line within the row thickness
                float halfThickness = _LineThickness * 0.5;
                float thicknessMask = step(0.5 - halfThickness, rowFrac) * step(rowFrac, 0.5 + halfThickness);
                
                float finalAlpha = lengthMask * thicknessMask;
                
                // Soften edges slightly for better look (optional, but keep it cheap)
                // finalAlpha *= smoothstep(0, 0.1, lineVal) * smoothstep(_LineLength * randLengthMultiplier, _LineLength * randLengthMultiplier - 0.1, lineVal);

                half4 color = (tex2D(_MainTex, uv) + 1.0) * input.color; // +1.0 is a placeholder if MainTex is empty, but UI usually has a white pixel or sprite
                color.a *= finalAlpha;

                // UI Clipping (usually for ScrollRect)
                #ifdef UNITY_UI_CLIP_RECT
                // For built-in UI clipping, we'd need to add more code, but standard URP UI shaders often use UnityGet2DClipping
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
