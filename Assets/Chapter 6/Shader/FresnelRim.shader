Shader "Hidden/Chapter6/FresnelRim"
{
    Properties
    {
        _RimColor("Rim Color", Color) = (1, 0.85, 0.75, 1)
        _RimPower("Rim Tightness",Range(0.25, 8.0)) = 3.0
        _RimThreshold("Rim Threshold",Range(0.0, 1.0)) = 0.5
        _RimSoftness("Rim Softness",Range(0.001, 0.5)) = 0.1
        _RimIntensity("Rim Intensity", Range(0.0, 10.0)) = 2.0
        _PlanarProjection("Planar Projection", Range(0.0, 1.0)) = 1.0
        _PlanarPlaneZ("Planar Plane Z", Float) = 0.0
        _PlanarGateStrength("Planar Facing Influence", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "Fresnel Rim"

            Cull Back
            ZTest LEqual
            ZWrite Off

            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex RimVertex
            #pragma fragment RimFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };


            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;

                float _RimPower;
                float _RimThreshold;
                float _RimSoftness;
                float _RimIntensity;
                float _PlanarProjection;
                float _PlanarPlaneZ;
                float _PlanarGateStrength;

            CBUFFER_END


            Varyings RimVertex(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }


            half4 RimFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Projecting the camera onto the gameplay plane provides a stable,
                // side-scroller-oriented facing direction for the directional gate.
                float3 planarCameraPosition = GetCurrentViewPosition();
                planarCameraPosition.z = lerp(planarCameraPosition.z, _PlanarPlaneZ, _PlanarProjection);

                float3 planarViewDirection = SafeNormalize(planarCameraPosition - input.positionWS);


                // -----------------------------------------------
                // Fresnel
                //
                // Camera facing surface:
                //
                // N.V = 1
                // Fresnel = 0
                //
                // Grazing surface:
                //
                // N.V = 0
                // Fresnel = 1
                // -----------------------------------------------

                float NdotV = saturate(dot(N, V));
                float planarFacing = saturate(dot(N, planarViewDirection));
                float planarGate = lerp(1.0, planarFacing, _PlanarGateStrength);
                float fresnel = pow(1.0 - NdotV, _RimPower) * planarGate;

                // Convert Fresnel into a controllable thin band
                float rim = smoothstep(_RimThreshold - _RimSoftness, _RimThreshold + _RimSoftness, fresnel);
                rim *= _RimIntensity;

                // Additive glowing rim
                half3 color = _RimColor.rgb * (rim * _RimColor.a);
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }
}
