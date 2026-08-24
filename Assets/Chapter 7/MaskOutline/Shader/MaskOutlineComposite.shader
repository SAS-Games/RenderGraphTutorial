Shader "Hidden/RenderTextureFeature/MaskOutline/Composite"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.82, 0, 1)
        _OutlineWidth("Outline Width", Range(1, 16)) = 3
        _OutlineSoftness("Outline Softness", Range(0, 8)) = 2
        _OutlineIntensity("Outline Intensity", Range(0, 5)) = 1
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _EdgeSoftness("Edge Softness", Range(0.001, 0.25)) = 0.03

        // 0 = Outside
        // 1 = Inside
        // 2 = Both
        _OutlineMode("Outline Mode", Float) = 0
    }


    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off


        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define MAX_OUTLINE_RADIUS 24


        TEXTURE2D_X(_MorphologyTexture);


        CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float4 _MaskTexelSize;

            float _OutlineWidth;
            float _OutlineSoftness;

            float _OutlineIntensity;

            float _MaskThreshold;
            float _EdgeSoftness;

            float _OutlineMode;

        CBUFFER_END


        // =========================================================
        // SOURCE MASK
        // =========================================================

        half SampleSoftMask(float2 uv)
        {
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;


            half lower = _MaskThreshold - _EdgeSoftness;


            half upper = _MaskThreshold + _EdgeSoftness;


            return smoothstep(lower, upper, mask);
        }


        // =========================================================
        // FEATHER WEIGHT
        //
        // Example:
        //
        // OutlineWidth    = 3
        // OutlineSoftness = 4
        //
        // Distance:
        //
        // 0 1 2 3   4    5    6    7
        //
        // Weight:
        //
        // 1 1 1 1  .75  .50  .25   0
        //
        // =========================================================

        half CalculateFeatherWeight(float distanceFromCenter)
        {
            if (distanceFromCenter <= _OutlineWidth)
                return 1.0h;

            // No softness requested.
            if (_OutlineSoftness <= 0.001)
                return 0.0h;

            float featherDistance = distanceFromCenter - _OutlineWidth;
            float normalizedDistance = featherDistance / _OutlineSoftness;

            half weight = 1.0h - saturate(normalizedDistance);
            return weight;
        }


        // =========================================================
        // HORIZONTAL MORPHOLOGY
        //
        // R = Solid expanded mask
        //
        // G = Weighted expanded mask
        //     Contains the gradual feather.
        //
        // B = Eroded mask
        //
        // =========================================================

        half4 HorizontalMorphologyFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);


            float2 uv = input.texcoord;
            int solidRadius = clamp((int)ceil(_OutlineWidth), 1,MAX_OUTLINE_RADIUS);
            int totalRadius = clamp((int)ceil(_OutlineWidth + _OutlineSoftness), 1,MAX_OUTLINE_RADIUS);

            half solidExpanded = 0.0h;
            half weightedExpanded = 0.0h;

            half eroded = 1.0h;


            [loop]
            for (
                int x = -MAX_OUTLINE_RADIUS;
                x <= MAX_OUTLINE_RADIUS;
                x++
            )
            {
                int pixelDistance =
                    abs(x);


                if (pixelDistance > totalRadius)
                {
                    continue;
                }


                float2 sampleUV =
                    uv +
                    float2(
                        x * _MaskTexelSize.x,
                        0.0
                    );


                half mask =
                    SampleSoftMask(sampleUV);


                // ---------------------------------------------
                // Solid expansion
                // ---------------------------------------------

                if (pixelDistance <= solidRadius)
                {
                    solidExpanded =
                        max(
                            solidExpanded,
                            mask
                        );


                    // Erosion is only based on the
                    // normal outline radius.

                    eroded =
                        min(
                            eroded,
                            mask
                        );
                }


                // ---------------------------------------------
                // Weighted expansion
                //
                // The further away the source pixel is,
                // the weaker its contribution becomes.
                // ---------------------------------------------

                half weight =
                    CalculateFeatherWeight(
                        (float)pixelDistance
                    );


                half weightedMask =
                    mask *
                    weight;


                weightedExpanded =
                    max(
                        weightedExpanded,
                        weightedMask
                    );
            }


            return half4(
                solidExpanded,
                weightedExpanded,
                eroded,
                1.0h
            );
        }


        // =========================================================
        // VERTICAL MORPHOLOGY
        //
        // Takes the horizontal results and performs the same
        // operation vertically.
        //
        // R = Final solid dilation
        // G = Final gradual feather
        // B = Final erosion
        //
        // =========================================================

        half4 VerticalMorphologyFragment(
            Varyings input
        ) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);


            float2 uv =
                input.texcoord;


            int solidRadius =
                clamp(
                    (int)ceil(_OutlineWidth),
                    1,
                    MAX_OUTLINE_RADIUS
                );


            int totalRadius =
                clamp(
                    (int)ceil(
                        _OutlineWidth +
                        _OutlineSoftness
                    ),
                    1,
                    MAX_OUTLINE_RADIUS
                );


            half solidExpanded =
                0.0h;


            half weightedExpanded =
                0.0h;


            half eroded =
                1.0h;


            [loop]
            for (
                int y = -MAX_OUTLINE_RADIUS;
                y <= MAX_OUTLINE_RADIUS;
                y++
            )
            {
                int pixelDistance =
                    abs(y);


                if (pixelDistance > totalRadius)
                {
                    continue;
                }


                float2 sampleUV =
                    uv +
                    float2(
                        0.0,
                        y * _MaskTexelSize.y
                    );


                half3 morphology =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        sampleUV
                    ).rgb;


                // ---------------------------------------------
                // SOLID DILATION
                //
                // R contains the horizontally expanded mask.
                // ---------------------------------------------

                if (pixelDistance <= solidRadius)
                {
                    solidExpanded =
                        max(
                            solidExpanded,
                            morphology.r
                        );


                    // B contains horizontal erosion.

                    eroded =
                        min(
                            eroded,
                            morphology.b
                        );
                }


                // ---------------------------------------------
                // GRADUAL FEATHER
                //
                // G already contains the horizontal weight.
                //
                // Multiply it by the vertical weight.
                //
                // This creates a 2D falloff.
                // ---------------------------------------------

                half verticalWeight =
                    CalculateFeatherWeight(
                        (float)pixelDistance
                    );


                half weightedCandidate =
                    morphology.g *
                    verticalWeight;


                weightedExpanded =
                    max(
                        weightedExpanded,
                        weightedCandidate
                    );
            }


            return half4(
                solidExpanded,
                weightedExpanded,
                eroded,
                1.0h
            );
        }


        // =========================================================
        // COMPOSITE
        // =========================================================

        half4 CompositeFragment(
            Varyings input
        ) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);


            float2 uv =
                input.texcoord;


            // -----------------------------------------------------
            // Original mask
            //
            // Keep the continuous mask instead of applying step().
            // This preserves anti-aliased silhouette information.
            // -----------------------------------------------------

            half center =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                ).r;


            // -----------------------------------------------------
            // Morphology texture
            //
            // R = solid dilation
            // G = weighted / feather dilation
            // B = erosion
            // -----------------------------------------------------

            half3 morphology =
                SAMPLE_TEXTURE2D_X(
                    _MorphologyTexture,
                    sampler_LinearClamp,
                    uv
                ).rgb;


            half solidExpanded =
                morphology.r;


            half weightedExpanded =
                morphology.g;


            half eroded =
                morphology.b;


            // =====================================================
            // OUTSIDE OUTLINE
            // =====================================================


            // Fully solid outline.
            half solidOutside =
                saturate(
                    solidExpanded -
                    center
                );


            // Weighted expanded result already contains:
            //
            // 1.00 near outline
            // 0.75
            // 0.50
            // 0.25
            // 0.00 outer edge

            half featheredOutside =
                saturate(
                    weightedExpanded -
                    center
                );


            // We always preserve the full-opacity solid outline.
            //
            // Outside of it, weightedExpanded naturally
            // creates the gradient.

            half outsideEdge =
                max(
                    solidOutside,
                    featheredOutside
                );


            // =====================================================
            // INSIDE OUTLINE
            // =====================================================

            half insideEdge =
                saturate(
                    center -
                    eroded
                );


            // =====================================================
            // SELECT MODE
            // =====================================================

            half edge =
                outsideEdge;


            // Both
            if (_OutlineMode >= 1.5)
            {
                edge =
                    max(
                        outsideEdge,
                        insideEdge
                    );
            }

            // Inside
            else if (_OutlineMode >= 0.5)
            {
                edge =
                    insideEdge;
            }


            // =====================================================
            // FINAL COLOR
            // =====================================================

            half alpha =
                saturate(
                    edge *
                    _OutlineColor.a *
                    _OutlineIntensity
                );


            half3 color =
                _OutlineColor.rgb *
                _OutlineIntensity;


            return half4(
                color,
                alpha
            );
        }
        ENDHLSL


        // =========================================================
        // PASS 0
        // =========================================================

        Pass
        {
            Name "HorizontalMorphology"

            Blend Off

            ColorMask RGB


            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HorizontalMorphologyFragment
            ENDHLSL
        }


        // =========================================================
        // PASS 1
        // =========================================================

        Pass
        {
            Name "VerticalMorphology"

            Blend Off

            ColorMask RGB


            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment VerticalMorphologyFragment
            ENDHLSL
        }


        // =========================================================
        // PASS 2
        // =========================================================

        Pass
        {
            Name "OutlineComposite"

            Blend SrcAlpha OneMinusSrcAlpha

            ColorMask RGB


            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }
}