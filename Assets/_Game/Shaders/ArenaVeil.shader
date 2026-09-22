Shader "Game/Arena Veil"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Density ("Density", Range(0, 1)) = 0.5
        _Overlay ("Overlay", Float) = 0
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex VeilVertex
            #pragma fragment VeilFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Density;
                float _Overlay;
                float _PixelsPerUnit;
            CBUFFER_END

            static const float BayerThresholds[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            static const float BayerSize = 4;
            static const float BayerLevels = 16;
            static const float PixelCenterBias = 0.25;

            Varyings VeilVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            bool IsVeiled(float2 positionWS)
            {
                float2 pixel = floor(positionWS * _PixelsPerUnit + PixelCenterBias);
                float2 cell = pixel - BayerSize * floor(pixel / BayerSize);
                return BayerThresholds[(int)(cell.y * BayerSize + cell.x)] < _Density * BayerLevels;
            }

            half4 VeilFragment(Varyings input) : SV_Target
            {
                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (IsVeiled(input.positionWS.xy))
                {
                    return half4(0, 0, 0, color.a);
                }

                return half4(color.rgb, color.a * (1 - _Overlay));
            }
            ENDHLSL
        }
    }
}
