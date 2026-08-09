Shader "Hidden/Chapter16/ThermalVision/Source"
{
    Properties
    {
        _ThermalHeat("Heat", Range(0.05, 1)) = 0.82
        _ThermalVariation("Temperature Variation", Range(0, 0.25)) = 0.1
        _ThermalPulseSpeed("Pulse Speed", Range(0, 5)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ThermalSource"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex ThermalVertex
            #pragma fragment ThermalFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _ThermalHeat;
                float _ThermalVariation;
                float _ThermalPulseSpeed;
            CBUFFER_END

            Varyings ThermalVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 ThermalFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float spatialWave = sin(dot(input.positionWS, float3(1.73, 2.91, 1.19)) * 2.0);
                float slowPulse = sin(_Time.y * _ThermalPulseSpeed + input.positionWS.y * 1.4);
                float variation = (spatialWave * 0.65 + slowPulse * 0.35) * _ThermalVariation;
                half heat = saturate(_ThermalHeat + variation);
                return half4(heat, 0.0h, 0.0h, 1.0h);
            }
            ENDHLSL
        }
    }
}
