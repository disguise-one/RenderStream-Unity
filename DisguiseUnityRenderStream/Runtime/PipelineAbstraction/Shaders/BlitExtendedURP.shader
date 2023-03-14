Shader "Hidden/Disguise/RenderStream/BlitExtendedURP"
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

        // Fullscreen triangle: No color space conversion
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragNoConversion
            ENDHLSL
        }
        
        // Fullscreen triangle: Linear to sRGB
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlitLinearToSRGB
            ENDHLSL
        }
        
        // Fullscreen triangle: sRGB to Linear
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlitSRGBToLinear
            ENDHLSL
        }
        
        // Quad: No color space conversion
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragNoConversion
            ENDHLSL
        }
        
        // Quad: Linear to sRGB
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragBlitLinearToSRGB
            ENDHLSL
        }
        
        // Quad: sRGB to Linear
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragBlitSRGBToLinear
            ENDHLSL
        }
    }
    
    Fallback Off
}
