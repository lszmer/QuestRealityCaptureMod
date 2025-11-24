Shader "RealityLog/CoverageSphere"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.9, 0.1, 0.15)
        _VisitedColor ("Visited Color", Color) = (0.1, 1.0, 0.1, 0.85)
        _CoverageTex ("Coverage Texture", 2D) = "black" {}
        _CoverageIntensity ("Coverage Intensity", Range(0, 1)) = 1
        _EdgeFeather ("Edge Feather", Range(0, 1)) = 0.1
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
            Name "Forward"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

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
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _VisitedColor;
                float _CoverageIntensity;
                float _EdgeFeather;
            CBUFFER_END

            TEXTURE2D(_CoverageTex);
            SAMPLER(sampler_CoverageTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half coverage = SAMPLE_TEXTURE2D(_CoverageTex, sampler_CoverageTex, input.uv).r;
                half4 color = lerp(_BaseColor, _VisitedColor, coverage * _CoverageIntensity);

                half ndotl = saturate(input.normalWS.z * 0.5h + 0.5h);
                color.a *= saturate(ndotl + _EdgeFeather);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

