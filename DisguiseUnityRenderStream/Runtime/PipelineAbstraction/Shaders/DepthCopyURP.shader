Shader "Hidden/Disguise/RenderStream/DepthCopyURP"
{
    Properties
    {
    }

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
            #include "DepthCopyURP.cginc"
            ENDHLSL
        }
    }
    Fallback Off
}
