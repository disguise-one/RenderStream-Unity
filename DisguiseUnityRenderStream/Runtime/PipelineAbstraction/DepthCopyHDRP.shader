Shader "Hidden/Disguise/RenderStream/DepthCopyHDRP"
{
    Properties
    {
    }

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

        Pass
        {
            Name "Depth Copy"

            Cull Off
            ZTest Always
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma multi_compile_local DEPTH_COPY_RAW DEPTH_COPY_EYE DEPTH_COPY_LINEAR01
            #pragma fragment Frag
            #pragma vertex Vert
            #include "DepthCopyHDRP.cginc"
            ENDHLSL
        }
    }
    Fallback Off
}
