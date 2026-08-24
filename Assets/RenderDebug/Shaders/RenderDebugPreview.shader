Shader "Hidden/SAS/RenderDebug/Preview"
{
    Properties
    {
        _MainTex("Texture", 2D) = "black" {}
        _CompareTex("Comparison Texture", 2D) = "black" {}
        _Channel("Channel", Float) = 0
        _Exposure("Exposure", Float) = 0
        _ViewMode("View Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CompareTex;
            float _Channel;
            float _Exposure;
            float _ViewMode;

            float4 Frag(v2f_img input) : SV_Target
            {
                float4 value = tex2D(_MainTex, input.uv);
                if (_ViewMode > 0.5)
                    value = abs(value - tex2D(_CompareTex, input.uv));

                value *= exp2(_Exposure);

                if (_Channel > 3.5)
                    return float4(value.aaa, 1.0);
                if (_Channel > 2.5)
                    return float4(value.bbb, 1.0);
                if (_Channel > 1.5)
                    return float4(value.ggg, 1.0);
                if (_Channel > 0.5)
                    return float4(value.rrr, 1.0);

                return float4(value.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
