Shader "Hidden/Disguise/RenderStream/DepthCopyHDRP"
{
    HLSLINCLUDE
        #include "DepthCopyHDRP.cginc"
    ENDHLSL

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition" : "13.1.8"
        }
        
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
        }

        // Raw
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragRaw
            ENDHLSL
        }
        
        // Eye
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragEye
            ENDHLSL
        }
        
        // Linear01
        Pass
        {
            Cull Off ZTest Always ZWrite Off Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragLinear01
            ENDHLSL
        }
    }
    Fallback Off
}
