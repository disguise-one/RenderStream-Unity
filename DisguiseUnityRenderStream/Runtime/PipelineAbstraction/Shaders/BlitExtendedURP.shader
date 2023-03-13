Shader "Hidden/Disguise/RenderStream/BlitExtendedHDRP"
{
    HLSLINCLUDE
        #include "BlitExtendedCommon.cginc"
    ENDHLSL

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal" : "13.1.8"
        }
        
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        // No color space conversion
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragNoConversion
            ENDHLSL
        }
        
        // Linear to sRGB
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlitLinearToSRGB
            ENDHLSL
        }
        
        // sRGB to Linear
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlitSRGBToLinear
            ENDHLSL
        }
    }
    
    Fallback Off
}
