Shader "RealityLog/FogSphere"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (0.8, 0.9, 1.0, 0.45)
        _FogIntensity("Fog Intensity", Range(0, 2)) = 1
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 2
        _MaskTex("Fog Mask", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "FogSphere"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogIntensity;
                float _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 noiseUV = input.uv * _NoiseScale;
                half noiseSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).r;
                half fogFactor = saturate((1.0h - mask) * _FogIntensity);

                half3 color = _FogColor.rgb;
                color *= lerp(0.8h, 1.1h, noiseSample);

                half alpha = _FogColor.a * fogFactor;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

