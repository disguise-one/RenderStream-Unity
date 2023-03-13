Shader "Hidden/Disguise/RenderStream/DepthCopyHDRP"
{
    HLSLINCLUDE
        #define REQUIRE_DEPTH_TEXTURE
        
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        
        // Define for DepthCopyNodes.cginc
        #define GetDepthForDisguise(uv) SampleCameraDepth(uv)
        
        // Defines depth operations
        #include "DepthCopyNodes.cginc"
        
        // Contains vertex and fragment functions
        #include "DepthCopyCommon.cginc"
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
